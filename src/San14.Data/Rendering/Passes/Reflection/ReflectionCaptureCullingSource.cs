using System.Numerics;

namespace San14.Data;

public readonly record struct ReflectionCaptureModelVisibility(uint FaceMask, IReadOnlyList<float?> Distances);

/// <summary>Actual mode2 CView bounds (105B680/105AF50) and per-model
/// CPU128 callback (104F320/105AC80/105AAE0). A new instance is one capture
/// traversal, not a retained cross-frame visibility cache.</summary>
public sealed class ReflectionCaptureCullingSource
{
    private readonly ReflectionCaptureFaceDepthRange[] depths = Enumerable.Repeat(
        new ReflectionCaptureFaceDepthRange(float.MaxValue, BitConverter.UInt32BitsToSingle(0x00800000)),6).ToArray();
    private ReflectionCaptureCullingSource(Vector4 center, Vector4 minimum, Vector4 maximum, uint mask,float far)
        => (Center,Minimum,Maximum,CandidateMask,FarClamp)=(center,minimum,maximum,mask,far);
    public Vector4 Center {get;}
    public Vector4 Minimum {get;}
    public Vector4 Maximum {get;}
    public uint CandidateMask {get;}
    // D69760 writes this clamp. Final face Near still depends on real geometry.
    public float NearClamp => 1f;
    public float FarClamp {get;}
    public uint VisibleMask {get;private set;}
    public IReadOnlyList<ReflectionCaptureFaceDepthRange> FaceDepths => Array.AsReadOnly(depths);

    public static ReflectionCaptureCullingSource Prepare(ReflectionCaptureFieldCellGeometry cell,
        MapCameraConstructionPacket actualCurrentCamera,uint actualClipProfile,NativeCameraTangentDispatch dispatch)
    {
        var camera=ReflectionCaptureFieldGeometry.PrepareCamera(cell,actualCurrentCamera.Geometry.Up,actualCurrentCamera.Lens);
        var matrices=camera.BuildMatrices(actualClipProfile,dispatch);
        float tangent=NativeCameraTangent.Tangent1020A90(M(camera.Lens.VerticalFovRadians,.5f),dispatch);
        float height=M(tangent,camera.ViewDistance9EC),width=M(camera.Lens.Aspect,height);
        var initial=DeferredEarlyShadowWorkVolumeSource.BoundedMatrixD19B10(
            new(S(-width,1),S(-height,1),-camera.ViewDistance9EC,1),
            new(A(width,1),A(height,1),-camera.Lens.Near,1),
            UnitWorldParentMath.InverseRigid3F6D20(matrices.WorldToView80));
        var cellBox=DeferredEarlyShadowWorkVolumeSource.BoundedMatrixD19B10(cell.BoundsMinimum,cell.BoundsMaximum,Matrix4x4.Identity);
        var forward=DeferredEarlyShadowConvexVolumeMath.NormalizeForwardA06350(camera.Eye,camera.Target);
        var frustum=DeferredEarlyShadowConvexVolumeMath.FrustumA040E0(
            DeferredEarlyShadowConvexVolumeMath.OwnFrustumBasisA07030(camera.Eye,forward,camera.Up),
            camera.Lens.Near,camera.ViewDistance9EC,M(tangent,camera.Lens.Aspect),tangent);
        float limit=BitConverter.UInt32BitsToSingle(0x7722B3DB);
        Vector4 lo=new(limit,limit,limit,1),hi=new(-limit,-limit,-limit,1);
        foreach(var work in new[]{initial})
            // A02D40 visits work-box points/edges before frustum points/edges.
            // A04C00's shared kernel visits its SECOND argument first, so the
            // operands deliberately reverse here, preserving the48-point cap.
            foreach(var point in DeferredEarlyShadowConvexVolumeMath.IntersectA04C00(
                frustum,DeferredEarlyShadowConvexVolumeMath.WorkBoxA03BB0(work)))
            {
                lo=new(Min(lo.X,point.X),Min(lo.Y,point.Y),Min(lo.Z,point.Z),1);
                hi=new(Max(hi.X,point.X),Max(hi.Y,point.Y),Max(hi.Z,point.Z),1);
            }
        float radius=MathF.Sqrt(cell.ViewRangeSquared);
        lo=new(Max(-radius,S(lo.X,cell.Position.X)),Max(-radius,S(lo.Y,cell.Position.Y)),Max(-radius,S(lo.Z,cell.Position.Z)),1);
        hi=new(Min(radius,S(hi.X,cell.Position.X)),Min(radius,S(hi.Y,cell.Position.Y)),Min(radius,S(hi.Z,cell.Position.Z)),1);
        uint mask=0;float far=0;
        void Side(float extent,uint bit){if(extent>0){mask|=bit;far=Max(far,extent);}}
        Side(-lo.X,2);Side(-lo.Y,8);Side(-lo.Z,16);Side(hi.X,1);Side(hi.Y,4);Side(hi.Z,32);
        var result=new ReflectionCaptureCullingSource(cell.Position,lo,hi,mask,Min(radius,far));
        // D69760 calls D08F80 on the cell's own half-range AABB after CF57A0.
        // This runs the real accumulating callback; it is not a second work
        // volume and does not publish any Model primitive.
        result.TestAndAccumulateWorldBox(cellBox);
        return result;
    }

    public ReflectionCaptureModelVisibility TestAndAccumulate(MapModelNativeOrientedBounds actualModelBounds)
        =>TestAndAccumulateWorldBox(DeferredEarlyShadowCasterSource.PrepareWorldBox(actualModelBounds));

    private ReflectionCaptureModelVisibility TestAndAccumulateWorldBox(Matrix4x4 box)
    {
        float x=A(A(MathF.Abs(box.M21),MathF.Abs(box.M11)),MathF.Abs(box.M31));
        float y=A(A(MathF.Abs(box.M22),MathF.Abs(box.M12)),MathF.Abs(box.M32));
        float z=A(A(MathF.Abs(box.M23),MathF.Abs(box.M13)),MathF.Abs(box.M33));
        Vector3 lo=new(S(S(box.M41,x),Center.X),S(S(box.M42,y),Center.Y),S(S(box.M43,z),Center.Z));
        Vector3 hi=new(S(A(x,box.M41),Center.X),S(A(y,box.M42),Center.Y),S(A(z,box.M43),Center.Z));
        var distances=new float?[6];uint accepted=0;
        if(((CandidateMask&2)!=0&&hi.X<=Minimum.X)||((CandidateMask&8)!=0&&hi.Y<=Minimum.Y)||
           ((CandidateMask&16)!=0&&hi.Z<=Minimum.Z)||((CandidateMask&1)!=0&&Maximum.X<=lo.X)||
           ((CandidateMask&4)!=0&&Maximum.Y<=lo.Y)||((CandidateMask&32)!=0&&Maximum.Z<=lo.Z))
            return new(0,Array.AsReadOnly(distances));
        Face(0,lo.Z,hi.Z,lo.Y,hi.Y,lo.X,hi.X);
        Face(1,lo.Z,hi.Z,lo.Y,hi.Y,-hi.X,-lo.X);
        Face(2,lo.X,hi.X,lo.Z,hi.Z,lo.Y,hi.Y);
        Face(3,lo.X,hi.X,lo.Z,hi.Z,-hi.Y,-lo.Y);
        Face(5,lo.X,hi.X,lo.Y,hi.Y,lo.Z,hi.Z);
        Face(4,lo.X,hi.X,lo.Y,hi.Y,-hi.Z,-lo.Z);
        VisibleMask|=accepted;return new(accepted,Array.AsReadOnly(distances));
        void Face(int face,float a,float b,float c,float d,float near,float far)
        {
            uint bit=1u<<face;
            if((CandidateMask&bit)==0||!(far>0&&a<far&&-b<far&&c<far&&-d<far))return;
            if(M(a,b)>0)near=Max(near,Min(MathF.Abs(a),MathF.Abs(b)));
            if(M(c,d)>0)near=Max(near,Min(MathF.Abs(c),MathF.Abs(d)));
            float distance=NearClamp<=near?near:NearClamp;distances[face]=distance;
            depths[face]=new(Min(depths[face].Minimum,distance),Max(depths[face].Maximum,far));accepted|=bit;
        }
    }
    private static float A(float a,float b)=>NativeFloat.Add(a,b);
    private static float S(float a,float b)=>NativeFloat.Subtract(a,b);
    private static float M(float a,float b)=>NativeFloat.Multiply(a,b);
    private static float Min(float a,float b)=>a<b?a:b;
    private static float Max(float a,float b)=>a>b?a:b;
}
