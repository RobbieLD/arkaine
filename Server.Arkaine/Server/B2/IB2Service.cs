namespace Server.Arkaine.B2
{
    public interface IB2Service
    {
        Task<AuthResponse> GetToken(string key, CancellationToken cancellationToken);
        Task<AlbumsResponse> ListAlbums(string userName, CancellationToken cancellationToken);
        Task<FilesResponse> ListFiles(FilesRequest request, string userName, CancellationToken cancellationToken);
        Task<IResult> Stream(string userName, string bucketName, string fileName, CancellationToken cancellationToken);
        Task<UploadResponse> Upload(string bucketName, string fileName, Stream stream, CancellationToken cancellationToken);
    }
}
