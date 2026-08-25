using Server.Arkaine.Favourites;

namespace Server.Arkaine.B2
{
    public class MockB2 : IB2Service
    {
        private static readonly Lazy<IReadOnlyList<B2File>> MockFiles = new(CreateMockFiles);

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
            var prefix = request.Prefix?.TrimStart('/') ?? string.Empty;
            if (prefix.Length > 0 && !prefix.EndsWith('/'))
            {
                prefix += "/";
            }

            var listedFiles = new Dictionary<string, B2File>(StringComparer.Ordinal);

            foreach (var file in MockFiles.Value)
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
                    var childCount = MockFiles.Value.Count(file =>
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
            return Results.Stream(File.OpenRead("test.jpg"));
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
