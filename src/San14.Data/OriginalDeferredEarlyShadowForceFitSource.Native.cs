namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalDeferredEarlyShadowForceFitSource LoadEarlyShadowForceFitSource(
        string directory,string locale)
    {
        if(locale is not ("TC" or "SC"))throw new NotSupportedException("This source layout requires SC or TC.");
        var ruleVersion=RenderRuleIdentity.For("shadow-force-fit",locale);

        int constructor=locale=="TC"?0x9FFCA0:0xA00CA0;
        // At+3 XOR R10D,R10D zero-extends the whole R10. The full source
        // reaches its+BB MOV [RAX+4A4],R10 without redefining that register.
        ulong r10=0; r10^=r10;
        return new(locale,$"{ruleVersion};"+
            $"full{constructor:X8}-1079B;XOR-R10D@{constructor+3:X8};qword4A4@{constructor+0xBB:X8};"+
            "fresh-owned-fragment;not-native-live-state-or-pool-reuse",r10);
    }
}
