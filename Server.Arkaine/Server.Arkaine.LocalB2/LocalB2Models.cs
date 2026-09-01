using System.Text.Json.Serialization;

namespace Server.Arkaine.LocalB2;

public sealed class LocalB2ListRequest
{
    [JsonPropertyName("bucketId")]
    public string BucketId { get; set; } = string.Empty;

    [JsonPropertyName("prefix")]
    public string? Prefix { get; set; }

    [JsonPropertyName("delimiter")]
    public string? Delimiter { get; set; }

    [JsonPropertyName("maxFileCount")]
    public int MaxFileCount { get; set; }

    [JsonPropertyName("startFileName")]
    public string? StartFileName { get; set; }
}

public sealed class LocalB2DeleteRequest
{
    [JsonPropertyName("fileId")]
    public string FileId { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;
}

public sealed class LocalB2CopyRequest
{
    [JsonPropertyName("sourceFileId")]
    public string SourceFileId { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;
}

public sealed class LocalB2UploadUrlRequest
{
    [JsonPropertyName("bucketId")]
    public string BucketId { get; set; } = string.Empty;
}

public sealed class LocalB2StartMultipartRequest
{
    [JsonPropertyName("bucketId")]
    public string BucketId { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;
}

public sealed class LocalB2UnfinishedRequest
{
    [JsonPropertyName("bucketId")]
    public string BucketId { get; set; } = string.Empty;

    [JsonPropertyName("namePrefix")]
    public string NamePrefix { get; set; } = string.Empty;

    [JsonPropertyName("maxFileCount")]
    public int MaxFileCount { get; set; }
}

public sealed class LocalB2FinishMultipartRequest
{
    [JsonPropertyName("fileId")]
    public string FileId { get; set; } = string.Empty;

    [JsonPropertyName("partSha1Array")]
    public IEnumerable<string>? PartSha1Array { get; set; }
}

public sealed class LocalB2GetPartUrlRequest
{
    [JsonPropertyName("fileId")]
    public string FileId { get; set; } = string.Empty;
}

public sealed record LocalB2File(
    string FileName,
    string FileId,
    string ContentType,
    long Length,
    string FullPath,
    string Type = "upload");

public sealed record LocalB2MultipartFile(
    string FileName,
    string FileId,
    string ContentType,
    long Length);
