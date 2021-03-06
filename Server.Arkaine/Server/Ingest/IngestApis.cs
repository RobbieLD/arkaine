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

                    try
                    {
                        var extractor = extractorFactory.GetExtractor(request.Url);
                        var resp = await extractor.Extract(request.Url, request.Name, cancellationToken);
                        var content = new StreamContent(resp.Content);
                        var response = await b2ervice.Upload(extractor.Bucket, resp.FileName, resp.MimeType, resp.Length, content, cancellationToken);
                        return Results.Ok(response);
                    }
                    catch (Exception ex)
                    {
                            return Results.Problem(ex.Message);
                    }
                });
        }
    }
}
