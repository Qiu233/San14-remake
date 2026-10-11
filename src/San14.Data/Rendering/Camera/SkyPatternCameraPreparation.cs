using System.Numerics;

namespace San14.Data;

/// <summary>An immutable camera pose, lens and forced matrix update for pattern generation.</summary>
public readonly record struct SkyPatternPreparedCamera(Vector4 Eye, Vector4 Target, Vector4 Up,
    CameraLens Lens, MapCameraConstructionMatrices Matrices);

/// <summary>The generation camera and its direction-only copy, both retaining orthographic projection.</summary>
public readonly record struct SkyPatternPreparedCameras(
    SkyPatternPreparedCamera Temporary, SkyPatternPreparedCamera DirectionOnly, uint NativeClipProfile)
{
    /// <summary>TC 1193A90: inverse temporary W2P and homogeneous near-minus-far corner directions.</summary>
    public SkyCloudPatternVertexMaterial PrepareVertexMaterial(Vector4 actualClipPlane)
    {
        (float far, float near) = NativeClipProfile switch
        {
            0 => (1, -1),
            2 => (1, 0),
            4 => (0, -1),
            _ => throw new NotSupportedException("Pattern camera clip profile has no supported source endpoint branch.")
        };
        if (Temporary.Lens.ReverseDepth) (far,near) = (near,far);
        Matrix4x4 rays = DirectionOnly.Matrices.ProjectionToWorld3F6EB0();
        Vector4 Corner(float x, float y)
        {
            Vector4 farPoint = HomogeneousPoint(x,y,far,rays);
            Vector4 nearPoint = HomogeneousPoint(x,y,near,rays);
            return new(NativeFloat.Subtract(nearPoint.X,farPoint.X),
                NativeFloat.Subtract(nearPoint.Y,farPoint.Y),NativeFloat.Subtract(nearPoint.Z,farPoint.Z),
                NativeFloat.Subtract(nearPoint.W,farPoint.W));
        }
        var directions = new SkyCloudPatternDirections(Corner(-1,1),Corner(-1,-1),Corner(1,1),Corner(1,-1));
        return new(actualClipPlane,Temporary.Matrices.ProjectionToWorld3F6EB0(),directions,new(far,near));
    }

    private static Vector4 HomogeneousPoint(float x, float y, float z, Matrix4x4 matrix)
    {
        float Lane(float a, float b, float c, float d) => NativeFloat.Add(NativeFloat.Add(
            NativeFloat.Add(NativeFloat.Multiply(a,x),NativeFloat.Multiply(b,y)),NativeFloat.Multiply(c,z)),d);
        float w = Lane(matrix.M14,matrix.M24,matrix.M34,matrix.M44);
        float inverse = Divide(1,w);
        return new(NativeFloat.Multiply(Lane(matrix.M11,matrix.M21,matrix.M31,matrix.M41),inverse),
            NativeFloat.Multiply(inverse,Lane(matrix.M12,matrix.M22,matrix.M32,matrix.M42)),
            NativeFloat.Multiply(inverse,Lane(matrix.M13,matrix.M23,matrix.M33,matrix.M43)),
            NativeFloat.Multiply(inverse,w));
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static float Divide(float numerator, float denominator) => numerator / denominator;
}

public static class SkyPatternCameraPreparation
{
    /// <summary>TC D49EE0 installs the auxiliary pose and lens overrides; 1193A90 copies it for directions.
    /// Near/far, view offset, reverse depth and all unrelated flag bits come from the actual map camera.</summary>
    public static SkyPatternPreparedCameras Prepare(CameraLens inheritedLens,
        SkyPatternProjectionMatrixWrite projection, uint nativeClipProfile,
        NativeCameraTangentDispatch tangentDispatch)
    {
        ArgumentNullException.ThrowIfNull(projection);
        CameraLens lens = inheritedLens with
        {
            VerticalFovRadians = BitConverter.UInt32BitsToSingle(0x3FC90FDB),
            Aspect = 1,
            OrthographicScale = 1,
            Flags = inheritedLens.Flags | 1u
        };
        var temporary = Complete(projection.AuxiliaryEye10,projection.AuxiliaryTarget20,
            projection.AuxiliaryUp30,lens,nativeClipProfile,tangentDispatch);
        Vector4 look = MapCameraConstructionMath.NormalizedLookDirection(temporary.Eye,temporary.Target);
        var direction = Complete(new(0,0,0,1),look,temporary.Up,
            lens with { Near = 1, Far = 100 },nativeClipProfile,tangentDispatch);
        return new(temporary,direction,nativeClipProfile);
    }

    private static SkyPatternPreparedCamera Complete(Vector4 eye, Vector4 target, Vector4 up,
        CameraLens lens, uint nativeClipProfile, NativeCameraTangentDispatch tangentDispatch) =>
        new(eye,target,up,lens,MapCameraConstructionMath.CompleteAB6C60(
            eye,target,up,lens,nativeClipProfile,tangentDispatch));
}
