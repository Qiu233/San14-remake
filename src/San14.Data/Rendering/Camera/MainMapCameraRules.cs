namespace San14.Data;

/// <summary>Version of the remake's camera behavior and serialized camera layout.</summary>
internal static class MainMapCameraRules
{
    internal const string Version = "map-camera/v1";

    internal static string Language(string locale) => locale switch
    {
        "SC" or "TC" => locale,
        _ => throw new ArgumentOutOfRangeException(nameof(locale), "Map camera rules support SC and TC.")
    };
}
