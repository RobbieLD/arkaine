using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Server.Arkaine.B2;
using System.Security.Cryptography;
using System.Text;

namespace Server.Arkaine.Ingest
{
    public static class IngestApis
    {
        public static void RegisterIngestApis(this WebApplication app)
        {
            app.MapHub<IngestHub>("/progress");

            app.MapPost("/ingest",
                [AllowAnonymous]
            async (IngestRequest request, HttpRequest httpRequest, IOptions<ArkaineOptions> config, IBackgroundTaskQueue queue, CancellationToken cancellationToken) =>
                {
                    // Validate api key
                    var configuredKey = config.Value.API_KEY;
                    var suppliedKey = httpRequest.Headers["X-Arkaine-Api-Key"];
                    if (string.IsNullOrWhiteSpace(configuredKey) ||
                        suppliedKey.Count != 1 ||
                        string.IsNullOrEmpty(suppliedKey[0]) ||
                        !CryptographicOperations.FixedTimeEquals(
                            Encoding.UTF8.GetBytes(suppliedKey[0]!),
                            Encoding.UTF8.GetBytes(configuredKey)))
                    {
                        return Results.Unauthorized();
                    }

                    if (string.IsNullOrWhiteSpace(request.Url))
                    {
                        return Results.BadRequest("No url supplied");
                    }

                    if (!await UrlSafetyValidator.IsSafeAsync(request.Url, cancellationToken))
                    {
                        return Results.BadRequest("The url must be an absolute public HTTPS URL");
                    }

                    if (string.IsNullOrWhiteSpace(request.Name) ||
                        request.Name.Length > 255 ||
                        request.Name.IndexOfAny(new[] { '/', '\\', ':', '\0' }) >= 0 ||
                        request.Name is "." or "..")
                    {
                        return Results.BadRequest("The name must be a single safe file name");
                    }

                    await queue.EnqueueAsync(request, cancellationToken);

                    return Results.Ok("Request Enqueued");
                });
        }
    }
}
