using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace Server.Arkaine.LocalB2;

public static class LocalB2Endpoints
{
    private const string AuthorizationToken = "local-b2-token";

    public static void MapLocalB2(this WebApplication app)
    {
        app.MapGet("/", () => Results.Ok(new { status = "Local B2 is running." }));
        app.MapGet("/status", () => Results.Ok(new { status = "Local B2 is running." }));
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

        app.MapGet("/b2api/v2/b2_authorize_account", (HttpRequest request, LocalB2Options options) =>
        {
            var baseUrl = GetBaseUrl(request);
            return Results.Ok(new
            {
                accountId = "local-account",
                authorizationToken = AuthorizationToken,
                apiUrl = baseUrl,
                downloadUrl = baseUrl
            });
        });

        app.MapPost(
            "/b2api/v2/b2_list_file_names",
            (LocalB2ListRequest request, LocalB2Options options, LocalB2Store store, CancellationToken cancellationToken) =>
                ExecuteAsync(() =>
                {
                    EnsureBucket(request.BucketId, options);
                    return ListFilesAsync(request, store, cancellationToken);
                }));

        app.MapGet(
            "/file/{bucketName}/{*fileName}",
            (string bucketName, string? fileName, LocalB2Options options, LocalB2Store store, CancellationToken cancellationToken) =>
                ExecuteAsync(() => DownloadAsync(bucketName, fileName, options, store, cancellationToken)));

        app.MapPost(
            "/b2api/v2/b2_get_upload_url",
            (LocalB2UploadUrlRequest request, HttpRequest httpRequest, LocalB2Options options) =>
            {
                try
                {
                    EnsureBucket(request.BucketId, options);
                    return Results.Ok(new
                    {
                        authorizationToken = AuthorizationToken,
                        uploadUrl = $"{GetBaseUrl(httpRequest)}/b2api/v2/b2_upload_file"
                    });
                }
                catch (LocalB2RequestException exception)
                {
                    return ToError(exception);
                }
            });

        app.MapPost(
            "/b2api/v2/b2_upload_file",
            (HttpRequest request, LocalB2Store store, CancellationToken cancellationToken) =>
                ExecuteAsync(() => UploadFileAsync(request, store, cancellationToken)));

        app.MapPost(
            "/b2api/v2/b2_delete_file_version",
            (LocalB2DeleteRequest request, LocalB2Store store, CancellationToken cancellationToken) =>
                ExecuteAsync(() => DeleteFileAsync(request, store, cancellationToken)));

        app.MapPost(
            "/b2api/v2/b2_copy_file",
            (LocalB2CopyRequest request, LocalB2Store store, CancellationToken cancellationToken) =>
                ExecuteAsync(() => CopyFileAsync(request, store, cancellationToken)));

        app.MapPost(
            "/b2api/v2/b2_list_unfinished_large_files",
            (LocalB2UnfinishedRequest request, LocalB2Options options, LocalB2Store store, CancellationToken cancellationToken) =>
                ExecuteAsync(() =>
                {
                    EnsureBucket(request.BucketId, options);
                    return ListUnfinishedFilesAsync(request, store, cancellationToken);
                }));

        app.MapPost(
            "/b2api/v2/b2_start_large_file",
            (LocalB2StartMultipartRequest request, LocalB2Options options, LocalB2Store store, CancellationToken cancellationToken) =>
                ExecuteAsync(() =>
                {
                    EnsureBucket(request.BucketId, options);
                    return StartMultipartAsync(request, store, cancellationToken);
                }));

        app.MapPost(
            "/b2api/v2/b2_get_upload_part_url",
            (LocalB2GetPartUrlRequest request, HttpRequest httpRequest, LocalB2Store store) =>
            {
                try
                {
                    if (!store.HasMultipart(request.FileId))
                    {
                        throw new LocalB2RequestException(StatusCodes.Status404NotFound, "The multipart upload was not found.");
                    }

                    return Results.Ok(new
                    {
                        authorizationToken = AuthorizationToken,
                        uploadUrl = $"{GetBaseUrl(httpRequest)}/b2api/v2/b2_upload_part?fileId={Uri.EscapeDataString(request.FileId)}"
                    });
                }
                catch (LocalB2RequestException exception)
                {
                    return ToError(exception);
                }
            });

        app.MapPost(
            "/b2api/v2/b2_upload_part",
            (HttpRequest request, LocalB2Store store, CancellationToken cancellationToken) =>
                ExecuteAsync(() => UploadPartAsync(request, store, cancellationToken)));

        app.MapPost(
            "/b2api/v2/b2_finish_large_file",
            (LocalB2FinishMultipartRequest request, LocalB2Store store, CancellationToken cancellationToken) =>
                ExecuteAsync(() => FinishMultipartAsync(request, store, cancellationToken)));
    }

    private static async Task<IResult> ListFilesAsync(
        LocalB2ListRequest request,
        LocalB2Store store,
        CancellationToken cancellationToken)
    {
        var prefix = store.ValidatePrefix(request.Prefix);
        var files = (await store.ListFilesAsync(cancellationToken))
            .Where(file => file.FileName.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();

        IEnumerable<LocalB2File> listedFiles = files;
        if (!string.IsNullOrEmpty(request.Delimiter))
        {
            var grouped = new Dictionary<string, LocalB2File>(StringComparer.Ordinal);
            foreach (var file in files)
            {
                var relativeName = file.FileName[prefix.Length..];
                var delimiterIndex = relativeName.IndexOf(request.Delimiter, StringComparison.Ordinal);
                if (delimiterIndex < 0)
                {
                    grouped.TryAdd(file.FileName, file);
                    continue;
                }

                var folderName = relativeName[..(delimiterIndex + request.Delimiter.Length)];
                var folderPath = prefix + folderName;
                grouped.TryAdd(
                    folderPath,
                    new LocalB2File(
                        folderPath,
                        LocalB2Store.GetFileId(folderPath),
                        "application/x-directory",
                        0,
                        string.Empty,
                        "folder"));
            }

            listedFiles = grouped.Values;
        }

        var orderedFiles = listedFiles
            .OrderBy(file => file.FileName, StringComparer.Ordinal)
            .Where(file => string.IsNullOrEmpty(request.StartFileName) ||
                           string.Compare(
                               file.FileName,
                               request.StartFileName,
                               StringComparison.Ordinal) > 0)
            .ToList();
        var limit = request.MaxFileCount > 0 ? request.MaxFileCount : 1000;
        var page = orderedFiles.Take(limit).ToList();

        return Results.Ok(new
        {
            files = page.Select(ToResponseFile).ToArray(),
            nextFileName = orderedFiles.Count > page.Count && page.Count > 0
                ? page[^1].FileName
                : string.Empty
        });
    }

    private static async Task<IResult> DownloadAsync(
        string bucketName,
        string? fileName,
        LocalB2Options options,
        LocalB2Store store,
        CancellationToken cancellationToken)
    {
        EnsureBucket(bucketName, options);
        if (string.IsNullOrEmpty(fileName))
        {
            throw new LocalB2RequestException(StatusCodes.Status400BadRequest, "A file name must be supplied.");
        }

        var file = await store.GetFileAsync(fileName, cancellationToken);
        return file is null
            ? Results.NotFound()
            : Results.File(file.FullPath, file.ContentType, enableRangeProcessing: true);
    }

    private static async Task<IResult> UploadFileAsync(
        HttpRequest request,
        LocalB2Store store,
        CancellationToken cancellationToken)
    {
        var encodedFileName = request.Headers["X-Bz-File-Name"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(encodedFileName))
        {
            throw new LocalB2RequestException(StatusCodes.Status400BadRequest, "The X-Bz-File-Name header is required.");
        }

        var fileName = DecodeFileName(encodedFileName);
        var file = await store.UploadAsync(
            fileName,
            request.ContentType ?? "application/octet-stream",
            request.Body,
            cancellationToken);

        return Results.Ok(new
        {
            fileName = file.FileName,
            contentLength = file.Length
        });
    }

    private static async Task<IResult> DeleteFileAsync(
        LocalB2DeleteRequest request,
        LocalB2Store store,
        CancellationToken cancellationToken)
    {
        var deleted = await store.DeleteAsync(request.FileName, request.FileId, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        return Results.Ok(new
        {
            fileName = request.FileName,
            fileId = request.FileId,
            action = "delete"
        });
    }

    private static async Task<IResult> CopyFileAsync(
        LocalB2CopyRequest request,
        LocalB2Store store,
        CancellationToken cancellationToken)
    {
        var file = await store.CopyAsync(request.SourceFileId, request.FileName, cancellationToken);
        return file is null
            ? Results.NotFound()
            : Results.Ok(new
            {
                fileName = file.FileName,
                fileId = file.FileId,
                action = "copy"
            });
    }

    private static async Task<IResult> ListUnfinishedFilesAsync(
        LocalB2UnfinishedRequest request,
        LocalB2Store store,
        CancellationToken cancellationToken)
    {
        var files = await store.ListMultipartAsync(
            request.NamePrefix,
            request.MaxFileCount,
            cancellationToken);
        return Results.Ok(new
        {
            files = files.Select(file => new
            {
                fileName = file.FileName,
                contentType = file.ContentType,
                contentLength = file.Length,
                action = "upload",
                fileId = file.FileId
            }).ToArray(),
            nextFileName = string.Empty
        });
    }

    private static async Task<IResult> StartMultipartAsync(
        LocalB2StartMultipartRequest request,
        LocalB2Store store,
        CancellationToken cancellationToken)
    {
        var fileId = await store.StartMultipartAsync(
            request.FileName,
            request.ContentType,
            cancellationToken);
        return Results.Ok(new
        {
            fileId
        });
    }

    private static async Task<IResult> UploadPartAsync(
        HttpRequest request,
        LocalB2Store store,
        CancellationToken cancellationToken)
    {
        var fileId = request.Query["fileId"].FirstOrDefault();
        var partNumberValue = request.Headers["X-Bz-Part-Number"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(fileId) ||
            !int.TryParse(partNumberValue, NumberStyles.None, CultureInfo.InvariantCulture, out var partNumber))
        {
            throw new LocalB2RequestException(StatusCodes.Status400BadRequest, "The multipart upload ID and part number are required.");
        }

        var checksum = request.Headers["X-Bz-Content-Sha1"].FirstOrDefault();
        await store.WriteMultipartPartAsync(
            fileId,
            partNumber,
            checksum,
            request.Body,
            cancellationToken);
        return Results.Ok(new
        {
            fileId,
            partNumber,
            contentLength = request.ContentLength ?? 0,
            contentSha1 = checksum ?? string.Empty
        });
    }

    private static async Task<IResult> FinishMultipartAsync(
        LocalB2FinishMultipartRequest request,
        LocalB2Store store,
        CancellationToken cancellationToken)
    {
        var file = await store.FinishMultipartAsync(
            request.FileId,
            request.PartSha1Array?.ToArray() ?? [],
            cancellationToken);
        return file is null
            ? Results.NotFound()
            : Results.Ok(new
            {
                action = "upload",
                fileName = file.FileName,
                fileId = file.FileId
            });
    }

    private static object ToResponseFile(LocalB2File file)
    {
        return new
        {
            fileName = file.FileName,
            contentType = file.ContentType,
            contentLength = file.Length,
            action = file.Type,
            fileId = file.FileId
        };
    }

    private static string DecodeFileName(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value.Replace("+", " ", StringComparison.Ordinal));
        }
        catch (UriFormatException exception)
        {
            throw new LocalB2RequestException(StatusCodes.Status400BadRequest, "The file name is not valid URL encoding.", exception);
        }
    }

    private static void EnsureBucket(string? bucket, LocalB2Options options)
    {
        if (string.IsNullOrWhiteSpace(bucket))
        {
            throw new LocalB2RequestException(StatusCodes.Status400BadRequest, "A bucket ID is required.");
        }

        if (!string.Equals(bucket, options.BucketId, StringComparison.Ordinal) &&
            !string.Equals(bucket, options.BucketName, StringComparison.Ordinal))
        {
            throw new LocalB2RequestException(StatusCodes.Status404NotFound, "The requested bucket was not found.");
        }
    }

    private static string GetBaseUrl(HttpRequest request)
    {
        return $"{request.Scheme}://{request.Host}{request.PathBase}";
    }

    private static Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        return ExecuteCoreAsync(action);
    }

    private static async Task<IResult> ExecuteCoreAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (LocalB2RequestException exception)
        {
            return ToError(exception);
        }
    }

    private static IResult ToError(LocalB2RequestException exception)
    {
        return Results.Json(
            new
            {
                code = GetErrorCode(exception.StatusCode),
                status = exception.StatusCode,
                message = exception.Message
            },
            statusCode: exception.StatusCode,
            contentType: "application/json");
    }

    private static string GetErrorCode(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "bad_request",
            StatusCodes.Status404NotFound => "not_found",
            StatusCodes.Status409Conflict => "conflict",
            _ => "local_b2_error"
        };
    }
}
