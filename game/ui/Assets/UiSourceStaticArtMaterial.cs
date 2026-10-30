using System.Buffers.Binary;
using San14.Data.Formats;

/// <summary>The authored, ordinary single-texture Type1 material. Dynamic
/// colour and alpha writers retain ownership of their corresponding setters.</summary>
internal sealed record UiSourceStaticArtMaterial(UiSourceSprite.Blend Blend, uint ColorScale, uint ColorOffset, float Saturation, byte Alpha)
{
    internal static UiSourceStaticArtMaterial Read(IReadOnlyList<LcskNodeRecord> nodes, int index)
    {
        var node = nodes[index];
        var bytes = node.RawBytes.AsSpan();
        if (node.Type != 1 || bytes.Length != 144 ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[114..]) != 1 ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[116..]) >> 24 != 0 ||
            bytes[123] != 255 ||
            !float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(bytes[124..])) ||
            BinaryPrimitives.ReadSingleLittleEndian(bytes[128..]) != 1 ||
            bytes[92] != 7 || bytes[93] != 0)
            throw new InvalidDataException($"Static UI artwork requires its ordinary material: {node.Name}.");
        UiSourceSprite.Blend blend = BinaryPrimitives.ReadUInt32LittleEndian(bytes[84..]) switch
        {
            0x00554400 => UiSourceSprite.Blend.Ordinary,
            0x00114400 => UiSourceSprite.Blend.Additive,
            _ => throw new InvalidDataException($"Static UI artwork has an unsupported blend: {node.Name}.")
        };
        return new(blend, BinaryPrimitives.ReadUInt32LittleEndian(bytes[120..]),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[116..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[124..]), EffectiveAlpha(index));

        byte EffectiveAlpha(int current)
        {
            var pane = nodes[current];
            int common = CommonOffset(pane);
            float alpha = BinaryPrimitives.ReadSingleLittleEndian(pane.RawBytes.AsSpan(common + 8, 4));
            if (!float.IsFinite(alpha) || alpha < 0 || alpha > 1)
                throw new InvalidDataException($"Static UI artwork has invalid pane alpha: {pane.Name}.");
            // B12630 truncates the authored float to the common alpha byte.
            byte own = (byte)(alpha * 255f);
            if (pane.ParentIndex is not int parent ||
                (BinaryPrimitives.ReadUInt32LittleEndian(nodes[parent].RawBytes.AsSpan(CommonOffset(nodes[parent]), 4)) & 1) == 0)
                return own;
            // ACB280 inherits the parent's refreshed byte, with this float32
            // multiply and truncation, before AF1330 publishes the draw state.
            return (byte)(EffectiveAlpha(parent) / 255f * (own / 255f) * 255f);
        }
    }

    private static int CommonOffset(LcskNodeRecord node) => node.Type switch
    {
        0 when node.RawBytes.Length == 88 => 64,
        1 when node.RawBytes.Length == 144 => 80,
        _ => throw new InvalidDataException($"Static UI artwork requires a supported alpha ancestor: {node.Name}.")
    };
}
