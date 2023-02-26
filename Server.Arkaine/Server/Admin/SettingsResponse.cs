namespace Server.Arkaine.Admin
{
    public class SettingsResponse
    {
        public SettingsResponse(long totalThumbnails, long badThumbnails, int thumbnailPageSize, int thumbnailWidth, string thumbnailDir, string thumbnailExt, bool isRunning)
        {
            BadThumbnails = badThumbnails;
            TotalThumbnails = totalThumbnails;
            ThumbnailPageSize = thumbnailPageSize;
            ThumbnailWidth = thumbnailWidth;
            ThumbnailDir = thumbnailDir;
            ThumbnailExtensions = thumbnailExt;
            IsRunning = isRunning;
        }

        public long BadThumbnails { get; }
        public long TotalThumbnails { get; }
        public string ThumbnailDir { get; }
        public string ThumbnailExtensions { get; }
        public int ThumbnailPageSize { get; }
        public int ThumbnailWidth { get; }
        public bool IsRunning { get;  }
    }
}
