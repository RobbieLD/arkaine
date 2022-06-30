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
                async (IngestRequest request, CancellationToken cancellationToken, IOptions<ArkaineOptions> config, IB2Service tokenService, IMemoryCache cache) =>
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

                    var extractor = ExtractorHandlerFactory.GetExtractor(request.Url);
                    var stream = extractor.Extract(request.Url);

                    // TODO: upload file to B2
                });
        }
    }
}
