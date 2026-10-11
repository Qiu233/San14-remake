using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Legacy runtime record import. Only consumed fields enter the domain;
/// unknown padding and unconsumed resource bytes are not current game state.</summary>
public static class CommonRenderEncoding
{
    public static MapCommonRenderState ReadCurrentState(long frameNumber,
        string settingsOwnerIdentity, string commonTableOwnerIdentity, string selectedViewOwnerIdentity,
        ReadOnlyMemory<byte>? completeCurrentAuxiliary460, ReadOnlyMemory<byte>? completeCurrentAmbientA40,
        string producerProvenance)
    {
        MapCommonRenderState.ValidateIdentity(frameNumber, settingsOwnerIdentity, commonTableOwnerIdentity, selectedViewOwnerIdentity, producerProvenance);
        return new(frameNumber, settingsOwnerIdentity, commonTableOwnerIdentity, selectedViewOwnerIdentity,
            producerProvenance, CommonRenderPublicationKind.Runtime,
            completeCurrentAuxiliary460 is { } auxiliary ? CommonRenderEncoding.ReadAuxiliary(auxiliary.Span) : null,
            completeCurrentAmbientA40 is { } ambient ? CommonRenderEncoding.ReadAmbient(ambient.Span) : null);
    }

    public static CommonAuxiliaryState ReadAuxiliary(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 0x460) throw new InvalidDataException("Common auxiliary record length.");
        return new(Word(bytes, 8), (CommonLightingOptions)Word(bytes, 0xC), Word(bytes, 0x404), bytes[0x3F2], bytes[0x400], bytes[0x12],
            BinaryPrimitives.ReadInt32LittleEndian(bytes[0x3F4..]), Word(bytes, 0x380),
            new(Word(bytes, 0xC8), Word(bytes, 0xCC), Word(bytes, 0xD0), Word(bytes, 0xD4)),
            SuppressOceanDrawing: (Word(bytes, 0x10) & 1u) != 0,
            SkyAlphaBlend: DecodeSkyAlphaBlend(Word(bytes, 0x3BC)));
    }

    public static AmbientMapSelection ReadAmbient(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 0xA40) throw new InvalidDataException("Common ambient record length.");
        return new(ClassifyAmbient(Word(bytes, 0x4C8)), ClassifyAmbient(Word(bytes, 0x4CC)), ClassifyAmbient(Word(bytes, 0x5F8)));
    }

    public static AmbientMapVariant ClassifyAmbient(uint mode)
    {
        uint variant = mode & 0x7FFFFFFF;
        return variant is >= 1 and <= 3 ? (AmbientMapVariant)(variant - 1) : AmbientMapVariant.Unsupported;
    }

    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);

    internal static SkyAlphaBlendMode DecodeSkyAlphaBlend(uint sourceMode) => sourceMode switch
    {
        0 => SkyAlphaBlendMode.Replace,
        2 => SkyAlphaBlendMode.Add,
        _ => SkyAlphaBlendMode.Maximum,
    };
}
