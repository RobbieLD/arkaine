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
                    var stream = await extractor.Extract(request.Url);
                    string name = new Uri(request.Url).LocalPath;
                    var response = await b2ervice.Upload(extractor.Bucket, name, stream, cancellationToken);
                    return Results.Ok(response);
                });
        }
    }
}
