namespace San14.Data;

/// <summary>Remake-owned initialization defaults. Later application and camera edits remain current inputs.</summary>
public sealed class LoadedMainMapCameraStartupSource
{
    internal LoadedMainMapCameraStartupSource(string locale) => Locale=MainMapCameraRules.Language(locale);
    public string Locale { get; }
    public string RuleVersion => MainMapCameraRules.Version;
    public uint StaticProjectionProfile => 2;
    public int AppMode288AfterReset => 0;
    public int ModeOverride28CAfterReset => -1;
    public uint FovOverride290BitsAfterReset => 0xBF800000;
    public float OwnerFovOverride290AfterReset => BitConverter.UInt32BitsToSingle(FovOverride290BitsAfterReset);
    public uint MaximumYOverrideWordAfterControllerReset => 0;
    public bool MaximumYOverrideAfterControllerReset => false;
    public string SourceEvidence => $"{RuleVersion};{Locale};fresh-app-and-controller;later-writers-are-current-inputs";
}

public static class MainMapCameraStartupSource
{
    public static LoadedMainMapCameraStartupSource CreateDefaults(string locale) => new(locale);
    public static LoadedMainMapCameraStartupSource Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return CreateDefaults(locale);
    }
}
