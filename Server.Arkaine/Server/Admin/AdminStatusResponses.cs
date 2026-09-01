using Server.Arkaine.Media;

namespace Server.Arkaine.Admin
{
    public sealed record ThumbnailJobStatusResponse(
        long TotalThumbnails,
        long BadThumbnails,
        int ThumbnailPageSize,
        int ThumbnailWidth,
        string ThumbnailDir,
        string ThumbnailExtensions,
        bool IsRunning,
        GenerationReport Report);

    public sealed record ConversionJobStatusResponse(
        int ConversionPageSize,
        string ImageExtensions,
        string VideoExtensions,
        string TempDirectory,
        bool IsRunning,
        ConversionReport Report);

    public sealed record AdminStatusResponse(
        ThumbnailJobStatusResponse Thumbnail,
        ConversionJobStatusResponse Conversion,
        ThumbnailCacheStats Cache,
        MediaConverterAvailability Ffmpeg);

    public sealed class AdminJobRequest
    {
        public string Job { get; set; } = "thumbnail";
        public string Path { get; set; } = string.Empty;
    }
}
