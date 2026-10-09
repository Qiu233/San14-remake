namespace San14.Data;

public readonly record struct MainMapCameraEntryInputs(
    bool MainDataPresent, bool HostGate210Present, bool MainCpuPresent);

public readonly record struct MainMapCameraEntryFarWrite(
    bool Applied, float SourceFar14, bool NativeViewportNotifyReached);

/// <summary>One map entry's camera clip rule. Rejected owner gates retain the prior value.</summary>
public sealed class LoadedMainMapCameraEntrySource
{
    internal LoadedMainMapCameraEntrySource(string locale) => Locale=MainMapCameraRules.Language(locale);
    public string Locale { get; }
    public string RuleVersion => MainMapCameraRules.Version;
    public int TitleExitFarInteger => 200000;
    public string SourceEvidence => $"{RuleVersion};{Locale};map-entry-far;retained-owner-gates";

    public MainMapCameraEntryFarWrite ApplyTitleExitFar(
        MainMapCameraEntryInputs inputs, float retainedSourceFar14)
    {
        if (!inputs.MainDataPresent || !inputs.HostGate210Present || !inputs.MainCpuPresent)
            return new(false, retainedSourceFar14, false);
        return new(true, (float)TitleExitFarInteger, true);
    }
}

public static class MainMapCameraEntrySource
{
    public static LoadedMainMapCameraEntrySource CreateDefaults(string locale) => new(locale);
    public static LoadedMainMapCameraEntrySource Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return CreateDefaults(locale);
    }
}
