
namespace San14.Data;

public sealed record OriginalWorldGeometryStageNativeSource(string Locale, string RuleVersion, int TargetExecuteRva, int PopExecuteRva, int PreRendererRva,
    int ModelsRva, int LocalCameraCopyRva, int LocalCameraCleanupRva)
{
    public string SourceProvenance => $"{RenderRuleIdentity.For("WorldGeometryStage",Locale)};" +
        "loaded-whole-native-callers;HostQ7F8-ctor0;View1B80/1BB0-ctor/reset0;" +
        "9599E0-push-row;E96F30-FCFFFFFF-mask;963060-copy-before-pop;no-GPU-state-claim";
}

public static partial class OriginalDeferredSamplerSources
{
    internal static OriginalWorldGeometryStageNativeSource LoadWorldGeometryStageNativeSource(string directory, string locale)
    {
        _ = RenderRuleIdentity.For("WorldGeometryStage",locale);

        bool tc = locale == "TC";

        return new(locale, RenderRuleIdentity.Version,
            tc ? 0xE931A0 : 0xE94190, tc ? 0xE96F30 : 0xE97F20,
            tc ? 0xD42610 : 0xD43340, tc ? 0xD2B110 : 0xD2BE40,
            tc ? 0xB992A0 : 0xB9A260, tc ? 0xD81A00 : 0x9390A0);
    }
}
