# Local B2 emulator

The Aspire development application uses `Server.Arkaine.LocalB2` instead of a
real Backblaze B2 bucket. It is an unauthenticated HTTP service that implements
the B2 operations currently used by Arkaine, so the normal `B2Service` HTTP
client is exercised during local development.

## Running Aspire

Start Aspire from the repository root:

```powershell
dotnet run --project Server.Arkaine\Server.Arkaine.AppHost
```

The `local-storage-setup` resource validates and creates the local bucket and
thumbnail directories before the application starts. The local bucket is
stored in `Server.Arkaine\Server.Arkaine.LocalB2\data`, with thumbnails beside
it in `Server.Arkaine\Server.Arkaine.LocalB2\thumbnails`. Add source media
beneath top-level folders in the bucket directory, for example:

```text
Server.Arkaine\Server.Arkaine.LocalB2\data\gallery-alpha\photo.webp
Server.Arkaine\Server.Arkaine.LocalB2\data\gallery-alpha\clips\video.mov
```

The `data\.local-b2` directory is reserved for metadata, upload staging, and
unfinished multipart uploads. It is not exposed through B2 listings.

## Configuration

These environment variables control the local Aspire setup:

| Setting | Default | Description |
| --- | --- | --- |
| `LOCAL_B2_ROOT` | `data` | Absolute bucket directory, or a path relative to the local B2 content root |
| `THUMBNAIL_DIR` | `<parent of LOCAL_B2_ROOT>\thumbnails` | Directory created by the setup resource and used by Arkaine for generated thumbnails |
| `LOCAL_B2_BUCKET_ID` | `local-bucket` | Bucket ID accepted by the compatibility API |
| `LOCAL_B2_BUCKET_NAME` | `local` | Bucket name used in download URLs |

Aspire configures Arkaine to use the local service automatically and passes the
thumbnail directory to the server. The setup resource requires the data and
thumbnail directories to be siblings. The local service does not validate
credentials, so its endpoint should only be exposed to a trusted local
development environment.

## Limitations and switching backends

The emulator models one current file per key rather than B2's complete version
history. It supports listing with prefixes, delimiters, and paging; downloads
including ranges; single and multipart uploads; server-side copy; and delete.
Files seeded directly into the bucket use extension-based content types.

The normal direct `Server.Arkaine` launch path uses the real `B2Service`.
Provide the normal B2 settings (`B2AuthUrl`, `B2_KEY_READ`, `B2_KEY_WRITE`,
`BUCKET_ID`, and `BUCKET_NAME`) through user secrets or environment variables.
The test fixtures register `MockB2` explicitly when an in-process fake is
needed. Do not expose real credentials in source-controlled configuration.

To run the Aspire development application against a real, isolated B2 test
bucket, set `B2_BACKEND` to `real` in the AppHost user secrets and provide
credentials there:

```powershell
dotnet user-secrets --project Server.Arkaine\Server.Arkaine.AppHost set B2_BACKEND real
dotnet user-secrets --project Server.Arkaine\Server.Arkaine.AppHost set B2AuthUrl https://api.backblazeb2.com/b2api/v2/b2_authorize_account
dotnet user-secrets --project Server.Arkaine\Server.Arkaine.AppHost set B2_KEY_READ "<read-application-key>"
dotnet user-secrets --project Server.Arkaine\Server.Arkaine.AppHost set B2_KEY_WRITE "<write-application-key>"
dotnet user-secrets --project Server.Arkaine\Server.Arkaine.AppHost set BUCKET_ID "<test-bucket-id>"
dotnet user-secrets --project Server.Arkaine\Server.Arkaine.AppHost set BUCKET_NAME "<test-bucket-name>"
```

Use separate application keys restricted to the test bucket. The read key
needs `listFiles` for scanning and unfinished multipart discovery, plus
`shareFiles` for video conversion (`readFiles` is also needed for image
downloads). The write key needs `writeFiles` for the upload operations. The
local emulator remains the default; switch back with:

```powershell
dotnet user-secrets --project Server.Arkaine\Server.Arkaine.AppHost set B2_BACKEND local
```

Video thumbnail generation asks B2 for a short-lived, file-scoped download
authorization so ffmpeg can read the original through HTTP range requests
without a full temporary download. A real B2 read key must include the
`shareFiles` capability for this request; the local emulator accepts it
without checking capabilities.
