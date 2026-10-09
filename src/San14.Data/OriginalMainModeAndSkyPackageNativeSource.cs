using System.Buffers.Binary;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static string ReadMainModeNativeRecipe(string sourceDirectory,string locale)
    {
        if(locale is not ("TC" or "SC"))throw new NotSupportedException("This source layout requires SC or TC.");var ruleVersion=RenderRuleIdentity.For("main-mode",locale);

        return $"{ruleVersion};source10CF0-reset->17D3D-classifier;thresholds2.5/3.5/4.5/5.5;primary-denominator10000";
    }
    internal static string ReadSelectedSkyPackageNativeRecipe(string sourceDirectory,string locale)
    {
        if(locale is not ("TC" or "SC"))throw new NotSupportedException("This source layout requires SC or TC.");var ruleVersion=RenderRuleIdentity.For("selected-sky-package",locale);

        var p=locale switch{
            "TC"=>0xD3ADEB,
            "SC"=>0xD3BB1B,
            _=>throw new InvalidDataException("Selected Sky package requires SC or TC.")};

        return $"{ruleVersion};D3A850-package={p:X8}/360;row0=L0..1F;ambient30=0;hemiCount=0/hemiPointer=null;Fog40=copyL150..1AF";
    }
}
