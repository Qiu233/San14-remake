using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>WORK's horizontal cube-face horizon clipping. Inputs are the
/// same prepared face and selected scene planet radius used for that draw.</summary>
public static class OriginalSkyWorkCloudClipPreparation
{
    public static Vector4 Prepare(ReadOnlySpan<byte> main,OriginalSkyScenePreparedCamera camera,
        int face,float planetRadius,OriginalMapPostEffect3PresentLightMathSource inverseTrig)
    {
        if(face is not (0 or 1 or 4 or 5))return Vector4.Zero;
        Vector4 originalEye=V(main,0x10),originalTarget=V(main,0x20),eye=camera.Position;
        float dx=S(originalTarget.X,originalEye.X),dy=S(originalTarget.Y,originalEye.Y),dz=S(originalTarget.Z,originalEye.Z);
        float length=MathF.Sqrt(A(A(M(dy,dy),M(dx,dx)),M(dz,dz))),raised=M(F(main,0x210),100000);
        var axis=OriginalSkySceneSourceDefaults.CubeFaces[face];
        Vector4 target=new(A(M(axis.Direction.X,length),originalEye.X),A(A(M(axis.Direction.Y,length),originalEye.Y),raised),
            A(M(axis.Direction.Z,length),originalEye.Z),1);
        var cloneView=OriginalMapCameraConstructionMath.ViewAB6BC0(eye,target,new(0,1,0,0),0).WorldToView80;
        var cloneProjection=TerrainFrustumMath.Multiply(cloneView,camera.MatrixB0);
        dx=S(target.X,eye.X);dy=S(target.Y,eye.Y);dz=S(target.Z,eye.Z);
        float reciprocal=D(1,MathF.Sqrt(A(A(M(dx,dx),M(dy,dy)),M(dz,dz))));
        dx=M(dx,reciprocal);dy=M(dy,reciprocal);dz=M(dz,reciprocal);
        Vector4 rotationAxis=new(S(M(dz,-1),M(dy,0)),S(M(dx,0),M(dz,0)),S(M(dy,0),M(dx,-1)),0);
        reciprocal=D(1,MathF.Sqrt(A(A(M(rotationAxis.Y,rotationAxis.Y),M(rotationAxis.X,rotationAxis.X)),M(rotationAxis.Z,rotationAxis.Z))));
        rotationAxis=new(M(rotationAxis.X,reciprocal),M(rotationAxis.Y,reciprocal),M(rotationAxis.Z,reciprocal),0);
        float altitude=eye.Y<=0?0:eye.Y;
        float angle=OriginalMapPostEffect3PresentLightMath.Asin(D(planetRadius,A(altitude,planetRadius)),inverseTrig);
        var horizon=Rotate(new(0,-1,0,0),rotationAxis,angle);
        reciprocal=D(1,MathF.Sqrt(A(A(M(horizon.X,horizon.X),M(horizon.Y,horizon.Y)),M(horizon.Z,horizon.Z))));
        float dot=A(A(M(M(horizon.X,reciprocal),dx),M(M(dy,horizon.Y),reciprocal)),M(M(horizon.Z,reciprocal),dz));
        float clamped=dot<1?(dot>=-1?dot:-1):1;
        float horizonY=D(Tangent(OriginalMapPostEffect3PresentLightMath.Acos(clamped,inverseTrig)),Tangent(M(camera.Scalar138,.5f)));
        if(M(horizon.Y,reciprocal)<dy)horizonY=Neg(horizonY);
        var transform=TerrainFrustumMath.Multiply(OriginalNativeMatrixMath.Invert(cloneProjection,out _),camera.MatrixF0);
        float far=(camera.Flags148&2)!=0?0:1;
        Vector4 Point(bool minus)
        {
            float w=minus?A(A(S(M(transform.M24,horizonY),M(transform.M14,1)),M(transform.M34,far)),transform.M44):
                A(A(A(transform.M14,M(transform.M24,horizonY)),M(transform.M34,far)),transform.M44);
            float inverse=D(1,w);
            float Term(float x,float y,float z,float t)=>M(A(A(minus?S(M(y,horizonY),M(x,1)):A(x,M(y,horizonY)),M(z,far)),t),inverse);
            return new(Term(transform.M11,transform.M21,transform.M31,transform.M41),Term(transform.M12,transform.M22,transform.M32,transform.M42),
                Term(transform.M13,transform.M23,transform.M33,transform.M43),1);
        }
        var negative=Point(true);var positive=Point(false);
        float lineX=S(positive.X,negative.X),lineY=S(positive.Y,negative.Y);
        reciprocal=D(1,MathF.Sqrt(A(A(M(lineY,lineY),M(lineX,lineX)),0)));
        Vector4 plane=new(Neg(M(lineY,reciprocal)),M(lineX,reciprocal),0,0);
        plane.W=Neg(A(A(M(negative.Y,plane.Y),M(plane.X,negative.X)),M(negative.Z,0)));
        return plane;
    }
    private static Vector4 Rotate(Vector4 v,Vector4 axis,float angle)
    {
        var sc=OriginalNativeTrigonometry.SinCos014590(angle);float s=sc.Sin,c=sc.Cos;
        float k=M(A(A(M(axis.X,v.X),M(axis.Y,v.Y)),M(axis.Z,v.Z)),S(1,c));
        return new(A(A(M(axis.X,k),M(v.X,c)),M(S(M(v.Z,axis.Y),M(axis.Z,v.Y)),s)),
            A(A(M(k,axis.Y),M(c,v.Y)),M(S(M(axis.Z,v.X),M(v.Z,axis.X)),s)),
            A(A(M(k,axis.Z),M(c,v.Z)),M(S(M(axis.X,v.Y),M(v.X,axis.Y)),s)),v.W);
    }
    private static float Tangent(float angle)
    {
        uint bits=BitConverter.SingleToUInt32Bits(angle);float a=BitConverter.UInt32BitsToSingle(bits&0x7fffffff);
        if(a==0||(bits&0x7fffffff)<0x32000000)return angle;
        double x=a,result;int count=0;
        if(x<C(0x3f20000000000000))result=DA(DM(DM(DM(x,x),x),C(0x3fd5555555555555)),x);
        else
        {
            if(x>C(0x3fe921fb54442d18)){count=(int)DA(DM(x,C(0x3fe45f306dc9c883)),.5);
                x=DS(DS(x,DM(count,C(0x3ff921fb54400000))),DM(count,C(0x3dd0b4611a626331)));}
            double square=DM(x,x),numerator=DA(DM(C(0xbf919dba6efd6aad),square),C(0x3fd8a8b0da56cb17));
            double denominator=DA(DM(DA(DM(C(0x3f92e29003c692d9),square),C(0xbfe07266d7b3511b)),square),C(0x3ff27e84a3e73a2e));
            result=DA(x,DM(DM(square,x),DD(numerator,denominator)));if(count%2!=0)result=DD(-1,result);
        }
        return(bits>>31)!=0?Neg((float)result):(float)result;
    }
    private static float F(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadSingleLittleEndian(bytes[at..]);
    private static Vector4 V(ReadOnlySpan<byte> bytes,int at)=>new(F(bytes,at),F(bytes,at+4),F(bytes,at+8),F(bytes,at+12));
    private static float Neg(float x)=>BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(x)^0x80000000);
    private static double C(ulong bits)=>BitConverter.UInt64BitsToDouble(bits);
    private static float A(float a,float b)=>OriginalNativeFloat.Add(a,b);
    private static float S(float a,float b)=>OriginalNativeFloat.Subtract(a,b);
    private static float M(float a,float b)=>OriginalNativeFloat.Multiply(a,b);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double DA(double a,double b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double DS(double a,double b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double DM(double a,double b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double DD(double a,double b)=>a/b;
}
