namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalOrdinarySdfHostPoolConfigurationSource LoadOrdinarySdfHostPoolSource(string directory,string locale)
    {
        string rules=RenderRuleIdentity.For("ordinary-sdf-host-pool",locale);
        const uint defaultCapacity=256;
        var startup=new OriginalOrdinarySdfHostPoolFields(0,0,defaultCapacity);
        var absentOverride=new OriginalOrdinarySdfHostPoolFields(defaultCapacity,defaultCapacity,0);
        return new(locale,startup,absentOverride,rules+";fresh-Host/explicit-absent-override;current-pool-preserves-on-zero-capacities");
    }
}
