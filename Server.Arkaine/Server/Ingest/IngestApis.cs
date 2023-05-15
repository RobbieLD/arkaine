using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Server.Arkaine.B2;

namespace Server.Arkaine.Ingest
{
    public static class IngestApis
    {
        public static void RegisterIngestApis(this WebApplication app)
        {
            app.MapHub<UpdateHub>("/updates");

            app.MapPost("/ingest",
                [AllowAnonymous]
            async (IngestRequest request, CancellationToken cancellationToken, IOptions<ArkaineOptions> config, IB2Service b2ervice, IMemoryCache cache, IExtractorFactory extractorFactory) =>
                {
                    // Validate api key
                    if (request.Key != config.Value.API_KEY)
                    {
                        return Results.Unauthorized();
                    }

                    if (string.IsNullOrEmpty(request.Url))
                    {
                        return Results.BadRequest("No url supplied");
                    }

                    var extractor = extractorFactory.GetExtractor(request.Url);
                    var resp = await extractor.Extract(request.Url, request.Name, cancellationToken);
                    
                    // If the content is greater than 20mb 
                    // These methods are fire and forget because the calling client opens a web sockets connection to listen for updates
                    if (resp.Length > config.Value.UPLOAD_CHUNK_SIZE)
                    {
                        await Task.Run(() => b2ervice.UploadParts(resp.FileName, resp.MimeType, resp.Content, cancellationToken)).ConfigureAwait(false);
                    }
                    else
                    {
                        await Task.Run(() => b2ervice.Upload(resp.FileName, resp.MimeType, new StreamContent(resp.Content), cancellationToken)).ConfigureAwait(false);
                    }

                    return Results.Ok();
                });
        }
    }
}
