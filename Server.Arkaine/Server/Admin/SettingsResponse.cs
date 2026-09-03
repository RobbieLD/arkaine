namespace Server.Arkaine.Admin
{
    public class SettingsResponse
    {
        public SettingsResponse()
        {
        }

        public SettingsResponse(long totalThumbnails, int thumbnailPageSize, int thumbnailWidth, string thumbnailDir, string thumbnailExt, bool isRunning)
        {
            TotalThumbnails = totalThumbnails;
            ThumbnailPageSize = thumbnailPageSize;
            ThumbnailWidth = thumbnailWidth;
            ThumbnailDir = thumbnailDir;
            ThumbnailExtensions = thumbnailExt;
            IsRunning = isRunning;
        }

        public long TotalThumbnails { get; set; }
        public string ThumbnailDir { get; set; } = string.Empty;
        public string ThumbnailExtensions { get; set; } = string.Empty;
        public int ThumbnailPageSize { get; set; }
        public int ThumbnailWidth { get; set; }
        public bool IsRunning { get;  set; }
    }
}
