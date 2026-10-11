using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record SkyStarMaterialWrite(uint Field, uint Destination, uint Bytes);
internal sealed record SkyStarMaterialStage(uint Stage, uint Slot, uint Bytes, IReadOnlyList<SkyStarMaterialWrite> Writes);

/// <summary>Named Star values mapped to the selected three-stage interfaces.
/// Padding and unused declarations have storage but no invented producer.</summary>
public sealed class SkyStarMaterialLayout
{
    private readonly SkyStarMaterialStage[] stages;
    private SkyStarMaterialLayout(SkyStarMaterialStage[] stages) => this.stages = stages;
    private static readonly (string Name, uint Id, int Bytes)[] fields =
    [
        ("g_mE2C", 0, 64), ("g_vPhaze", 1, 16), ("g_vTable", 2, 256),
        ("g_vStarInfo", 3, 16), ("g_vPointScale", 4, 16),
        ("g_vStarShowerInfo0", 5, 16), ("g_vStarShowerInfo1", 6, 16),
    ];

    internal static SkyStarMaterialLayout Import(DxbcResourceDefinitions vertex,
        DxbcResourceDefinitions geometry, DxbcResourceDefinitions pixel)
        => new([ImportStage(vertex, 1, 0xFFFE), ImportStage(geometry, 2, 0x4753), ImportStage(pixel, 0, 0xFFFF)]);

    /// <summary>Encode the controller-point pass through its imported named
    /// destinations. WORK supplies its own camera and retained draw values.</summary>
    public SkyStarConstantPacket EncodeController(Matrix4x4 eyeToClip, Vector4 phase,
        ReadOnlySpan<float> twinkleTable, Vector4 starInfo, Vector4 pointScale)
    {
        if (twinkleTable.Length != 64)
            throw new ArgumentException("Star twinkle table requires sixteen float4 entries.", nameof(twinkleTable));
        var values = new byte[368];
        float[] matrix = [eyeToClip.M11, eyeToClip.M12, eyeToClip.M13, eyeToClip.M14,
            eyeToClip.M21, eyeToClip.M22, eyeToClip.M23, eyeToClip.M24,
            eyeToClip.M31, eyeToClip.M32, eyeToClip.M33, eyeToClip.M34,
            eyeToClip.M41, eyeToClip.M42, eyeToClip.M43, eyeToClip.M44];
        for (int index = 0; index < matrix.Length; index++) Put(index * 4, matrix[index]);
        Vector(64, phase);
        for (int index = 0; index < twinkleTable.Length; index++) Put(80 + index * 4, twinkleTable[index]);
        Vector(336, starInfo); Vector(352, pointScale);
        byte[] Encode(uint stage)
        {
            var layout = stages.Single(value => value.Stage == stage);
            var bytes = new byte[layout.Bytes];
            foreach (var write in layout.Writes)
            {
                int source = write.Field switch { 0 => 0, 1 => 64, 2 => 80, 3 => 336, 4 => 352,
                    _ => throw new NotSupportedException("Controller Star pass cannot produce generated-stream material fields.") };
                values.AsSpan(source, checked((int)write.Bytes)).CopyTo(bytes.AsSpan(checked((int)write.Destination)));
            }
            return bytes;
        }
        return new(Encode(1), Encode(2), Encode(0));
        void Put(int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(values.AsSpan(offset, 4), value);
        void Vector(int offset, Vector4 value)
        { Put(offset, value.X); Put(offset + 4, value.Y); Put(offset + 8, value.Z); Put(offset + 12, value.W); }
    }

    private static SkyStarMaterialStage ImportStage(DxbcResourceDefinitions definitions, uint stage, uint shaderType)
    {
        if (definitions.RawShaderType != shaderType || definitions.Buffers.Count != 1)
            throw new NotSupportedException("Star material requires one actual constant buffer per shader stage.");
        var buffer = definitions.Buffers[0];
        if (buffer.BufferType != 0 || buffer.ByteSize <= 0 || buffer.ByteSize > 65536 || buffer.ByteSize % 16 != 0)
            throw new NotSupportedException("Star constant buffer storage is unsupported.");
        var writes = new List<SkyStarMaterialWrite>();
        foreach (var variable in buffer.Variables)
        {
            if ((variable.Flags & 2) == 0) continue;
            var field = fields.SingleOrDefault(value => value.Name == variable.Name.Text);
            if (field.Name is null) throw new NotSupportedException($"Star material has no producer for {variable.Name.Text}.");
            bool validStage = stage switch
            {
                1 => field.Id is 0 or 1 or 2 or 3 or 5 or 6,
                2 => field.Id == 4,
                0 => field.Id == 3,
                _ => false,
            };
            var type = definitions.Types.Single(value => value.Offset == variable.TypeOffset);
            bool matrix = field.Id == 0, table = field.Id == 2;
            if (!validStage || variable.StartOffset < 0 || variable.StartOffset % 4 != 0 || variable.ByteSize != field.Bytes ||
                (long)variable.StartOffset + field.Bytes > buffer.ByteSize || type.BaseType != 3 ||
                type.Class != (matrix ? 2 : 1) || type.Rows != (matrix ? 4 : 1) || type.Columns != 4 ||
                type.Elements != (table ? 16 : 0) || type.Members.Count != 0)
                throw new NotSupportedException($"Star material field {field.Name} has an unsupported typed layout.");
            writes.Add(new(field.Id, (uint)variable.StartOffset, (uint)field.Bytes));
        }
        var ordered = writes.OrderBy(value => value.Destination).ToArray();
        for (int index = 1; index < ordered.Length; index++)
            if (ordered[index - 1].Destination + ordered[index - 1].Bytes > ordered[index].Destination)
                throw new NotSupportedException("Star used material fields overlap.");
        var bindings = definitions.Bindings.Where(value => value.InputType == 0).ToArray();
        if (bindings.Length != 1 || bindings[0].Name.Text != buffer.Name.Text || bindings[0].BindCount != 1 ||
            bindings[0].BindPoint >= 14 || bindings[0].Dimension != 0)
            throw new NotSupportedException("Star constant buffer binding is unsupported.");
        return new(stage, bindings[0].BindPoint, (uint)buffer.ByteSize, Array.AsReadOnly(ordered));
    }

    internal static void WriteNativeInputs(string directory, IReadOnlyList<SkyStarProgramSelection> passes)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(0x314C5453u); // STL1: Star typed material layout.
        writer.Write((uint)passes.Count);
        for (int pass = 0; pass < passes.Count; pass++)
        {
            writer.Write((uint)pass);
            writer.Write((uint)passes[pass].MaterialLayout.stages.Length);
            foreach (var stage in passes[pass].MaterialLayout.stages)
            {
                writer.Write(stage.Stage); writer.Write(stage.Slot); writer.Write(stage.Bytes); writer.Write((uint)stage.Writes.Count);
                foreach (var write in stage.Writes)
                { writer.Write(write.Field); writer.Write(write.Destination); writer.Write(0u); writer.Write(write.Bytes); }
            }
        }
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "material-layout.bin"), stream.ToArray());
    }
}
