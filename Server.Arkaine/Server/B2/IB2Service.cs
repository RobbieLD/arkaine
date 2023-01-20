namespace Server.Arkaine.B2
{
    public interface IB2Service
    {
        Task<AuthResponse> GetToken(string key, CancellationToken cancellationToken);
        Task<FilesResponse> ListFiles(FilesRequest request, string userName, CancellationToken cancellationToken);
        Task<IResult> Stream(string userName, string fileName, CancellationToken cancellationToken);
        Task<UploadResponse> Upload(string fileName, string contentType, long length, StreamContent content, CancellationToken cancellationToken);
    }
}
