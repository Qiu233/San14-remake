using System.Numerics;

namespace San14.Data;

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedDeferredEarlyIndirectPatternMapSource LoadEarlyIndirectPatternMapSource(string directory,string locale)
    {
        string rules=RenderRuleIdentity.For("early-indirect-pattern-map",locale);
        const float defaultTransformScale=32000f;
        const float affineNumerator=1f;
        float resetScale=affineNumerator/defaultTransformScale;
        var reset=new Matrix4x4(resetScale,0,0,0, 0,0,-1,0,
            0,resetScale,0,0, 0,0,0,1);
        return new(locale,rules+";constructor-identity/factory-reset;current-transform-owned-by-caller",
            RenderRuleNumericEncoding.Matrix(Matrix4x4.Identity),
            RenderRuleNumericEncoding.Vectors(new Vector4(0,0,-1,0)),
            defaultTransformScale,affineNumerator,RenderRuleNumericEncoding.Matrix(reset),0,
            RenderRuleNumericEncoding.Vectors(Vector4.Zero,Vector4.Zero));
    }
}
