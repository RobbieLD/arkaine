﻿using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Server.Arkaine.B2;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Server.Arkaine.Admin
{
    public class ThumbnailManager
    {
        private readonly ArkaineOptions _options;
        private readonly ILogger _logger;
        private CancellationTokenSource _stoppingToken;
        private readonly IHubContext<AdminHub> _hubContext;
        private readonly IServiceProvider _serviceProvider;
        private GenerationReport _report;
        private bool _running = false;
        

        public ThumbnailManager(IServiceProvider serviceProvider, IOptions<ArkaineOptions> config, IHubContext<AdminHub> hubContext, ILogger<ThumbnailManager> logger)
        {
            _options = config.Value;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
            _stoppingToken = new CancellationTokenSource();
            _report = new GenerationReport();
        }

        private long CountThumbnails()
        {
            return Directory.EnumerateFiles(_options.THUMBNAIL_DIR, "*.*", SearchOption.AllDirectories).Count();
        }

        private long CountBadFiles()
        {
            return Directory.EnumerateFiles(_options.THUMBNAIL_DIR, "*.bad", SearchOption.AllDirectories).Count();
        }

        public void CancelGeneration()
        {
            _running = false;
            _stoppingToken.Cancel();
        }

        public SettingsResponse GetSettings()
        {
            return new SettingsResponse(
                    CountThumbnails(),
                    CountBadFiles(),
                    _options.THUMBNAIL_PAGE_SIZE,
                    _options.THUMBNAIL_WIDTH,
                    _options.THUMBNAIL_DIR,
                    _options.THUMBNAIL_EXTENSIONS,
                    _running);
        }

        public async Task GenerateThumbnails(string userName)
        {
            _stoppingToken = new CancellationTokenSource();
            _running = true;
            _report = new GenerationReport();

            var request = new FilesRequest
            {
                PageSize = _options.THUMBNAIL_PAGE_SIZE,
                BucketId = _options.BUCKET_ID,
            };

            while (!_stoppingToken.Token.IsCancellationRequested)
            {
                using var scope = _serviceProvider.CreateScope();
                var uploader = scope.ServiceProvider.GetRequiredService<IB2Service>();

                var page = await uploader.ListFiles(request, userName, null, _stoppingToken.Token);
                await ProcessPage(page, userName, uploader, _stoppingToken.Token);
                request.StartFile = page.NextFileName;               

                // if there's no more files to process finish
                if (string.IsNullOrEmpty(page.NextFileName))
                {
                    break;
                }
            }

            _running = false;
            _report.Finished = true;

            await _hubContext.Clients.All.SendAsync("update", _report);
        }

        private async Task ProcessPage(FilesResponse page, string userName, IB2Service updloader, CancellationToken cancellationToken)
        {
            foreach (var file in page.Files)
            {
                _report.Scanned++;

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (!_options.THUMBNAIL_EXTENSIONS.Contains(Path.GetExtension(file.FileName)))
                {
                    continue;
                }

                var fn = Path.Combine(_options.THUMBNAIL_DIR, file.FileName);

                Directory.CreateDirectory(Path.GetDirectoryName(fn) ?? throw new($"{file.FileName} not a valid filename"));

                if (string.IsNullOrEmpty(file.Thumbnail) && !File.Exists(fn + ".bad"))
                {
                    try
                    {
                        // Don't cancel midway through making a thumbnail
                        await GenerateThumbnail(userName, fn, file.FileName, updloader, CancellationToken.None);
                        _report.Generated++;
                    }
                    catch (Exception ex)
                    {
                        // Create a marker which indicates don't try to recreate this file in the future
                        File.Create(fn + ".bad").Close();
                        _logger.LogError(ex.Message);
                        _report.Failed++;
                    }
                }

                if (_report.Scanned > 0 && _report.Scanned % 100 == 0)
                {
                    await _hubContext.Clients.All.SendAsync("update", _report);
                }
            }
        }

        private async Task GenerateThumbnail(string userName, string thumbnailName, string fileName, IB2Service uploader, CancellationToken cancellationToken)
        {
            // generate thumbnail
            _logger.LogInformation($"Generating thumbnail {thumbnailName}");
            var stream = await uploader.Download(userName, fileName, cancellationToken);
            using var image = Image.Load(stream);
            image.Mutate(x => x.Resize(350, 0));
            await image.SaveAsync(thumbnailName, cancellationToken);
        }
    }
}
