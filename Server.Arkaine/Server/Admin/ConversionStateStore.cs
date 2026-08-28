using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Server.Arkaine.Admin
{
    public interface IConversionStateStore
    {
        IReadOnlyList<ConversionStateMarker> LoadAll();
        void Save(ConversionStateMarker marker);
        void Delete(ConversionStateMarker marker);
        string MarkerDirectory { get; }
    }

    public sealed class ConversionStateMarker
    {
        public string MarkerId { get; set; } = string.Empty;
        public string SourceFile { get; set; } = string.Empty;
        public string TargetFile { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public bool TargetUploaded { get; set; }
        public bool ReferencesRenamed { get; set; }
        public bool ThumbnailMoved { get; set; }
        public bool SourceDeleted { get; set; }
        public DateTimeOffset CreatedUtc { get; set; }
        public DateTimeOffset UpdatedUtc { get; set; }

        public static string CreateMarkerId(string sourceFile, string targetFile)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{sourceFile}|{targetFile}"));
            return Convert.ToHexStringLower(bytes);
        }
    }

    public class FileSystemConversionStateStore : IConversionStateStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
        private readonly object _syncRoot = new();

        public FileSystemConversionStateStore(IOptions<ArkaineOptions> options)
        {
            var value = options.Value;
            value.Normalize();
            MarkerDirectory = Path.Combine(
                string.IsNullOrWhiteSpace(value.THUMBNAIL_DIR) ? AppContext.BaseDirectory : value.THUMBNAIL_DIR,
                ".conversion-state");
        }

        public string MarkerDirectory { get; }

        public IReadOnlyList<ConversionStateMarker> LoadAll()
        {
            lock (_syncRoot)
            {
                Directory.CreateDirectory(MarkerDirectory);

                var markers = new List<ConversionStateMarker>();
                foreach (var file in Directory.EnumerateFiles(MarkerDirectory, "*.json", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var marker = JsonSerializer.Deserialize<ConversionStateMarker>(File.ReadAllText(file), SerializerOptions);
                        if (marker is not null)
                        {
                            markers.Add(marker);
                        }
                    }
                    catch (JsonException)
                    {
                    }
                    catch (IOException)
                    {
                    }
                }

                return markers;
            }
        }

        public void Save(ConversionStateMarker marker)
        {
            lock (_syncRoot)
            {
                Directory.CreateDirectory(MarkerDirectory);
                marker.MarkerId = string.IsNullOrWhiteSpace(marker.MarkerId)
                    ? ConversionStateMarker.CreateMarkerId(marker.SourceFile, marker.TargetFile)
                    : marker.MarkerId;
                marker.UpdatedUtc = DateTimeOffset.UtcNow;
                if (marker.CreatedUtc == default)
                {
                    marker.CreatedUtc = marker.UpdatedUtc;
                }

                var markerPath = GetMarkerPath(marker);
                var temporaryPath = $"{markerPath}.{Guid.NewGuid():N}.tmp";

                try
                {
                    File.WriteAllText(temporaryPath, JsonSerializer.Serialize(marker, SerializerOptions));
                    File.Move(temporaryPath, markerPath, overwrite: true);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
            }
        }

        public void Delete(ConversionStateMarker marker)
        {
            lock (_syncRoot)
            {
                var path = GetMarkerPath(marker);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private string GetMarkerPath(ConversionStateMarker marker)
        {
            var markerId = string.IsNullOrWhiteSpace(marker.MarkerId)
                ? ConversionStateMarker.CreateMarkerId(marker.SourceFile, marker.TargetFile)
                : marker.MarkerId;

            if (markerId.Length != 64 || markerId.Any(character => !Uri.IsHexDigit(character)))
            {
                markerId = ConversionStateMarker.CreateMarkerId(marker.SourceFile, marker.TargetFile);
            }

            return Path.Combine(MarkerDirectory, $"{markerId}.json");
        }
    }
}
