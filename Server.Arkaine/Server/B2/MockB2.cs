using Server.Arkaine.Favourites;

namespace Server.Arkaine.B2
{
    public class MockB2 : IB2Service
    {
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
            var rand = new Random();
            var files = new List<B2File>();

            for (int i = 0; i < rand.Next(1, 30); i++)
            {
                files.Add(new B2File
                {
                    ContentType = "image/jpeg",
                    FileName = "test.jpg",
                    Size = "666kb"
                });
            }

            return Task.FromResult(new FilesResponse { Files = files });
        }

        public IResult Preview(string fileName)
        {
            return Results.Stream(File.OpenRead("test.jpg"));
        }

        public Task<IResult> Stream(string userName, string fileName, CancellationToken cancellationToken)
        {
            return Task.FromResult(Results.Stream(File.OpenRead("test.jpg"), contentType: "image/jpg", enableRangeProcessing: false));
        }
    }
}
