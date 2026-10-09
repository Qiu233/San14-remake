using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>1061380's two actual sampler vectors at CPU6D0/6E0.
/// The first is the view-depth plane in the first shadow texture's space;
/// the second contains cascade distances relative to its first endpoint.
/// Atlas scale/offset at6F0 is a separate1059670 writer.</summary>
public readonly record struct DeferredEarlyShadowCompactSamplerNumericFields(
    Vector4 DepthPlane6D0,Vector4 RelativeEnds6E0)
{
    public void WriteNumericFields(Span<byte> actualDestination32)
    {
        if(actualDestination32.Length!=32)
            throw new ArgumentException("Compact sampler numeric writer needs its actual32-byte destination.",nameof(actualDestination32));
        float[] words=[DepthPlane6D0.X,DepthPlane6D0.Y,DepthPlane6D0.Z,DepthPlane6D0.W,
            RelativeEnds6E0.X,RelativeEnds6E0.Y,RelativeEnds6E0.Z,RelativeEnds6E0.W];
        for(int i=0;i<words.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(actualDestination32[(i*4)..],words[i]);
    }
}

public static class DeferredEarlyShadowCompactSamplerSource
{
    /// <summary>Complete1061380 numeric kernel. The matrix is the actual
    /// first5D0 final projection, not its preceding light-viewBE0.</summary>
    public static DeferredEarlyShadowCompactSamplerNumericFields PrepareNumericFields(
        Matrix4x4 actualFirstShadow5D0,ReadOnlySpan<byte> actualOwnCameraPrefix150,
        IReadOnlyList<float> actualPassEndsBB0ToBBC)
    {
        if(actualOwnCameraPrefix150.Length!=0x150)
            throw new ArgumentException("Compact sampler requires its actual own150 Camera prefix.",nameof(actualOwnCameraPrefix150));
        if(actualPassEndsBB0ToBBC.Count!=4)
            throw new ArgumentException("Compact sampler reads all four actual retained endpoint words.",nameof(actualPassEndsBB0ToBBC));
        var m=actualFirstShadow5D0;
        m.M11=M(m.M11,.5f);m.M21=M(m.M21,.5f);m.M31=M(m.M31,.5f);m.M41=M(m.M41,.5f);
        m.M12=M(m.M12,-.5f);m.M22=M(m.M22,-.5f);m.M32=M(m.M32,-.5f);m.M42=M(m.M42,-.5f);
        Matrix4x4 texture=DeferredEarlyShadowCompactFinalMatrixSource.Translate98B630(m,new(.5f,.5f,0));
        Vector4 eye=ReadVector(actualOwnCameraPrefix150,0),target=ReadVector(actualOwnCameraPrefix150,0x10),
            up=ReadVector(actualOwnCameraPrefix150,0x20);
        float dx=S(target.X,eye.X),dy=S(target.Y,eye.Y),dz=S(target.Z,eye.Z);
        float reciprocal=D(1,MathF.Sqrt(A(A(M(dy,dy),M(dx,dx)),M(dz,dz))));
        Vector4 direction=new(M(dx,reciprocal),M(dy,reciprocal),M(dz,reciprocal),0);
        Matrix4x4 view=DeferredEarlyShadowLightViewSource.LightViewA07370(eye,direction,up);
        Matrix4x4 inverse=NativeMatrixMath.Invert(texture,out _);
        Vector4 plane=new(view.M13,view.M23,view.M33,S(view.M43,actualPassEndsBB0ToBBC[0]));
        Vector4 output=new(RowDot(plane,inverse.M11,inverse.M12,inverse.M13,inverse.M14),
            RowDot(plane,inverse.M21,inverse.M22,inverse.M23,inverse.M24),
            RowDot(plane,inverse.M31,inverse.M32,inverse.M33,inverse.M34),
            RowDot(plane,inverse.M41,inverse.M42,inverse.M43,inverse.M44));
        return new(output,new(S(actualPassEndsBB0ToBBC[1],actualPassEndsBB0ToBBC[0]),
            S(actualPassEndsBB0ToBBC[2],actualPassEndsBB0ToBBC[0]),
            S(actualPassEndsBB0ToBBC[3],actualPassEndsBB0ToBBC[0]),0));
    }
    private static Vector4 ReadVector(ReadOnlySpan<byte> source,int at)=>new(
        BinaryPrimitives.ReadSingleLittleEndian(source[at..]),BinaryPrimitives.ReadSingleLittleEndian(source[(at+4)..]),
        BinaryPrimitives.ReadSingleLittleEndian(source[(at+8)..]),BinaryPrimitives.ReadSingleLittleEndian(source[(at+12)..]));
    private static float RowDot(Vector4 v,float x,float y,float z,float w)=>A(A(A(M(v.Y,y),M(v.X,x)),M(v.Z,z)),M(v.W,w));
    private static float M(float a,float b)=>NativeFloat.Multiply(a,b);
    private static float A(float a,float b)=>NativeFloat.Add(a,b);
    private static float S(float a,float b)=>NativeFloat.Subtract(a,b);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}
