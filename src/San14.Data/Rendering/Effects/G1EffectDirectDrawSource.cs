using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Projects the current Direct generic's material declaration for the
/// normal GPU pipeline consumer. Particle vertices and draw arguments remain
/// owned by its GPU allocation.</summary>
public static class G1EffectDirectDrawSource
{
    public static bool IsAdmitted(ReadOnlyMemory<byte> currentGenericBytes, float moduleOpacity) =>
        !(moduleOpacity < 1.1920929e-5f) || !G1EffectDrawWriter.HasOpacityController(currentGenericBytes.Span);

    /// <summary>Returns material metadata only. DynamicCount and offset are
    /// zero because Direct draws bind the allocation's GPU vertex buffer and
    /// indirect arguments. Consume through BuildDirect, not a CPU draw batch.
    /// SubmissionOrdinal follows the owner's system/generic traversal order;
    /// Direct submission does not use the ordinary CPU depth buckets.</summary>
    public static G1EffectDrawPacket ReadMetadata(LoadedMapEffectResources resources,
        G1EffectParticleDeclaration declaration, ReadOnlyMemory<byte> currentGenericBytes,
        long submissionOrdinal = 0)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(declaration);
        if (!declaration.UsesDirectBirth || declaration.Record.Type is not (0 or 1))
            throw new NotSupportedException("Direct sprite draw metadata requires a Direct generic of type zero or one.");
        ReadOnlySpan<byte> current = currentGenericBytes.Span;
        if (current.Length < 0x18 || Word(current, 4) != declaration.Record.Type.Value ||
            (Word(current, 8) & 0x200) == 0)
            throw new InvalidDataException("Current Direct generic is incomplete or differs from its allocation declaration.");
        int layout = checked(0x14 + BinaryPrimitives.ReadInt32LittleEndian(current[0x14..]));
        if (layout < 0 || layout > current.Length - 0x40)
            throw new InvalidDataException("Direct generic layout escapes its current record.");
        int stride = BinaryPrimitives.ReadInt32LittleEndian(current[layout..]);
        if (stride < 0x40 || stride > current.Length - layout)
            throw new InvalidDataException("Direct generic layout has an invalid particle stride.");

        // Direct submission reads the layout header dispatch at +4, not the
        // particle render-payload type at +20. The shared CPU writer's prepared
        // D8..100 fields belong to a different owner and are never read here.
        ushort dispatch = Half(current, layout + 4);
        if (dispatch is not (0 or 2))
            throw new NotSupportedException($"Direct layout dispatch {dispatch} requires its own GPU draw consumer.");
        ReadOnlySpan<byte> material = current.Slice(layout + 0x20, 0x20);
        ushort sourceFlags = Half(material, 2), renderEnum = Half(material, 0xA);
        int pass = renderEnum switch
        {
            7 => 7,
            19 => 17,
            26 => 22,
            34 => 18,
            _ => throw new NotSupportedException($"Direct sprite render enum {renderEnum} requires its original pass mapping.")
        };
        int configuration = Half(material, 8) & 7;
        int texture = Half(material, 0xC);
        if (!resources.TextureSlots.ContainsKey(texture))
            throw new InvalidDataException($"Direct effect texture slot {texture} is not declared.");

        // The Direct callback supplies its own flags. They differ from the
        // ordinary CPU particle writer's prepared flags, including bit 0x800.
        uint flags = 1u | ((uint)sourceFlags >> 8 & 1) << 2 |
            ((uint)sourceFlags >> 11 & 1) << 5;
        uint header = 3u | (uint)configuration << 3;
        byte alpha = (sourceFlags & 8) != 0 ? unchecked((byte)~material[0x13]) : (byte)0;
        return new(resources, declaration.Record.FileOffset, declaration.Record.Type.Value,
            configuration, pass, header, flags, texture, alpha, 0, 32, 0, null,
            0, submissionOrdinal, currentGenericBytes.Slice(layout + 0x20, 0x20));
    }

    private static uint Word(ReadOnlySpan<byte> bytes, int at) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static ushort Half(ReadOnlySpan<byte> bytes, int at) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes[at..]);
}
