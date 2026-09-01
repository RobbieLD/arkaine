using Microsoft.AspNetCore.StaticFiles;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Server.Arkaine.LocalB2;

public sealed class LocalB2Store
{
    private const string InternalDirectoryName = ".local-b2";
    private const int MaximumMultipartPartCount = 10_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly LocalB2Options _options;
    private readonly FileExtensionContentTypeProvider _contentTypes = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, LocalB2Metadata> _metadata;
    private readonly string _internalDirectory;
    private readonly string _metadataPath;
    private readonly string _stagingDirectory;
    private readonly string _multipartDirectory;

    public LocalB2Store(LocalB2Options options)
    {
        _options = options;
        _options.Normalize();

        Directory.CreateDirectory(_options.RootDirectory);
        EnsureRootIsSafe();
        _internalDirectory = Path.Combine(_options.RootDirectory, InternalDirectoryName);
        _metadataPath = Path.Combine(_internalDirectory, "metadata.json");
        _stagingDirectory = Path.Combine(_internalDirectory, "staging");
        _multipartDirectory = Path.Combine(_internalDirectory, "multipart");
        Directory.CreateDirectory(_internalDirectory);
        EnsureNoReparsePoints(_internalDirectory);
        Directory.CreateDirectory(_stagingDirectory);
        EnsureNoReparsePoints(_stagingDirectory);
        Directory.CreateDirectory(_multipartDirectory);
        EnsureNoReparsePoints(_multipartDirectory);
        _metadata = LoadMetadata();
    }

    public string RootDirectory => _options.RootDirectory;

    public string ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) ||
            key.Contains('\\') ||
            key.Contains('\0') ||
            key.StartsWith('/') ||
            key.EndsWith('/'))
        {
            throw new LocalB2RequestException(StatusCodes.Status400BadRequest, "The file name is not a valid B2 key.");
        }

        var segments = key.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or "..") ||
            IsInternalKey(key))
        {
            throw new LocalB2RequestException(StatusCodes.Status400BadRequest, "The file name is not a valid B2 key.");
        }

        var fullPath = _options.RootDirectory;
        foreach (var segment in segments)
        {
            fullPath = Path.Combine(fullPath, segment);
        }

        fullPath = Path.GetFullPath(fullPath);
        EnsureWithinRoot(fullPath);
        EnsureNoReparsePoints(fullPath);
        return fullPath;
    }

    public string ValidatePrefix(string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return string.Empty;
        }

        if (prefix.Contains('\\') ||
            prefix.Contains('\0') ||
            prefix.StartsWith('/') ||
            IsInternalKey(prefix.TrimEnd('/')))
        {
            throw new LocalB2RequestException(StatusCodes.Status400BadRequest, "The prefix is not a valid B2 key prefix.");
        }

        var segments = prefix.Split('/');
        if (segments
            .Select((segment, index) => (segment, index))
            .Any(item => item.segment is "." or ".." ||
                         (item.segment.Length == 0 && item.index != segments.Length - 1)))
        {
            throw new LocalB2RequestException(StatusCodes.Status400BadRequest, "The prefix is not a valid B2 key prefix.");
        }

        return prefix;
    }

    public async Task<IReadOnlyList<LocalB2File>> ListFilesAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return EnumerateFilesUnsafe();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LocalB2File?> GetFileAsync(string key, CancellationToken cancellationToken)
    {
        var fullPath = ValidateKey(key);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return GetFileUnsafe(key, fullPath);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LocalB2File?> FindFileByIdAsync(string fileId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return EnumerateFilesUnsafe()
                .SingleOrDefault(file => string.Equals(file.FileId, fileId, StringComparison.Ordinal));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LocalB2File> UploadAsync(
        string key,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        var destinationPath = ValidateKey(key);

        await _gate.WaitAsync(cancellationToken);
        var temporaryPath = Path.Combine(_stagingDirectory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await using (var output = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             128 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await content.CopyToAsync(output, cancellationToken);
            }

            File.Move(temporaryPath, destinationPath, overwrite: true);
            _metadata[key] = new LocalB2Metadata(NormalizeContentType(contentType));
            SaveMetadataUnsafe();
            return CreateFileUnsafe(key, destinationPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            _gate.Release();
        }
    }

    public async Task<bool> DeleteAsync(
        string key,
        string fileId,
        CancellationToken cancellationToken)
    {
        var fullPath = ValidateKey(key);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var file = GetFileUnsafe(key, fullPath);
            if (file is null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(fileId) &&
                !string.Equals(file.FileId, fileId, StringComparison.Ordinal))
            {
                throw new LocalB2RequestException(StatusCodes.Status409Conflict, "The file ID does not match the current file.");
            }

            File.Delete(fullPath);
            RemoveEmptyDirectoriesUnsafe(Path.GetDirectoryName(fullPath));
            _metadata.Remove(key);
            SaveMetadataUnsafe();
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LocalB2File?> CopyAsync(
        string sourceFileId,
        string destinationKey,
        CancellationToken cancellationToken)
    {
        var destinationPath = ValidateKey(destinationKey);

        await _gate.WaitAsync(cancellationToken);
        var temporaryPath = Path.Combine(_stagingDirectory, $"{Guid.NewGuid():N}.copy");
        try
        {
            var source = EnumerateFilesUnsafe()
                .SingleOrDefault(file => string.Equals(file.FileId, sourceFileId, StringComparison.Ordinal));
            if (source is null)
            {
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(source.FullPath, temporaryPath, overwrite: false);
            File.Move(temporaryPath, destinationPath, overwrite: true);
            _metadata[destinationKey] = new LocalB2Metadata(source.ContentType);
            SaveMetadataUnsafe();
            return CreateFileUnsafe(destinationKey, destinationPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            _gate.Release();
        }
    }

    public async Task<string> StartMultipartAsync(
        string key,
        string contentType,
        CancellationToken cancellationToken)
    {
        _ = ValidateKey(key);
        var fileId = $"local-multipart-{Guid.NewGuid():N}";
        var state = new LocalB2MultipartState
        {
            FileName = key,
            ContentType = NormalizeContentType(contentType),
            CreatedUtc = DateTimeOffset.UtcNow
        };

        await _gate.WaitAsync(cancellationToken);
        try
        {
            SaveMultipartStateUnsafe(fileId, state);
            return fileId;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<LocalB2MultipartFile>> ListMultipartAsync(
        string? namePrefix,
        int maxFileCount,
        CancellationToken cancellationToken)
    {
        var prefix = ValidatePrefix(namePrefix);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var files = new List<LocalB2MultipartFile>();
            foreach (var statePath in Directory.EnumerateFiles(_multipartDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                var fileId = Path.GetFileNameWithoutExtension(statePath);
                if (!IsValidMultipartId(fileId))
                {
                    continue;
                }

                var state = LoadMultipartStateUnsafe(fileId);
                if (!state.FileName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var length = Directory
                    .EnumerateFiles(_multipartDirectory, $"{fileId}.*.part", SearchOption.TopDirectoryOnly)
                    .Sum(path => new FileInfo(path).Length);
                files.Add(new LocalB2MultipartFile(state.FileName, fileId, state.ContentType, length));
            }

            var limit = maxFileCount > 0 ? maxFileCount : 1000;
            return files
                .OrderBy(file => file.FileName, StringComparer.Ordinal)
                .Take(limit)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WriteMultipartPartAsync(
        string fileId,
        int partNumber,
        string? expectedSha1,
        Stream content,
        CancellationToken cancellationToken)
    {
        ValidateMultipartId(fileId);
        if (partNumber is < 1 or > MaximumMultipartPartCount)
        {
            throw new LocalB2RequestException(StatusCodes.Status400BadRequest, "The multipart part number is invalid.");
        }

        await _gate.WaitAsync(cancellationToken);
        var temporaryPath = Path.Combine(_multipartDirectory, $"{fileId}.{partNumber:D5}.{Guid.NewGuid():N}.tmp");
        try
        {
            _ = LoadMultipartStateUnsafe(fileId);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);

            await using (var output = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             128 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[128 * 1024];
                while (true)
                {
                    var read = await content.ReadAsync(buffer.AsMemory(), cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    hash.AppendData(buffer, 0, read);
                }
            }

            var actualSha1 = Convert.ToHexStringLower(hash.GetHashAndReset());
            if (!string.IsNullOrWhiteSpace(expectedSha1) &&
                !string.Equals(actualSha1, expectedSha1, StringComparison.OrdinalIgnoreCase))
            {
                throw new LocalB2RequestException(StatusCodes.Status400BadRequest, "The multipart part checksum is invalid.");
            }

            File.Move(
                temporaryPath,
                GetMultipartPartPath(fileId, partNumber),
                overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            _gate.Release();
        }
    }

    public async Task<LocalB2File?> FinishMultipartAsync(
        string fileId,
        IReadOnlyList<string> expectedHashes,
        CancellationToken cancellationToken)
    {
        ValidateMultipartId(fileId);

        await _gate.WaitAsync(cancellationToken);
        var temporaryPath = Path.Combine(_stagingDirectory, $"{Guid.NewGuid():N}.multipart");
        try
        {
            var state = LoadMultipartStateUnsafe(fileId);
            var parts = Directory
                .EnumerateFiles(_multipartDirectory, $"{fileId}.*.part", SearchOption.TopDirectoryOnly)
                .Select(path => (Path: path, Number: ParsePartNumber(path, fileId)))
                .OrderBy(part => part.Number)
                .ToList();

            if (parts.Count == 0 ||
                parts.Count != expectedHashes.Count ||
                parts.Select((part, index) => part.Number == index + 1).Any(isSequential => !isSequential))
            {
                throw new LocalB2RequestException(StatusCodes.Status400BadRequest, "The multipart upload parts are incomplete.");
            }

            await using (var output = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             128 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                for (var index = 0; index < parts.Count; index++)
                {
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
                    await using var input = new FileStream(
                        parts[index].Path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        128 * 1024,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);
                    var buffer = new byte[128 * 1024];
                    while (true)
                    {
                        var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken);
                        if (read == 0)
                        {
                            break;
                        }

                        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                        hash.AppendData(buffer, 0, read);
                    }

                    var actualSha1 = Convert.ToHexStringLower(hash.GetHashAndReset());
                    if (!string.Equals(actualSha1, expectedHashes[index], StringComparison.OrdinalIgnoreCase))
                    {
                        throw new LocalB2RequestException(StatusCodes.Status400BadRequest, "The multipart checksum is invalid.");
                    }
                }
            }

            var destinationPath = ValidateKey(state.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Move(temporaryPath, destinationPath, overwrite: true);
            _metadata[state.FileName] = new LocalB2Metadata(state.ContentType);
            SaveMetadataUnsafe();

            foreach (var part in parts)
            {
                File.Delete(part.Path);
            }

            File.Delete(GetMultipartStatePath(fileId));
            return CreateFileUnsafe(state.FileName, destinationPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            _gate.Release();
        }
    }

    public bool HasMultipart(string fileId)
    {
        ValidateMultipartId(fileId);
        return File.Exists(GetMultipartStatePath(fileId));
    }

    public static string GetFileId(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return $"local-{Convert.ToHexStringLower(bytes)}";
    }

    private List<LocalB2File> EnumerateFilesUnsafe()
    {
        var files = new List<LocalB2File>();
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var fullPath in Directory.EnumerateFiles(_options.RootDirectory, "*", enumerationOptions))
        {
            var relativePath = Path.GetRelativePath(_options.RootDirectory, fullPath);
            var key = relativePath.Replace(Path.DirectorySeparatorChar, '/');
            if (IsInternalKey(key))
            {
                continue;
            }

            try
            {
                _ = ValidateKey(key);
                var file = GetFileUnsafe(key, fullPath);
                if (file is not null)
                {
                    files.Add(file);
                }
            }
            catch (LocalB2RequestException)
            {
                // Files with unsafe names cannot be exposed as B2 keys.
            }
        }

        return files
            .OrderBy(file => file.FileName, StringComparer.Ordinal)
            .ToList();
    }

    private LocalB2File? GetFileUnsafe(string key, string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var info = new FileInfo(fullPath);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            return null;
        }

        return CreateFileUnsafe(key, fullPath);
    }

    private LocalB2File CreateFileUnsafe(string key, string fullPath)
    {
        var info = new FileInfo(fullPath);
        var contentType = _metadata.TryGetValue(key, out var metadata) && !string.IsNullOrWhiteSpace(metadata.ContentType)
            ? metadata.ContentType
            : GetContentType(key);

        return new LocalB2File(key, GetFileId(key), contentType, info.Length, fullPath);
    }

    private string GetContentType(string key)
    {
        return _contentTypes.TryGetContentType(key, out var contentType)
            ? contentType
            : "application/octet-stream";
    }

    private Dictionary<string, LocalB2Metadata> LoadMetadata()
    {
        if (!File.Exists(_metadataPath))
        {
            return new Dictionary<string, LocalB2Metadata>(StringComparer.Ordinal);
        }

        var document = JsonSerializer.Deserialize<LocalB2MetadataDocument>(
            File.ReadAllText(_metadataPath),
            JsonOptions) ?? throw new InvalidDataException("The local B2 metadata file is invalid.");

        return new Dictionary<string, LocalB2Metadata>(
            document.Files ?? new Dictionary<string, LocalB2Metadata>(),
            StringComparer.Ordinal);
    }

    private void SaveMetadataUnsafe()
    {
        var temporaryPath = $"{_metadataPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var document = new LocalB2MetadataDocument
            {
                Files = _metadata
            };
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(document, JsonOptions));
            File.Move(temporaryPath, _metadataPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void SaveMultipartStateUnsafe(string fileId, LocalB2MultipartState state)
    {
        File.WriteAllText(GetMultipartStatePath(fileId), JsonSerializer.Serialize(state, JsonOptions));
    }

    private LocalB2MultipartState LoadMultipartStateUnsafe(string fileId)
    {
        ValidateMultipartId(fileId);
        var path = GetMultipartStatePath(fileId);
        if (!File.Exists(path))
        {
            throw new LocalB2RequestException(StatusCodes.Status404NotFound, "The multipart upload was not found.");
        }

        return JsonSerializer.Deserialize<LocalB2MultipartState>(File.ReadAllText(path), JsonOptions)
               ?? throw new InvalidDataException("The local B2 multipart state is invalid.");
    }

    private string GetMultipartStatePath(string fileId) =>
        Path.Combine(_multipartDirectory, $"{fileId}.json");

    private string GetMultipartPartPath(string fileId, int partNumber) =>
        Path.Combine(_multipartDirectory, $"{fileId}.{partNumber:D5}.part");

    private static int ParsePartNumber(string path, string fileId)
    {
        var name = Path.GetFileName(path);
        var prefix = $"{fileId}.";
        var suffix = ".part";
        if (!name.StartsWith(prefix, StringComparison.Ordinal) ||
            !name.EndsWith(suffix, StringComparison.Ordinal) ||
            !int.TryParse(name[prefix.Length..^suffix.Length], out var number))
        {
            throw new InvalidDataException("The local B2 multipart part name is invalid.");
        }

        return number;
    }

    private static void ValidateMultipartId(string fileId)
    {
        if (!IsValidMultipartId(fileId))
        {
            throw new LocalB2RequestException(StatusCodes.Status400BadRequest, "The multipart upload ID is invalid.");
        }
    }

    private static bool IsValidMultipartId(string fileId)
    {
        return fileId.StartsWith("local-multipart-", StringComparison.Ordinal) &&
               Guid.TryParseExact(fileId["local-multipart-".Length..], "N", out _);
    }

    private static string NormalizeContentType(string? contentType)
    {
        var value = contentType?.Split(';', 2)[0].Trim();
        return string.IsNullOrWhiteSpace(value) ? "application/octet-stream" : value;
    }

    private static bool IsInternalKey(string key)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return key.Equals(InternalDirectoryName, comparison) ||
               key.StartsWith($"{InternalDirectoryName}/", comparison);
    }

    private void EnsureWithinRoot(string fullPath)
    {
        var root = _options.RootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                   + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullPath.StartsWith(root, comparison))
        {
            throw new LocalB2RequestException(StatusCodes.Status400BadRequest, "The file name resolves outside the local B2 bucket.");
        }
    }

    private void EnsureNoReparsePoints(string fullPath)
    {
        var relativePath = Path.GetRelativePath(_options.RootDirectory, fullPath);
        var currentPath = _options.RootDirectory;
        foreach (var segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            try
            {
                var attributes = File.GetAttributes(currentPath);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new LocalB2RequestException(StatusCodes.Status400BadRequest, "The file name crosses a symbolic link.");
                }
            }
            catch (FileNotFoundException)
            {
                // The missing path segment will be created by the operation.
            }
            catch (DirectoryNotFoundException)
            {
                // The missing path segment will be created by the operation.
            }
        }
    }

    private void EnsureRootIsSafe()
    {
        var attributes = File.GetAttributes(_options.RootDirectory);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("The local B2 root directory cannot be a symbolic link.");
        }
    }

    private static void RemoveEmptyDirectoriesUnsafe(string? directory)
    {
        while (!string.IsNullOrEmpty(directory) &&
               Directory.Exists(directory) &&
               Directory.EnumerateFileSystemEntries(directory).Any() == false)
        {
            Directory.Delete(directory);
            directory = Path.GetDirectoryName(directory);
        }
    }

    private sealed class LocalB2MetadataDocument
    {
        public Dictionary<string, LocalB2Metadata>? Files { get; set; }
    }

    private sealed record LocalB2Metadata(string ContentType);

    private sealed class LocalB2MultipartState
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public DateTimeOffset CreatedUtc { get; set; }
    }
}
