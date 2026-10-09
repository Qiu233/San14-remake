using System.Numerics;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalSkyPatternProjectionMatrixSource LoadSkyPatternProjectionMatrixSource(string directory, string locale)
    {
        string ruleVersion=RenderRuleIdentity.For("sky-pattern-projection",locale);
        var constants=new OriginalSkyPatternProjectionConstants(
            .5f,100000f,BitConverter.UInt32BitsToSingle(0x37480000),2f,-2f,-1f,
            new(0,-1,0,0),new(0,1,0,0),new(1,0,0,0),new(0,0,-1,0),
            Matrix4x4.Identity,new(.5f,.5f,0,0));
        return new(locale,ruleVersion+";TC-D49EE0/SC-D4AC10;"+
            "actualCamera10/18/144/148/14C/210;selectedView1B20-CPU8-direction;98B630/11510/17B070;owned-matrix-rules",constants);
    }
}
