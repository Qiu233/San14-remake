namespace San14.Data;
public static partial class OriginalDeferredSamplerSources
{
 internal static string ReadAppliedStarNativeRecipe(string directory,string locale)
 {
        _ = RenderRuleIdentity.For("SkyAppliedSourceStarNative",locale);

  int phase=locale=="TC"?0x27F3E00:0x27F4FC0;

  return $"{RenderRuleIdentity.For("SkyAppliedSourceStarNative",locale)};fresh-static-phase={phase:X}/PE-.data-zero-virtual-tail;118F040-table->11902E0-current-Star-writes;C5197F/C419BD=614->7F8;621->814;explicit-owned-clock-input-not-originalClock254";
 }
}
