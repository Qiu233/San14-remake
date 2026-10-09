using System.Collections.ObjectModel;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalModelNativeParameterDirectorySource LoadModelNativeParameterDirectorySource(
        string sourceDirectory,string locale)
    {
        _ = RenderRuleIdentity.For("ModelNativeParameterDirectory",locale);
        var directories=new Dictionary<string,IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["Class18Base"] = Array.AsReadOnly(new[]{"vAmbDifScl","vAmbSpcScl","fAmbMapsDORate"}),
            ["PointBase"] = Array.AsReadOnly(new[]{"gridW2P","texel_size","voxel_res","w2vox"}),
            ["OccluderBase"] = Array.AsReadOnly(new[]{"atest"}),
            ["GBuffer"] = Array.AsReadOnly(new[]{"nMtrID","drawID"}),
            ["ReflectionProbeGBuffer"] = Array.AsReadOnly(new[]{"vProbePosition","vDistanceConv"}),
            ["BranchCurveType"] = Array.AsReadOnly(new[]{"nAttCurvType","nIESIndex"}),
            ["PatternMap"] = Array.AsReadOnly(new[]{"mW2Pt","fPtn","fPtnPlSub","fPtn1"}),
            ["OccFader"] = Array.AsReadOnly(new[]{"vOccRate"}),
            ["AmbientMapTransform"] = Array.AsReadOnly(new[]{"mW2E"}),
            ["VolumetricFog"] = Array.AsReadOnly(new[]{"gVfogInfo","gCalcVolumeFactor"}),
            ["ClusterLightList"] = Array.AsReadOnly(new[]{"nClstPtch","vClstZPrm","vFadeDist","vPartialP2V","fClstScl"}),
            ["SubPointLightShadows"] = Array.AsReadOnly(new[]{"vSplsNumSS","vSubCbW2S","vSubCubSz","fSplSize"}),
            ["SubSpotLightShadows"] = Array.AsReadOnly(new[]{"vSslsNumSS","mSubSptW2S","vSubSMSz","vSslSize"}),
        };
        return new(locale,RenderRuleIdentity.Version,
            new ReadOnlyDictionary<string,IReadOnlyList<string>>(directories));
    }
}
