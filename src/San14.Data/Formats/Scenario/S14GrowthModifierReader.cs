using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data.Formats;

public sealed record S14GrowthStateDefinition(int Slot, byte RawAdvantage30, byte[] RawBytes);
public sealed record S14GrowthAdvantageDefinition(int Slot, byte RequiredCitiesB3,
    byte[] EffectSlotsB4, byte[] MagnitudesB6, byte[] RawBytes);
public sealed record S14GrowthModifierCatalog(string DecodedSha256,
    IReadOnlyList<S14GrowthStateDefinition> States, IReadOnlyList<S14GrowthAdvantageDefinition> Advantages)
{
    public IReadOnlyList<string> RegionNames { get; init; } = [];
}

/// <summary>Paired CE CStateData section0 and CAdvantageData section35.
/// Full rows are retained; only actual growth-consumer fields are projected.</summary>
public static class S14GrowthModifierReader
{
    public static S14GrowthModifierCatalog Read(S14Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        byte[] bytes = document.DecodedPayload;
        if (document.FormatMarker != 0x66) throw new InvalidDataException("Growth modifiers require CE fixed data.");
        Header(0, 0x66, 12); Header(248, 0, 23); Header(992, 1, 52);
        Header(352044, 35, 18); Header(355076, 36, 5);
        var states = Enumerable.Range(0, 23).Select(slot => {
            byte[] raw = bytes.AsSpan(256 + slot * 32, 32).ToArray();
            return new S14GrowthStateDefinition(slot, raw[31], raw);
        }).ToArray();
        var advantages = Enumerable.Range(0, 18).Select(slot => {
            byte[] raw = bytes.AsSpan(352052 + slot * 168, 168).ToArray();
            return new S14GrowthAdvantageDefinition(slot, raw[163], raw[164..166], raw[166..168], raw);
        }).ToArray();
        return new(Convert.ToHexString(SHA256.HashData(bytes)), states, advantages)
        {
            RegionNames = Array.AsReadOnly(Enumerable.Range(0, 12).Select(slot =>
                S14ForceNameDefinitionReader.ReadLiteralUnits(bytes.AsSpan(8 + slot * 20, 6))).ToArray())
        };
        void Header(int offset, int section, int count)
        {
            if (offset < 0 || offset > bytes.Length - 8 ||
                BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset)) != section ||
                BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 4)) != count)
                throw new InvalidDataException($"Growth modifier fixed section{section} boundary differs.");
        }
    }
}
