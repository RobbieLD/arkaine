using Server.Arkaine.Favourites;
using Server.Arkaine.Ingest;

namespace Server.Arkaine.B2
{
    public interface IB2Service
    {
        Task<AuthResponse> GetToken(string key, CancellationToken cancellationToken);
        Task<FilesResponse> ListFiles(FilesRequest request, string userName, IFavouritesService? favouritesService, CancellationToken cancellationToken);
        Task<IResult> Stream(string userName, string fileName, CancellationToken cancellationToken);
        IResult Preview(string fileName);
        Task Upload(IngestRequest request, CancellationToken cancellationToken);
        Task<Stream> Download(string userName, string fileName, CancellationToken cancellationToken);
    }
}
