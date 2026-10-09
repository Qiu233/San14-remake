namespace San14.Data;
public static partial class RenderSamplerSourceAdapters
{
 internal static LoadedDeferredEarlyLitSource LoadEarlyLitSource(string directory,string locale,
  LoadedDeferredEarlyIndirectSource programs,LoadedDeferredSamplerNativeSource sampler)
 {
  if(locale is not ("TC" or "SC"))throw new NotSupportedException("This source layout requires SC or TC.");var ruleVersion=RenderRuleIdentity.For("early-lit",locale);

  return new(locale,$"{ruleVersion};"+
   "actual2939+created-subshadow/PatternMap/ShadowReceiverBias-source;full-numeric7/physical-descriptor-source",
   programs,sampler);
 }
}
