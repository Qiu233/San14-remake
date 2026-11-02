using System.Globalization;

namespace San14.Data;

/// <summary>A stable logical name. Only file, RDB payload, and UI text are defined so far.</summary>
public readonly record struct GameResourceId
{
    public string Kind { get; }
    public string Key { get; }

    private GameResourceId(string kind, string key)
    {
        Kind = kind;
        Key = key;
    }

    public static GameResourceId File(string relativePath)
    {
        ValidateSegments(relativePath);
        return new GameResourceId("file", relativePath);
    }

    public static GameResourceId Rdb(string archiveName, uint fileId)
    {
        if (archiveName.Length == 0 || archiveName.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("Archive name must be alphanumeric.", nameof(archiveName));
        return new GameResourceId("rdb", $"{archiveName}/{fileId:X8}");
    }

    public static GameResourceId Ui(string locale) => locale switch
    {
        "SC" or "TC" => new GameResourceId("ui", locale),
        _ => throw new ArgumentException("UI locale must be SC or TC.", nameof(locale))
    };

    public static GameResourceId Parse(string value)
    {
        const string prefix = "san14://";
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
            throw new FormatException("Resource URI must begin with san14://.");
        string tail = value[prefix.Length..];
        int slash = tail.IndexOf('/');
        if (slash < 1 || slash == tail.Length - 1)
            throw new FormatException("Resource URI has no kind or key.");
        string kind = tail[..slash];
        string key = tail[(slash + 1)..];
        try
        {
            return kind switch
            {
                "file" => File(key),
                "ui" => Ui(key),
                "rdb" => ParseRdb(key),
                _ => throw new FormatException($"Unsupported resource kind: {kind}")
            };
        }
        catch (ArgumentException error)
        {
            throw new FormatException($"Invalid resource URI: {value}", error);
        }
    }

    public string OverrideRelativePath => Kind switch
    {
        "file" => Key,
        "rdb" => $"rdb/{Key}.bin",
        "ui" => $"ui/{Key}.json",
        _ => throw new InvalidOperationException("Unsupported resource kind.")
    };

    public override string ToString() => $"san14://{Kind}/{Key}";

    private static GameResourceId ParseRdb(string key)
    {
        string[] parts = key.Split('/');
        if (parts.Length != 2 || parts[1].Length != 8 ||
            !uint.TryParse(parts[1], NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture, out uint id))
            throw new FormatException("RDB resource URI must use archive/eight-digit-hex-id.");
        return Rdb(parts[0], id);
    }

    private static void ValidateSegments(string path)
    {
        if (path.Length == 0 || path.Contains('\\') || path.Contains(':') ||
            path.Contains('?') || path.Contains('#') || path.Contains('%') ||
            path.Split('/').Any(part => part.Length == 0 || part is "." or ".." ||
                part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new ArgumentException("Resource path must be a safe relative slash-separated path.");
    }
}

/// <summary>Maps a logical resource URI to an optional file under one override root.</summary>
public sealed class ResourceOverrides
{
    public string? DirectoryPath { get; }

    public ResourceOverrides(string? directory)
    {
        if (directory is not null)
            DirectoryPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
    }

    public string? Find(GameResourceId id)
    {
        string? path = CandidatePath(id);
        return path is not null && System.IO.File.Exists(path) ? path : null;
    }

    internal string? CandidatePath(GameResourceId id)
    {
        if (DirectoryPath is null) return null;
        string path = Path.GetFullPath(Path.Combine(DirectoryPath,
            id.OverrideRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = DirectoryPath.EndsWith(Path.DirectorySeparatorChar)
            ? DirectoryPath : DirectoryPath + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Override path escapes its root: {id}");
        return path;
    }
}
