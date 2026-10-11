using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>The actual registry results for the prepared Camera's three
/// PatternMap resources. A declaration key, a CPU word or an absent device
/// binding does not implement this current lookup boundary.</summary>
public interface IDeferredEarlyIndirectCurrentPatternResources
{
    bool PatternMap1680Present { get; }
    bool PatternMap1888Present { get; }
    bool PatternMap1890Present { get; }
    /// <summary>Actual ready STA CPU returned by this named Data. It only
    /// selects the special branch; its numeric tables are not the Sky matrix.</summary>
    SkyAtmoTableCpuOwner? PatternMap1680StaData => null;
    string SourceEvidence { get; }
    void RequireCurrent(MapSourceCreatedF103CurrentFrame actualGeometry);
}

/// <summary>Finite numeric tail published by the same actual V468/Main/V418
/// writer as the accepted current Camera. Resource lookups remain separate.</summary>
public sealed class DeferredEarlyIndirectPatternCameraTail
{
    private readonly float[] values;
    internal DeferredEarlyIndirectPatternCameraTail(MapSourceCreatedF103CurrentFrame frame,
        SkySeasonMainCameraInputs inputs, MapCameraConstructionPacket preparedCamera,
        float[] values, uint camera544, int a00, float a28, uint a3c)
        => (Frame, MainInputs, PreparedCamera, this.values, Camera544Word, CameraA00, CameraA28, CameraA3C) =
            (frame, inputs, preparedCamera, values, camera544, a00, a28, a3c);
    public MapSourceCreatedF103CurrentFrame Frame { get; }
    public SkySeasonMainCameraInputs MainInputs { get; }
    public MapCameraConstructionPacket PreparedCamera { get; }
    public IReadOnlyList<float> Fields14EcTo1504 => Array.AsReadOnly(values);
    public uint Camera544Word { get; }
    public int CameraA00 { get; }
    public float CameraA28 { get; }
    public uint CameraA3C { get; }
    public int CameraKind3Ac => BinaryPrimitives.ReadInt32LittleEndian(PreparedCamera.Prefix.Span.Slice(0x3AC, 4));
    public float CameraPattern27C => BinaryPrimitives.ReadSingleLittleEndian(PreparedCamera.Prefix.Span.Slice(0x27C, 4));
    public string SourceEvidence => MainInputs.Source.PostEffectScalarSource.SourceEvidence +
        ";V468-selectedCPU121C..1234-to-Main121C..1234;V418-embeddedK14EC..1504;" +
        "V468-selectedCPU2E8-to-Main2E8/V418-K544;current-numeric-writer/resources-separate";
    public void RequireCurrent(MapSourceCreatedF103CurrentFrame actual)
    {
        if (!ReferenceEquals(actual, Frame)) throw new InvalidDataException("Early68 numeric Camera tail belongs to another current geometry.");
        (PreparedCamera.CurrentSeasonPostEffectWriteIdentity ??
            throw new InvalidDataException("Early68 prepared Camera lacks its actual Main writer.")).RequireActualMainPreparation(MainInputs, PreparedCamera);
        if (PreparedCamera.FrameNumber != actual.FrameNumber || PreparedCamera.ExecutableLocale != actual.Camera.ExecutableLocale ||
            !PreparedCamera.Prefix.Span.SequenceEqual(actual.Lighting.PreparedCameraPrefix.Span) ||
            PreparedCamera.SourceWorldToProjection != actual.Camera.SourceWorldToProjection ||
            MainInputs.MainFrame.Camera.FrameNumber != actual.FrameNumber ||
            !MainInputs.MainFrame.Camera.Prefix.Span.SequenceEqual(actual.Camera.Prefix.Span))
            throw new InvalidDataException("Early68 needs this geometry's exact prior Main and same Lighting prepared Camera.");
    }
    public DeferredEarlyIndirectPatternCamera PrepareCurrent(
        IDeferredEarlyIndirectCurrentPatternResources actualResources)
    {
        ArgumentNullException.ThrowIfNull(actualResources); RequireCurrent(Frame); actualResources.RequireCurrent(Frame);
        return new(values[0], values[1], values[2], values[3], values[4], values[5], values[6],
            actualResources.PatternMap1680Present, CameraKind3Ac, actualResources.PatternMap1888Present, Camera544Word != 0);
    }
}

public static class DeferredEarlyIndirectPatternCameraSource
{
    // These are imported selected-package values, never a prepared Camera,
    // current resource pointer, current Generic time or current lighting row.
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, ImportedFields> imported = new();
    private sealed class ImportedFields
    {
        internal readonly float[] Values;
        internal readonly uint Camera544;
        internal readonly int A00;
        internal readonly float A28;
        internal readonly uint A3C;
        internal ImportedFields(MapSeasonCameraPackage package)
        {
            if (package.Camera.ClassId != MapCameraConstructionSource.PerspectiveCameraClassId)
                throw new InvalidDataException("Early68 PatternMap numeric source is not its selected Perspective package.");
            var pattern = PatternCameraSettingsImport.Load(package);
            Values = [pattern.Scale.X, pattern.Scale.Y, pattern.Velocity.X, pattern.Velocity.Y,
                pattern.RotationAngles.X, pattern.RotationAngles.Y, pattern.RotationAngles.Z];
            Camera544 = Word(309, 0x46D194A5, 4);
            uint Word(int ordinal, uint hash, uint type)
            {
                var field = package.Camera.Properties[ordinal];
                if (field.Index != ordinal || field.NameHash != hash || field.TypeCode != type ||
                    field.ElementCount != 1 || field.ElementSize != 4 || field.RawValue.Length != 4)
                    throw new InvalidDataException("Early68 selected shadow Camera field differs at import.");
                return BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
            }
            A00 = unchecked((int)Word(24, 0x0611DBC4, 4));
            A28 = BitConverter.UInt32BitsToSingle(Word(129, 0x0D8D3389, 8));
            A3C = Word(26, 0x96E32C4A, 4);
            if (!float.IsFinite(A28)) throw new InvalidDataException("Early68 current shadow extent is nonfinite.");
        }
    }
    public static DeferredEarlyIndirectPatternCameraTail CaptureCurrent(
        MapSourceCreatedF103CurrentFrame actualGeometry, SkySeasonMainCameraInputs actualMainInputs)
        => CaptureCurrent(actualGeometry, actualMainInputs, actualGeometry.Camera);
    /// <summary>The ordinary caller supplies the actual post-season Camera
    /// returned by Lighting.CapturePreparedCameraFromSameAcceptedMain. The
    /// geometry Camera remains its earlier visibility snapshot.</summary>
    public static DeferredEarlyIndirectPatternCameraTail CaptureCurrent(
        MapSourceCreatedF103CurrentFrame actualGeometry, SkySeasonMainCameraInputs actualMainInputs,
        MapCameraConstructionPacket actualPreparedCamera)
    {
        ArgumentNullException.ThrowIfNull(actualGeometry); ArgumentNullException.ThrowIfNull(actualMainInputs);
        ArgumentNullException.ThrowIfNull(actualPreparedCamera);
        (actualPreparedCamera.CurrentSeasonPostEffectWriteIdentity ??
            throw new InvalidDataException("Early68 numeric tail needs the actual accepted Main preparation.")).RequireActualMainPreparation(
                actualMainInputs, actualPreparedCamera);
        if (actualMainInputs.Source.Selection.SecondPackage is not null)
            throw new NotSupportedException("Early68 numeric tail currently requires the actual direct selected package writer.");
        var fields = imported.GetValue(actualMainInputs.Source.Selection.FirstPackage, static package => new(package));
        var current = new DeferredEarlyIndirectPatternCameraTail(actualGeometry, actualMainInputs, actualPreparedCamera,
            fields.Values.ToArray(), fields.Camera544, fields.A00, fields.A28, fields.A3C);
        current.RequireCurrent(actualGeometry); return current;
    }
}
