namespace Server.Arkaine.B2
{
    public class MockB2 : IB2Service
    {
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

        public Task<AlbumsResponse> ListAlbums(string userName, CancellationToken cancellationToken)
        {
            var rand = new Random();
            var buckets = new List<Bucket>();

            for (int i = 0; i < rand.Next(1, 15); i++)
            {
                var unique = rand.Next(10, 100).ToString();

                buckets.Add(new Bucket
                {
                    AccountId = "test",
                    BucketId = unique,
                    Name = "bucket_" + unique,
                });
            }

            return Task.FromResult(new AlbumsResponse
            {
                Buckets = buckets
            });
        }

        public Task<FilesResponse> ListFiles(FilesRequest request, string userName, CancellationToken cancellationToken)
        {
            var rand = new Random();
            var files = new List<B2File>();

            for (int i = 0; i < rand.Next(1, 30); i++)
            {
                files.Add(new B2File
                {
                    ContentType = "image/jpeg",
                    FileName = "test.jpg",
                    Rating = new Meta.Rating
                    {
                        Bucket = request.BucketId,
                        FileName = "jpg",
                        Id = i,
                        Value = 0
                    },
                    Size = "666kb"
                });
            }

            return Task.FromResult(new FilesResponse { Files = files });
        }

        public Task<IResult> Stream(string userName, string bucketName, string fileName, CancellationToken cancellationToken)
        {
            return Task.FromResult(Results.Stream(File.OpenRead("test.jpg"), contentType: "image/jpg", enableRangeProcessing: false));
        }

        public Task<UploadResponse> Upload(string bucketId, string fileName, string contentType, long length, StreamContent content, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
