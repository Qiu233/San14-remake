using System.Numerics;

namespace San14.Data;

public static partial class MapCameraController
{
    /// <summary>E790 uses current source V80/V88 getter values. It rotates +Z
    /// with the raw quaternion and normalizes XYZ; both returned W values are1.</summary>
    public static MainMapCameraEyeForward EyeForward(MapCameraCurrentPose currentPose)
    {
        var f=MapCameraConstructionMath.ForwardC11350(currentPose.Quaternion88);
        return new(new(currentPose.Position80,1),new(f.X,f.Y,f.Z,1));
    }

    /// <summary>Build a picking ray from the displayed view. Pixel coordinates
    /// use the canonical 1920×1080 viewport; client scaling happens before this call.</summary>
    public static MainMapCameraCursorRay CursorRay(MapCameraGeometry camera,
        int canonicalPixelX,int canonicalPixelY)
    {
        Matrix4x4 inverse=InvertAffine(camera.Matrices.WorldToProjection100);
        float x=D(unchecked(canonicalPixelX-960),960f),y=D(unchecked(540-canonicalPixelY),540f),
            ratio=D(camera.Lens.Far,camera.Lens.Near),farX=M(ratio,x),farY=M(ratio,y);
        float Near(float r0,float r1,float r2,float r3)=>A(A(A(M(r1,y),M(r0,x)),M(r2,0)),r3);
        float Far(float r0,float r1,float r2,float r3)=>A(S(A(M(r1,farY),M(r0,farX)),r2),r3);
        float dx=S(Far(inverse.M11,inverse.M21,inverse.M31,inverse.M41),Near(inverse.M11,inverse.M21,inverse.M31,inverse.M41)),
            dy=S(Far(inverse.M12,inverse.M22,inverse.M32,inverse.M42),Near(inverse.M12,inverse.M22,inverse.M32,inverse.M42)),
            dz=S(Far(inverse.M13,inverse.M23,inverse.M33,inverse.M43),Near(inverse.M13,inverse.M23,inverse.M33,inverse.M43));
        float factor=D(1f,MathF.Sqrt(A(A(M(dy,dy),M(dx,dx)),M(dz,dz))));
        var direction=new Vector4(M(dx,factor),M(dy,factor),M(dz,factor),
            Far(inverse.M14,inverse.M24,inverse.M34,inverse.M44));
        var origin=camera.Eye;
        bool invalid=new[]{origin.X,origin.Y,origin.Z,origin.W,direction.X,direction.Y,direction.Z,direction.W}.Any(float.IsNaN);
        return invalid?new(new(0,0,0,1),new(0,0,0,1),false):new(origin,direction,true);
    }

    /// <summary>Invert the 3×3 basis and translation. Picking intentionally uses
    /// this affine form of the combined projection, with a final (0,0,0,1) column.</summary>
    public static Matrix4x4 InvertAffine(Matrix4x4 m)
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
