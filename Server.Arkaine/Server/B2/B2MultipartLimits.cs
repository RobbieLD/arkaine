namespace Server.Arkaine.B2
{
    public static class B2MultipartLimits
    {
        public const int MinimumPartSizeBytes = 5_000_000;
        public const int MaximumPartCount = 10_000;
        public const long MaximumFileSizeBytes = 10_000_000_000_000;
    }
}
