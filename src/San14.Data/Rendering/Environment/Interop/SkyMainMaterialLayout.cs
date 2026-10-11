using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record SkyMainMaterialWrite(uint FieldId, uint Destination, uint SourceOffset, uint ByteCount);
internal sealed record SkyMainMaterialStage(uint Stage, uint Slot, uint ByteLength, IReadOnlyList<SkyMainMaterialWrite> Writes);

/// <summary>Actual selected shader destinations for the Main Sky's named values.
/// Unused declarations and padding have zero storage; no cached material is imported.</summary>
public sealed class SkyMainMaterialLayout
{
    private readonly SkyMainMaterialStage vertex, pixel;
    private SkyMainMaterialLayout(SkyMainMaterialStage vertex, SkyMainMaterialStage pixel)
        => (this.vertex, this.pixel) = (vertex, pixel);

    private static readonly (string Name, uint Id, int Size)[] vertexFields =
    [ ("g_mC2L", 0, 64), ("g_fProjFarZ", 1, 4), ("g_mL2G", 2, 64) ];
    private static readonly (string Name, uint Id, int Size)[] pixelFields =
    [
        ("fog", 3, 16), ("Scat", 4, 64), ("g_vViewPos", 5, 16),
        ("g_vSkyInfo0", 6, 16), ("g_vSkyInfo1", 7, 16),
        ("g_vSunRGB", 8, 16), ("g_vSunDir", 9, 16), ("g_vMoonRGB", 10, 16), ("g_vMoonDir", 11, 16),
        ("g_mMoonFace", 12, 64), ("g_vSunScreenPos", 13, 16), ("g_vMoonScreenPos", 14, 16),
        ("g_vAltitudeInfo", 15, 16), ("g_vStableDim", 16, 16), ("g_vClampValue", 17, 16),
    ];

    internal static SkyMainMaterialLayout Import(DxbcResourceDefinitions vertex, DxbcResourceDefinitions pixel)
        => new(ImportStage(vertex, 0xFFFE, 1, vertexFields), ImportStage(pixel, 0xFFFF, 0, pixelFields));

    private static SkyMainMaterialStage ImportStage(DxbcResourceDefinitions definitions, ushort shaderType,
        uint stage, (string Name, uint Id, int Size)[] fields)
    {
        if (definitions.RawShaderType != shaderType || definitions.Buffers.Count != 1)
            throw Unsupported("shader stage or constant buffers");
        var buffer = definitions.Buffers[0];
        if (buffer.BufferType != 0 || buffer.ByteSize <= 0 || buffer.ByteSize > 65536 || buffer.ByteSize % 16 != 0)
            throw Unsupported("constant buffer storage");
        if (buffer.Variables.Select(value => value.Name.Text).Distinct().Count() != buffer.Variables.Count)
            throw Unsupported("duplicate constant names");
        var writes = new List<SkyMainMaterialWrite>();
        foreach (var variable in buffer.Variables)
        {
            if ((variable.Flags & 2) == 0) continue;
            var field = fields.SingleOrDefault(value => value.Name == variable.Name.Text);
            if (field.Name is null) throw Unsupported($"live constant {variable.Name.Text}");
            var type = definitions.Types.Single(value => value.Offset == variable.TypeOffset);
            (int Class, int Rows, int Columns, int Elements) shape = field.Name switch
            {
                "g_mC2L" or "g_mL2G" or "g_mMoonFace" => (2, 4, 4, 0),
                "Scat" => (1, 1, 4, 4),
                _ => (field.Size == 4 ? 0 : 1, 1, field.Size / 4, 0),
            };
            if (variable.ByteSize != field.Size || variable.StartOffset % 4 != 0 ||
                type.BaseType != 3 || type.Class != shape.Class || type.Rows != shape.Rows ||
                type.Columns != shape.Columns || type.Elements != shape.Elements || type.Members.Count != 0)
                throw Unsupported($"typed constant {field.Name}");
            writes.Add(new(field.Id, (uint)variable.StartOffset, 0, (uint)field.Size));
        }
        var ordered = writes.OrderBy(value => value.Destination).ToArray();
        for (int index = 1; index < ordered.Length; index++)
            if (ordered[index - 1].Destination + ordered[index - 1].ByteCount > ordered[index].Destination)
                throw Unsupported("overlapping live constants");
        var binding = definitions.Bindings.SingleOrDefault(value => value.Name.Text == buffer.Name.Text && value.InputType == 0);
        if (binding is null || binding.Dimension != 0 || binding.BindCount != 1 || binding.BindPoint >= 14 ||
            definitions.Bindings.Count(value => value.InputType == 0) != 1)
            throw Unsupported("constant buffer binding");
        return new(stage, binding.BindPoint, (uint)buffer.ByteSize, Array.AsReadOnly(ordered));
    }

    public void WriteNativeInputs(string directory)
    {
        SkyMainMaterialStage[] stages = [vertex, pixel];
        byte[] bytes = new byte[8 + stages.Sum(stage => 16 + stage.Writes.Count * 16)];
        int at = 0;
        void Word(uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at, 4), value);
            at += 4;
        }
        Word(0x314C4D53); // SML1: Sky material layout.
        Word((uint)stages.Length);
        foreach (var stage in stages)
        {
            Word(stage.Stage); Word(stage.Slot); Word(stage.ByteLength); Word((uint)stage.Writes.Count);
            foreach (var write in stage.Writes)
            { Word(write.FieldId); Word(write.Destination); Word(write.SourceOffset); Word(write.ByteCount); }
        }
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "material-layout.bin"), bytes);
    }

    /// <summary>Pack the same named material consumed by Native, using this
    /// selection's imported destinations. This also serves offline comparisons.</summary>
    public byte[] Pack(Sky2DrawInputs material, uint stageCode)
    {
        var stage = stageCode switch
        {
            1 => vertex,
            0 => pixel,
            _ => throw new ArgumentOutOfRangeException(nameof(stageCode)),
        };
        byte[] values = new byte[SkyMainMaterialEncoding.ByteLength];
        SkyMainMaterialEncoding.Write(material, values);
        byte[] packed = new byte[stage.ByteLength];
        foreach (var write in stage.Writes)
        {
            int source = 4;
            for (int field = 0; field < write.FieldId; field++)
                source += SkyMainMaterialEncoding.FieldByteLengths[field];
            values.AsSpan(source + (int)write.SourceOffset, (int)write.ByteCount)
                .CopyTo(packed.AsSpan((int)write.Destination));
        }
        return packed;
    }

    private static NotSupportedException Unsupported(string detail)
        => new($"Main Sky material cannot supply the selected {detail}.");
}
