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
            app.MapHub<IngestHub>("/progress");

            app.MapPost("/ingest",
                [AllowAnonymous]
            async (IngestRequest request, IOptions<ArkaineOptions> config, IBackgroundTaskQueue queue, CancellationToken cancellationToken) =>
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

                    await queue.EnqueueAsync(request, cancellationToken);

                    return Results.Ok("Request Enqueued");
                });
        }
    }
}
