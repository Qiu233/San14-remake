using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record ReflectionCaptureFilterLighting(Vector4 Direction, Vector4 Color, Matrix4x4 WorldToShadow);
public sealed record ReflectionCaptureFilterTuning(float IndirectWeight, float SpecularWeight, Vector2 TraceBias)
{
    public static ReflectionCaptureFilterTuning FreshShader => new(.8f, 1f, new(.01f, .01f));
}

/// <summary>BD4820 named constants for the complete original wrapper filter.
/// Lighting is borrowed from the actual capture light/shadow preparation;
/// current shader input index is shared by the original shader owner.</summary>
public static class ReflectionCaptureFilterParameters
{
    private sealed record Field(string Name, int Offset);
    private sealed record Layout(int Bytes, Field[] Fields);
    private static readonly ConditionalWeakTable<ReflectionShaderStage, Layout> layouts = new();

    public static byte[] Encode(ReflectionShaderStage stage, ReflectionCaptureFieldResourcePlan storage,
        ReflectionComputeProbeGeometry geometry, ReflectionCaptureFilterStep work,
        uint actualTargetWidth, uint actualTargetHeight, uint actualInputTextureWidth,
        int actualShaderRetainedInputIndex, ReflectionCaptureFilterTuning tuning,
        ReflectionCaptureFilterLighting? lighting)
    {
        var layout = layouts.GetValue(stage, Compile);
        uint tile = storage.Field.TileSize;
        if (tile == 0) throw Invalid("tile");
        uint columns = storage.AtlasWidth / tile, rows = storage.AtlasHeight / tile;
        if (columns == 0 || rows == 0) throw Invalid("tile_divisor");
        var tilePitch = new Vector4(columns, rows, storage.CaptureArrayLayers / 3, tile);
        var fieldPitch = new Vector4(geometry.Width, geometry.Height, geometry.Depth, geometry.Pitch);
        Vector4 output, input = new(work.Layer, 0, 0, 0);
        if (work.Pass == 2)
        {
            if (actualInputTextureWidth == 0) throw Invalid("integration_texture");
            tilePitch.W = (float)actualInputTextureWidth / columns;
            input.X = work.Kind == ReflectionCaptureFilterStepKind.IntegratePropagation ? 0 : work.Layer;
            output = new(work.Layer, 0, 0, 0);
        }
        else if (work.Pass is 6 or 7)
        {
            if (actualTargetWidth == 0 || actualTargetHeight == 0) throw Invalid("raster_extent");
            output = new(1f / actualTargetWidth, 1f / actualTargetHeight, 1f / ((float)actualTargetWidth / columns), 0);
            input.Y = work.Iteration;
        }
        else if (work.Pass == 8)
            output = new(work.Kind == ReflectionCaptureFilterStepKind.PublishCoefficients ? 1 : 0, 0, 0, 0);
        else if (work.Pass == 9)
            output = new(1f / storage.AtlasWidth, 1f / storage.AtlasHeight,
                1f / ((float)storage.AtlasWidth / columns), actualShaderRetainedInputIndex);
        else if (work.Pass == 11)
        {
            if (work.Mip == 0 || work.Mip >= storage.RadianceMipLevels || actualTargetWidth == 0)
                throw Invalid("mip");
            output = new(1f / actualTargetWidth, (float)actualTargetWidth / columns, 0, 0);
            tilePitch.W = (float)(storage.AtlasWidth >> (int)(work.Mip - 1)) / columns;
        }
        else throw Invalid("pass");
        var bytes = new byte[layout.Bytes];
        foreach (var field in layout.Fields)
        {
            if (field.Name == "mW2S")
            {
                var m = (lighting ?? throw Invalid("actual_light_shadow")).WorldToShadow;
                Write(bytes, field.Offset, new(m.M11, m.M12, m.M13, m.M14));
                Write(bytes, field.Offset + 16, new(m.M21, m.M22, m.M23, m.M24));
                Write(bytes, field.Offset + 32, new(m.M31, m.M32, m.M33, m.M34));
                Write(bytes, field.Offset + 48, new(m.M41, m.M42, m.M43, m.M44));
                continue;
            }
            Vector4 value = field.Name switch
            {
                "litDir" => (lighting ?? throw Invalid("actual_light")).Direction,
                "litCol" => (lighting ?? throw Invalid("actual_light")).Color,
                "gv_info_out" => output, "gv_info_in" => input,
                "gv_probefield_org" => new(geometry.Origin, 0),
                "gv_probefield_pitch_size" => fieldPitch,
                "gv_probefield_pitch_size_rv" => Reciprocal(fieldPitch),
                "gv_tiledoct_pitch_octsize" => tilePitch,
                "gv_tiledoct_pitch_octsize_rv" => Reciprocal(tilePitch),
                "gv_probe_scale_offset" => new(geometry.DistanceScale, 0, 0, 0),
                "gv_indirect_weight" => new(tuning.IndirectWeight, 0, 0, tuning.SpecularWeight),
                "gv_trace_bias" => new(tuning.TraceBias, 0, 0),
                "gv_mip_info" => new(work.Mip, (float)work.Mip / (storage.RadianceMipLevels - 1), 0, 0),
                _ => throw Invalid("semantic")
            };
            Write(bytes, field.Offset, value);
        }
        return bytes;
    }

    private static Layout Compile(ReflectionShaderStage stage)
    {
        if (stage.ProgramIndex is not (3 or 4 or 5 or 9 or 12 or 13)) throw Invalid("program");
        var cb = stage.Definitions.Buffers.Single(value => value.Name.Text == "$Globals");
        var names = stage.ProgramIndex switch
        {
            3 => new[] { "litDir", "litCol", "mW2S", "gv_info_out", "gv_info_in", "gv_probefield_org", "gv_probefield_pitch_size", "gv_tiledoct_pitch_octsize", "gv_probe_scale_offset" },
            4 => new[] { "gv_info_out", "gv_info_in", "gv_probefield_org", "gv_probefield_pitch_size", "gv_probefield_pitch_size_rv", "gv_tiledoct_pitch_octsize", "gv_tiledoct_pitch_octsize_rv", "gv_probe_scale_offset", "gv_indirect_weight", "gv_trace_bias" },
            5 => new[] { "gv_info_out", "gv_info_in", "gv_tiledoct_pitch_octsize", "gv_mip_info" },
            9 => new[] { "gv_info_out", "gv_info_in", "gv_tiledoct_pitch_octsize", "gv_tiledoct_pitch_octsize_rv" },
            12 => new[] { "gv_info_out", "gv_tiledoct_pitch_octsize" },
            _ => new[] { "gv_info_out", "gv_probefield_org", "gv_probefield_pitch_size", "gv_probefield_pitch_size_rv", "gv_tiledoct_pitch_octsize", "gv_tiledoct_pitch_octsize_rv", "gv_probe_scale_offset", "gv_indirect_weight", "gv_trace_bias" }
        };
        if (cb.BufferType != 0 || cb.Variables.Count != names.Length ||
            !cb.Variables.Select(value => value.Name.Text).Order().SequenceEqual(names.Order())) throw Invalid("globals");
        var fields = new List<Field>(); var ranges = new List<(int Start, int End)>();
        foreach (var variable in cb.Variables)
        {
            string name = variable.Name.Text; bool matrix = name == "mW2S";
            var type = stage.Definitions.Types.Single(value => value.Offset == variable.TypeOffset);
            int size = matrix ? 64 : 16;
            if (variable.ByteSize != size || variable.StartOffset < 0 || variable.StartOffset > cb.ByteSize - size ||
                type.Class != (matrix ? 2 : 1) || type.BaseType != 3 || type.Rows != (matrix ? 4 : 1) || type.Columns != 4 ||
                type.Elements != (name is "litDir" or "litCol" or "mW2S" ? 1 : 0) || type.Members.Count != 0 ||
                ranges.Any(range => variable.StartOffset < range.End && variable.StartOffset + size > range.Start))
                throw Invalid("globals_field." + name);
            ranges.Add((variable.StartOffset, variable.StartOffset + size)); fields.Add(new(name, variable.StartOffset));
        }
        return new(cb.ByteSize, fields.ToArray());
    }

    private static Vector4 Reciprocal(Vector4 value) => new(1f / value.X, 1f / value.Y, 1f / value.Z, 1f / value.W);
    private static void Write(Span<byte> bytes, int at, Vector4 value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(bytes[at..], value.X);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[(at + 4)..], value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[(at + 8)..], value.Z);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[(at + 12)..], value.W);
    }
    private static InvalidDataException Invalid(string field) => new("reflection.filter_parameters." + field);
}
