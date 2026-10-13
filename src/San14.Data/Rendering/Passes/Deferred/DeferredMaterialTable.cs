using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public readonly record struct DeferredMaterialEntry(uint FlagsInput, uint RimInput, uint RampInput, uint HighInput)
{
    /// <summary>Original A00760/9FF740 packer: only the first three inputs narrow to bytes.</summary>
    public uint PackedWord => (uint)(byte)FlagsInput | ((uint)(byte)RimInput << 8) |
        ((uint)(byte)RampInput << 16) | unchecked(HighInput << 24);
}

public sealed record LoadedDeferredMaterialTable(LoadedShaderContainer Shader,
    G1ShaderConfigurationRecord Layout, G1ShaderConfigurationParameter SlotParameter,
    DxbcRdefBinding PixelBinding, byte[] Bytes, int EntryCount, bool ActiveBindingObserved);

/// <summary>Original successful-new-table construction. Entry0 is reserved zero;
/// explicitly supplied external source rows begin at1. Never imports an existing cache.</summary>
public static class DeferredMaterialTable
{
    public static LoadedDeferredMaterialTable Create(LoadedShaderContainer shader, int explicitCombination,
        IReadOnlyList<DeferredMaterialEntry> externalSourceEntries)
    {
        ArgumentNullException.ThrowIfNull(shader); ArgumentNullException.ThrowIfNull(externalSourceEntries);
        G1ShaderConfigurations configurations = shader.ReadConfigurations();
        // Native lookup compares both hash and complete name, and takes the first matching record.
        G1ShaderConfigurationRecord? layout = configurations.Records.FirstOrDefault(r =>
            r.NameHash == 0x2D8DB7D5 && r.Name == "GBufferLayout");
        var slot = layout?.Find(0x2A9DED55);
        if (layout is null || slot is null || slot.RawValue == uint.MaxValue)
            throw new InvalidDataException("Original material table requires an explicit GBufferLayout MaterialTableSlot.");
        var sourceBindings = shader.ReadG1ResourceBindings(explicitCombination);
        var stages = sourceBindings.Combination.Stages.Where(s => s.StageCode == 0 && s.Entry is not null).ToArray();
        if (stages.Length != 1) throw new InvalidDataException("Material table requires one explicit source pixel program.");
        var definitions = shader.ReadResourceDefinitions(stages[0].Entry!.ProgramIndex);
        var bindings = definitions.Bindings.Where(b => b.InputType == 2 && b.Name.Text == "tMaterialTable").ToArray();
        if (bindings.Length != 1 || bindings[0].BindPoint != slot.RawValue || bindings[0].Dimension != 1 ||
            bindings[0].ReturnType != 4 || bindings[0].BindCount != 1)
            throw new InvalidDataException("Material table source slot differs from the pixel uint-buffer declaration.");
        int count = checked(externalSourceEntries.Count + 1);
        byte[] bytes = new byte[checked(count * 4)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, 4), new DeferredMaterialEntry(0, 0, 0, 0).PackedWord);
        for (int i = 0; i < externalSourceEntries.Count; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan((i + 1) * 4, 4), externalSourceEntries[i].PackedWord);
        return new(shader, layout, slot, bindings[0], bytes, count, false);
    }
}
