using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Source pass7's fixed origin camera: six native direction/up pairs,
/// negative aspect, near1/far100 and the original full SSE matrix inverse.
/// This is not a general camera, source variant or interpolation selector.</summary>
public static class SkyCubeBlendFaceMath
{
    public static byte[] EncodeNativeFaceMatrix(uint face)
    {
        (Vector3 direction,Vector3 up)=face switch
        {
            0=>(Vector3.UnitX,Vector3.UnitY),1=>(-Vector3.UnitX,Vector3.UnitY),
            2=>(Vector3.UnitY,-Vector3.UnitZ),3=>(-Vector3.UnitY,Vector3.UnitZ),
            4=>(Vector3.UnitZ,Vector3.UnitY),5=>(-Vector3.UnitZ,Vector3.UnitY),
            _=>throw new ArgumentOutOfRangeException(nameof(face),"Original cube camera has exactly six faces.")
        };
        // FOV bits3FC90FDB * .5 ->3F490FDB. Both original tanf branches yield
        // exactly1 in nearest-even mode (bounded native fixed-input replay).
        const float tangent=1f,near=1f,far=100f;
        Vector3 z=-direction,x=Cross(up,z),y=Cross(z,x);
        float horizontal=-1f/tangent,vertical=1f/tangent;
        float depth=far/NativeFloat.Subtract(near,far);
        // Fixed eye0 and unit-axis basis make the View*Projection product
        // sparse. Retain each original f32 coefficient and use 3F6EB0 inverse.
        var viewProjection=new Matrix4x4(
            NativeFloat.Multiply(x.X,horizontal),NativeFloat.Multiply(y.X,vertical),NativeFloat.Multiply(z.X,depth),-z.X,
            NativeFloat.Multiply(x.Y,horizontal),NativeFloat.Multiply(y.Y,vertical),NativeFloat.Multiply(z.Y,depth),-z.Y,
            NativeFloat.Multiply(x.Z,horizontal),NativeFloat.Multiply(y.Z,vertical),NativeFloat.Multiply(z.Z,depth),-z.Z,
            0,0,NativeFloat.Multiply(near,depth),0);
        Matrix4x4 matrix=NativeMatrixMath.Invert(viewProjection,out _);
        float[] values=[matrix.M11,matrix.M12,matrix.M13,matrix.M14,
            matrix.M21,matrix.M22,matrix.M23,matrix.M24,
            matrix.M31,matrix.M32,matrix.M33,matrix.M34,
            matrix.M41,matrix.M42,matrix.M43,matrix.M44];
        byte[] bytes=new byte[64];
        for(int i=0;i<values.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*4,4),values[i]);
        return bytes;
    }

    private static Vector3 Cross(Vector3 a,Vector3 b)=>new(
        NativeFloat.Subtract(NativeFloat.Multiply(a.Y,b.Z),NativeFloat.Multiply(a.Z,b.Y)),
        NativeFloat.Subtract(NativeFloat.Multiply(a.Z,b.X),NativeFloat.Multiply(a.X,b.Z)),
        NativeFloat.Subtract(NativeFloat.Multiply(a.X,b.Y),NativeFloat.Multiply(a.Y,b.X)));
}
