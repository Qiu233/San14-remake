namespace San14.Data;

public readonly record struct OriginalMainMapCameraEntryInputs(
    bool MainDataPresent, bool HostGate210Present, bool MainCpuPresent);

public readonly record struct OriginalMainMapCameraEntryFarWrite(
    bool Applied, float SourceFar14, bool NativeViewportNotifyReached);

/// <summary>One map entry's camera clip rule. Rejected owner gates retain the prior value.</summary>
public sealed class LoadedOriginalMainMapCameraEntrySource
{
    internal LoadedOriginalMainMapCameraEntrySource(string locale) => Locale=OriginalMainMapCameraRules.Language(locale);
    public string Locale { get; }
    public string RuleVersion => OriginalMainMapCameraRules.Version;
    public int TitleExitFarInteger => 200000;
    public string SourceEvidence => $"{RuleVersion};{Locale};map-entry-far;retained-owner-gates";

    public OriginalMainMapCameraEntryFarWrite ApplyTitleExitFar(
        OriginalMainMapCameraEntryInputs inputs, float retainedSourceFar14)
    {
        if (!inputs.MainDataPresent || !inputs.HostGate210Present || !inputs.MainCpuPresent)
            return new(false, retainedSourceFar14, false);
        return new(true, (float)TitleExitFarInteger, true);
    }
}

public static class OriginalMainMapCameraEntrySource
{
    public static LoadedOriginalMainMapCameraEntrySource CreateDefaults(string locale) => new(locale);
    public static LoadedOriginalMainMapCameraEntrySource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return CreateDefaults(locale);
    }
}
