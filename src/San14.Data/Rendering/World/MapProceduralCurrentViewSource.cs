using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record ProceduralViewSettings(uint CullingType450, uint ScanningType454, string SourceIdentity)
{
    /// <summary>D223C0 properties100/101, preserved by D1E410 for the owned
    /// selected fresh CommonView without a subsequent callback or overlay.</summary>
    public static ProceduralViewSettings FreshSelected(LoadedObjects actualCommonTemplate)
    {
        var source=CommonRenderResources.ImportSettings(actualCommonTemplate);
        uint Read(uint hash) => CommonRenderResources.ReadScalar(actualCommonTemplate, hash, 5);
        return new(Read(0x5F8F6D22),Read(0x2696E06D),source.SourceIdentity+";D223C0->D1E410 no-overlay current450/454");
    }
}

public sealed record ProceduralCurrentView(Vector3 Eye, TerrainFrustumPlanes Frustum,
    Vector3 FrustumMinimum,Vector3 FrustumMaximum,ProceduralViewSettings Settings)
{
    /// <summary>BB8710 camera clone's exact planes, then1091250 plane-triple
    /// intersections and1090730 extrema. Uses the original full inverse kernel.</summary>
    public static ProceduralCurrentView FromPreparedCamera(Vector3 actualEye,Matrix4x4 actualWorldToProjection,
        bool swapDepthPlanes,ProceduralViewSettings actualSettings)
    {
        var frustum=TerrainFrustumMath.Create(actualWorldToProjection,swapDepthPlanes);
        int[][] triples=[[4,0,3],[4,1,3],[5,1,3],[5,0,3],[4,0,2],[4,1,2],[5,1,2],[5,0,2]];
        var points=new List<Vector3>();
        foreach(var triple in triples)
        {
            Vector4 a=frustum.Planes[triple[0]].Equation,b=frustum.Planes[triple[1]].Equation,c=frustum.Planes[triple[2]].Equation;
            var matrix=new Matrix4x4(a.X,b.X,c.X,0,a.Y,b.Y,c.Y,0,a.Z,b.Z,c.Z,0,0,0,0,1);
            var inverse=NativeMatrixMath.Invert(matrix,out _);
            points.Add(new(A(A(A(M(inverse.M21,-b.W),M(inverse.M11,-a.W)),M(inverse.M31,-c.W)),inverse.M41),
                A(A(A(M(inverse.M22,-b.W),M(inverse.M12,-a.W)),M(inverse.M32,-c.W)),inverse.M42),
                A(A(A(M(inverse.M23,-b.W),M(inverse.M13,-a.W)),M(inverse.M33,-c.W)),inverse.M43)));
        }
        return new(actualEye,frustum,new(points.Min(p=>p.X),points.Min(p=>p.Y),points.Min(p=>p.Z)),
            new(points.Max(p=>p.X),points.Max(p=>p.Y),points.Max(p=>p.Z)),actualSettings);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
}

public sealed record ProceduralLodSlice(int ElementIndex,int TemplateIndex,int Lod,int Start,int Count);
/// <summary>Finite source current-view admission and Hi/Mid slices. Original
/// slot existence is supplied by the real PartsSetTypeContainer publisher;
/// it must not be inferred from primitive flags or from all disk draw records.</summary>
public static class MapProceduralCurrentViewSource
{
    public static IReadOnlyList<ProceduralLodSlice> SelectBlock(ProceduralPlacementDeclaration declaration,
        ProceduralBlock block,IReadOnlyList<ProceduralTemplateBounds> bounds,
        ProceduralCurrentView view,IReadOnlyList<bool> actualMidPartsExists,
        bool declaredBlockHorizontalPrecheck70C0)
    {
        if(declaredBlockHorizontalPrecheck70C0)
            throw new NotSupportedException("The optional70C0 infinite-height preliminary box needs its own source publication.");
        if(view.Settings.CullingType450!=1||view.Settings.ScanningType454!=0)
            throw new NotSupportedException("Finite BB8710 path requires actual culling1/scanning0 current source.");
        if(actualMidPartsExists.Count!=declaration.Templates.Count||bounds.Count!=declaration.Templates.Count)
            throw new InvalidDataException("Current procedural view needs original template bounds and PartsSet slots.");
        float width=declaration.FieldSizeX/declaration.BlocksX;
        int ClampIndex(float value,int maximum)=>Math.Clamp((int)value,0,maximum);
        int absoluteX0=ClampIndex(D(S(S(view.Eye.X,declaration.AbsolutelyRenderDistanceH),declaration.FieldOffsetX),width),(int)declaration.BlocksX);
        int absoluteX1=ClampIndex(D(S(A(view.Eye.X,declaration.AbsolutelyRenderDistanceH),declaration.FieldOffsetX),width),(int)declaration.BlocksX);
        int absoluteZ0=ClampIndex(D(S(S(view.Eye.Z,declaration.AbsolutelyRenderDistanceH),declaration.FieldOffsetZ),width),(int)declaration.BlocksZ);
        int absoluteZ1=ClampIndex(D(S(A(view.Eye.Z,declaration.AbsolutelyRenderDistanceH),declaration.FieldOffsetZ),width),(int)declaration.BlocksZ);
        bool absolute=block.X>=absoluteX0&&block.X<absoluteX1&&block.Z>=absoluteZ0&&block.Z<absoluteZ1;
        int x0=Math.Max(ClampIndex(D(S(S(view.Eye.X,declaration.ViewDistance),declaration.FieldOffsetX),width),(int)declaration.BlocksX),
            (int)D(S(view.FrustumMinimum.X,declaration.FieldOffsetX),width));
        int z0=Math.Max(ClampIndex(D(S(S(view.Eye.Z,declaration.ViewDistance),declaration.FieldOffsetZ),width),(int)declaration.BlocksZ),
            (int)D(S(view.FrustumMinimum.Z,declaration.FieldOffsetZ),width));
        int x1=Math.Min(ClampIndex(D(S(A(view.Eye.X,declaration.ViewDistance),declaration.FieldOffsetX),width),(int)declaration.BlocksX),
            (int)MathF.Ceiling(D(S(view.FrustumMaximum.X,declaration.FieldOffsetX),width)));
        int z1=Math.Min(ClampIndex(D(S(A(view.Eye.Z,declaration.ViewDistance),declaration.FieldOffsetZ),width),(int)declaration.BlocksZ),
            (int)MathF.Ceiling(D(S(view.FrustumMaximum.Z,declaration.FieldOffsetZ),width)));
        if(!absolute&&(block.X<x0||block.X>=x1||block.Z<z0||block.Z>=z1))return [];
        float minY=float.MaxValue,maxY=BitConverter.UInt32BitsToSingle(0x00800000);
        foreach(var bank in block.Banks)
        for(int instanceIndex=0;instanceIndex<bank.Instances.Count;instanceIndex++)
        {
            var instance=bank.Instances[instanceIndex];
            var b=bounds[bank.TemplateIndex]; float range=S(b.Maximum.Y,b.Minimum.Y),fraction;
            if(b.Maximum.Y>=0&&b.Minimum.Y>=0)fraction=1;
            else if(b.Maximum.Y<0&&b.Minimum.Y<0)fraction=0;
            else fraction=D(b.Maximum.Y,range);
            var affine=instance.Affine;
            float scale=MathF.Sqrt(A(A(M(affine.Row0.Y,affine.Row0.Y),M(affine.Row1.Y,affine.Row1.Y)),M(affine.Row2.Y,affine.Row2.Y)));
            bool inFourRecordLoop=instanceIndex<(bank.Instances.Count/4)*4;
            float high=A(inFourRecordLoop?M(M(fraction,range),scale):M(M(scale,range),fraction),affine.Row1.W);
            float low=S(affine.Row1.W,inFourRecordLoop?M(M(S(1f,fraction),range),scale):M(M(scale,range),S(1f,fraction)));
            if(maxY<high)maxY=high;if(low<minY)minY=low;
        }
        if(!(MathF.Abs(maxY)>1.1920928955078125e-5f)||minY==float.MaxValue)return [];
        Vector3 minimum=new(A(M(block.X,width),declaration.FieldOffsetX),minY,A(M(block.Z,width),declaration.FieldOffsetZ));
        Vector3 maximum=new(A(minimum.X,width),maxY,A(minimum.Z,width));
        bool absoluteHit=absolute&&maximum.X>=S(view.Eye.X,declaration.AbsolutelyRenderDistanceH)&&minimum.X<=A(view.Eye.X,declaration.AbsolutelyRenderDistanceH)&&
            maximum.Z>=S(view.Eye.Z,declaration.AbsolutelyRenderDistanceH)&&minimum.Z<=A(view.Eye.Z,declaration.AbsolutelyRenderDistanceH)&&
            maximum.Y>=S(view.Eye.Y,declaration.AbsolutelyRenderDistanceV)&&minimum.Y<=A(view.Eye.Y,declaration.AbsolutelyRenderDistanceV);
        if(!absoluteHit&&!TerrainFrustumMath.IntersectsAabb(view.Frustum,minimum,maximum))return [];
        float dx=S(A(M(A(block.X,.5f),width),declaration.FieldOffsetX),view.Eye.X);
        float dz=S(A(M(A(block.Z,.5f),width),declaration.FieldOffsetZ),view.Eye.Z);
        float distance=MathF.Sqrt(A(A(M(dx,dx),0f),M(dz,dz)));
        var result=new List<ProceduralLodSlice>();
        foreach(var bank in block.Banks)
        {
            int n=bank.Instances.Count;if(n==0)continue;
            var element=declaration.Elements[bank.ElementIndex];
            bool mid=actualMidPartsExists[bank.TemplateIndex]&&(element.Flags&8)!=0;
            int hi=n,midCount=0;
            if(mid&&distance>=element.LodDistances[1]){hi=0;midCount=n;}
            else if(mid&&distance>=element.LodDistances[0])
            {
                float fraction=D(S(distance,element.LodDistances[0]),S(element.LodDistances[1],element.LodDistances[0]));
                midCount=(int)MathF.Floor(M(n,fraction));hi=(int)MathF.Ceiling(M(S(1f,fraction),n));
                int excess=midCount+hi-n;midCount-=excess; // BA08F0 removes count excess from highest LOD first
            }
            if(hi!=0)result.Add(new(bank.ElementIndex,bank.TemplateIndex,0,0,hi));
            if(midCount!=0)result.Add(new(bank.ElementIndex,bank.TemplateIndex,1,hi,midCount));
        }
        return result;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}
