namespace San14.Data;
public static partial class RenderSamplerSourceAdapters
{
 internal static string ReadAppliedStarNativeRecipe(string directory,string locale)
 {
  return $"{RenderRuleIdentity.For("SkyAppliedSourceStarNative",locale)};fresh-phase=0;table->current-Star-writes;614->7F8;621->814;explicit-owned-clock-input";
 }
}
