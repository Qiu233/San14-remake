using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>A059B0's actual retained-zero-force-fit numeric writes. A null
/// matrix/span/vector means the original branch did not write that output.</summary>
public sealed record OriginalDeferredEarlyShadowCompactFinalPassNumericFields(int Index,
    Matrix4x4? Shadow5D0,Matrix4x4 CenteredLightViewBE0,float? DepthSpanA0,Vector4? DepthVector870,Vector4? DepthVector880);

/// <summary>Only source-backed projection numbers for the explicit
/// unit48=0 branch; this does not create its unknown later field writer.</summary>
public static class OriginalDeferredEarlyShadowCompactFinalMatrixSource
{
    /// <summary>Complete A059B0 normal unit48=0 branch with a supplied own
    /// Camera. This is a numeric kernel, not current caster-list publication.</summary>
    public static OriginalDeferredEarlyShadowCompactFinalPassNumericFields PrepareNumericNormalFields(
        int index,OriginalDeferredEarlyShadowCasterPassNumericFields actualPass,bool actualClipPresent40,
        ReadOnlySpan<byte> actualOwnCameraPrefix150,uint actualForceFit48)
    {
        if(actualOwnCameraPrefix150.Length!=0x150)
            throw new ArgumentException("Final shadow numeric pass requires its actual own150 Camera prefix.",nameof(actualOwnCameraPrefix150));
        if(actualForceFit48!=0)
            throw new NotSupportedException("Actual force-fit48 requires its separate perspective/snap producer; a zero field cannot replace it.");
        var view=actualPass.WorldToLightBE0;
        if(actualPass.CasterDirty44==0&&!actualClipPresent40)
        {
            var fallback=ReadMatrix(actualOwnCameraPrefix150,0x70);
            float far=BinaryPrimitives.ReadSingleLittleEndian(actualOwnCameraPrefix150[0x134..]);
            fallback.M44=A(far,far);
            return new(index,fallback,view,null,null,null);
        }
        Vector4 lo=actualPass.Minimum0,hi=actualPass.Maximum10;
        if(actualPass.CasterDirty44!=0)
        {
            lo=new(Maxss(lo.X,actualPass.CasterMinimum20.X),Maxss(lo.Y,actualPass.CasterMinimum20.Y),actualPass.CasterMinimum20.Z,lo.W);
            hi=new(Maxss(Minss(hi.X,actualPass.CasterMaximum30.X),lo.X),
                Maxss(Minss(hi.Y,actualPass.CasterMaximum30.Y),lo.Y),
                Maxss(Minss(hi.Z,actualPass.CasterMaximum30.Z),lo.Z),hi.W);
        }
        float width=S(hi.X,lo.X),height=S(hi.Y,lo.Y),depth=S(hi.Z,lo.Z);
        float translateX=Neg(A(M(hi.X,.5f),M(lo.X,.5f))),
            translateY=Neg(A(M(lo.Y,.5f),M(hi.Y,.5f)));
        Matrix4x4 centered=Translate98B630(view,new(translateX,translateY,0));
        float halfWidth=M(width,.5f),halfHeight=M(height,.5f),farValue=A(lo.Z,depth);
        if(!(halfWidth>0&&halfHeight>0&&farValue>lo.Z))
            return new(index,null,centered,null,null,null);
        Matrix4x4 projection=ProjectionA071F0(halfWidth,halfHeight,lo.Z,farValue);
        Matrix4x4 shadow=OriginalUnitWorldTransformMath.ComposeAffine17ADD0(centered,projection);
        float span=S(farValue,lo.Z);
        return new(index,shadow,centered,span,new(0,0,S(lo.Z,farValue),Neg(lo.Z)),new(0,0,0,1));
    }

    /// <summary>Complete source ortho numeric branch. Its X scale is -1/w,
    /// Y scale +1/h and depth +1/(far-near), matching source texture axes.</summary>
    public static Matrix4x4 ProjectionA071F0(float halfWidth,float halfHeight,float near,float far)
    {
        float low=BitConverter.UInt32BitsToSingle(0xB7480000),high=BitConverter.UInt32BitsToSingle(0x37480000);
        if((halfWidth>=low&&halfWidth<=high)||(halfHeight>=low&&halfHeight<=high))
            throw new NotSupportedException("Original A071F0 leaves its destination unwritten for this near-zero width/height; no guessed projection is supplied.");
        float depth=S(far,near);
        if(depth>=low&&depth<=high)depth=BitConverter.UInt32BitsToSingle(0x3C23D70A);
        return new(D(-1,halfWidth),0,0,0,0,D(1,halfHeight),0,0,0,0,D(1,depth),0,0,0,Neg(D(near,depth)),1);
    }

    public static Matrix4x4 Translate98B630(Matrix4x4 m,Vector3 translation)
    {
        Vector4 Turn(Vector4 v)=>new(A(M(v.W,translation.X),v.X),A(M(v.W,translation.Y),v.Y),A(M(v.W,translation.Z),v.Z),v.W);
        Vector4 a=Turn(new(m.M11,m.M12,m.M13,m.M14)),b=Turn(new(m.M21,m.M22,m.M23,m.M24)),
            c=Turn(new(m.M31,m.M32,m.M33,m.M34)),d=Turn(new(m.M41,m.M42,m.M43,m.M44));
        return new(a.X,a.Y,a.Z,a.W,b.X,b.Y,b.Z,b.W,c.X,c.Y,c.Z,c.W,d.X,d.Y,d.Z,d.W);
    }
    private static Matrix4x4 ReadMatrix(ReadOnlySpan<byte> b,int at)
    {
        float[] v=new float[16];for(int i=0;i<16;i++)v[i]=BinaryPrimitives.ReadSingleLittleEndian(b[(at+i*4)..]);
        return new(v[0],v[1],v[2],v[3],v[4],v[5],v[6],v[7],v[8],v[9],v[10],v[11],v[12],v[13],v[14],v[15]);
    }
    private static float Minss(float a,float b)=>a<b?a:b;
    private static float Maxss(float a,float b)=>a>b?a:b;
    private static float Neg(float v)=>BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(v)^0x80000000u);
    private static float M(float a,float b)=>OriginalNativeFloat.Multiply(a,b);
    private static float A(float a,float b)=>OriginalNativeFloat.Add(a,b);
    private static float S(float a,float b)=>OriginalNativeFloat.Subtract(a,b);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}
