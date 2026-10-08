using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>The native eight-vertex/six-plane numeric volume. Geometry
/// masks and scene inventory owners remain with their actual publishers.</summary>
public sealed class OriginalDeferredEarlyShadowConvexVolume8
{
    internal OriginalDeferredEarlyShadowConvexVolume8(Vector4[] vertices, Vector4[] planes)
        => (Vertices, Planes) = (Array.AsReadOnly(vertices), Array.AsReadOnly(planes));
    public IReadOnlyList<Vector4> Vertices { get; }
    public IReadOnlyList<Vector4> Planes { get; }
}

/// <summary>Actual A034E0/A03BB0/A040E0/A04C00 convex numeric kernels.
/// Native edge order, classification and interpolation are retained.</summary>
public static class OriginalDeferredEarlyShadowConvexVolumeMath
{
    private static readonly (int First, int Second)[] edges =
        [(0,1),(2,3),(0,2),(1,3),(4,5),(6,7),(4,6),(5,7),(0,4),(1,5),(2,6),(3,7)];

    public static OriginalDeferredEarlyShadowConvexVolume8 WorkBoxA03BB0(Matrix4x4 work)
    {
        Vector4 a = new(work.M11,work.M12,work.M13,work.M14), b = new(work.M21,work.M22,work.M23,work.M24),
            c = new(work.M31,work.M32,work.M33,work.M34), center = new(work.M41,work.M42,work.M43,work.M44);
        var v = new Vector4[8];
        v[0] = new(S(S(S(center.X,a.X),b.X),c.X), S(S(S(center.Y,a.Y),b.Y),c.Y),
            S(S(S(center.Z,a.Z),b.Z),c.Z), center.W);
        Vector4 da = Add(a,a), db = Add(b,b), dc = Add(c,c);
        v[1] = Add(v[0],dc); v[2] = Add(v[0],db); v[3] = Add(v[2],dc);
        v[4] = Add(v[0],da); v[5] = Add(v[4],dc); v[6] = Add(v[4],db); v[7] = Add(v[6],dc);
        Vector4[] planes = [Plane(Normalize(a),v[0]), Plane(Normalize(Scale(a,-1)),v[7]),
            Plane(Normalize(b),v[0]), Plane(Normalize(Scale(b,-1)),v[7]),
            Plane(Normalize(c),v[0]), Plane(Normalize(Scale(c,-1)),v[7])];
        return new(v,planes);
    }

    /// <summary>The exact A07030 row-basis matrix consumed by A040E0.</summary>
    public static Matrix4x4 OwnFrustumBasisA07030(Vector4 eye, Vector4 normalizedForward, Vector4 originalUp)
    {
        Matrix4x4 view = OriginalDeferredEarlyShadowLightViewSource.LightViewA07370(eye,normalizedForward,originalUp);
        return new(view.M11,view.M21,view.M31,0, view.M12,view.M22,view.M32,0,
            normalizedForward.X,normalizedForward.Y,normalizedForward.Z,normalizedForward.W,eye.X,eye.Y,eye.Z,eye.W);
    }

    public static OriginalDeferredEarlyShadowConvexVolume8 FrustumA040E0(Matrix4x4 basis,
        float near, float far, float tangentHorizontal, float tangentVertical)
    {
        Vector4 right = new(basis.M11,basis.M12,basis.M13,basis.M14),
            up = new(basis.M21,basis.M22,basis.M23,basis.M24),
            forward = new(basis.M31,basis.M32,basis.M33,basis.M34),
            eye = new(basis.M41,basis.M42,basis.M43,basis.M44);
        var v = new Vector4[8];
        for (int side = 0; side < 2; side++)
        {
            float distance = side == 0 ? near : far;
            Vector4 center = new(A(M(distance,forward.X),eye.X), A(M(distance,forward.Y),eye.Y),
                A(M(distance,forward.Z),eye.Z),eye.W);
            Vector4 h = Scale(right,M(distance,tangentHorizontal)), w = Scale(up,M(distance,tangentVertical));
            Vector4 below = Subtract(center,w), above = Add(w,center);
            v[side] = Subtract(below,h) with { W=eye.W };
            v[side+2] = Subtract(above,h) with { W=eye.W };
            v[side+4] = Add(h,below) with { W=eye.W };
            v[side+6] = Add(h,above) with { W=eye.W };
        }
        Vector4[] planes = [Plane(Normalize(Add(Scale(forward,tangentHorizontal),right)),eye),
            Plane(Normalize(Add(Negate(right),Scale(forward,tangentHorizontal))),eye),
            Plane(Normalize(Add(Scale(forward,tangentVertical),up)),eye),
            Plane(Normalize(Add(Negate(up),Scale(forward,tangentVertical))),eye),
            Plane(forward,v[0]), Plane(Negate(forward),v[7])];
        return new(v,planes);
    }

    /// <summary>A04C00 emits contained vertices first, interleaving the two
    /// volumes. Full containment skips edge intersections. Otherwise all
    /// twelve source edges against six source planes are visited; source
    /// endpoint t=0 is excluded, t=1 is accepted, no deduplication is added.</summary>
    public static IReadOnlyList<Vector4> IntersectA04C00(OriginalDeferredEarlyShadowConvexVolume8 box,
        OriginalDeferredEarlyShadowConvexVolume8 frustum)
    {
        float[,] frustumDistance = new float[8,6], boxDistance = new float[8,6];
        uint[] frustumMasks = new uint[8], boxMasks = new uint[8];
        var result = new List<Vector4>(48);
        int frustumContained=0,boxContained=0;
        for(int vertex=0;vertex<8;vertex++)
        {
            for(int plane=0;plane<6;plane++)
            {
                float first=DotPlane(frustum.Vertices[vertex],box.Planes[plane]);
                float second=DotPlane(box.Vertices[vertex],frustum.Planes[plane]);
                frustumDistance[vertex,plane]=first;boxDistance[vertex,plane]=second;
                if(first>=0)frustumMasks[vertex]|=1u<<plane;
                if(second>=0)boxMasks[vertex]|=1u<<plane;
            }
            if(frustumMasks[vertex]==0x3F){result.Add(frustum.Vertices[vertex]);frustumContained++;}
            if(boxMasks[vertex]==0x3F){result.Add(box.Vertices[vertex]);boxContained++;}
        }
        if(frustumContained==8||boxContained==8)return result.AsReadOnly();
        foreach(var (first,second) in edges)
            for(int plane=0;plane<6;plane++)
            {
                if(Append(frustum,box,frustumDistance,frustumMasks,first,second,plane,result))return result.AsReadOnly();
                if(Append(box,frustum,boxDistance,boxMasks,first,second,plane,result))return result.AsReadOnly();
            }
        return result.AsReadOnly();
    }

    private static bool Append(OriginalDeferredEarlyShadowConvexVolume8 vertices,OriginalDeferredEarlyShadowConvexVolume8 planes,
        float[,] distance,uint[] masks,int first,int second,int plane,List<Vector4> result)
    {
        uint flag=1u<<plane;
        if((masks[first]&flag)==(masks[second]&flag))return false;
        float t=D(distance[first,plane],S(distance[first,plane],distance[second,plane]));
        if(t<=0||t>1)return false;
        float complement=S(1,t);
        Vector4 p=Add(Scale(vertices.Vertices[first],complement),Scale(vertices.Vertices[second],t));
        for(int other=0;other<6;other++)
            if(other!=plane&&DotPlane(p,planes.Planes[other])<0)return false;
        result.Add(p);return result.Count==48;
    }

    /// <summary>A075F0's ordered light-coordinate min/max stores. All source
    /// point W values are ignored by this affine consumer.</summary>
    public static (Vector4 Minimum,Vector4 Maximum) LightBoundsA075F0(IEnumerable<Vector4> points,Matrix4x4 lightView)
    {
        float limit=BitConverter.UInt32BitsToSingle(0x7722B3DB);
        float minX=limit,minY=limit,minZ=limit,maxX=Neg(limit),maxY=Neg(limit),maxZ=Neg(limit);
        foreach(var p in points)
        {
            float x=A(A(A(M(p.Y,lightView.M21),M(p.X,lightView.M11)),M(p.Z,lightView.M31)),lightView.M41);
            float y=A(A(A(M(p.X,lightView.M12),M(p.Y,lightView.M22)),M(p.Z,lightView.M32)),lightView.M42);
            float z=A(A(A(M(p.X,lightView.M13),M(p.Y,lightView.M23)),M(p.Z,lightView.M33)),lightView.M43);
            minX=Minss(minX,x);minY=Minss(minY,y);minZ=Minss(minZ,z);
            maxX=Maxss(maxX,x);maxY=Maxss(maxY,y);maxZ=Maxss(maxZ,z);
        }
        // A06350 clamps the empty/default maximum to its final minimum.
        return(new(minX,minY,minZ,1),new(Maxss(maxX,minX),Maxss(maxY,minY),Maxss(maxZ,minZ),1));
    }

    public static Vector4 NormalizeForwardA06350(Vector4 eye,Vector4 target)
    {
        Vector4 delta=Subtract(target,eye);
        Vector4 normalized=Normalize(delta);
        return normalized with {W=delta.W};
    }
    private static Vector4 Normalize(Vector4 v)
    {
        float inverse=D(1,MathF.Sqrt(A(A(M(v.Y,v.Y),M(v.X,v.X)),M(v.Z,v.Z))));
        return new(M(v.X,inverse),M(v.Y,inverse),M(v.Z,inverse),0);
    }
    private static Vector4 Plane(Vector4 normal,Vector4 point)=>new(normal.X,normal.Y,normal.Z,
        Neg(A(A(M(normal.Y,point.Y),M(normal.X,point.X)),M(normal.Z,point.Z))));
    private static float DotPlane(Vector4 p,Vector4 plane)=>A(A(A(M(p.Y,plane.Y),M(p.X,plane.X)),M(p.Z,plane.Z)),M(p.W,plane.W));
    private static Vector4 Add(Vector4 a,Vector4 b)=>new(A(a.X,b.X),A(a.Y,b.Y),A(a.Z,b.Z),A(a.W,b.W));
    private static Vector4 Subtract(Vector4 a,Vector4 b)=>new(S(a.X,b.X),S(a.Y,b.Y),S(a.Z,b.Z),S(a.W,b.W));
    private static Vector4 Scale(Vector4 a,float s)=>new(M(a.X,s),M(a.Y,s),M(a.Z,s),M(a.W,s));
    private static Vector4 Negate(Vector4 a)=>new(Neg(a.X),Neg(a.Y),Neg(a.Z),Neg(a.W));
    private static float Neg(float value)=>BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(value)^0x80000000u);
    private static float Minss(float a,float b)=>a<b?a:b;
    private static float Maxss(float a,float b)=>a>b?a:b;
    private static float M(float a,float b)=>OriginalNativeFloat.Multiply(a,b);
    private static float A(float a,float b)=>OriginalNativeFloat.Add(a,b);
    private static float S(float a,float b)=>OriginalNativeFloat.Subtract(a,b);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a,float b)=>a/b;
}
