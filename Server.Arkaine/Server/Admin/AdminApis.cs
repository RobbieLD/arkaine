using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Arkaine.B2;
using Server.Arkaine.Media;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;

namespace Server.Arkaine.Admin
{
    public static class AdminApis
    {
        public static void RegisterAdminApis(this WebApplication app)
        {
            app.MapGet("/admin/status",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            async (ThumbnailManager thumbnailManager, ConversionManager conversionManager, IThumbnailInfoProvider cache, IMediaConverter converter, CancellationToken cancellationToken) =>
            {
                return Results.Ok(await CreateStatusResponse(thumbnailManager, conversionManager, cache, converter, cancellationToken));
            });

            app.MapGet("/admin",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            async (ThumbnailManager thumbnailManager, ConversionManager conversionManager, IThumbnailInfoProvider cache, IMediaConverter converter, CancellationToken cancellationToken) =>
            {
                return Results.Ok(await CreateStatusResponse(thumbnailManager, conversionManager, cache, converter, cancellationToken));
            });

            app.MapGet("/admin/reports",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            async (IProcessingReportService reports, CancellationToken cancellationToken) =>
            {
                return Results.Ok(await reports.ListAsync(cancellationToken));
            });

            app.MapGet("/admin/reports/{id:int}",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            async (int id, IProcessingReportService reports, CancellationToken cancellationToken) =>
            {
                if (id <= 0)
                {
                    return Results.BadRequest("A report id must be positive.");
                }

                var report = await reports.GetAsync(id, cancellationToken);
                if (report is null)
                {
                    return Results.NotFound();
                }

                var fileName = $"{report.Type}-{report.Name.Replace(':', '-')}.html";
                return Results.File(
                    Encoding.UTF8.GetBytes(report.Html),
                    "text/html; charset=utf-8",
                    fileName);
            });

            app.MapPost("/admin/reports/clear",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            async (IProcessingReportService reports, CancellationToken cancellationToken) =>
            {
                await reports.ClearAsync(cancellationToken);
                return Results.NoContent();
            });

            app.MapGet("/admin/conversion/paths",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            async (ClaimsPrincipal user, IB2Service b2, CancellationToken cancellationToken) =>
            {
                var userName = user?.Identity?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return Results.BadRequest("User name must be supplied.");
                }

                var request = new FilesRequest
                {
                    Delimiter = "/"
                };
                var paths = new HashSet<string>(StringComparer.Ordinal)
                {
                    ConversionPath.RootSelection
                };

                while (!cancellationToken.IsCancellationRequested)
                {
                    var page = await b2.ListFiles(request, userName, null, cancellationToken);

                    foreach (var file in page.Files.Where(file =>
                                 string.Equals(file.Type, "folder", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (ConversionPath.TryNormalize(file.FileName, out var path))
                        {
                            paths.Add(path);
                        }
                    }

                    if (string.IsNullOrEmpty(page.NextFileName))
                    {
                        break;
                    }

                    request.StartFile = page.NextFileName;
                }

                return Results.Ok(paths.OrderBy(path => path, StringComparer.Ordinal).ToArray());
            });

            app.MapGet("/admin/conversion/requests",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            async ([FromQuery] string? status, [FromServices] IVideoConversionRequestStore requests, CancellationToken cancellationToken) =>
            {
                return Results.Ok(await requests.ListAsync(status, cancellationToken));
            });

            app.MapPost("/admin/conversion/requests",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            async (
                VideoConversionRequestInput request,
                ClaimsPrincipal user,
                [FromServices] IOptions<ArkaineOptions> options,
                [FromServices] IB2Service b2,
                [FromServices] IVideoConversionRequestStore requests,
                [FromServices] ConversionManager conversionManager,
                [FromServices] IMediaConverter converter,
                CancellationToken cancellationToken) =>
            {
                var userName = user?.Identity?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return Results.BadRequest("User name must be supplied.");
                }

                if (!TryNormalizeVideoFileName(request.FileName, out var fileName))
                {
                    return Results.BadRequest("A valid video file name must be supplied.");
                }

                var response = await b2.ListFiles(
                    new FilesRequest
                    {
                        ExactFileName = fileName,
                        PageSize = 1
                    },
                    userName,
                    null,
                    cancellationToken);
                var file = response.Files.SingleOrDefault();
                if (file is null)
                {
                    return Results.NotFound();
                }

                if (!options.Value.IsVideoFile(file.FileName, file.ContentType) ||
                    options.Value.IsCompressedVideo(file.FileName))
                {
                    return Results.BadRequest("Only original video files can be queued for compression.");
                }

                if (!string.IsNullOrWhiteSpace(request.FileId) &&
                    !string.Equals(request.FileId, file.Id, StringComparison.Ordinal))
                {
                    return Results.Conflict("The file has changed since it was listed.");
                }

                var queued = await requests.EnqueueAsync(
                    file.FileName,
                    file.Id,
                    userName,
                    VideoConversionRequestReason.Manual,
                    cancellationToken);

                var availability = await converter.GetAvailabilityAsync(cancellationToken);
                if (availability.IsAvailable && !conversionManager.IsRunning)
                {
                    conversionManager.TryStartForFile(userName, file.FileName);
                }

                return Results.Ok(ToResponse(queued));
            });

            app.MapDelete("/admin/conversion/requests/{id:int}",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            async (int id, [FromServices] IVideoConversionRequestStore requests, CancellationToken cancellationToken) =>
            {
                if (id <= 0)
                {
                    return Results.BadRequest("A conversion request id must be positive.");
                }

                return await requests.CancelAsync(id, cancellationToken)
                    ? Results.NoContent()
                    : Results.Conflict("The conversion request was not queued.");
            });

            app.MapGet("/admin/cache",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            (IThumbnailInfoProvider cache) =>
            {
                return Results.Ok(cache.GetStats());
            });

            app.MapGet("/admin/thumbnail-cache",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            (IThumbnailInfoProvider cache) =>
            {
                return Results.Ok(cache.GetStats());
            });

            app.MapPost("/admin/cache/clear",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            (IThumbnailInfoProvider cache) =>
            {
                cache.Clear();
                return Results.Ok(cache.GetStats());
            });

            app.MapPost("/admin/thumbnail-cache/clear",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            (IThumbnailInfoProvider cache) =>
            {
                cache.Clear();
                return Results.Ok(cache.GetStats());
            });

            app.MapPost("/admin/start",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            async (AdminJobRequest request, ClaimsPrincipal user, ThumbnailManager thumbnailManager, ConversionManager conversionManager, IThumbnailInfoProvider cache, IMediaConverter converter, CancellationToken cancellationToken) =>
            {
                var userName = user?.Identity?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return Results.BadRequest("User name must be supplied.");
                }

                return await StartJobAsync(request, userName, thumbnailManager, conversionManager, cache, converter, cancellationToken);
            });

            app.MapPost("/admin/stop",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            async (AdminJobRequest request, ThumbnailManager thumbnailManager, ConversionManager conversionManager, IThumbnailInfoProvider cache, IMediaConverter converter, CancellationToken cancellationToken) =>
            {
                return await StopJobAsync(request, thumbnailManager, conversionManager, cache, converter, cancellationToken);
            });

            app.MapPost("/admin/thumbnails/start",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            async (ClaimsPrincipal user, ThumbnailManager thumbnailManager, ConversionManager conversionManager, IThumbnailInfoProvider cache, IMediaConverter converter, CancellationToken cancellationToken) =>
            {
                var userName = user?.Identity?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return Results.BadRequest("User name must be supplied.");
                }

                return await StartJobAsync(new AdminJobRequest { Job = "thumbnail" }, userName, thumbnailManager, conversionManager, cache, converter, cancellationToken);
            });

            app.MapPost("/admin/thumbnails/stop",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            async (ThumbnailManager thumbnailManager, ConversionManager conversionManager, IThumbnailInfoProvider cache, IMediaConverter converter, CancellationToken cancellationToken) =>
            {
                return await StopJobAsync(new AdminJobRequest { Job = "thumbnail" }, thumbnailManager, conversionManager, cache, converter, cancellationToken);
            });

            app.MapPost("/admin/conversion/start",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            async (AdminJobRequest request, ClaimsPrincipal user, ThumbnailManager thumbnailManager, ConversionManager conversionManager, IThumbnailInfoProvider cache, IMediaConverter converter, CancellationToken cancellationToken) =>
            {
                var userName = user?.Identity?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return Results.BadRequest("User name must be supplied.");
                }

                return await StartJobAsync(new AdminJobRequest
                {
                    Path = request.Path,
                    Job = "conversion"
                }, userName, thumbnailManager, conversionManager, cache, converter, cancellationToken);
            });

            app.MapPost("/admin/convert/start",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            async (AdminJobRequest request, ClaimsPrincipal user, ThumbnailManager thumbnailManager, ConversionManager conversionManager, IThumbnailInfoProvider cache, IMediaConverter converter, CancellationToken cancellationToken) =>
            {
                var userName = user?.Identity?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return Results.BadRequest("User name must be supplied.");
                }

                return await StartJobAsync(new AdminJobRequest
                {
                    Job = "conversion",
                    Path = request.Path
                }, userName, thumbnailManager, conversionManager, cache, converter, cancellationToken);
            });

            app.MapPost("/admin/conversion/stop",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            async (ThumbnailManager thumbnailManager, ConversionManager conversionManager, IThumbnailInfoProvider cache, IMediaConverter converter, CancellationToken cancellationToken) =>
            {
                return await StopJobAsync(new AdminJobRequest { Job = "conversion" }, thumbnailManager, conversionManager, cache, converter, cancellationToken);
            });
            app.MapPost("/admin/convert/stop",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            async (ThumbnailManager thumbnailManager, ConversionManager conversionManager, IThumbnailInfoProvider cache, IMediaConverter converter, CancellationToken cancellationToken) =>
            {
                return await StopJobAsync(new AdminJobRequest { Job = "conversion" }, thumbnailManager, conversionManager, cache, converter, cancellationToken);
            });

            app.MapGet("/settings",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            (ThumbnailManager manager) =>
            {
                return Results.Ok(manager.GetSettings());
            });

            app.MapPost("/settings/start",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            (ThumbnailManager manager, ClaimsPrincipal user) =>
            {
                var userName = user?.Identity?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return Results.BadRequest("User name must be supplied.");
                }

                return manager.TryStart(userName)
                    ? Results.Ok(manager.GetSettings())
                    : Results.Conflict(manager.GetSettings());
            });

            app.MapPost("/settings/stop",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            (ThumbnailManager manager) =>
            {
                manager.CancelGeneration();
                return Results.Ok(manager.GetSettings());
            });

            app.MapGet("/settings/thumbnail-cache",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            (IThumbnailInfoProvider cache) =>
            {
                return Results.Ok(cache.GetStats());
            });

            app.MapPost("/settings/thumbnail-cache/clear",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
            (IThumbnailInfoProvider cache) =>
            {
                cache.Clear();
                return Results.Ok(cache.GetStats());
            });

            app.MapHub<AdminHub>("/updates");
        }

        private static async Task<IResult> StartJobAsync(
            AdminJobRequest request,
            string userName,
            ThumbnailManager thumbnailManager,
            ConversionManager conversionManager,
            IThumbnailInfoProvider cache,
            IMediaConverter converter,
            CancellationToken cancellationToken)
        {
            return NormalizeJob(request.Job) switch
            {
                "thumbnail" => thumbnailManager.TryStart(userName)
                    ? Results.Ok(await CreateStatusResponse(thumbnailManager, conversionManager, cache, converter, cancellationToken))
                    : Results.Conflict(await CreateStatusResponse(thumbnailManager, conversionManager, cache, converter, cancellationToken)),
                "conversion" => await StartConversionJobAsync(
                    request,
                    userName,
                    thumbnailManager,
                    conversionManager,
                    cache,
                    converter,
                    cancellationToken),
                _ => Results.BadRequest("Unknown admin job.")
            };
        }

        private static async Task<IResult> StartConversionJobAsync(
            AdminJobRequest request,
            string userName,
            ThumbnailManager thumbnailManager,
            ConversionManager conversionManager,
            IThumbnailInfoProvider cache,
            IMediaConverter converter,
            CancellationToken cancellationToken)
        {
            if (!ConversionPath.TryNormalize(request.Path, out _))
            {
                return Results.BadRequest("A conversion path must be supplied as the root or a top-level folder.");
            }

            var availability = await converter.GetAvailabilityAsync(cancellationToken);
            if (!availability.IsAvailable)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Media conversion is unavailable.",
                    detail: string.IsNullOrWhiteSpace(availability.Error)
                        ? "ffmpeg or the required encoders are unavailable."
                        : availability.Error);
            }

            return conversionManager.TryStart(userName, request.Path)
                ? Results.Ok(await CreateStatusResponse(thumbnailManager, conversionManager, cache, converter, cancellationToken))
                : Results.Conflict(await CreateStatusResponse(thumbnailManager, conversionManager, cache, converter, cancellationToken));
        }

        private static async Task<IResult> StopJobAsync(
            AdminJobRequest request,
            ThumbnailManager thumbnailManager,
            ConversionManager conversionManager,
            IThumbnailInfoProvider cache,
            IMediaConverter converter,
            CancellationToken cancellationToken)
        {
            switch (NormalizeJob(request.Job))
            {
                case "thumbnail":
                    thumbnailManager.CancelGeneration();
                    break;
                case "conversion":
                    conversionManager.Cancel();
                    break;
                default:
                    return Results.BadRequest("Unknown admin job.");
            }

            return Results.Ok(await CreateStatusResponse(thumbnailManager, conversionManager, cache, converter, cancellationToken));
        }

        private static async Task<AdminStatusResponse> CreateStatusResponse(
            ThumbnailManager thumbnailManager,
            ConversionManager conversionManager,
            IThumbnailInfoProvider cache,
            IMediaConverter converter,
            CancellationToken cancellationToken)
        {
            return new AdminStatusResponse(
                thumbnailManager.GetStatus(),
                conversionManager.GetStatus(),
                cache.GetStats(),
                await converter.GetAvailabilityAsync(cancellationToken));
        }

        private static string NormalizeJob(string? job)
        {
            return job?.Trim().ToLowerInvariant() switch
            {
                "thumbnail" or "thumbnails" => "thumbnail",
                "conversion" or "convert" => "conversion",
                _ => string.Empty
            };
        }

        private static bool TryNormalizeVideoFileName(string? value, out string fileName)
        {
            fileName = string.Empty;
            var candidate = value?.Trim() ?? string.Empty;
            if (candidate.Length == 0 ||
                candidate.Contains('\\') ||
                candidate.Contains('\0') ||
                candidate.StartsWith("/", StringComparison.Ordinal))
            {
                return false;
            }

            var segments = candidate.Split('/');
            if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
            {
                return false;
            }

            fileName = candidate;
            return true;
        }

        private static VideoConversionRequestResponse ToResponse(VideoConversionRequest request)
        {
            return new VideoConversionRequestResponse(
                request.Id,
                request.FileName,
                request.FileId,
                request.RequestedBy,
                request.Status,
                request.Reason,
                request.Error,
                request.RequestedUtc,
                request.UpdatedUtc);
        }
    }
}
