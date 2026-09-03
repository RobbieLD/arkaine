# Media conversion

The Admin page can convert configured image and video sources within the
selected root or top-level folder into files that browsers can display or play.
Conversion creates the browser-friendly target beside the source and leaves the
source unchanged. If the target already exists, the source is skipped.

The Admin page loads the root and available top-level folders from
`GET /admin/conversion/paths`. Starting a conversion requires a request body
containing the selected path:

```json
{
  "path": "/"
}
```

The root is represented by `/` in the request and is sent to B2 with an empty
listing `prefix`. A folder path is sent as its listing prefix. Nested paths,
other absolute paths, and an empty path are rejected. Image targets use the
`.jpg` extension and video targets use `.mp4`.

## Configuration

Settings are read from the normal ASP.NET Core configuration sources, including
environment variables:

| Setting | Default | Description |
| --- | --- | --- |
| `CONVERT_IMAGE_EXTENSIONS` | `.avif,.bmp,.heic,.heif,.tif,.tiff,.webp` | Image source extensions |
| `CONVERT_VIDEO_EXTENSIONS` | `.avi,.flv,.m4v,.mkv,.mov,.mpeg,.mpg,.wmv` | Video source extensions |
| `CONVERT_PAGE_SIZE` | `THUMBNAIL_PAGE_SIZE` | Number of B2 files scanned per page |
| `CONVERT_IMAGE_QUALITY` | `2` | ffmpeg JPEG quality value (`0`-`31`) |
| `CONVERT_VIDEO_CRF` | `23` | libx264 constant rate factor (`0`-`51`) |
| `CONVERT_VIDEO_PRESET` | `medium` | libx264 encoding preset |
| `CONVERT_VIDEO_AUDIO_BITRATE` | `128k` | AAC audio bitrate |
| `CONVERT_IMAGE_TIMEOUT_SECONDS` | `300` | Maximum time for one image conversion |
| `CONVERT_VIDEO_TIMEOUT_SECONDS` | `3600` | Maximum time for one video conversion |
| `FFMPEG_PATH` | `ffmpeg` | ffmpeg executable path |

Extensions are case-insensitive and may be separated by commas, semicolons, spaces,
or newlines. Image sources produce a JPEG beside the source. Video sources produce
an MP4 with H.264 video, `yuv420p` pixels, AAC audio when present, and fast-start
metadata.

The server probes ffmpeg and requires the `libx264` and `aac` encoders before a
conversion job can start. The deployed runtime and local development machines must
install ffmpeg separately or set `FFMPEG_PATH`.

`UPLOAD_CHUNK_SIZE` must be at least 5 MB (`5000000`) for B2 multipart uploads.
Multipart uploads must contain at least two parts and cannot exceed B2's 10,000-part
or 10 TB limits.

The conversion job does not persist progress or recovery markers. A target is
considered complete solely when the destination file exists in B2. Failed
conversions can therefore be retried on a later run, and cleanup of source files
or other duplicates is handled outside this application.

Each completed thumbnail or conversion run is saved as an HTML report in the
database. Reports can be listed, downloaded, or cleared from the Admin page.
The report contains a run summary; conversion reports also contain one row for
each processed file, while thumbnail reports contain rows for failures.

The canonical Admin endpoints are:

- `GET /admin`
- `GET /admin/reports`
- `GET /admin/reports/{id}`
- `POST /admin/reports/clear`
- `GET /admin/conversion/paths`
- `GET /admin/thumbnail-cache`
- `POST /admin/thumbnail-cache/clear`
- `POST /admin/thumbnails/start|stop`
- `POST /admin/convert/start|stop` (`start` requires a `path`)

All Admin endpoints require an authenticated user with the `Admin` role.
