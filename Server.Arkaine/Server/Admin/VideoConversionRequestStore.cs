using Microsoft.EntityFrameworkCore;

namespace Server.Arkaine.Admin
{
    public sealed class VideoConversionRequestStore : IVideoConversionRequestStore
    {
        private readonly ArkaineDbContext _context;

        public VideoConversionRequestStore(ArkaineDbContext context)
        {
            _context = context;
        }

        public async Task<VideoConversionRequest> EnqueueAsync(
            string fileName,
            string fileId,
            string requestedBy,
            string reason,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("A file name must be supplied.", nameof(fileName));
            }

            var now = DateTimeOffset.UtcNow;
            var request = await _context.VideoConversionRequests
                .SingleOrDefaultAsync(
                    item => item.FileName == fileName && item.FileId == fileId,
                    cancellationToken);

            if (request is null)
            {
                request = new VideoConversionRequest
                {
                    FileName = fileName,
                    FileId = fileId,
                    RequestedBy = requestedBy,
                    Reason = reason,
                    Status = VideoConversionRequestStatus.Queued,
                    RequestedUtc = now,
                    UpdatedUtc = now
                };
                _context.VideoConversionRequests.Add(request);
            }
            else
            {
                request.RequestedBy = requestedBy;
                request.Reason = reason;
                request.Status = VideoConversionRequestStatus.Queued;
                request.Error = string.Empty;
                request.UpdatedUtc = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return request;
        }

        public async Task<IReadOnlyList<VideoConversionRequest>> GetPendingAsync(
            string? prefix,
            CancellationToken cancellationToken)
        {
            var query = _context.VideoConversionRequests
                .Where(request =>
                    request.Status == VideoConversionRequestStatus.Queued ||
                    request.Status == VideoConversionRequestStatus.Failed ||
                    request.Status == VideoConversionRequestStatus.Running);

            if (!string.IsNullOrEmpty(prefix))
            {
                query = query.Where(request => request.FileName.StartsWith(prefix));
            }

            return await query
                .OrderBy(request => request.Id)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<VideoConversionRequestResponse>> ListAsync(
            string? status,
            CancellationToken cancellationToken)
        {
            var query = _context.VideoConversionRequests.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(request => request.Status == status.Trim().ToLowerInvariant());
            }

            return await query
                .OrderByDescending(request => request.UpdatedUtc)
                .Select(request => new VideoConversionRequestResponse(
                    request.Id,
                    request.FileName,
                    request.FileId,
                    request.RequestedBy,
                    request.Status,
                    request.Reason,
                    request.Error,
                    request.RequestedUtc,
                    request.UpdatedUtc))
                .ToListAsync(cancellationToken);
        }

        public Task MarkRunningAsync(int id, CancellationToken cancellationToken)
        {
            return UpdateStatusAsync(id, VideoConversionRequestStatus.Running, string.Empty, cancellationToken);
        }

        public Task MarkCompletedAsync(int id, CancellationToken cancellationToken)
        {
            return UpdateStatusAsync(id, VideoConversionRequestStatus.Completed, string.Empty, cancellationToken);
        }

        public Task MarkFailedAsync(int id, string error, CancellationToken cancellationToken)
        {
            return UpdateStatusAsync(id, VideoConversionRequestStatus.Failed, error, cancellationToken);
        }

        public Task MarkQueuedAsync(int id, CancellationToken cancellationToken)
        {
            return UpdateStatusAsync(id, VideoConversionRequestStatus.Queued, string.Empty, cancellationToken);
        }

        public async Task<bool> CancelAsync(int id, CancellationToken cancellationToken)
        {
            var request = await _context.VideoConversionRequests
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (request is null)
            {
                return false;
            }

            if (request.Status is not VideoConversionRequestStatus.Queued and not VideoConversionRequestStatus.Failed)
            {
                return false;
            }

            request.Status = VideoConversionRequestStatus.Cancelled;
            request.Error = string.Empty;
            request.UpdatedUtc = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task UpdateStatusAsync(
            int id,
            string status,
            string error,
            CancellationToken cancellationToken)
        {
            var request = await _context.VideoConversionRequests
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (request is null)
            {
                return;
            }

            request.Status = status;
            request.Error = error;
            request.UpdatedUtc = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
