
namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapSceneProxyNativeSource LoadMapSceneProxySource(string directory,string locale)
    {
        _ = RenderRuleIdentity.For("MapSceneProxy",locale);

        LoadedOriginalMapSceneProxyNativeSource recipe;
        switch(locale)
        {
            case "SC":
                recipe=new("SC",RenderRuleIdentity.Version,0xE822B0,0xE6DC60,
                    0x016E7A08,0x00EAA2F0,0xEAA439,0xC0EEB0,
                    0xD14016,0xD14C48,0xD03A37,
                    0xD230F0,0xD1F140,0xD13827);

                break;
            case "TC":
                recipe=new("TC",RenderRuleIdentity.Version,0xE81200,0xE6CBB0,
                    0x016E6DD8,0x00EA9300,0xEA9449,0xC0D9D0,
                    0xD132E6,0xD13F18,0xD02D77,
                    0xD223C0,0xD1E410,0xD12AF7);

                break;
            default:throw new NotSupportedException("Scene proxy source requires SC/TC.");
        }

        return recipe;
    }
}

internal sealed record LoadedOriginalMapSceneProxyNativeSource(string Locale,string RuleVersion,
    int FactoryRva,int ConstructorRva,int Node1VtableRva,int SelectorRva,int SelectionWindowRva,int InstallerRva,
    int MainForcedSelectorRva,int MainPublicationRva,int MainCopyRva,int CommonLoaderRva,int CommonCopyRva,int AuxPublicationRva)
{
    public string SourceEvidence=>$"{RenderRuleIdentity.For("MapSceneProxy",Locale)};"+
        $"node1/5F0A8446:{FactoryRva:X}-typed-arg5/6/8;{ConstructorRva:X}:CPU460/464/468;VT{Node1VtableRva:X}/v8={SelectorRva:X};"+
        $"Q110[0]={MainPublicationRva:X}-prepared-View;{MainForcedSelectorRva:X}-forces-Camera1CC=-1;{MainCopyRva:X}-copy;"+
        $"Common/Aux={CommonLoaderRva:X}/{CommonCopyRva:X}/{AuxPublicationRva:X};Q240=actual-Common-Aux;"+
        $"selector={SelectionWindowRva:X};named90B366D4-not-HDR881E16F9;installer={InstallerRva:X}-C8-counted-install";
}
