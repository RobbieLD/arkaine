using Server.Arkaine.B2;

namespace Server.Arkaine
{
    /// <summary>
    /// Resolves a file's thumbnail URL plus its intrinsic dimensions so the client can
    /// reserve exact space in the gallery before the image has loaded.
    /// Shared by the real and mock B2 services so local development exercises the same path.
    /// </summary>
    public static class ThumbnailPreview
    {
        public static void Populate(B2File file, string? thumbnailDir, IThumbnailInfoProvider thumbnails)
        {
            var relativeThumbnailPath = file.FileName;

            if (file.Type == "folder")
            {
                relativeThumbnailPath = $"{file.FileName.TrimEnd('/', '\\')}/thumb.jpg";
            }

            file.Thumbnail = string.Empty;
            file.PreviewWidth = null;
            file.PreviewHeight = null;

            if (!ThumbnailPathResolver.TryResolve(thumbnailDir, relativeThumbnailPath, out var thumbnailPath))
            {
                return;
            }

            var info = thumbnails.Get(thumbnailPath);

            if (info is null)
            {
                return;
            }

            file.Thumbnail = ThumbnailPathResolver.ToUrlPath(relativeThumbnailPath);

            if (info.Width > 0 && info.Height > 0)
            {
                file.PreviewWidth = info.Width;
                file.PreviewHeight = info.Height;
            }
        }
    }
}
