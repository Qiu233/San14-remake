using San14.Data.Formats;

namespace San14.Data;

public static partial class Sky2NullTextureAdapter
{
    public static void ValidateNullTexturePacket(Sky2NullTextureSpirv adapter,
        IReadOnlyList<Sky2NullTextureBinding> actualProgramBindings,
        DxbcResourceDefinitions definitions, ReadOnlySpan<byte> completePixelPacket)
        => ReadLoadedPacketInterface(adapter, definitions)
            .Validate(actualProgramBindings, definitions, completePixelPacket);

    public static Sky2LoadedNullTexturePacketInterface ReadLoadedPacketInterface(
        Sky2NullTextureSpirv adapter, DxbcResourceDefinitions definitions)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        adapter.NullTextureBindings[0].Validate();
        if (!ReferenceEquals(definitions, adapter.SourceDefinitions))
            throw new InvalidDataException("Sky null sampling packet belongs to a different shader import.");
        return adapter.PacketInterface;
    }

    private static Sky2LoadedNullTexturePacketInterface PreparePacketInterface(
        IReadOnlyList<Sky2NullTextureBinding> bindings, DxbcResourceDefinitions definitions)
    {
        var buffer = definitions.Buffers.Single();
        var presence = buffer.Variables.Single(v => v.Name.Text == "g_vSkyInfo1");
        RequireFloat4(presence);
        var moon = buffer.Variables.SingleOrDefault(v => v.Name.Text == "g_vMoonRGB" && (v.Flags & 2) != 0);
        if (moon is not null) RequireFloat4(moon);
        int[] lanes = bindings.Select(binding => binding.Role switch
        {
            SkySamplingRole.Earth => 0,
            SkySamplingRole.Moon => 1,
            SkySamplingRole.MilkyWay => 2,
            _ => throw new InvalidDataException("Sky null sampling requires an Earth, Moon or Milky Way binding.")
        }).ToArray();
        return new(bindings, definitions, buffer.ByteSize, presence.StartOffset, lanes, moon?.StartOffset);

        void RequireFloat4(DxbcRdefVariable field)
        {
            var type = definitions.Types.Single(t => t.Offset == field.TypeOffset);
            if (field.ByteSize != 16 || field.StartOffset < 0 || field.StartOffset > buffer.ByteSize - 16 ||
                type.Class != 1 || type.BaseType != 3 || type.Rows != 1 || type.Columns != 4 || type.Elements != 0)
                throw new InvalidDataException("Sky sampling flags and used Moon color require bounded float4 fields.");
        }
    }
}
