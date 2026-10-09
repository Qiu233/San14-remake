namespace San14.Data;
public static partial class OriginalDeferredSamplerSources
{
 internal static LoadedOriginalDeferredEarlyLitSource LoadEarlyLitSource(string directory,string locale,
  LoadedOriginalDeferredEarlyIndirectSource programs,LoadedOriginalDeferredSamplerNativeSource sampler)
 {
  if(locale is not ("TC" or "SC"))throw new NotSupportedException("This source layout requires SC or TC.");var ruleVersion=RenderRuleIdentity.For("early-lit",locale);

  return new(locale,$"{ruleVersion};"+
   "actual2939+created-subshadow/PatternMap/ShadowReceiverBias-source;full-numeric7/physical-descriptor-source",
   programs,sampler);
 }
}
