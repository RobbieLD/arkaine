using Server.Arkaine.Favourites;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Server.Arkaine.B2
{
    public class MockB2 : IB2Service
    {
        private static readonly Lazy<MockLibrary> Library = new(CreateLibrary);

        private readonly IThumbnailInfoProvider _thumbnails;

        public MockB2(IThumbnailInfoProvider thumbnails)
        {
            _thumbnails = thumbnails;
        }

        private sealed record MockLibrary(IReadOnlyList<B2File> Files, string ThumbnailDir);

        public Task<Stream> Download(string userName, string fileName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<AuthResponse> GetToken(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AuthResponse
            {
                AccountId = "test",
                ApiBaseUrl= "test",
                DownloadBaseUrl= "test",
                Token = "test"
            });
        }

        public Task<FilesResponse> ListFiles(FilesRequest request, string userName, IFavouritesService? favouriteService, CancellationToken cancellationToken)
        {
            var library = Library.Value;
            var prefix = request.Prefix?.TrimStart('/') ?? string.Empty;
            if (prefix.Length > 0 && !prefix.EndsWith('/'))
            {
                prefix += "/";
            }

            var listedFiles = new Dictionary<string, B2File>(StringComparer.Ordinal);

            foreach (var file in library.Files)
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
                    var childCount = library.Files.Count(file =>
                        file.FileName.StartsWith(folderPath, StringComparison.Ordinal) &&
                        !file.FileName[folderPath.Length..].Contains('/'));

                    listedFiles.TryAdd(folderPath, new B2File
                    {
                        ChildCount = childCount,
                        ContentType = "application/x-directory",
                        FileName = folderPath,
                        Type = "folder",
                        Id = $"mock-{folderPath}"
                    });
                    continue;
                }

                listedFiles.TryAdd(file.FileName, file);
            }

            var orderedFiles = listedFiles.Values
                .OrderBy(file => file.FileName, StringComparer.Ordinal)
                .ToList();
            var startFile = request.StartFile;
            var filesAfterStart = string.IsNullOrEmpty(startFile)
                ? orderedFiles
                : orderedFiles
                    .Where(file => string.Compare(file.FileName, startFile, StringComparison.Ordinal) > 0)
                    .ToList();
            var pageSize = request.PageSize > 0 ? request.PageSize : 25;
            var files = filesAfterStart.Take(pageSize).ToList();

            // Uses the same resolver and dimension cache as the real service so local
            // development reflects production behaviour, including the cache statistics.
            foreach (var file in files)
            {
                ThumbnailPreview.Populate(file, library.ThumbnailDir, _thumbnails);
            }

            return Task.FromResult(new FilesResponse
            {
                Files = files,
                NextFileName = filesAfterStart.Count > files.Count && files.Count > 0
                    ? files[^1].FileName
                    : string.Empty
            });
        }

        public IResult Preview(string fileName)
        {
            if (ThumbnailPathResolver.TryResolve(Library.Value.ThumbnailDir, fileName, out var path) &&
                File.Exists(path))
            {
                return Results.File(path, contentType: "image/jpeg");
            }

            return Results.Stream(File.OpenRead("test.jpg"), contentType: "image/jpeg");
        }

        public Task<IResult> Stream(string userName, string fileName, CancellationToken cancellationToken)
        {
            var (testFile, contentType, enableRangeProcessing) = Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".mp4" => ("test.mp4", "video/mp4", true),
                ".mp3" => ("test.mp3", "audio/mpeg", true),
                _ => ("test.jpg", "image/jpeg", false)
            };

            return Task.FromResult(Results.Stream(
                File.OpenRead(testFile),
                contentType: contentType,
                enableRangeProcessing: enableRangeProcessing));
        }

        public Task UploadMultiPartFile(string fileName, string contentType, Stream content, int chunkSize, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task UploadSingleFile(string fileName, string contentType, long length, Stream content, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private static MockLibrary CreateLibrary()
        {
            var files = CreateMockFiles();
            var thumbnailDir = Path.Combine(AppContext.BaseDirectory, "mock-thumbnails");

            WriteThumbnails(files, thumbnailDir);

            return new MockLibrary(files, thumbnailDir);
        }

        /// <summary>
        /// Writes a real JPEG per mock file at varied dimensions so the gallery gets genuine
        /// aspect ratios and the dimension cache has something to read. Audio files are left
        /// without a thumbnail, which mirrors a partially generated library.
        /// </summary>
        private static void WriteThumbnails(IReadOnlyList<B2File> files, string thumbnailDir)
        {
            var sizes = new[]
            {
                (320, 240), (240, 320), (320, 320), (320, 180), (200, 320), (320, 260), (280, 320)
            };

            var relativePaths = new List<string>();

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

            foreach (var relativePath in relativePaths.Distinct(StringComparer.Ordinal))
            {
                if (!ThumbnailPathResolver.TryResolve(thumbnailDir, relativePath, out var fullPath))
                {
                    continue;
                }

                if (File.Exists(fullPath))
                {
                    continue;
                }

                // Deterministic so the dimensions survive a restart and stay comparable.
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

        private static IReadOnlyList<B2File> CreateMockFiles()
        {
            var random = Random.Shared;
            var folders = new List<string>();
            var folderCount = random.Next(3, 6);

            while (folders.Count < folderCount)
            {
                var folder = $"gallery-{random.Next(1000, 10000)}";
                if (!folders.Contains(folder, StringComparer.Ordinal))
                {
                    folders.Add(folder);
                }
            }

            var files = new List<B2File>();
            var imageCount = random.Next(12, 25);

            for (var index = 0; index < imageCount; index++)
            {
                var folder = index < folders.Count
                    ? folders[index]
                    : folders[random.Next(folders.Count)];

                files.Add(new B2File
                {
                    ContentType = "image/jpeg",
                    FileName = $"{folder}/image-{index + 1:00}.jpg",
                    Id = $"mock-image-{index + 1:00}",
                    Size = "666kb",
                    Type = "upload"
                });
            }

            for (var index = 1; index <= 3; index++)
            {
                files.Add(new B2File
                {
                    ContentType = "video/mp4",
                    FileName = index == 1 ? "test.mp4" : $"test-{index:00}.mp4",
                    Id = $"mock-video-{index:00}",
                    Size = "19 MB",
                    Type = "upload"
                });
            }

            for (var index = 1; index <= 3; index++)
            {
                files.Add(new B2File
                {
                    ContentType = "audio/mpeg",
                    FileName = index == 1 ? "test.mp3" : $"test-{index:00}.mp3",
                    Id = $"mock-audio-{index:00}",
                    Size = "6 MB",
                    Type = "upload"
                });
            }

            return files;
        }
    }
}
