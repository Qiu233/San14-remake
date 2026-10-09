namespace San14.Data;

/// <summary>Remake-owned initialization defaults. Later application and camera edits remain current inputs.</summary>
public sealed class LoadedOriginalMainMapCameraStartupSource
{
    internal LoadedOriginalMainMapCameraStartupSource(string locale) => Locale=OriginalMainMapCameraRules.Language(locale);
    public string Locale { get; }
    public string RuleVersion => OriginalMainMapCameraRules.Version;
    /// <summary>Opaque address namespace in the existing CPU packet format; never a loaded image or callable address.</summary>
    public ulong CpuAddressBase => 0x140000000UL;
    public uint StaticProjectionProfile => 2;
    public int AppMode288AfterReset => 0;
    public int ModeOverride28CAfterReset => -1;
    public uint FovOverride290BitsAfterReset => 0xBF800000;
    public float OwnerFovOverride290AfterReset => BitConverter.UInt32BitsToSingle(FovOverride290BitsAfterReset);
    public uint MaximumYOverrideWordAfterControllerReset => 0;
    public bool MaximumYOverrideAfterControllerReset => false;
    public string SourceEvidence => $"{RuleVersion};{Locale};fresh-app-and-controller;later-writers-are-current-inputs";
}

public static class OriginalMainMapCameraStartupSource
{
    public static LoadedOriginalMainMapCameraStartupSource CreateDefaults(string locale) => new(locale);
    public static LoadedOriginalMainMapCameraStartupSource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return CreateDefaults(locale);
    }
}
