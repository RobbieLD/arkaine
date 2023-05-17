﻿using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Server.Arkaine.Favourites;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using System;
using System.Net;
using Microsoft.AspNetCore.SignalR;
using Server.Arkaine.Ingest;
using System.Linq;

namespace Server.Arkaine.B2
{
    // TODO: Add in Sha1 checking
    public class B2Service : IB2Service
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;
        private readonly IMemoryCache _cache;
        private readonly IHubContext<IngestHub> _hubContext;
        private readonly ArkaineOptions _options;

        public B2Service(
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            IOptions<ArkaineOptions> config,
            IHubContext<IngestHub> hubContext,
            ILogger<B2Service> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _cache = cache;
            _options = config.Value;
            _hubContext = hubContext;
        }

        public async Task<AuthResponse> GetToken(string key, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(key));

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.GetAsync(_options.B2AuthUrl, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseModel = JsonSerializer.Deserialize<AuthResponse>(responseString) ?? throw new("Response not in the correct form");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Auth API call responded with: {response.StatusCode}");
                throw new (responseString);
            }

            _logger.LogInformation("Get token succeeded");

            await _hubContext.Clients.All.SendAsync("update", "Get token succeeded", cancellationToken);
            
            return responseModel;
        }

        public async Task<FilesResponse> ListFiles(FilesRequest request, string userName, IFavouritesService? favouritesService, CancellationToken cancellationToken)
        {
            // Fill in config options in the request
            request.BucketId = _options.BUCKET_ID;
            request.PageSize = request.PageSize > 0 ? request.PageSize : int.Parse(_options.PAGE_SIZE);

            // This is a special case of a pseudo collection
            if (favouritesService != null && (request.Prefix?.StartsWith("Favourites") ?? false))
            {
                var favouriteResponse = await favouritesService.GetAllFavouritesPage(userName, request.PageSize, request.StartFile);

                foreach (var file in favouriteResponse.Files)
                {
                    file.Thumbnail = GetPreviewUrl(file.FileName, file.Type);
                }

                return favouriteResponse;
            }

            var response = await MakeAuthenticatedRequest<FilesRequest, FilesResponse>(request, userName, "/b2api/v2/b2_list_file_names", cancellationToken);

            IEnumerable<string> favourites = Array.Empty<string>();

            if (favouritesService != null)
            {
                favourites = await favouritesService.GetAllFavourites(userName);
            }

            // populate thumbnails
            foreach (var file in response.Files)
            {
                file.Thumbnail = GetPreviewUrl(file.FileName, file.Type);

                file.IsFavoureite = favourites.Contains(file.FileName);
            }

            return response;
        }

        public IResult Preview(string path)
        {
            return Results.Stream(File.OpenRead(path));
        }

        public async Task<IResult> Stream(string userName, string fileName, CancellationToken cancellationToken)
        {
            var cacheModel = await GetCache(userName, cancellationToken);
            var client = _httpClientFactory.CreateClient();
            var stream = await client.GetSeekableStreamAsync(cacheModel.Token, $"{cacheModel.DownloadUrl}/file/{_options.BUCKET_NAME}/{fileName}", cancellationToken);
            return Results.Stream(stream, contentType: stream.ContentType, enableRangeProcessing: true);
        }

        public async Task<Stream> Download(string userName, string fileName, CancellationToken cancellationToken)
        {
            var cacheModel = await GetCache(userName, cancellationToken);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", cacheModel.Token);
            return await client.GetStreamAsync($"{cacheModel.DownloadUrl}/file/{_options.BUCKET_NAME}/{fileName}", cancellationToken);
        }

        public async Task Upload(string fileName, string contentType, StreamContent content, CancellationToken cancellationToken)
        {
            var urlResponse = await GetUploadUri(cancellationToken);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", urlResponse.Token);
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-File-Name", HttpUtility.UrlEncode(fileName));
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-Content-Sha1", "do_not_verify");
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-Info-Author", "Arkaine");
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-Server-Side-Encryption", "AES256");
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            
            var response = await client.PostAsync(urlResponse.UploadUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Upload API call responded with: {response.StatusCode}");
                await _hubContext.Clients.All.SendAsync("update", $"Upload file {fileName} succeeded", cancellationToken);
            }
            else
            {
                await _hubContext.Clients.All.SendAsync("update", $"Upload file {fileName} succeeded", cancellationToken);
                _logger.LogInformation($"File upload compelted: {fileName}");
            }
        }

        public async Task UploadParts(string fileName, string contentType, Stream content, CancellationToken cancellationToken)
        {
            // Check if this file is already partially uploaded.
            var unfinishedFilesResponse = await CheckForUnfinishedFile(fileName, cancellationToken);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string fileId;

            if (unfinishedFilesResponse.Files.Count > 1)
            {
                throw new($"There are multiple files started named {fileName}");
            }
            else if (unfinishedFilesResponse.Files.Count == 1)
            {
                fileId = unfinishedFilesResponse.Files[0].Id;
            }
            else
            {
                // Start the upload
                var createFileResponse = await StartPartUpload(fileName, contentType, cancellationToken);
                fileId = createFileResponse.FileId;
            }


            // Get the upload URI
            var getUploadUriResponse = await GetPartUploadUri(fileId, cancellationToken);

            // Upload each chunk
            var partNumber = 1;
            var buffer = new Memory<byte>();
            
            while (true)
            {
                int read = await content.ReadAtLeastAsync(buffer, _options.UPLOAD_CHUNK_SIZE, false, cancellationToken);
                
                if (read < 1) break;
                await UploadPart(getUploadUriResponse.UploadUrl, getUploadUriResponse.AuthorizationToken, partNumber, buffer, read, cancellationToken);
                partNumber++;
            }

            // Finish the upload
            await FinishUploadFile(fileId, cancellationToken);
            await _hubContext.Clients.All.SendAsync("update", $"Upload multi part file {fileName} succeeded", cancellationToken);
        }

        private async Task UploadPart(string url, string token, int partNumber, Memory<byte> bytes, int count, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", token);
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-Part-Number", partNumber.ToString());
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-Content-Sha1", "do_not_verify");
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-Server-Side-Encryption", "AES256");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // This probably isn't needed
            //client.DefaultRequestHeaders.Add("Connection", "Keep-Alive");
            //client.DefaultRequestHeaders.Add("Keep-Alive", "3600");

            var stream = File.OpenWrite(@"C:\Temp\new_test.mp4");
            stream.Seek(0, SeekOrigin.End);
            stream.Write(bytes.ToArray(), 0, count);
            stream.Close();
            return;

            var content = new ByteArrayContent(bytes.ToArray(), 0, count);
            try
            {
                var response = await client.PostAsync(url, content, cancellationToken);
                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Upload part resposne call responded with: {response.StatusCode}");
                    await _hubContext.Clients.All.SendAsync("update", $"Upload part failed with {responseString}", cancellationToken);
                    throw new(responseString);
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }

            await _hubContext.Clients.All.SendAsync("update", $"Upload part {partNumber} succeeded", cancellationToken);
        }

        // TODO: Refactor to use make auth request
        private async Task FinishUploadFile(string fileId, CancellationToken cancellationToken)
        {
            // Auth
            var auth = await GetToken(_options.B2_KEY_WRITE, cancellationToken);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth.Token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Req
            var req = new { fileId };
            var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(req));
            var byteContent = new ByteArrayContent(buffer);

            // Resp
            var response = await client.PostAsync(auth.ApiBaseUrl + "/b2api/v2/b2_finish_large_file ", byteContent, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Finish part file call responded with: {response.StatusCode}");
                await _hubContext.Clients.All.SendAsync("update", $"Finish multi part file {fileId} failed with {responseString}", cancellationToken);
                throw new(responseString);
            }

            _logger.LogInformation($"Finished multi part file: {fileId}");
            await _hubContext.Clients.All.SendAsync("update", $"Finish multi part file {fileId} succeeded", cancellationToken);
        }

        private string GetPreviewUrl(string fileName, string type)
        {
            var thumb = Path.Combine(_options.THUMBNAIL_DIR, fileName);

            if (type == "folder")
            {
                thumb = Path.Combine(thumb, "thumb.jpg");
            }

            if (File.Exists(thumb))
            {
                return thumb;
            }

            return string.Empty;
        }

        private async Task<TResponse> MakeAuthenticatedRequest<TRequest, TResponse>(TRequest request, string userName, string url, CancellationToken cancellationToken)
        {
            var cacheModel = await GetCache(userName, cancellationToken);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", cacheModel.Token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var content = JsonSerializer.Serialize(request, options: new JsonSerializerOptions
            {
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var buffer = Encoding.UTF8.GetBytes(content);

            var byteContent = new ByteArrayContent(buffer);
            var response = await client.PostAsync(cacheModel.ApiUrl + url, byteContent, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"API call to {url} responded with: {response.StatusCode}");
                throw new(responseString);
            }

            var model = JsonSerializer.Deserialize<TResponse>(responseString) ?? throw new($"Response is not a valid format for {nameof(TResponse)}");
            _logger.LogInformation($"{url} call succeeded");

            return model;
        }

        // TODO: refactor to use make auth request
        private async Task<GetUploadPartsResponse> GetPartUploadUri(string fileId, CancellationToken cancellationToken)
        {
            // Auth
            var auth = await GetToken(_options.B2_KEY_WRITE, cancellationToken);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth.Token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Req
            var req = new { fileId };
            var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(req));
            var byteContent = new ByteArrayContent(buffer);

            // Resp
            var response = await client.PostAsync(auth.ApiBaseUrl + "/b2api/v2/b2_get_upload_part_url ", byteContent, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseModel = JsonSerializer.Deserialize<GetUploadPartsResponse>(responseString) ?? throw new("Start part file response is an invalid format");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Start part file call responded with: {response.StatusCode}");
                await _hubContext.Clients.All.SendAsync("update", $"Get upload url for file {fileId} failed with {responseString}", cancellationToken);
                throw new(responseString);
            }

            _logger.LogInformation("Get upload url succeeded");
            await _hubContext.Clients.All.SendAsync("update", $"Get upload url for file {fileId} succeeded", cancellationToken);
            return responseModel;
        }

        // TODO: Refactor to use make auth request
        private async Task<FilesResponse> CheckForUnfinishedFile(string fileName, CancellationToken cancellationToken)
        {
            // Auth
            var auth = await GetToken(_options.B2_KEY_READ, cancellationToken);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth.Token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Req
            var req = new
            {
                bucketId = _options.BUCKET_ID,
                namePrefix = fileName,
            };

            var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(req));
            var byteContent = new ByteArrayContent(buffer);

            // Resp
            var response = await client.PostAsync(auth.ApiBaseUrl + "/b2api/v2/b2_list_unfinished_large_files", byteContent, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseModel = JsonSerializer.Deserialize<FilesResponse>(responseString) ?? throw new("List unfinished files response is an invalid format");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Start part file call responded with: {response.StatusCode}");
                await _hubContext.Clients.All.SendAsync("update", $"check for unfinished files failed with {responseString}", cancellationToken);
                throw new(responseString);
            }

            _logger.LogInformation("Check for finished files succeeded");
            await _hubContext.Clients.All.SendAsync("update", $"Check for unfinished files finished and found {responseModel.Files.Count} files", cancellationToken);
            return responseModel;
        }

        // TODO: refactor to use make auth request
        private async Task<StartPartUploadResponse> StartPartUpload(string fileName, string contentType, CancellationToken cancellationToken)
        {
            // Auth
            var auth = await GetToken(_options.B2_KEY_WRITE, cancellationToken);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth.Token);
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Bz-Server-Side-Encryption", "AES256");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Req
            var req = new {
                bucketId = _options.BUCKET_ID,
                fileName,
                contentType
            };

            var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(req));
            var byteContent = new ByteArrayContent(buffer);

            // Resp
            var response = await client.PostAsync(auth.ApiBaseUrl + "/b2api/v2/b2_start_large_file", byteContent, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseModel = JsonSerializer.Deserialize<StartPartUploadResponse>(responseString) ?? throw new("Start part file response is an invalid format");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Start part file call responded with: {response.StatusCode}");
                await _hubContext.Clients.All.SendAsync("update", $"Start multi part file failed with {responseString}", cancellationToken);
                throw new(responseString);
            }

            _logger.LogInformation("Start multi part file succeeded");
            await _hubContext.Clients.All.SendAsync("update", $"Start multi part file for file {fileName} succeeded", cancellationToken);
            return responseModel;
        }

        // TODO: refactor to use make auth request
        private async Task<UploadUrlResponse> GetUploadUri(CancellationToken cancellationToken)
        {
            // Auth
            var auth = await GetToken(_options.B2_KEY_WRITE, cancellationToken);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth.Token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Req
            var req = new { bucketId = _options.BUCKET_ID };
            var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(req));
            var byteContent = new ByteArrayContent(buffer);
            
            // Resp
            var response = await client.PostAsync(auth.ApiBaseUrl + "/b2api/v2/b2_get_upload_url", byteContent, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseModel = JsonSerializer.Deserialize<UploadUrlResponse>(responseString) ?? throw new("Upload url response is an invalid format");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Get Upload URL API call responded with: {response.StatusCode}");
                await _hubContext.Clients.All.SendAsync("update", $"Get upload url failed with {responseString}", cancellationToken);
                throw new(responseString);
            }

            _logger.LogInformation("Get upload uri suceeded");
            await _hubContext.Clients.All.SendAsync("update", $"Get upload url succeeded", cancellationToken);
            return responseModel;
        }

        private async Task<CacheModel> GetCache(string key, CancellationToken cancellationToken)
        {
            var cacheModel = _cache.Get(key) as CacheModel;

            if (cacheModel == null)
            {
                _logger.LogWarning($"Cache model not found for {key}");
                var response = await GetToken(_options.B2_KEY_READ, cancellationToken);
                cacheModel = new CacheModel(response.Token, response.DownloadBaseUrl, response.ApiBaseUrl, response.AccountId);
                _cache.Set(key, cacheModel);
            }

            return cacheModel;
        }
    }
}
