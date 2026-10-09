using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Current Camera inputs of EBCD70. This is not a complete CB and
/// does not identify numeric parameter8 with a physical RDEF variable.</summary>
public sealed class DeferredEarlyIndirectCurrentCamera
{
    private readonly byte[] projectionToWorld;
    internal DeferredEarlyIndirectCurrentCamera(MapSourceCreatedF103CurrentFrame frame,
        byte[] projectionToWorld, uint camera3BC)
        => (Frame, this.projectionToWorld, Camera3BCWord) = (frame, projectionToWorld, camera3BC);
    public MapSourceCreatedF103CurrentFrame Frame { get; }
    public long FrameNumber => Frame.FrameNumber;
    public ReadOnlyMemory<byte> ProjectionToWorldMatrix => projectionToWorld;
    public uint Camera3BCWord { get; }
    public float Camera3BC => BitConverter.UInt32BitsToSingle(Camera3BCWord);
    public string SourceEvidence => Frame.Lighting.SourceProvenance +
        ";EBCD70:actualpreparedCamera;GeneralScene-mP2W/3F6EB0;EBD5D1-Camera3BC->A52D40-ID8;" +
        "numericID8-destination-requires-actual2939-directory;no-GPU-or-complete-CB";

    public void RequireCurrent(MapSourceCreatedF103CurrentFrame current)
    {
        if (!ReferenceEquals(current, Frame) || current.Lighting.FrameNumber != FrameNumber ||
            current.Lighting.Binding != current.ViewFields.Binding || current.Context.FrameNumber != FrameNumber)
            throw new InvalidDataException("Indirect68 Camera belongs to another actual World/View/preparation.");
    }
}

public static class DeferredEarlyIndirectCurrentCameraSource
{
    /// <summary>The same accepted Camera which produced the current kind1
    /// lighting. Sun/Moon transition3BC is read after that writer; it is not
    /// taken from the stock camera declaration or a separately prepared frame.</summary>
    public static DeferredEarlyIndirectCurrentCamera Capture(
        MapSourceCreatedF103CurrentFrame currentGeometry,
        MapSourceCreatedLightingPacket actualCurrentPreparedLighting)
    {
        ArgumentNullException.ThrowIfNull(currentGeometry); ArgumentNullException.ThrowIfNull(actualCurrentPreparedLighting);
        if (!ReferenceEquals(actualCurrentPreparedLighting, currentGeometry.Lighting) ||
            actualCurrentPreparedLighting.FrameNumber != currentGeometry.FrameNumber ||
            actualCurrentPreparedLighting.Binding != currentGeometry.ViewFields.Binding ||
            currentGeometry.Context.FrameNumber != currentGeometry.FrameNumber ||
            currentGeometry.Camera.FrameNumber != currentGeometry.FrameNumber)
            throw new InvalidDataException("Indirect68 needs the actual same geometry Camera/light/View frame.");
        var prefix = actualCurrentPreparedLighting.PreparedCameraPrefix.Span;
        var camera = PreparedCameraSceneSource.ReadCompletePrefix(prefix);
        if (camera != actualCurrentPreparedLighting.SceneInputs.Camera ||
            camera.SourceWorldToProjection != currentGeometry.Camera.SourceWorldToProjection)
            throw new InvalidDataException("Indirect68 Camera matrix was not produced by this geometry and lighting preparation.");
        var matrix = NativeMatrixMath.Invert(camera.SourceWorldToProjection, out float determinant);
        if (!float.IsFinite(determinant) || determinant == 0)
            throw new InvalidDataException("Indirect68 current projection is singular.");
        uint fade = BinaryPrimitives.ReadUInt32LittleEndian(prefix.Slice(0x3BC, 4));
        if (!float.IsFinite(BitConverter.UInt32BitsToSingle(fade)))
            throw new InvalidDataException("Indirect68 actual Camera3BC is nonfinite.");
        return new(currentGeometry, MatrixBytes(matrix), fade);
    }
    internal static byte[] MatrixBytes(Matrix4x4 value)
    {
        float[] words = [value.M11, value.M12, value.M13, value.M14, value.M21, value.M22, value.M23, value.M24,
            value.M31, value.M32, value.M33, value.M34, value.M41, value.M42, value.M43, value.M44];
        var result = new byte[64];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(i * 4, 4), words[i]);
        return result;
    }
}
