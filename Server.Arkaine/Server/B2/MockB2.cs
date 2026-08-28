using Server.Arkaine.Favourites;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Server.Arkaine.B2
{
    public class MockB2 : IB2Service
    {
        private readonly IThumbnailInfoProvider _thumbnails;
        private readonly Store _store;

        public MockB2(IThumbnailInfoProvider thumbnails, Store store)
        {
            _thumbnails = thumbnails;
            _store = store;
        }

        public sealed class Store
        {
            private readonly string _defaultThumbnailDirectory;

            internal object SyncRoot { get; } = new();
            internal MockLibrary Library { get; private set; }

            public Store()
            {
                _defaultThumbnailDirectory = Path.Combine(
                    AppContext.BaseDirectory,
                    "mock-thumbnails",
                    Guid.NewGuid().ToString("n"));
                Library = CreateLibrary(CreateDefaultObjects(), _defaultThumbnailDirectory);
            }

            public void Reset()
            {
                Reset(CreateDefaultObjects(), _defaultThumbnailDirectory);
            }

            public void Reset(IEnumerable<MockB2Object> objects)
            {
                Reset(objects, _defaultThumbnailDirectory);
            }

            public void Reset(IEnumerable<MockB2Object> objects, string? thumbnailDirectory)
            {
                lock (SyncRoot)
                {
                    Library = CreateLibrary(objects, thumbnailDirectory);
                }
            }

            public IReadOnlyList<B2File> SnapshotFiles()
            {
                lock (SyncRoot)
                {
                    return Library.Files.Values
                        .Select(file => Clone(file.Metadata))
                        .OrderBy(file => file.FileName, StringComparer.Ordinal)
                        .ToList();
                }
            }
        }

        public Task<Stream> Download(string userName, string fileName, CancellationToken cancellationToken)
        {
            lock (_store.SyncRoot)
            {
                if (!_store.Library.Files.TryGetValue(fileName, out var file))
                {
                    throw new FileNotFoundException($"Mock file '{fileName}' was not found.", fileName);
                }

                return Task.FromResult<Stream>(new MemoryStream(file.Content, writable: false));
            }
        }

        public Task<AuthResponse> GetToken(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AuthResponse
            {
                AccountId = "test",
                ApiBaseUrl = "test",
                DownloadBaseUrl = "test",
                Token = "test"
            });
        }

        public async Task<FilesResponse> ListFiles(FilesRequest request, string userName, IFavouritesService? favouriteService, CancellationToken cancellationToken)
        {
            if (favouriteService != null && (request.Prefix?.StartsWith("Favourites", StringComparison.Ordinal) ?? false))
            {
                var favourites = await favouriteService.GetAllFavouritesPage(userName, request.PageSize > 0 ? request.PageSize : 25, request.StartFile);
                PopulatePreviews(favourites.Files);
                return ApplyExactFileFilter(favourites, request.ExactFileName);
            }

            var prefix = request.Prefix ?? string.Empty;
            var listedFiles = new Dictionary<string, B2File>(StringComparer.Ordinal);
            IReadOnlyList<B2File> sourceFiles;

            lock (_store.SyncRoot)
            {
                sourceFiles = _store.Library.Files.Values
                    .Select(file => Clone(file.Metadata))
                    .OrderBy(file => file.FileName, StringComparer.Ordinal)
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(request.ExactFileName))
            {
                var exactMatch = sourceFiles
                    .Where(file => string.Equals(file.FileName, request.ExactFileName, StringComparison.Ordinal))
                    .ToList();
                PopulatePreviews(exactMatch);
                return new FilesResponse
                {
                    Files = exactMatch,
                    NextFileName = string.Empty
                };
            }

            foreach (var file in sourceFiles)
            {
                if (!file.FileName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var relativeName = file.FileName[prefix.Length..];
                if (relativeName.Length == 0)
                {
                    continue;
                }

                var delimiter = request.Delimiter;
                var delimiterIndex = string.IsNullOrEmpty(delimiter)
                    ? -1
                    : relativeName.IndexOf(delimiter, StringComparison.Ordinal);

                if (delimiterIndex >= 0)
                {
                    var folderName = relativeName[..(delimiterIndex + delimiter!.Length)];
                    var folderPath = prefix + folderName;
                    var childCount = sourceFiles.Count(candidate =>
                        candidate.FileName.StartsWith(folderPath, StringComparison.Ordinal) &&
                        !candidate.FileName[folderPath.Length..].Contains('/'));

                    listedFiles.TryAdd(folderPath, new B2File
                    {
                        ChildCount = childCount,
                        ContentType = "application/x-directory",
                        FileName = folderPath,
                        Type = "folder",
                        Id = $"mock-folder-{folderPath}"
                    });
                    continue;
                }

                listedFiles.TryAdd(file.FileName, file);
            }

            var orderedFiles = listedFiles.Values
                .OrderBy(file => file.FileName, StringComparer.Ordinal)
                .ToList();
            var filesAfterStart = string.IsNullOrEmpty(request.StartFile)
                ? orderedFiles
                : orderedFiles.Where(file => string.Compare(file.FileName, request.StartFile, StringComparison.Ordinal) > 0).ToList();
            var pageSize = request.PageSize > 0 ? request.PageSize : 25;
            var files = filesAfterStart.Take(pageSize).ToList();

            PopulatePreviews(files);

            return ApplyExactFileFilter(new FilesResponse
            {
                Files = files,
                NextFileName = filesAfterStart.Count > files.Count && files.Count > 0
                    ? files[^1].FileName
                    : string.Empty
            }, request.ExactFileName);
        }

        public IResult Preview(string fileName)
        {
            lock (_store.SyncRoot)
            {
                if (ThumbnailPathResolver.TryResolve(_store.Library.ThumbnailDir, fileName, out var path) && File.Exists(path))
                {
                    return Results.File(path, contentType: "image/jpeg");
                }

                return Results.Stream(File.OpenRead(_store.Library.FallbackImagePath), contentType: "image/jpeg");
            }
        }

        public Task<IResult> Stream(string userName, string fileName, CancellationToken cancellationToken)
        {
            lock (_store.SyncRoot)
            {
                if (!_store.Library.Files.TryGetValue(fileName, out var file))
                {
                    throw new FileNotFoundException($"Mock file '{fileName}' was not found.", fileName);
                }

                return Task.FromResult<IResult>(Results.Stream(
                    new MemoryStream(file.Content, writable: false),
                    contentType: file.Metadata.ContentType,
                    enableRangeProcessing: file.Metadata.ContentType.StartsWith("video/", StringComparison.Ordinal) ||
                                           file.Metadata.ContentType.StartsWith("audio/", StringComparison.Ordinal)));
            }
        }

        public async Task UploadMultiPartFile(string fileName, string contentType, Stream content, int chunkSize, CancellationToken cancellationToken)
        {
            await UploadSingleFile(fileName, contentType, content.Length, content, cancellationToken);
        }

        public async Task UploadSingleFile(string fileName, string contentType, long length, Stream content, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);

            lock (_store.SyncRoot)
            {
                var existing = _store.Library.Files.TryGetValue(fileName, out var stored) ? stored : null;
                var fileId = existing?.Metadata.Id ?? $"mock-upload-{_store.Library.NextId++:D4}";
                _store.Library.Files[fileName] = new MockStoredFile(
                    new B2File
                    {
                        ContentType = contentType,
                        FileName = fileName,
                        Id = fileId,
                        Size = length.ToString(),
                        Type = "upload"
                    },
                    buffer.ToArray());
            }
        }

        public Task Delete(DeleteModel request, CancellationToken cancellationToken)
        {
            lock (_store.SyncRoot)
            {
                var toDelete = _store.Library.Files
                    .FirstOrDefault(file =>
                        string.Equals(file.Key, request.FileName, StringComparison.Ordinal) ||
                        string.Equals(file.Value.Metadata.Id, request.Id, StringComparison.Ordinal));

                if (!string.IsNullOrEmpty(toDelete.Key))
                {
                    _store.Library.Files.Remove(toDelete.Key);
                }
            }

            return Task.CompletedTask;
        }

        private void PopulatePreviews(IEnumerable<B2File> files)
        {
            string thumbnailDir;

            lock (_store.SyncRoot)
            {
                thumbnailDir = _store.Library.ThumbnailDir;
            }

            foreach (var file in files)
            {
                ThumbnailPreview.Populate(file, thumbnailDir, _thumbnails);
            }
        }

        private static FilesResponse ApplyExactFileFilter(FilesResponse response, string? exactFileName)
        {
            if (string.IsNullOrWhiteSpace(exactFileName))
            {
                return response;
            }

            response.Files = response.Files
                .Where(file => string.Equals(file.FileName, exactFileName, StringComparison.Ordinal))
                .ToList();
            response.NextFileName = string.Empty;
            return response;
        }

        private static MockLibrary CreateLibrary(IEnumerable<MockB2Object> objects, string? thumbnailDirectory)
        {
            var thumbnailDir = string.IsNullOrWhiteSpace(thumbnailDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "mock-thumbnails")
                : thumbnailDirectory;
            var fallbackImagePath = Path.Combine(AppContext.BaseDirectory, "test.jpg");

            try
            {
                if (Directory.Exists(thumbnailDir))
                {
                    Directory.Delete(thumbnailDir, recursive: true);
                }
            }
            catch (IOException)
            {
                // Keep going with any existing files if the folder is transiently locked.
            }

            var files = new Dictionary<string, MockStoredFile>(StringComparer.Ordinal);
            var nextId = 1;

            foreach (var item in objects.OrderBy(file => file.FileName, StringComparer.Ordinal))
            {
                var fileId = string.IsNullOrWhiteSpace(item.Id) ? $"mock-{nextId:D4}" : item.Id;
                nextId++;

                files[item.FileName] = new MockStoredFile(
                    new B2File
                    {
                        ContentType = item.ContentType,
                        FileName = item.FileName,
                        Id = fileId,
                        Size = item.Size ?? item.Content.LongLength.ToString(),
                        Type = "upload"
                    },
                    item.Content.ToArray());
            }

            WriteThumbnails(files.Values.Select(file => file.Metadata).ToList(), thumbnailDir);

            return new MockLibrary(files, thumbnailDir, fallbackImagePath, nextId);
        }

        private static void WriteThumbnails(IReadOnlyList<B2File> files, string thumbnailDir)
        {
            var sizes = new[]
            {
                (320, 240), (240, 320), (320, 320), (320, 180), (200, 320), (320, 260), (280, 320)
            };
            var relativePaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (var file in files)
            {
                if (file.ContentType.StartsWith("audio/", StringComparison.Ordinal))
                {
                    continue;
                }

                relativePaths.Add(file.FileName);

                var separator = file.FileName.IndexOf('/');
                if (separator > 0)
                {
                    relativePaths.Add($"{file.FileName[..separator]}/thumb.jpg");
                }
            }

            foreach (var relativePath in relativePaths)
            {
                if (!ThumbnailPathResolver.TryResolve(thumbnailDir, relativePath, out var fullPath))
                {
                    continue;
                }

                var hash = relativePath.Aggregate(17, (current, character) => (current * 31) + character);
                var (width, height) = sizes[Math.Abs(hash) % sizes.Length];

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                    using var image = new Image<Rgba32>(width, height);
                    image.SaveAsJpeg(fullPath);
                }
                catch (IOException)
                {
                    // A mock thumbnail that cannot be written must not stop the app starting.
                }
            }
        }

        private static IReadOnlyList<MockB2Object> CreateDefaultObjects()
        {
            var jpeg = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "test.jpg"));
            var mp4 = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "test.mp4"));
            var mp3 = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "test.mp3"));
            var webp = CreateWebpBytes();

            return
            [
                new MockB2Object("gallery-alpha/image-01.jpg", "image/jpeg", jpeg, "mock-image-01", "666kb"),
                new MockB2Object("gallery-alpha/image-02.jpg", "image/jpeg", jpeg, "mock-image-02", "666kb"),
                new MockB2Object("gallery-beta/image-03.jpg", "image/jpeg", jpeg, "mock-image-03", "666kb"),
                new MockB2Object("gallery-gamma/image-04.jpg", "image/jpeg", jpeg, "mock-image-04", "666kb"),
                new MockB2Object("gallery-alpha/animated.webp", "image/webp", webp, "mock-image-webp-01", "512kb"),
                new MockB2Object("gallery-beta/landscape.webp", "image/webp", webp, "mock-image-webp-02", "512kb"),
                new MockB2Object("legacy-video.avi", "video/x-msvideo", mp4, "mock-video-legacy-01", "19 MB"),
                new MockB2Object("gallery-alpha/legacy-video.mov", "video/quicktime", mp4, "mock-video-legacy-02", "19 MB"),
                new MockB2Object("test.mp4", "video/mp4", mp4, "mock-video-01", "19 MB"),
                new MockB2Object("test-02.mp4", "video/mp4", mp4, "mock-video-02", "19 MB"),
                new MockB2Object("test-03.mp4", "video/mp4", mp4, "mock-video-03", "19 MB"),
                new MockB2Object("test.mp3", "audio/mpeg", mp3, "mock-audio-01", "6 MB"),
                new MockB2Object("test-02.mp3", "audio/mpeg", mp3, "mock-audio-02", "6 MB"),
                new MockB2Object("test-03.mp3", "audio/mpeg", mp3, "mock-audio-03", "6 MB")
            ];
        }

        private static byte[] CreateWebpBytes()
        {
            using var buffer = new MemoryStream();
            using (var image = new Image<Rgba32>(64, 48))
            {
                image.SaveAsWebp(buffer);
            }

            return buffer.ToArray();
        }

        private static B2File Clone(B2File file)
        {
            return new B2File
            {
                ChildCount = file.ChildCount,
                ContentType = file.ContentType,
                FileName = file.FileName,
                Id = file.Id,
                IsFavoureite = file.IsFavoureite,
                PreviewHeight = file.PreviewHeight,
                PreviewWidth = file.PreviewWidth,
                Size = file.Size,
                Tags = file.Tags.ToArray(),
                Thumbnail = file.Thumbnail,
                Type = file.Type
            };
        }

        public sealed record MockB2Object(string FileName, string ContentType, byte[] Content, string? Id = null, string? Size = null);

        internal sealed class MockLibrary(
            Dictionary<string, MockStoredFile> files,
            string thumbnailDir,
            string fallbackImagePath,
            int nextId)
        {
            public Dictionary<string, MockStoredFile> Files { get; } = files;
            public string ThumbnailDir { get; } = thumbnailDir;
            public string FallbackImagePath { get; } = fallbackImagePath;
            public int NextId { get; set; } = nextId;
        }

        internal sealed record MockStoredFile(B2File Metadata, byte[] Content);
    }
}
