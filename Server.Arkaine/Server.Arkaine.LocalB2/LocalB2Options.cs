namespace Server.Arkaine.LocalB2;

public sealed class LocalB2Options
{
    public string RootDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "local-b2-data");
    public string BucketId { get; set; } = "local-bucket";
    public string BucketName { get; set; } = "local";

    public void Normalize()
    {
        RootDirectory = Path.GetFullPath(
            string.IsNullOrWhiteSpace(RootDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "local-b2-data")
                : RootDirectory.Trim());
        BucketId = string.IsNullOrWhiteSpace(BucketId) ? "local-bucket" : BucketId.Trim();
        BucketName = string.IsNullOrWhiteSpace(BucketName) ? "local" : BucketName.Trim();
    }
}
