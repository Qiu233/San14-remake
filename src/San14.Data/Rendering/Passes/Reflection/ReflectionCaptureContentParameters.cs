using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>BD4820 parameters for FD7160 capture conversion and FD7FA0 sky.
/// These source stages consume actual rendered cubes and field storage;
/// they do not select a Scene, create a capture View or attest filtered SH.</summary>
public static class ReflectionCaptureContentParameters
{
    private sealed record Layout(int Bytes, int Output, int Input, int Tiles);
    private static readonly ConditionalWeakTable<ReflectionShaderStage, Layout> layouts = new();

    public static byte[] Capture(ReflectionShaderStage stage, ReflectionCaptureFieldResourcePlan field,
        int sourceOrdinal, uint channel, uint actualCubeWidth)
    {
        if (stage.ProgramIndex is not (7 or 8) || channel > 2 || actualCubeWidth == 0)
            throw Invalid("capture_inputs");
        var layout = layouts.GetValue(stage, Compile);
        uint columns = field.AtlasWidth / field.Field.TileSize;
        uint rows = field.AtlasHeight / field.Field.TileSize;
        uint layers = field.CaptureArrayLayers / 3;
        if (columns == 0 || rows == 0 || layers == 0) throw Invalid("atlas_divisor");
        Vector4 output = Vector4.Zero;
        if (sourceOrdinal >= 0)
        {
            int plane = checked((int)(columns * rows));
            uint x = unchecked((uint)(sourceOrdinal % (int)columns) * field.Field.TileSize);
            uint y = unchecked((uint)((sourceOrdinal % plane) / (int)columns) * field.Field.TileSize);
            int slice = sourceOrdinal / plane % (int)layers;
            output = new((float)x, (float)y, (float)slice + (float)(channel * layers), 0);
        }
        return Encode(layout, output, new(actualCubeWidth, 0, 0, 0),
            new(columns, rows, layers, field.Field.TileSize));
    }

    public static byte[] Sky(ReflectionShaderStage stage, ReflectionCaptureFieldResourcePlan field, uint layer)
    {
        if (stage.ProgramIndex != 14 || layer >= field.AtlasLayers) throw Invalid("sky_inputs");
        var layout = layouts.GetValue(stage, Compile);
        uint columns = field.AtlasWidth / field.Field.TileSize;
        uint rows = field.AtlasHeight / field.Field.TileSize;
        if (columns == 0 || rows == 0) throw Invalid("atlas_divisor");
        // BD7810 binds the octmap as1430 for this stage. Its actual width
        // divided by the integer tile-column count supplies this float W.
        return Encode(layout, new(layer, 0, 0, 0), new(layer, 0, 0, 0),
            new(columns, rows, field.CaptureArrayLayers / 3, (float)field.AtlasWidth / columns));
    }

    private static Layout Compile(ReflectionShaderStage stage)
    {
        if (stage.StageCode != 5 || stage.ProgramIndex is not (7 or 8 or 14)) throw Invalid("stage");
        var cb = stage.Definitions.Buffers.Single(buffer => buffer.Name.Text == "$Globals");
        bool input = stage.ProgramIndex != 7;
        if (cb.BufferType != 0 || cb.ByteSize != (input ? 48 : 32) || cb.Variables.Count != (input ? 3 : 2))
            throw Invalid("globals");
        int Find(string name)
        {
            var variable = cb.Variables.Single(value => value.Name.Text == name);
            var type = stage.Definitions.Types.Single(value => value.Offset == variable.TypeOffset);
            if (variable.ByteSize != 16 || variable.StartOffset < 0 || variable.StartOffset > cb.ByteSize - 16 ||
                type.Class != 1 || type.BaseType != 3 || type.Rows != 1 || type.Columns != 4 ||
                type.Elements != 0 || type.Members.Count != 0) throw Invalid("globals_field");
            return variable.StartOffset;
        }
        var result = new Layout(cb.ByteSize, Find("gv_info_out"), input ? Find("gv_info_in") : -1,
            Find("gv_tiledoct_pitch_octsize"));
        int[] offsets = input ? [result.Output, result.Input, result.Tiles] : [result.Output, result.Tiles];
        if (!offsets.Order().SequenceEqual(Enumerable.Range(0, offsets.Length).Select(index => index * 16)))
            throw Invalid("globals_ranges");
        return result;
    }

    private static byte[] Encode(Layout layout, Vector4 output, Vector4 input, Vector4 tiles)
    {
        var bytes = new byte[layout.Bytes];
        void Write(int at, Vector4 value)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at), value.X);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at + 4), value.Y);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at + 8), value.Z);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at + 12), value.W);
        }
        Write(layout.Output, output); if (layout.Input >= 0) Write(layout.Input, input); Write(layout.Tiles, tiles);
        return bytes;
    }

    private static InvalidDataException Invalid(string field) => new("reflection.capture_parameters." + field);
}
