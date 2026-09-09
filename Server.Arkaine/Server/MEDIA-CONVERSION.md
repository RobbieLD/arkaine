# Media conversion

The Admin page can convert configured image and video sources within the
selected root or top-level folder into files that browsers can display or play.
Conversion creates the browser-friendly target beside the source and leaves the
source unchanged. Image targets use `.jpg`; every video target uses a
`_compressed.mp4` suffix, for example `recording.mov` becomes
`recording_compressed.mp4`. If the target already exists, the source is skipped.

Configured image and video extensions are always converted. Other video files
are inspected through ffprobe over a short-lived authenticated B2 URL. If the
format or video-stream bitrate is above `CONVERT_VIDEO_MAX_BITRATE`, the file is
converted automatically. Missing bitrate metadata is not treated as a reason to
convert; those files can be added to the persistent manual queue from the
library's compression action.

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
other absolute paths, and an empty path are rejected.

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
| `CONVERT_VIDEO_MAX_BITRATE` | `8000000` | Maximum format or video-stream bitrate before automatic compression and maximum output video bitrate |
| `CONVERT_IMAGE_TIMEOUT_SECONDS` | `300` | Maximum time for one image conversion |
| `CONVERT_VIDEO_TIMEOUT_SECONDS` | `3600` | Maximum time for one video conversion |
| `FFMPEG_PATH` | `ffmpeg` | ffmpeg executable path |
| `FFPROBE_PATH` | Derived from `FFMPEG_PATH` | ffprobe executable path |

Extensions are case-insensitive and may be separated by commas, semicolons, spaces,
or newlines. Image sources produce a JPEG beside the source. Video sources produce
an MP4 with H.264 video, `yuv420p` pixels, AAC audio when present, and fast-start
metadata. The video encoder applies the configured maximum bitrate as a VBV cap.
When ffprobe reports the source size, a generated video is uploaded only when it
is smaller than the original; otherwise it is discarded and reported as skipped.
When source and output durations are available, the converted video is also
checked against the original before upload. A duration mismatch is recorded as
a conversion error and the output is not uploaded.

The server probes ffmpeg and requires the `libx264` and `aac` encoders before a
conversion job can start. Automatic high-bitrate detection also requires
ffprobe. The deployed runtime and local development machines must install both
executables or set `FFMPEG_PATH` and `FFPROBE_PATH`.

`UPLOAD_CHUNK_SIZE` must be at least 5 MB (`5000000`) for B2 multipart uploads.
Multipart uploads must contain at least two parts and cannot exceed B2's 10,000-part
or 10 TB limits.

Manual and automatic video selections are stored in the
`VideoConversionRequests` database table with queued, running, completed,
failed, and cancelled states. The conversion run is the durable worker: failed
or interrupted running requests remain retryable on a later run, while an
existing target is treated as complete. Originals are never deleted.

An administrator can manually queue a video with:

```json
{
  "fileName": "gallery/recording.mp4",
  "fileId": "4_zExampleFileId"
}
```

Send that body to `POST /admin/conversion/requests`.
The file id is checked when supplied so a stale library entry cannot queue a
different object. The library's compression action supplies both values and
starts a single-file conversion run when no run is active; it does not scan the
whole containing folder. Queue entries can be inspected
with `GET /admin/conversion/requests` and cancelled while queued with
`DELETE /admin/conversion/requests/{id}`. Before a queued supported video is
transcoded, its bitrate is checked again; if it is already within the configured
limit, the request is completed without creating another copy.

Each completed thumbnail or conversion run is saved as an HTML report in the
database. Reports can be listed, downloaded, or cleared from the Admin page.
The report contains a run summary; conversion reports also contain one row for
each converted or failed file, including the original and converted sizes and
the ffmpeg command used. Thumbnail reports contain rows for failures.

The canonical Admin endpoints are:

- `GET /admin`
- `GET /admin/reports`
- `GET /admin/reports/{id}`
- `POST /admin/reports/clear`
- `GET /admin/conversion/paths`
- `GET /admin/conversion/requests`
- `POST /admin/conversion/requests`
- `DELETE /admin/conversion/requests/{id}`
- `GET /admin/thumbnail-cache`
- `POST /admin/thumbnail-cache/clear`
- `POST /admin/thumbnails/start|stop`
- `POST /admin/convert/start|stop` (`start` requires a `path`)

All Admin endpoints require an authenticated user with the `Admin` role.
