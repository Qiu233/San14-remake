namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static string ReadSky2MoonGeometryRecipe(string sourceDirectory,string locale)
    {
        _ = RenderRuleIdentity.For("Sky2MoonGeometryPreparation",locale);

        return $"{RenderRuleIdentity.For("Sky2MoonGeometryPreparation",locale)};Moon118E6B0-face15C0/token11+118E7B0-screen/token13;9-native-windows/finite-acos101E910/distinct-sincos3F7280";
    }
}
