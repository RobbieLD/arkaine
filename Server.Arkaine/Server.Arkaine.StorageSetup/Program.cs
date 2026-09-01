const string dataDirectoryVariable = "LOCAL_B2_ROOT";
const string thumbnailDirectoryVariable = "THUMBNAIL_DIR";

var dataDirectory = ResolveDirectory(dataDirectoryVariable);
var thumbnailDirectory = ResolveDirectory(thumbnailDirectoryVariable);

if (!IsNestedDirectory(dataDirectory, thumbnailDirectory))
{
    throw new InvalidOperationException(
        $"{thumbnailDirectoryVariable} must point to a directory inside {dataDirectoryVariable}.");
}

Console.WriteLine($"Validating local data directory: {dataDirectory}");
Directory.CreateDirectory(dataDirectory);

Console.WriteLine($"Validating thumbnail directory: {thumbnailDirectory}");
Directory.CreateDirectory(thumbnailDirectory);

if (!Directory.Exists(dataDirectory) || !Directory.Exists(thumbnailDirectory))
{
    throw new InvalidOperationException("The local storage directories could not be created.");
}

Console.WriteLine("Local storage directories are ready.");

static string ResolveDirectory(string variableName)
{
    var value = Environment.GetEnvironmentVariable(variableName);
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"{variableName} is required.");
    }

    return Path.GetFullPath(value.Trim());
}

static bool IsNestedDirectory(string parent, string child)
{
    var relative = Path.GetRelativePath(parent, child);
    if (relative is "." or ".." || Path.IsPathRooted(relative))
    {
        return false;
    }

    return !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
           !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
}
