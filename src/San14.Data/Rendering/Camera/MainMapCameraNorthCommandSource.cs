using System.Numerics;

namespace San14.Data;

/// <summary>North toolbar command: a timed yaw request through the existing controller.</summary>
public sealed class LoadedMainMapCameraNorthCommandSource
{
    private readonly MainMapCameraControllerRequest request = new(500,Vector4.Zero,-1,
        BitConverter.UInt64BitsToDouble(0x400921FB60000000UL),-1,1);
    internal LoadedMainMapCameraNorthCommandSource(string locale) => Locale=MainMapCameraRules.Language(locale);
    public string Locale { get; }
    public string RuleVersion => MainMapCameraRules.Version;
    public int ButtonIndex => 1;
    public int LayoutNodeIndex => 42;
    public int IconIndex => 467;
    public int NativeCommand => 3;
    public string SourceEvidence => $"{RuleVersion};{Locale};north-toolbar;500ms-yaw-only-selector1";
    public MainMapCameraControllerRequest CreateNorthRequest() => request;
}

public static class MainMapCameraNorthCommandSource
{
    public static LoadedMainMapCameraNorthCommandSource CreateDefaults(string locale) => new(locale);
    public static LoadedMainMapCameraNorthCommandSource Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return CreateDefaults(locale);
    }
}
