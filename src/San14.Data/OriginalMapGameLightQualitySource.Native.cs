namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapGameLightQualitySource LoadGameLightQualitySource(string directory,string locale)
    {
        string ruleVersion=RenderRuleIdentity.For("game-light-quality",locale);

        LoadedOriginalMapGameLightQualitySource recipe;
        switch(locale)
        {
            case "SC":
                recipe=new("SC",ruleVersion,
                    0x3E61C0,0x01A48CB0,0x01A48D1C,0x003EC1C0,
                    0x003EC400,0x003EC236,0x164180,0);

                break;
            case "TC":
                recipe=new("TC",ruleVersion,
                    0x3E5BC0,0x01A47C80,0x01A47CEC,0x003EBB80,
                    0x003EBDC0,0x003EBBF6,0x1643D0,0);

                break;
            default:throw new NotSupportedException("Game Light quality native source requires SC/TC.");
        }

        return recipe;
    }
}

internal sealed record LoadedOriginalMapGameLightQualitySource(string Locale,string RuleVersion,
    int InitializerRva,uint EnvironmentHolderRva,uint PrimaryQualityRva,int OptionGetterRva,int Option18TableEntryRva,
    int Option18ArmRva,int WriterRva,int FreshQuality)
{
    public string SourceProvenance=>$"{Locale}{InitializerRva:X}-primary64-zero;fresh-before-saved-config;"+
        $"ENV={EnvironmentHolderRva:X};option18={OptionGetterRva:X}/{Option18ArmRva:X};writer={WriterRva:X};"+
        RuleVersion;
}
