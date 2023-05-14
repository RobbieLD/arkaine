﻿using Server.Arkaine.Favourites;

namespace Server.Arkaine.B2
{
    public interface IB2Service
    {
        Task<AuthResponse> GetToken(string key, CancellationToken cancellationToken);
        Task<FilesResponse> ListFiles(FilesRequest request, string userName, IFavouritesService? favouritesService, CancellationToken cancellationToken);
        Task<IResult> Stream(string userName, string fileName, CancellationToken cancellationToken);
        IResult Preview(string fileName);
        Task<UploadResponse> Upload(string fileName, string contentType, StreamContent content, CancellationToken cancellationToken);
        Task<UploadResponse> UploadParts(string fileName, string contentType, Stream content, CancellationToken cancellationToken);
        Task<Stream> Download(string userName, string fileName, CancellationToken cancellationToken);
    }
}
