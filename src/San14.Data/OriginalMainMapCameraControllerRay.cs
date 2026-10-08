using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public static partial class OriginalMainMapCameraController
{
    /// <summary>E790 uses current source V80/V88 getter values. It rotates +Z
    /// with the raw quaternion and normalizes XYZ; both returned W values are1.</summary>
    public static OriginalMainMapCameraEyeForward EyeForward(OriginalMapCameraCurrentPose currentPose)
    {
        var f=OriginalMapCameraConstructionMath.ForwardC11350(currentPose.Quaternion88);
        return new(new(currentPose.Position80,1),new(f.X,f.Y,f.Z,1));
    }

    /// <summary>EFB0's current source V420 prefix. Input pixels are the native
    /// canonical1920×1080 coordinate pair; conversion from physical viewport
    /// coordinates belongs to the input producer. No viewport aspect is substituted.</summary>
    public static OriginalMainMapCameraCursorRay CursorRay(ReadOnlySpan<byte> completeSourceCameraPrefix,
        int canonicalPixelX,int canonicalPixelY)
    {
        if(completeSourceCameraPrefix.Length!=OriginalMapCameraConstruction.PrefixByteLength)
            throw new InvalidDataException("Native cursor ray requires its complete owned3C0 current Camera prefix.");
        byte[] prefix=completeSourceCameraPrefix.ToArray();
        float F(int offset)=>BinaryPrimitives.ReadSingleLittleEndian(prefix.AsSpan(offset,4));
        float[] raw=new float[16];for(int i=0;i<16;i++)raw[i]=F(0x100+i*4);
        var matrix=new Matrix4x4(raw[0],raw[1],raw[2],raw[3],raw[4],raw[5],raw[6],raw[7],
            raw[8],raw[9],raw[10],raw[11],raw[12],raw[13],raw[14],raw[15]);
        Matrix4x4 inverse=AffineInverse11510(matrix);
        float x=D(unchecked(canonicalPixelX-960),960f),y=D(unchecked(540-canonicalPixelY),540f),
            ratio=D(F(0x144),F(0x140)),farX=M(ratio,x),farY=M(ratio,y);
        float Near(float r0,float r1,float r2,float r3)=>A(A(A(M(r1,y),M(r0,x)),M(r2,0)),r3);
        float Far(float r0,float r1,float r2,float r3)=>A(S(A(M(r1,farY),M(r0,farX)),r2),r3);
        float dx=S(Far(inverse.M11,inverse.M21,inverse.M31,inverse.M41),Near(inverse.M11,inverse.M21,inverse.M31,inverse.M41)),
            dy=S(Far(inverse.M12,inverse.M22,inverse.M32,inverse.M42),Near(inverse.M12,inverse.M22,inverse.M32,inverse.M42)),
            dz=S(Far(inverse.M13,inverse.M23,inverse.M33,inverse.M43),Near(inverse.M13,inverse.M23,inverse.M33,inverse.M43));
        float factor=D(1f,MathF.Sqrt(A(A(M(dy,dy),M(dx,dx)),M(dz,dz))));
        var direction=new Vector4(M(dx,factor),M(dy,factor),M(dz,factor),
            Far(inverse.M14,inverse.M24,inverse.M34,inverse.M44));
        var origin=new Vector4(F(0x10),F(0x14),F(0x18),F(0x1C));
        bool invalid=new[]{origin.X,origin.Y,origin.Z,origin.W,direction.X,direction.Y,direction.Z,direction.W}.Any(float.IsNaN);
        return invalid?new(new(0,0,0,1),new(0,0,0,1),false):new(origin,direction,true);
    }

    /// <summary>11510 inverts only the source3×3 and affine translation, ignores
    /// its fourth column and writes(0,0,0,1). EFB0 applies this to K+100.</summary>
    public static Matrix4x4 AffineInverse11510(Matrix4x4 m)
    {
        float a=m.M11,b=m.M12,c=m.M13,d=m.M21,e=m.M22,f=m.M23,g=m.M31,h=m.M32,i=m.M33;
        float c00=S(M(e,i),M(f,h)),c01=S(M(c,h),M(b,i)),c02=S(M(b,f),M(c,e));
        float det=A(A(M(a,c00),M(d,c01)),M(g,c02));
        if(det==0)throw new InvalidDataException("Native affine inverse retains unknown stack bytes for a singular source matrix.");
        float c10=S(M(g,f),M(d,i)),c11=S(M(a,i),M(g,c)),c12=S(M(d,c),M(a,f)),
            c20=S(M(d,h),M(g,e)),c21=S(M(g,b),M(a,h)),c22=S(M(a,e),M(d,b)),factor=D(1f,det);
        float tx=FlipSign(m.M41),ty=FlipSign(m.M42),tz=FlipSign(m.M43);
        return new(M(factor,c00),M(factor,c01),M(factor,c02),0,
            M(c10,factor),M(c11,factor),M(c12,factor),0,M(c20,factor),M(c21,factor),M(c22,factor),0,
            M(A(A(M(ty,c10),M(tx,c00)),M(tz,c20)),factor),
            M(A(A(M(ty,c11),M(tx,c01)),M(tz,c21)),factor),
            M(A(A(M(ty,c12),M(tx,c02)),M(tz,c22)),factor),1);
    }
}
