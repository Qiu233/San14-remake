using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ReflectionComputeProbeGeometry(uint Width, uint Height, uint Depth,
    float Pitch, uint TileSize, float DistanceScale, Vector3 Origin);

/// <summary>Octmap extent/layers, plus the independently selected radiance texture's mip count.</summary>
public sealed record ReflectionComputeAtlasLayout(uint Width, uint Height, uint Layers, uint MipLevels);

/// <summary>SkyMapMissing is the shader owner's actual retained flag, including pass5 inheritance.</summary>
public sealed record ReflectionComputeOutput(uint Width, uint Height, uint SourceListIndex, bool SkyMapMissing);

public sealed record ReflectionComputeTuning(float IndirectWeight, float SpecularWeight,
    Vector2 TraceBias, uint MipLevelCount, float MipOffset)
{
    /// <summary>Only the successful shader initialization's proved defaults.
    /// The independent mip-selection fields still require their actual owner.</summary>
    public static ReflectionComputeTuning FreshShader(uint mipLevelCount, float mipOffset) =>
        new(0.8f, 1f, new(0.01f, 0.01f), mipLevelCount, mipOffset);
}

public sealed record ReflectionComputeParameterInputs(DeferredCameraInputs Camera,
    ReflectionComputeProbeGeometry Probe, ReflectionComputeAtlasLayout Atlas,
    ReflectionComputeOutput Output, ReflectionComputeTuning Tuning);

/// <summary>BD4820's screen-evaluation fields for RefProbe CS10/11. Writes
/// the selected resource's named RDEF layout, without a native object image.</summary>
public static class ReflectionComputeParameterSource
{
    public static byte[] Encode(ReflectionShaderStage stage, ReflectionComputeParameterInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(inputs.Camera);
        ArgumentNullException.ThrowIfNull(inputs.Probe);
        ArgumentNullException.ThrowIfNull(inputs.Atlas);
        ArgumentNullException.ThrowIfNull(inputs.Output);
        ArgumentNullException.ThrowIfNull(inputs.Tuning);
        var layout = Layouts.GetValue(stage, CompileLayout);
        bool reflection = layout.Reflection;
        var p = inputs.Probe;
        var a = inputs.Atlas;
        var o = inputs.Output;
        var t = inputs.Tuning;
        // These extents come from actual ushort texture descriptors. Probe
        // grid counts remain uint; they are not reinterpreted as float bits.
        if (o.Width is 0 or > ushort.MaxValue || o.Height is 0 or > ushort.MaxValue ||
            a.Width is 0 or > ushort.MaxValue || a.Height is 0 or > ushort.MaxValue ||
            a.Layers is 0 or > ushort.MaxValue || p.TileSize == 0 || a.MipLevels > 31)
            throw Invalid("texture_extent_or_tile_size");
        if (!Finite(p.Origin) || !Finite(inputs.Camera.SourceEye) || !float.IsFinite(p.Pitch) ||
            !float.IsFinite(p.DistanceScale) || !float.IsFinite(t.IndirectWeight) ||
            !float.IsFinite(t.SpecularWeight) || !Finite(t.TraceBias) || !float.IsFinite(t.MipOffset))
            throw Invalid("nonfinite_input");

        // Preserve integer division before conversion to Single, and source
        // reciprocal semantics; no clamp or fallback for zero grid counts.
        var fieldPitch = new Vector4((float)p.Width, (float)p.Height, (float)p.Depth, p.Pitch);
        var tilePitch = new Vector4((float)(a.Width / p.TileSize), (float)(a.Height / p.TileSize),
            (float)(a.Layers / 3u), (float)p.TileSize);
        if (reflection && a.MipLevels == 0) throw Invalid("radiance_mip_count");
        var viewToWorld = NativeMatrixMath.Invert(inputs.Camera.SourceWorldToView, out _);
        var projectionToWorld = NativeMatrixMath.Invert(inputs.Camera.SourceWorldToProjection, out _);
        var bytes = new byte[layout.ByteSize];
        foreach (var field in layout.Fields)
        {
            if (field.Semantic == Field.ViewToWorld)
                WriteMatrix(bytes, field.Offset, viewToWorld);
            else if (field.Semantic == Field.ProjectionToWorld)
                WriteMatrix(bytes, field.Offset, projectionToWorld);
            else
            {
                Vector4 value = field.Semantic switch
                {
                    Field.Output => new(o.Width, o.Height, o.SourceListIndex == 0 ? 0f : 1f, o.SkyMapMissing ? 1f : 0f),
                    Field.Origin => new(p.Origin, 0f),
                    Field.FieldPitch => fieldPitch,
                    Field.FieldPitchReciprocal => Reciprocal(fieldPitch),
                    Field.TilePitch => tilePitch,
                    Field.TilePitchReciprocal => Reciprocal(tilePitch),
                    Field.ScaleOffset => new(p.DistanceScale, 0f, 0f, 0f),
                    Field.TraceBias => new(t.TraceBias, 0f, 0f),
                    Field.Distance => new(1f / p.DistanceScale, 0f, p.DistanceScale, 0f),
                    Field.Mip => new((float)t.MipLevelCount, t.MipOffset, (float)(a.MipLevels - 1u), 0f),
                    Field.Eye => inputs.Camera.SourceEye,
                    Field.IndirectWeight => new(t.IndirectWeight, 0f, 0f, t.SpecularWeight),
                    _ => throw Invalid("compiled_field")
                };
                WriteVector(bytes, field.Offset, value);
            }
        }
        return bytes;
    }

    // ReflectionShaderStage is the immutable, loaded resource owner. Cache only
    // its compiled named layout; frame inputs never participate in this cache.
    private static readonly ConditionalWeakTable<ReflectionShaderStage, CompiledLayout> Layouts = new();
    private enum Field
    {
        ViewToWorld, ProjectionToWorld, Output, Origin, FieldPitch, FieldPitchReciprocal,
        TilePitch, TilePitchReciprocal, ScaleOffset, TraceBias, Distance, Mip, Eye, IndirectWeight
    }
    private sealed record BoundField(Field Semantic, int Offset);
    private sealed record CompiledLayout(bool Reflection, int ByteSize, BoundField[] Fields);

    private static CompiledLayout CompileLayout(ReflectionShaderStage stage)
    {
        if (stage.StageCode != 5 || stage.ProgramIndex is not (10 or 11)) throw Invalid("stage");
        bool reflection = stage.ProgramIndex == 11;
        var cb = stage.Definitions.Buffers.SingleOrDefault(buffer => buffer.Name.Text == "$Globals")
            ?? throw Invalid("globals");
        if (cb.BufferType != 0 || cb.ByteSize != (reflection ? 304 : 272)) throw Invalid("globals_shape");
        if (cb.Variables.Count != (reflection ? 13 : 11)) throw Invalid("globals_fields");
        var semantics = new HashSet<Field>();
        var ranges = new List<(int Start, int End)>();
        var fields = new BoundField[cb.Variables.Count];
        int index = 0;
        foreach (var variable in cb.Variables)
        {
            string name = variable.Name.Text;
            Field semantic = name switch
            {
                "mV2W" => Field.ViewToWorld,
                "mP2W" => Field.ProjectionToWorld,
                "gv_info_out" => Field.Output,
                "gv_probefield_org" => Field.Origin,
                "gv_probefield_pitch_size" => Field.FieldPitch,
                "gv_probefield_pitch_size_rv" => Field.FieldPitchReciprocal,
                "gv_tiledoct_pitch_octsize" => Field.TilePitch,
                "gv_tiledoct_pitch_octsize_rv" => Field.TilePitchReciprocal,
                "gv_probe_scale_offset" => Field.ScaleOffset,
                "gv_trace_bias" => Field.TraceBias,
                "gv_distance_info" when reflection => Field.Distance,
                "gv_mip_info" when reflection => Field.Mip,
                "vEye" when reflection => Field.Eye,
                "gv_indirect_weight" when !reflection => Field.IndirectWeight,
                _ => throw Invalid("unexpected_field." + name)
            };
            if (!semantics.Add(semantic) || variable.StartOffset < 0 || variable.ByteSize < 0 ||
                variable.StartOffset > cb.ByteSize - variable.ByteSize ||
                ranges.Any(range => variable.StartOffset < range.End && variable.StartOffset + variable.ByteSize > range.Start))
                throw Invalid("globals_field_range");
            var type = stage.Definitions.Types.SingleOrDefault(type => type.Offset == variable.TypeOffset)
                ?? throw Invalid("globals_field_type");
            bool matrix = semantic is Field.ViewToWorld or Field.ProjectionToWorld;
            if (variable.ByteSize != (matrix ? 64 : 16) || type.Class != (matrix ? 2 : 1) ||
                type.BaseType != 3 || type.Rows != (matrix ? 4 : 1) || type.Columns != 4 ||
                type.Elements != 0 || type.Members.Count != 0)
                throw Invalid("field_shape." + name);
            fields[index++] = new(semantic, variable.StartOffset);
            ranges.Add((variable.StartOffset, variable.StartOffset + variable.ByteSize));
        }
        return new(reflection, cb.ByteSize, fields);
    }

    private static void WriteMatrix(Span<byte> bytes, int at, Matrix4x4 matrix)
    {
        WriteVector(bytes, at, new(matrix.M11, matrix.M12, matrix.M13, matrix.M14));
        WriteVector(bytes, at + 16, new(matrix.M21, matrix.M22, matrix.M23, matrix.M24));
        WriteVector(bytes, at + 32, new(matrix.M31, matrix.M32, matrix.M33, matrix.M34));
        WriteVector(bytes, at + 48, new(matrix.M41, matrix.M42, matrix.M43, matrix.M44));
    }

    private static Vector4 Reciprocal(Vector4 value) => new(1f / value.X, 1f / value.Y, 1f / value.Z, 1f / value.W);
    private static void WriteVector(Span<byte> bytes, int at, Vector4 value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(bytes[at..], value.X);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[(at + 4)..], value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[(at + 8)..], value.Z);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[(at + 12)..], value.W);
    }
    private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static bool Finite(Vector4 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) && float.IsFinite(value.W);
    private static InvalidDataException Invalid(string field) => new("reflection.compute_parameters." + field);
}
