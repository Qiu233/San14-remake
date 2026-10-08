using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalDirectionalLightDeclaration(uint ObjectId, uint SourceGroupMask,
    IReadOnlyList<Vector4> WorldQuaternions, IReadOnlyList<Vector3> Colors);
public sealed record LoadedOriginalDirectionalLightDeclaration(OriginalDirectionalLightDeclaration Declaration,
    LoadedRdbAsset Source);
public sealed record OriginalDirectionalLightSourceRow(Vector4 Direction, Vector4 Color);

/// <summary>
/// The explicit E01606F4 Directional source loader's four ordinary light rows.
/// Source declarations are not a selected View, live light, or final modifier result.
/// Point, Spot, HDRI and Hemi declarations have distinct original loaders.
/// </summary>
public static class OriginalDirectionalLightSource
{
    public const uint SourceClass = 0xE01606F4;
    private static readonly uint[] QuaternionProperties = [0xF52EA910, 0xB65C0542, 0x0705E343, 0x57AFC144];
    private static readonly uint[] ColorProperties = [0x754E8B8D, 0xF600D81B, 0xFDB27D7A, 0x056422D9];

    public static LoadedOriginalDirectionalLightDeclaration Load(OriginalGameRepository repository,
        string archive, uint fileId, uint objectId)
    {
        ArgumentNullException.ThrowIfNull(repository);
        LoadedObjects loaded = repository.LoadObjects(archive, fileId);
        DokObject[] matches = loaded.Document.Objects.Where(item => item.ObjectId == objectId).ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException($"Directional source {loaded.Source.ResourceUri}: object {objectId:X8} is missing or ambiguous.");
        try { return new(Read(matches[0]), loaded.Source); }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"Directional source {loaded.Source.ResourceUri}: {error.Message}", error); }
    }

    public static OriginalDirectionalLightDeclaration Read(DokObject source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Kind != DokObjectKind.Inline || source.ClassId != SourceClass)
            throw new InvalidDataException($"Directional object {source.ObjectId:X8} requires inline class {SourceClass:X8}; reference inheritance and other light loaders are unresolved.");
        var quaternions = new Vector4[4]; var colors = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            DokProperty q = Property(source, QuaternionProperties[i], 8, 4);
            DokProperty c = Property(source, ColorProperties[i], 8, 3);
            quaternions[i] = new(ReadFloat(q, 0), ReadFloat(q, 1), ReadFloat(q, 2), ReadFloat(q, 3));
            colors[i] = new(ReadFloat(c, 0), ReadFloat(c, 1), ReadFloat(c, 2));
        }
        uint mask = BinaryPrimitives.ReadUInt32LittleEndian(Property(source, 0xC0C5A5B5, 5, 1).RawValue);
        return new(source.ObjectId, mask, Array.AsReadOnly(quaternions), Array.AsReadOnly(colors));
    }

    /// <summary>CBFAB0 normalizes each source quaternion, rotates the original
    /// (0,0,1,0) vector through016700, then normalizes XYZ throughC0FE40.
    /// Colors copy RGB and explicitly write W=0. All operations remain binary32.</summary>
    public static IReadOnlyList<OriginalDirectionalLightSourceRow> PrepareRows(OriginalDirectionalLightDeclaration source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.WorldQuaternions.Count != 4 || source.Colors.Count != 4)
            throw new InvalidDataException("Directional source requires all four declared quaternions and RGB rows.");
        var result = new OriginalDirectionalLightSourceRow[4];
        for (int i = 0; i < result.Length; i++)
        {
            Vector3 rgb = source.Colors[i]; Finite(rgb.X); Finite(rgb.Y); Finite(rgb.Z);
            result[i] = new(PrepareDirection(source.WorldQuaternions[i]), new(rgb, 0));
        }
        return Array.AsReadOnly(result);
    }

    /// <summary>Updates only the eight direction/color vectors in an explicit
    /// LightData prefix. Occurrence, ambient, fog and modifier bytes are retained.</summary>
    public static byte[] ApplyRows(ReadOnlySpan<byte> explicitLightDataPrefix,
        OriginalDirectionalLightDeclaration source)
    {
        if (explicitLightDataPrefix.Length != 0x1B0)
            throw new InvalidDataException("Directional source row application requires the complete explicit1B0 LightData prefix.");
        IReadOnlyList<OriginalDirectionalLightSourceRow> rows = PrepareRows(source);
        byte[] result = explicitLightDataPrefix.ToArray();
        for (int i = 0; i < rows.Count; i++)
        { Write(result, i * 32, rows[i].Direction); Write(result, i * 32 + 16, rows[i].Color); }
        return result;
    }

    /// <summary>Only the unmodified ordinary kind1 path. Caller supplies actual
    /// occurrence/ambient and shader capacity; this does not select a publisher.</summary>
    public static OriginalDeferredLightingInputs PrepareUnmodifiedBaseLighting(
        OriginalDirectionalLightDeclaration source, IReadOnlyList<float> explicitOccurrences,
        Vector4 explicitAmbient, int explicitShaderCapacity)
    {
        ArgumentNullException.ThrowIfNull(explicitOccurrences);
        if (explicitOccurrences.Count != 4 || explicitShaderCapacity is < 1 or > 4)
            throw new InvalidDataException("Directional base preparation requires four explicit occurrences and a shader capacity1..4.");
        Finite(explicitAmbient.X); Finite(explicitAmbient.Y); Finite(explicitAmbient.Z); Finite(explicitAmbient.W);
        IReadOnlyList<OriginalDirectionalLightSourceRow> rows = PrepareRows(source);
        int count = explicitShaderCapacity;
        // D77008..D77121 retains the first row when every RGB is nonpositive.
        while (count > 1 && rows[count - 1].Color.X <= 0 && rows[count - 1].Color.Y <= 0 && rows[count - 1].Color.Z <= 0) count--;
        var lights = new OriginalDeferredDirectionalLight[count];
        for (int i = 0; i < lights.Length; i++)
        { Finite(explicitOccurrences[i]); lights[i] = new(rows[i].Direction, rows[i].Color, explicitOccurrences[i]); }
        return new(Array.AsReadOnly(lights), explicitAmbient);
    }

    public static Vector4 PrepareDirection(Vector4 sourceQuaternion)
    {
        Finite(sourceQuaternion.X); Finite(sourceQuaternion.Y); Finite(sourceQuaternion.Z); Finite(sourceQuaternion.W);
        float norm = Add(Add(Add(Mul(sourceQuaternion.Y, sourceQuaternion.Y), Mul(sourceQuaternion.X, sourceQuaternion.X)),
            Mul(sourceQuaternion.Z, sourceQuaternion.Z)), Mul(sourceQuaternion.W, sourceQuaternion.W));
        float inv = Div(1, SqrtPositive(norm));
        Vector4 q = new(Mul(inv, sourceQuaternion.X), Mul(inv, sourceQuaternion.Y),
            Mul(inv, sourceQuaternion.Z), Mul(inv, sourceQuaternion.W));
        return RotatePreparedQuaternion(q);
    }

    /// <summary>C0FE40 / C0FEE0 consumes an already prepared quaternion without
    /// first normalizing its four components. Hemi uses original positive Y.</summary>
    public static Vector4 RotatePreparedQuaternion(Vector4 quaternion, bool hemi = false)
    {
        Finite(quaternion.X); Finite(quaternion.Y); Finite(quaternion.Z); Finite(quaternion.W);
        Vector4 v = Rotate016700(hemi ? new(0, 1, 0, 0) : new(0, 0, 1, 0), quaternion);
        float norm = Add(Add(Mul(v.Y, v.Y), Mul(v.X, v.X)), Mul(v.Z, v.Z));
        float inv = Div(1, SqrtPositive(norm));
        Vector4 result = new(Mul(inv, v.X), Mul(inv, v.Y), Mul(inv, v.Z), v.W);
        Finite(result.X); Finite(result.Y); Finite(result.Z); Finite(result.W);
        return result;
    }

    // Preserve016700's scalar operation order and zero products. Replacing this
    // with Quaternion.Transform or the expanded3x3 formula changes rounding.
    private static Vector4 Rotate016700(Vector4 v, Vector4 q)
    {
        float t = Add(Add(Mul(v.X, q.X), Mul(v.Y, q.Y)), Mul(v.Z, q.Z));
        float a = Add(Sub(Mul(v.Z, q.Y), Mul(q.Z, v.Y)), Mul(v.X, q.W));
        float b = Add(Sub(Mul(q.X, v.Y), Mul(v.X, q.Y)), Mul(v.Z, q.W));
        float c = Add(Sub(Mul(q.Z, v.X), Mul(v.Z, q.X)), Mul(v.Y, q.W));
        float x = Add(Mul(a, q.W), Add(Sub(Mul(q.Y, b), Mul(q.Z, c)), Mul(q.X, t)));
        float y = Add(Mul(c, q.W), Add(Sub(Mul(q.Z, a), Mul(q.X, b)), Mul(q.Y, t)));
        float z = Add(Mul(b, q.W), Add(Sub(Mul(q.X, c), Mul(q.Y, a)), Mul(q.Z, t)));
        return new(x, y, z, v.W);
    }

    private static float SqrtPositive(float value)
    {
        if (!float.IsFinite(value) || value <= 0)
            throw new InvalidDataException("Directional source normalization would produce a zero-divisor/nonfinite value.");
        if (!Sse.IsSupported) throw new PlatformNotSupportedException("Original Directional finite normalization requires SSE.");
        return Sse.SqrtScalar(Vector128.CreateScalar(value)).GetElement(0);
    }
    private static DokProperty Property(DokObject source, uint hash, uint type, int count)
    {
        DokProperty[] found = source.Properties.Where(item => item.NameHash == hash).ToArray();
        if (found.Length != 1 || found[0].TypeCode != type || found[0].ElementCount != count || found[0].RawValue.Length != count * 4)
            throw new InvalidDataException($"Directional object {source.ObjectId:X8} requires unique property {hash:X8}, type{type}, count{count}.");
        return found[0];
    }
    private static float ReadFloat(DokProperty property, int element)
    { float result = BinaryPrimitives.ReadSingleLittleEndian(property.RawValue.AsSpan(element * 4, 4)); Finite(result); return result; }
    private static void Finite(float value)
    { if (!float.IsFinite(value)) throw new InvalidDataException("Directional source requires finite consumed float values."); }
    private static void Write(byte[] bytes, int offset, Vector4 value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset, 4), value.X);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 4, 4), value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 8, 4), value.Z);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 12, 4), value.W);
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Mul(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Sub(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Div(float a, float b) => a / b;
}
