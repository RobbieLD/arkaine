namespace Server.Arkaine
{
    public static class ThumbnailPathResolver
    {
        private static readonly char[] InvalidFileNameCharacters = ['<', '>', '"', '|', '?', '*'];

        public static bool TryResolve(string? rootDirectory, string? relativePath, out string fullPath)
        {
            fullPath = string.Empty;

            if (string.IsNullOrWhiteSpace(rootDirectory) ||
                string.IsNullOrWhiteSpace(relativePath) ||
                relativePath.IndexOfAny(new[] { '\0', ':' }) >= 0 ||
                IsRootedPath(relativePath))
            {
                return false;
            }

            var normalizedPath = relativePath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            var segments = normalizedPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0 ||
                segments.Any(segment =>
                    segment is "." or ".." ||
                    segment.Any(character => character < ' ' || InvalidFileNameCharacters.Contains(character)) ||
                    IsReservedWindowsName(segment) ||
                    segment.EndsWith('.') ||
                    segment.EndsWith(' ')))
            {
                return false;
            }

            try
            {
                var root = Path.GetFullPath(rootDirectory);
                var candidate = Path.GetFullPath(Path.Combine(root, normalizedPath));
                var relative = Path.GetRelativePath(root, candidate);

                if (relative == "." ||
                    relative == ".." ||
                    relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    Path.IsPathRooted(relative) ||
                    ContainsReparsePoint(root, candidate))
                {
                    return false;
                }

                fullPath = candidate;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        public static string ToUrlPath(string relativePath)
        {
            return relativePath.Replace('\\', '/');
        }

        private static bool IsRootedPath(string path)
        {
            return Path.IsPathRooted(path) ||
                   path.StartsWith("//", StringComparison.Ordinal) ||
                   path.StartsWith(@"\\", StringComparison.Ordinal) ||
                   (path.Length > 1 && char.IsLetter(path[0]) && path[1] == ':');
        }

        private static bool IsReservedWindowsName(string segment)
        {
            var name = segment.Split('.', 2)[0];
            return name.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
                   (name.Length == 4 &&
                    (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                     name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                    name[3] is >= '1' and <= '9');
        }

        private static bool ContainsReparsePoint(string root, string path)
        {
            var relative = Path.GetRelativePath(root, path);
            var current = root;

            foreach (var segment in relative.Split(
                         new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                if (!File.Exists(current) && !Directory.Exists(current))
                {
                    continue;
                }

                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
