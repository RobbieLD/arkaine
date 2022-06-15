namespace Server.Arkaine.B2
{
    public interface IB2Service
    {
        Task<AuthResponse> GetToken(CancellationToken cancellationToken);
        Task<AlbumsResponse> ListAlbums(AlbumsRequest request, string userName, CancellationToken cancellationToken);
        Task<FilesResponse> ListFiles(FilesRequest request, string userName, CancellationToken cancellationToken);
        Task<IResult> Stream(string bucketName, string fileName, CancellationToken cancellationToken);
    }
}
