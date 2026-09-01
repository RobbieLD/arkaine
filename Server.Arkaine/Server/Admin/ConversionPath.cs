namespace Server.Arkaine.Admin
{
    public static class ConversionPath
    {
        public const string RootSelection = "/";

        public static string Normalize(string? value)
        {
            if (!TryNormalize(value, out var path))
            {
                throw new ArgumentException(
                    "A conversion path must be the root or a top-level folder.",
                    nameof(value));
            }

            return path;
        }

        public static bool TryNormalize(string? value, out string path)
        {
            path = string.Empty;
            var candidate = value?.Trim() ?? string.Empty;

            if (candidate == RootSelection)
            {
                return true;
            }

            if (candidate.Length == 0 ||
                candidate.Contains('\\') ||
                candidate.StartsWith("/", StringComparison.Ordinal))
            {
                return false;
            }

            candidate = candidate.TrimEnd('/');

            if (candidate.Length == 0 ||
                candidate.Contains('/') ||
                candidate is "." or "..")
            {
                return false;
            }

            path = $"{candidate}/";
            return true;
        }
    }
}
