using System.Numerics;

namespace San14.Data;

/// <summary>North toolbar command: a timed yaw request through the existing controller.</summary>
public sealed class LoadedOriginalMainMapCameraNorthCommandSource
{
    private readonly OriginalMainMapCameraControllerRequest request = new(500,Vector4.Zero,-1,
        BitConverter.UInt64BitsToDouble(0x400921FB60000000UL),-1,1);
    internal LoadedOriginalMainMapCameraNorthCommandSource(string locale) => Locale=OriginalMainMapCameraRules.Language(locale);
    public string Locale { get; }
    public string RuleVersion => OriginalMainMapCameraRules.Version;
    public int ButtonIndex => 1;
    public int LayoutNodeIndex => 42;
    public int IconIndex => 467;
    public int NativeCommand => 3;
    public string SourceEvidence => $"{RuleVersion};{Locale};north-toolbar;500ms-yaw-only-selector1";
    public OriginalMainMapCameraControllerRequest CreateNorthRequest() => request;
}

public static class OriginalMainMapCameraNorthCommandSource
{
    public static LoadedOriginalMainMapCameraNorthCommandSource CreateDefaults(string locale) => new(locale);
    public static LoadedOriginalMainMapCameraNorthCommandSource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return CreateDefaults(locale);
    }
}
