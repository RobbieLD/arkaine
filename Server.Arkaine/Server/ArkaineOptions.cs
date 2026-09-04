using System.Collections.ObjectModel;
using Server.Arkaine.B2;

namespace Server.Arkaine
{
    public class ArkaineOptions
    {
        private static readonly char[] ExtensionSeparators = [',', ';', '|', ' ', '\r', '\n', '\t'];
        private static readonly string[] DefaultThumbnailExtensions = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"];
        private static readonly string[] DefaultImageExtensions = [".avif", ".bmp", ".heic", ".heif", ".tif", ".tiff", ".webp"];
        private static readonly string[] DefaultVideoExtensions = [".avi", ".flv", ".m4v", ".mkv", ".mov", ".mpeg", ".mpg", ".wmv"];

        public string DB_CONNECTION_STRING { get; set; } = string.Empty;
        public string B2_KEY_READ { get; set; } = string.Empty;
        public string B2_KEY_WRITE { get; set; } = string.Empty;
        public string B2AuthUrl { get; set; } = string.Empty;
        public string CORS_ORIGIN { get; set; } = string.Empty;
        public string ACCEPT_IP_RANGE { get; set; } = string.Empty;
        public string TRUSTED_PROXY { get; set; } = string.Empty;
        public string MAX_COOKIE_LIFETIME { get; set; } = string.Empty;
        public string BUCKET_ID { get; set; } = string.Empty;
        public string BUCKET_NAME { get; set; } = string.Empty;
        public string API_KEY { get; set; } = string.Empty;
        public string PAGE_SIZE { get; set; } = string.Empty;
        public string SITE_KEYS { get; set; } = string.Empty;
        public string THUMBNAIL_DIR { get; set; } = string.Empty;
        public string THUMBNAIL_EXTENSIONS { get; set; } = string.Empty;
        public string CONVERT_IMAGE_EXTENSIONS { get; set; } = string.Empty;
        public string CONVERT_VIDEO_EXTENSIONS { get; set; } = string.Empty;
        public string IMAGE_EXTENSIONS { get; set; } = string.Empty;
        public string VIDEO_EXTENSIONS { get; set; } = string.Empty;
        public string FFMPEG_PATH { get; set; } = "ffmpeg";
        public string FFPROBE_PATH { get; set; } = string.Empty;
        public string CONVERSION_TEMP_DIR { get; set; } = string.Empty;
        public int THUMBNAIL_PAGE_SIZE { get; set; }
        public int THUMBNAIL_WIDTH { get; set; }
        public int UPLOAD_CHUNK_SIZE { get; set; }
        public int CONVERT_PAGE_SIZE { get; set; }
        public int CONVERSION_PAGE_SIZE { get; set; }
        public int CONVERT_IMAGE_QUALITY { get; set; } = 2;
        public int CONVERT_VIDEO_CRF { get; set; } = 23;
        public string CONVERT_VIDEO_PRESET { get; set; } = "medium";
        public string CONVERT_VIDEO_AUDIO_BITRATE { get; set; } = "128k";
        public long CONVERT_VIDEO_MAX_BITRATE { get; set; } = 8_000_000;
        public int FFMPEG_PROBE_TIMEOUT_SECONDS { get; set; } = 15;
        public int FFMPEG_CONVERSION_TIMEOUT_SECONDS { get; set; }
        public int CONVERT_IMAGE_TIMEOUT_SECONDS { get; set; }
        public int CONVERT_VIDEO_TIMEOUT_SECONDS { get; set; }

        public void Normalize()
        {
            THUMBNAIL_EXTENSIONS = NormalizeExtensionList(THUMBNAIL_EXTENSIONS, DefaultThumbnailExtensions);
            CONVERT_IMAGE_EXTENSIONS = NormalizeExtensionList(
                string.IsNullOrWhiteSpace(CONVERT_IMAGE_EXTENSIONS) ? IMAGE_EXTENSIONS : CONVERT_IMAGE_EXTENSIONS,
                DefaultImageExtensions);
            CONVERT_VIDEO_EXTENSIONS = NormalizeExtensionList(
                string.IsNullOrWhiteSpace(CONVERT_VIDEO_EXTENSIONS) ? VIDEO_EXTENSIONS : CONVERT_VIDEO_EXTENSIONS,
                DefaultVideoExtensions);
            IMAGE_EXTENSIONS = CONVERT_IMAGE_EXTENSIONS;
            VIDEO_EXTENSIONS = CONVERT_VIDEO_EXTENSIONS;
            FFMPEG_PATH = string.IsNullOrWhiteSpace(FFMPEG_PATH) ? "ffmpeg" : FFMPEG_PATH.Trim();
            FFPROBE_PATH = string.IsNullOrWhiteSpace(FFPROBE_PATH)
                ? GetDefaultFfprobePath(FFMPEG_PATH)
                : FFPROBE_PATH.Trim();
            CONVERSION_TEMP_DIR = string.IsNullOrWhiteSpace(CONVERSION_TEMP_DIR) ? string.Empty : CONVERSION_TEMP_DIR.Trim();

            if (CONVERT_PAGE_SIZE <= 0)
            {
                CONVERT_PAGE_SIZE = CONVERSION_PAGE_SIZE > 0
                    ? CONVERSION_PAGE_SIZE
                    : THUMBNAIL_PAGE_SIZE;
            }

            CONVERSION_PAGE_SIZE = CONVERT_PAGE_SIZE;

            if (CONVERT_IMAGE_TIMEOUT_SECONDS <= 0)
            {
                CONVERT_IMAGE_TIMEOUT_SECONDS = FFMPEG_CONVERSION_TIMEOUT_SECONDS > 0
                    ? FFMPEG_CONVERSION_TIMEOUT_SECONDS
                    : 300;
            }

            if (CONVERT_VIDEO_TIMEOUT_SECONDS <= 0)
            {
                CONVERT_VIDEO_TIMEOUT_SECONDS = FFMPEG_CONVERSION_TIMEOUT_SECONDS > 0
                    ? FFMPEG_CONVERSION_TIMEOUT_SECONDS
                    : 3600;
            }

            CONVERT_VIDEO_PRESET = string.IsNullOrWhiteSpace(CONVERT_VIDEO_PRESET)
                ? "medium"
                : CONVERT_VIDEO_PRESET.Trim();
            CONVERT_VIDEO_AUDIO_BITRATE = string.IsNullOrWhiteSpace(CONVERT_VIDEO_AUDIO_BITRATE)
                ? "128k"
                : CONVERT_VIDEO_AUDIO_BITRATE.Trim();
        }

        public void Validate()
        {
            if (CONVERT_PAGE_SIZE < 0)
            {
                throw new InvalidOperationException($"{nameof(CONVERT_PAGE_SIZE)} cannot be negative.");
            }

            if (CONVERSION_PAGE_SIZE < 0)
            {
                throw new InvalidOperationException($"{nameof(CONVERSION_PAGE_SIZE)} cannot be negative.");
            }

            if (FFMPEG_CONVERSION_TIMEOUT_SECONDS < 0)
            {
                throw new InvalidOperationException($"{nameof(FFMPEG_CONVERSION_TIMEOUT_SECONDS)} cannot be negative.");
            }

            if (CONVERT_IMAGE_TIMEOUT_SECONDS < 0)
            {
                throw new InvalidOperationException($"{nameof(CONVERT_IMAGE_TIMEOUT_SECONDS)} cannot be negative.");
            }

            if (CONVERT_VIDEO_TIMEOUT_SECONDS < 0)
            {
                throw new InvalidOperationException($"{nameof(CONVERT_VIDEO_TIMEOUT_SECONDS)} cannot be negative.");
            }

            Normalize();

            if (THUMBNAIL_PAGE_SIZE <= 0)
            {
                throw new InvalidOperationException($"{nameof(THUMBNAIL_PAGE_SIZE)} must be greater than zero.");
            }

            if (THUMBNAIL_WIDTH <= 0)
            {
                throw new InvalidOperationException($"{nameof(THUMBNAIL_WIDTH)} must be greater than zero.");
            }

            if (UPLOAD_CHUNK_SIZE < B2MultipartLimits.MinimumPartSizeBytes)
            {
                throw new InvalidOperationException(
                    $"{nameof(UPLOAD_CHUNK_SIZE)} must be at least {B2MultipartLimits.MinimumPartSizeBytes} bytes.");
            }

            if (CONVERT_PAGE_SIZE <= 0)
            {
                throw new InvalidOperationException($"{nameof(CONVERT_PAGE_SIZE)} must be greater than zero or fall back to {nameof(THUMBNAIL_PAGE_SIZE)}.");
            }

            if (CONVERT_IMAGE_QUALITY is < 0 or > 31)
            {
                throw new InvalidOperationException($"{nameof(CONVERT_IMAGE_QUALITY)} must be between 0 and 31.");
            }

            if (CONVERT_VIDEO_CRF is < 0 or > 51)
            {
                throw new InvalidOperationException($"{nameof(CONVERT_VIDEO_CRF)} must be between 0 and 51.");
            }

            if (CONVERT_VIDEO_MAX_BITRATE <= 0)
            {
                throw new InvalidOperationException($"{nameof(CONVERT_VIDEO_MAX_BITRATE)} must be greater than zero.");
            }

            if (FFMPEG_PROBE_TIMEOUT_SECONDS <= 0)
            {
                throw new InvalidOperationException($"{nameof(FFMPEG_PROBE_TIMEOUT_SECONDS)} must be greater than zero.");
            }

            if (FFMPEG_CONVERSION_TIMEOUT_SECONDS <= 0)
            {
                FFMPEG_CONVERSION_TIMEOUT_SECONDS = Math.Max(CONVERT_IMAGE_TIMEOUT_SECONDS, CONVERT_VIDEO_TIMEOUT_SECONDS);
            }

            if (CONVERT_IMAGE_TIMEOUT_SECONDS <= 0)
            {
                throw new InvalidOperationException($"{nameof(CONVERT_IMAGE_TIMEOUT_SECONDS)} must be greater than zero.");
            }

            if (CONVERT_VIDEO_TIMEOUT_SECONDS <= 0)
            {
                throw new InvalidOperationException($"{nameof(CONVERT_VIDEO_TIMEOUT_SECONDS)} must be greater than zero.");
            }

            _ = GetPageSize();
        }

        public IReadOnlySet<string> GetThumbnailExtensions()
        {
            Normalize();
            return new ReadOnlySet(ParseExtensions(THUMBNAIL_EXTENSIONS, DefaultThumbnailExtensions));
        }

        public IReadOnlySet<string> GetImageExtensions()
        {
            Normalize();
            return new ReadOnlySet(ParseExtensions(IMAGE_EXTENSIONS, DefaultImageExtensions));
        }

        public IReadOnlySet<string> GetVideoExtensions()
        {
            Normalize();
            return new ReadOnlySet(ParseExtensions(VIDEO_EXTENSIONS, DefaultVideoExtensions));
        }

        public int GetPageSize()
        {
            if (!int.TryParse(PAGE_SIZE, out var pageSize) || pageSize <= 0)
            {
                throw new InvalidOperationException($"{nameof(PAGE_SIZE)} must be a positive integer.");
            }

            return pageSize;
        }

        public bool IsThumbnailExtension(string fileName)
        {
            return HasExtension(fileName, GetThumbnailExtensions());
        }

        public bool IsConvertibleImage(string fileName)
        {
            return HasExtension(fileName, GetImageExtensions());
        }

        public bool IsConvertibleVideo(string fileName)
        {
            return HasExtension(fileName, GetVideoExtensions());
        }

        public bool IsVideoFile(string fileName, string? contentType = null)
        {
            if (contentType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            return HasExtension(
                fileName,
                new ReadOnlySet(ParseExtensions(
                    ".avi,.flv,.m4v,.mkv,.mov,.mp4,.mpeg,.mpg,.webm,.wmv",
                    [])));
        }

        public bool IsCompressedVideo(string fileName)
        {
            return IsVideoFile(fileName) &&
                   Path.GetFileNameWithoutExtension(fileName)
                       .EndsWith("_compressed", StringComparison.OrdinalIgnoreCase);
        }

        public string GetConversionTargetFileName(string fileName)
        {
            if (IsConvertibleImage(fileName))
            {
                return Path.ChangeExtension(fileName, ".jpg") ?? fileName;
            }

            if (IsConvertibleVideo(fileName))
            {
                return GetCompressedVideoTargetFileName(fileName);
            }

            return fileName;
        }

        public string GetCompressedVideoTargetFileName(string fileName)
        {
            var directory = Path.GetDirectoryName(fileName);
            var name = Path.GetFileNameWithoutExtension(fileName);
            var targetName = $"{name}_compressed.mp4";
            return string.IsNullOrEmpty(directory)
                ? targetName
                : Path.Combine(directory, targetName).Replace('\\', '/');
        }

        public string GetConversionTempDirectory()
        {
            Normalize();

            if (!string.IsNullOrWhiteSpace(CONVERSION_TEMP_DIR))
            {
                return CONVERSION_TEMP_DIR;
            }

            if (!string.IsNullOrWhiteSpace(THUMBNAIL_DIR))
            {
                return Path.Combine(THUMBNAIL_DIR, ".conversion-temp");
            }

            return Path.Combine(AppContext.BaseDirectory, ".conversion-temp");
        }

        private static bool HasExtension(string fileName, IReadOnlySet<string> extensions)
        {
            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return false;
            }

            return extensions.Contains(extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}");
        }

        private static string NormalizeExtensionList(string? value, IEnumerable<string> fallback)
        {
            return string.Join(',', ParseExtensions(value, fallback).OrderBy(extension => extension, StringComparer.Ordinal));
        }

        private static HashSet<string> ParseExtensions(string? value, IEnumerable<string> fallback)
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var source = string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Split(ExtensionSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var item in source)
            {
                var normalized = NormalizeExtension(item);
                if (!string.IsNullOrEmpty(normalized))
                {
                    extensions.Add(normalized);
                }
            }

            return extensions;
        }

        private static string NormalizeExtension(string value)
        {
            var normalized = value.Trim().Trim('"', '\'').ToLowerInvariant();
            if (normalized.Length == 0)
            {
                return string.Empty;
            }

            if (!normalized.StartsWith('.'))
            {
                normalized = $".{normalized}";
            }

            return normalized;
        }

        private static string GetDefaultFfprobePath(string ffmpegPath)
        {
            var directory = Path.GetDirectoryName(ffmpegPath);
            var extension = Path.GetExtension(ffmpegPath);
            var fileName = $"ffprobe{extension}";
            return string.IsNullOrEmpty(directory)
                ? fileName
                : Path.Combine(directory, fileName);
        }

        private sealed class ReadOnlySet(HashSet<string> values) : ReadOnlyCollection<string>(values.OrderBy(value => value, StringComparer.Ordinal).ToList()), IReadOnlySet<string>
        {
            private readonly HashSet<string> _values = values;

            public new bool Contains(string item) => _values.Contains(item);
            public bool IsProperSubsetOf(IEnumerable<string> other) => _values.IsProperSubsetOf(other);
            public bool IsProperSupersetOf(IEnumerable<string> other) => _values.IsProperSupersetOf(other);
            public bool IsSubsetOf(IEnumerable<string> other) => _values.IsSubsetOf(other);
            public bool IsSupersetOf(IEnumerable<string> other) => _values.IsSupersetOf(other);
            public bool Overlaps(IEnumerable<string> other) => _values.Overlaps(other);
            public bool SetEquals(IEnumerable<string> other) => _values.SetEquals(other);
        }
    }
}
