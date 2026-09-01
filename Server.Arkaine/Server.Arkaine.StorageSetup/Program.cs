const string dataDirectoryVariable = "LOCAL_B2_ROOT";
const string thumbnailDirectoryVariable = "THUMBNAIL_DIR";

var dataDirectory = ResolveDirectory(dataDirectoryVariable);
var thumbnailDirectory = ResolveDirectory(thumbnailDirectoryVariable);

if (!AreSiblingDirectories(dataDirectory, thumbnailDirectory))
{
    throw new InvalidOperationException(
        $"{thumbnailDirectoryVariable} must point to a sibling directory of {dataDirectoryVariable}.");
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

static bool AreSiblingDirectories(string first, string second)
{
    var firstParent = Directory.GetParent(first)?.FullName;
    var secondParent = Directory.GetParent(second)?.FullName;

    if (string.IsNullOrWhiteSpace(firstParent) || string.IsNullOrWhiteSpace(secondParent))
    {
        return false;
    }

    var comparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    return string.Equals(firstParent, secondParent, comparison) &&
           !string.Equals(first, second, comparison);
}
