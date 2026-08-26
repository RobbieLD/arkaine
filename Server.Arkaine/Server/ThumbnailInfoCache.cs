using SixLabors.ImageSharp;
using System.Collections.Concurrent;

namespace Server.Arkaine
{
    public record ThumbnailInfo(int Width, int Height, DateTimeOffset LastModified, long Length);

    /// <summary>
    /// A point in time snapshot of the thumbnail dimension cache.
    /// </summary>
    public record ThumbnailCacheStats(
        int Entries,
        int Capacity,
        long Hits,
        long Misses,
        long Stale,
        long Unreadable,
        long NotFound,
        long Resets,
        DateTimeOffset StartedUtc,
        DateTimeOffset? LastResetUtc);

    public interface IThumbnailInfoProvider
    {
        /// <summary>
        /// Returns metadata for a generated thumbnail, or null when it does not exist.
        /// Width and height are zero when the file could not be identified as an image.
        /// </summary>
        ThumbnailInfo? Get(string fullPath);

        /// <summary>
        /// Returns counters describing how effectively the cache is being used.
        /// </summary>
        ThumbnailCacheStats GetStats();

        /// <summary>
        /// Drops every cached entry. Counters are preserved so the hit rate stays comparable.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// Reads thumbnail dimensions straight from the image header and memoises them.
    /// Listing a folder needs the dimensions of every thumbnail on the page so the client
    /// can reserve space before the image loads; without this cache that would be one
    /// header read per file per request.
    /// </summary>
    public class ThumbnailInfoCache : IThumbnailInfoProvider
    {
        private const int MaxEntries = 20000;

        private readonly ConcurrentDictionary<string, ThumbnailInfo> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly DateTimeOffset _started = DateTimeOffset.UtcNow;

        private long _hits;
        private long _misses;
        private long _stale;
        private long _unreadable;
        private long _notFound;
        private long _resets;
        private long _lastResetTicks;

        public ThumbnailInfo? Get(string fullPath)
        {
            FileInfo info;

            try
            {
                info = new FileInfo(fullPath);

                if (!info.Exists)
                {
                    _cache.TryRemove(fullPath, out _);
                    Interlocked.Increment(ref _notFound);
                    return null;
                }
            }
            catch (IOException)
            {
                Interlocked.Increment(ref _notFound);
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                Interlocked.Increment(ref _notFound);
                return null;
            }

            // Truncated to seconds so the value round trips through Last-Modified unchanged.
            var lastModified = new DateTimeOffset(info.LastWriteTimeUtc).ToUniversalTime();
            lastModified = lastModified.AddTicks(-(lastModified.Ticks % TimeSpan.TicksPerSecond));

            if (_cache.TryGetValue(fullPath, out var cached))
            {
                if (cached.LastModified == lastModified && cached.Length == info.Length)
                {
                    Interlocked.Increment(ref _hits);
                    return cached;
                }

                Interlocked.Increment(ref _stale);
            }
            else
            {
                Interlocked.Increment(ref _misses);
            }

            var identified = Identify(fullPath);

            if (identified.Width == 0 || identified.Height == 0)
            {
                Interlocked.Increment(ref _unreadable);
            }

            var result = new ThumbnailInfo(identified.Width, identified.Height, lastModified, info.Length);

            if (_cache.Count >= MaxEntries)
            {
                _cache.Clear();
                Interlocked.Increment(ref _resets);
                Interlocked.Exchange(ref _lastResetTicks, DateTimeOffset.UtcNow.UtcTicks);
            }

            _cache[fullPath] = result;
            return result;
        }

        public ThumbnailCacheStats GetStats()
        {
            var lastResetTicks = Interlocked.Read(ref _lastResetTicks);

            return new ThumbnailCacheStats(
                _cache.Count,
                MaxEntries,
                Interlocked.Read(ref _hits),
                Interlocked.Read(ref _misses),
                Interlocked.Read(ref _stale),
                Interlocked.Read(ref _unreadable),
                Interlocked.Read(ref _notFound),
                Interlocked.Read(ref _resets),
                _started,
                lastResetTicks == 0 ? null : new DateTimeOffset(lastResetTicks, TimeSpan.Zero));
        }

        public void Clear()
        {
            _cache.Clear();
            Interlocked.Increment(ref _resets);
            Interlocked.Exchange(ref _lastResetTicks, DateTimeOffset.UtcNow.UtcTicks);
        }

        private static (int Width, int Height) Identify(string fullPath)
        {
            try
            {
                var imageInfo = Image.Identify(fullPath);
                return (imageInfo.Width, imageInfo.Height);
            }
            catch (Exception)
            {
                // Cached as 0x0 so a corrupt thumbnail is not re-read on every listing.
                return (0, 0);
            }
        }
    }
}
