using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The current ordinary D74F30 fields, read from a real complete
/// CKIDSCamera. They are not inferred from a null pattern resource or3C0 prefix.</summary>
public readonly record struct DeferredEarlyIndirectPatternCamera(float ScaleX14EC, float ScaleY14F0,
    float SpeedX14F4, float SpeedY14F8, float Angle14FC, float Angle1500, float Angle1504,
    bool Resource1680Present, int Camera3AC, bool Resource1888Present, bool Camera544Nonzero)
{
    public static DeferredEarlyIndirectPatternCamera ReadActualCamera(ReadOnlySpan<byte> actualCamera18C0)
    {
        if (actualCamera18C0.Length != 0x18C0)
            throw new InvalidDataException("PatternMap requires the actual complete18C0 Camera, not declared defaults or a3C0 lighting packet.");
        return new(Read(actualCamera18C0, 0x14EC), Read(actualCamera18C0, 0x14F0), Read(actualCamera18C0, 0x14F4),
            Read(actualCamera18C0, 0x14F8), Read(actualCamera18C0, 0x14FC), Read(actualCamera18C0, 0x1500), Read(actualCamera18C0, 0x1504),
            BinaryPrimitives.ReadUInt64LittleEndian(actualCamera18C0.Slice(0x1680, 8)) != 0,
            BinaryPrimitives.ReadInt32LittleEndian(actualCamera18C0.Slice(0x3AC, 4)),
            BinaryPrimitives.ReadUInt64LittleEndian(actualCamera18C0.Slice(0x1888, 8)) != 0,
            BinaryPrimitives.ReadUInt32LittleEndian(actualCamera18C0.Slice(0x544, 4)) != 0);
    }
    private static float Read(ReadOnlySpan<byte> value, int at) => BinaryPrimitives.ReadSingleLittleEndian(value.Slice(at, 4));
}

/// <summary>Imported99FE10/SC9A0DB0 constructor and the ordinary current
/// PatternMap writer. Current resource branches and current inputs are not cached.</summary>
public sealed class LoadedDeferredEarlyIndirectPatternMapSource
{
    private readonly byte[] constructorMatrix;
    internal readonly byte[] AffineRow1;
    internal readonly byte[] ResetMatrix, ResetExtra32;
    internal readonly float ResetPattern;
    internal LoadedDeferredEarlyIndirectPatternMapSource(string locale, string evidence, byte[] matrix, byte[] affineRow1,
        float unchangedScale, float numerator, byte[] resetMatrix, float resetPattern, byte[] resetExtra)
        => (Locale, SourceEvidence, constructorMatrix, AffineRow1, UnchangedTransformScale, AffineScaleNumerator, ResetMatrix, ResetPattern, ResetExtra32) =
            (locale, evidence, matrix, affineRow1, unchangedScale, numerator, resetMatrix, resetPattern, resetExtra);
    public string Locale { get; }
    public string SourceEvidence { get; }
    public float UnchangedTransformScale { get; }
    internal float AffineScaleNumerator { get; }
    public DeferredEarlyIndirectPatternMapState ConstructOwnedState()
        => new(this, constructorMatrix.ToArray(), 1, new byte[32]);
    /// <summary>1593F0→C14A60→C13360 publishes the source reset literals
    /// during actual factory preparation, after the99FE10 parameter ctor.</summary>
    public DeferredEarlyIndirectPatternMapState ConstructAfterSourceFactoryPreparation()
        => new(this, ResetMatrix.ToArray(), ResetPattern, ResetExtra32.ToArray());
    public static LoadedDeferredEarlyIndirectPatternMapSource Load(GameResourceRepository repository, string locale)
        => RenderSamplerSourceAdapters.LoadEarlyIndirectPatternMapSource(repository.SourceDirectory, locale);
}

/// <summary>Actual CPU parameter state. A default current transform retains
/// its prior matrix; it does not reset the module each request. This state
/// neither owns a GPU CB nor grants initialized texture contents.</summary>
public sealed class DeferredEarlyIndirectPatternMapState
{
    private byte[] matrix, extra;
    private float pattern;
    private DeferredEarlyIndirectPatternMapWrite? lastWrite;
    internal DeferredEarlyIndirectPatternMapState(LoadedDeferredEarlyIndirectPatternMapSource source,
        byte[] matrix, float pattern, byte[] extra)
        => (Source, this.matrix, this.pattern, this.extra) = (source, matrix, pattern, extra);
    public LoadedDeferredEarlyIndirectPatternMapSource Source { get; }
    public ReadOnlyMemory<byte> WorldToPointMatrix => matrix;
    public float Pattern => pattern;
    public ReadOnlyMemory<byte> PatternPlaneSubAndPattern1 => extra;

    /// <summary>Actual D74FCD..D75066. STA selects this path, while the
    /// matrix is copied from the actual Host.v1A8 Sky CPU+180. The source
    /// ignores the ordinary transform and modifier on this branch.</summary>
    public DeferredEarlyIndirectPatternMapWrite ApplyCurrentSpecial(
        DeferredEarlyIndirectPatternCameraTail actualCamera,
        IDeferredEarlyIndirectCurrentPatternResources actualResources,
        IDeferredEarlyIndirectCurrentPatternSky actualSky)
    {
        ArgumentNullException.ThrowIfNull(actualSky);
        if (!DeferredEarlyIndirectPatternSpecialSource.UsesSpecialBranch(actualCamera, actualResources))
            throw new InvalidDataException("Special PatternMap writer requires its actual STA/Camera3AC branch.");
        var sta = DeferredEarlyIndirectPatternSpecialSource.RequireActualSta(actualCamera.Frame, actualResources);
        actualSky.RequireCurrent(actualCamera.Frame);
        return ApplySpecialFields(actualCamera.CameraPattern27C,
            () => actualSky.ReadCurrentSkyPalette(actualCamera.Frame),
            sta.SourceEvidence + ";" + actualSky.SourceEvidence);
    }
    // Inert original-opcode fixtures enter here; normal callers use the
    // actual Main/STA/Host owner entry above. No matrix is synthesized.
    internal DeferredEarlyIndirectPatternMapWrite ApplySpecialFields(float actualCamera27C,
        Func<DeferredEarlyIndirectPatternSkyPalette?> readActualSky, string actualEvidence)
    {
        if (!float.IsFinite(actualCamera27C)) throw new InvalidDataException("Current PatternMap Camera27C is nonfinite.");
        bool scalarWrite = actualCamera27C != 0;
        // Native order publishes the scalar before invoking Host.v1A8.
        if (scalarWrite) pattern = actualCamera27C;
        var palette = readActualSky(); bool matrixWrite = false;
        if (palette is not null)
        {
            if (palette.Wrappers40.Count != 4)
                throw new InvalidDataException("Current Sky PatternMap needs its four actual nullable wrapper references.");
            uint index = (palette.PackedSelector168 >> 10) & 7;
            if (index < 4 && palette.Wrappers40[(int)index] is not null)
            {
                if (palette.WorldToPatternMatrix180.Length != 64)
                    throw new InvalidDataException("Selected Sky PatternMap requires its actual complete64B CPU180 matrix.");
                matrix = palette.WorldToPatternMatrix180.ToArray(); matrixWrite = true;
            }
        }
        return lastWrite = new(this, matrix.ToArray(), pattern, extra.ToArray(), matrixWrite, false,
            Source.SourceEvidence + ";" + actualEvidence + ";D74FCD-Camera27C/D75009-Host-v1A8/168-low3-at10/40-selected/180-copy;no-STA-matrix-synthesis",
            scalarWrite);
    }

    /// <summary>Port of the source's no-rotation ordinary branch. Modifier
    /// is the actual D59210 Param (or its actual null result); Time is the
    /// owning Generic FD8*FD4+FD0 value. Resource1680 and nonzero angles require
    /// their separate native writers and are rejected before changing state.</summary>
    public DeferredEarlyIndirectPatternMapWrite ApplyCurrentOrdinary(
        DeferredEarlyIndirectPatternCamera camera, ReadOnlySpan<byte> actualModifierParam,
        float actualGenericParameterTime)
    {
        if (camera.Resource1680Present && camera.Camera3AC != 5)
            throw new NotSupportedException("Current PatternMap1680 uses the original special resource/matrix writer.");
        float sx = camera.ScaleX14EC, sy = camera.ScaleY14F0, vx = camera.SpeedX14F4, vy = camera.SpeedY14F8;
        float tx = 0, ty = 0;
        float nextPattern = pattern; byte[] nextExtra = extra;
        bool modifierWrites = !actualModifierParam.IsEmpty &&
            (BinaryPrimitives.ReadUInt32LittleEndian(RequireModifier(actualModifierParam).Slice(0x44, 4)) & 8) == 0;
        if (modifierWrites)
        {
            if (!(camera.Resource1888Present && camera.Camera544Nonzero))
            {
                sx = Read(actualModifierParam, 0xF0); sy = Read(actualModifierParam, 0xF4);
                vx = Read(actualModifierParam, 0xF8); vy = Read(actualModifierParam, 0xFC);
                tx = Read(actualModifierParam, 0x100); ty = Read(actualModifierParam, 0x104);
            }
            nextPattern = Read(actualModifierParam, 0xE0);
            nextExtra = new byte[32];
            actualModifierParam.Slice(0xE4, 8).CopyTo(nextExtra);
        }
        float[] values = [sx, sy, vx, vy, tx, ty, camera.Angle14FC, camera.Angle1500, camera.Angle1504, nextPattern];
        if (values.Any(value => !float.IsFinite(value)) || sx == 0 || sy == 0)
            throw new InvalidDataException("Current PatternMap ordinary fields must be finite and its scales nonzero.");
        if (camera.Angle14FC != 0 || camera.Angle1500 != 0 || camera.Angle1504 != 0)
            throw new NotSupportedException("Current PatternMap rotations require98B630/17B070/3F6CD0/AB6090/11510.");
        bool matrixWrites = sx != Source.UnchangedTransformScale || sy != Source.UnchangedTransformScale || vx != 0 || vy != 0 || tx != 0 || ty != 0;
        byte[] nextMatrix = matrix;
        if (matrixWrites)
        {
            if (!float.IsFinite(actualGenericParameterTime))
                throw new InvalidDataException("Current PatternMap needs its actual Generic parameter time.");
            nextMatrix = matrix.ToArray();
            // D75304/0A and D75335..54. Original global writes place the
            // literal in row1, and the Y reciprocal in row2 (offset36).
            Array.Clear(nextMatrix);
            Write(nextMatrix, 0, Source.AffineScaleNumerator / sx);
            Write(nextMatrix, 36, Source.AffineScaleNumerator / sy);
            Source.AffineRow1.CopyTo(nextMatrix, 16); Write(nextMatrix, 60, 1);
            Write(nextMatrix, 48, NativeFloat.Add(NativeFloat.Multiply(actualGenericParameterTime, vx), tx));
            Write(nextMatrix, 52, NativeFloat.Add(NativeFloat.Multiply(actualGenericParameterTime, vy), ty));
        }
        matrix = nextMatrix; pattern = nextPattern; extra = nextExtra;
        return lastWrite = new(this, matrix.ToArray(), pattern, extra.ToArray(), matrixWrites, modifierWrites,
            Source.SourceEvidence + ";actualordinary-D74F30-fields;no-rotation;default-transform-retains-matrix;source-only");
    }
    /// <summary>EBD6EE..F7 invokes C13360 after each changed group, including
    /// a rejected v60. This CPU reset is distinct from99FE10 construction and
    /// is not proof that v90 submitted a device draw or that the group returned.</summary>
    public void ApplySourceRecordReset(DeferredEarlyIndirectPatternMapWrite actualWrite)
    {
        if (!ReferenceEquals(actualWrite, lastWrite) || !ReferenceEquals(actualWrite.State, this))
            throw new InvalidDataException("PatternMap reset belongs to another current record/parameter owner.");
        if (actualWrite.RequiresSourceRecordReset)
        { matrix = Source.ResetMatrix.ToArray(); pattern = Source.ResetPattern; extra = Source.ResetExtra32.ToArray(); }
        lastWrite = null;
    }
    private static ReadOnlySpan<byte> RequireModifier(ReadOnlySpan<byte> value)
    {
        if (value.Length < 0x108) throw new InvalidDataException("Actual PatternMap modifier lacks Param+44/E0..104.");
        return value;
    }
    private static float Read(ReadOnlySpan<byte> value, int at) => BinaryPrimitives.ReadSingleLittleEndian(value.Slice(at, 4));
    private static void Write(Span<byte> bytes, int at, float value) => BinaryPrimitives.WriteSingleLittleEndian(bytes.Slice(at, 4), value);
}

public sealed class DeferredEarlyIndirectPatternMapWrite
{
    internal DeferredEarlyIndirectPatternMapWrite(DeferredEarlyIndirectPatternMapState state,
        ReadOnlyMemory<byte> matrix, float pattern, ReadOnlyMemory<byte> extra, bool matrixWrite, bool modifierWrite, string evidence,
        bool cameraPatternWrite = false)
        => (State, WorldToPointMatrix, Pattern, Extra32, WroteMatrix, WroteModifierParameters, SourceEvidence, WroteCameraPattern27C) =
            (state, matrix, pattern, extra, matrixWrite, modifierWrite, evidence, cameraPatternWrite);
    public DeferredEarlyIndirectPatternMapState State { get; }
    public ReadOnlyMemory<byte> WorldToPointMatrix { get; }
    public float Pattern { get; }
    public ReadOnlyMemory<byte> Extra32 { get; }
    public bool WroteMatrix { get; }
    public bool WroteModifierParameters { get; }
    public bool WroteCameraPattern27C { get; }
    public bool RequiresSourceRecordReset => WroteMatrix || WroteModifierParameters || WroteCameraPattern27C;
    public string SourceEvidence { get; }
}
