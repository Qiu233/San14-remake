using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

public readonly record struct ReflectionCaptureFaceDepthRange(float Minimum,float Maximum);
public sealed record ReflectionCaptureCubeCameraFields(IReadOnlyList<Matrix4x4> FaceWorldToProjection,
    Matrix4x4 CubeWorldTransform,Vector4 CubeOffsetScale,float CubeDepthOffset);

/// <summary>105B390 consumes the actual current culling near/far ranges and
/// visibility mask. It is not a fixed six-camera pose or a Field-range estimate.
/// Inactive faces retain their actual preceding matrices, as the source does.</summary>
public static class ReflectionCaptureCubeCameraSource
{
    private static readonly Vector4[] directions=[new(1,0,0,0),new(-1,0,0,0),new(0,1,0,0),new(0,-1,0,0),new(0,0,-1,0),new(0,0,1,0)];
    private static readonly Vector4[] ups=[new(0,1,0,0),new(0,1,0,0),new(0,0,1,0),new(0,0,-1,0),new(0,1,0,0),new(0,1,0,0)];
    public static ReflectionCaptureCubeCameraFields Prepare(Vector4 actualCubeCenter,uint actualVisibleMask,
        float actualNearClamp,float actualFarClamp,IReadOnlyList<ReflectionCaptureFaceDepthRange> actualFaceDepths,
        IReadOnlyList<Matrix4x4> actualRetainedFaceMatrices)
    {
        ArgumentNullException.ThrowIfNull(actualFaceDepths);ArgumentNullException.ThrowIfNull(actualRetainedFaceMatrices);
        if(actualFaceDepths.Count!=6||actualRetainedFaceMatrices.Count!=6)throw new InvalidDataException("reflection.capture_camera.actual_six_face_bounds");
        var matrices=actualRetainedFaceMatrices.ToArray();float smallest=float.MaxValue;
        float largest=BitConverter.UInt32BitsToSingle(0x00800000);
        for(int face=0;face<6;face++)
        {
            if((actualVisibleMask&(1u<<face))==0)continue;
            float near=actualNearClamp<=actualFaceDepths[face].Minimum?actualFaceDepths[face].Minimum:actualNearClamp;
            if(near<smallest)smallest=near;
            float actualMaximum=actualFaceDepths[face].Maximum;if(largest<actualMaximum)largest=actualMaximum;
            float far=actualMaximum<=actualFarClamp?actualMaximum:actualFarClamp;
            // Source symmetric90-degree projection includes its X sign.
            float twice=M(near,2);
            var projection=new Matrix4x4(D(M(near,-2),twice),0,0,0,
                0,D(A(near,near),twice),0,0,0,0,D(far,S(far,near)),1,
                0,0,D(M(near,far),S(near,far)),0);
            var view=DeferredEarlyShadowLightViewSource.LightViewA07370(actualCubeCenter,directions[face],ups[face]);
            matrices[face]=UnitWorldTransformMath.ComposeAffine17ADD0(view,projection);
        }
        if(actualFarClamp<largest)largest=actualFarClamp;
        float scale=D(1,S(M(MathF.Sqrt(3),largest),smallest));float neg=-scale;
        var cube=new Matrix4x4(scale,0,0,0,0,scale,0,0,0,0,scale,0,
            M(neg,actualCubeCenter.X),M(neg,actualCubeCenter.Y),M(neg,actualCubeCenter.Z),-M(scale,smallest));
        return new(Array.AsReadOnly(matrices),cube,new(-actualCubeCenter.X,-actualCubeCenter.Y,-actualCubeCenter.Z,scale),-M(scale,smallest));
    }
    /// <summary>Fresh inactive CView matrices have no initialized value. This
    /// overload publishes only matrices written by the actual visible faces;
    /// its inactive scratch slots are never exposed as retained source state.</summary>
    public static ReflectionCaptureVisibleCameraFields PrepareVisible(Vector4 center,uint visibleMask,
        float nearClamp,float farClamp,IReadOnlyList<ReflectionCaptureFaceDepthRange> depths)
    {
        var scratch=Prepare(center,visibleMask,nearClamp,farClamp,depths,new Matrix4x4[6]);
        var visible=new Dictionary<int,Matrix4x4>();
        for(int face=0;face<6;face++)if((visibleMask&(1u<<face))!=0)visible.Add(face,scratch.FaceWorldToProjection[face]);
        return new(new System.Collections.ObjectModel.ReadOnlyDictionary<int,Matrix4x4>(visible),
            scratch.CubeWorldTransform,scratch.CubeOffsetScale,scratch.CubeDepthOffset);
    }
    private static float M(float a,float b)=>NativeFloat.Multiply(a,b);
    private static float A(float a,float b)=>NativeFloat.Add(a,b);
    private static float S(float a,float b)=>NativeFloat.Subtract(a,b);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}

public sealed record ReflectionCaptureVisibleCameraFields(IReadOnlyDictionary<int,Matrix4x4> VisibleFaceWorldToProjection,
    Matrix4x4 CubeWorldTransform,Vector4 CubeOffsetScale,float CubeDepthOffset);
