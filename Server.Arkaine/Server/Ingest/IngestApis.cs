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
                    
                    UploadResponse response;

                    // If the content is greater than 20mb 
                    if (resp.Length > config.Value.UPLOAD_CHUNK_SIZE)
                    {
                        response = await b2ervice.UploadParts(resp.FileName, resp.MimeType, resp.Length, resp.Content, cancellationToken);
                    }
                    else
                    {
                        response = await b2ervice.Upload(resp.FileName, resp.MimeType, resp.Length, new StreamContent(resp.Content), cancellationToken);
                    }

                    return Results.Ok(response);
                });
        }
    }
}
