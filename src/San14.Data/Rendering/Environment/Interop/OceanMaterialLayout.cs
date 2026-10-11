using System.Buffers.Binary;

namespace San14.Data;

internal sealed record OceanMaterialFieldWrite(uint FieldId, uint Destination, uint SourceOffset, uint ByteCount);
internal sealed record OceanMaterialStageLayout(uint Stage, uint Slot, uint ByteLength,
    IReadOnlyList<OceanMaterialFieldWrite> Writes);

/// <summary>Selected shader destinations for the Native material's named values.
/// This is layout metadata, never an initial or current material snapshot.</summary>
internal sealed class OceanMaterialLayout(OceanMaterialStageLayout vertex, OceanMaterialStageLayout pixel)
{
    public void WriteNativeInputs(string directory)
    {
        OceanMaterialStageLayout[] stages = [vertex, pixel];
        byte[] bytes = new byte[8 + stages.Sum(stage => 16 + stage.Writes.Count * 16)];
        int at = 0;
        void Word(uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at, 4), value);
            at += 4;
        }
        Word(0x314C4D4F); // OML1: Ocean material layout.
        Word((uint)stages.Length);
        foreach (var stage in stages)
        {
            Word(stage.Stage); Word(stage.Slot); Word(stage.ByteLength); Word((uint)stage.Writes.Count);
            foreach (var write in stage.Writes)
            {
                Word(write.FieldId); Word(write.Destination); Word(write.SourceOffset); Word(write.ByteCount);
            }
        }
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "material-layout.bin"), bytes);
    }
}
