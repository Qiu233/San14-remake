using System.Buffers.Binary;
using System.Globalization;
using System.Reflection.PortableExecutable;
using System.Text;

namespace San14.Data;

public sealed record OriginalArmyMapPortraitNames(int AppearanceIndex, int ResolvedAppearanceIndex,
    string ResourceName, string SpriteName);
public readonly record struct OriginalArmyMapIconSelection(int SpriteId, int Palette);

/// <summary>Loaded stock portrait name mapping and the actual Army icon table.
/// Current world conditions choose a route outside this static source owner.</summary>
public sealed class OriginalArmyMapUiSources
{
    private readonly (int From, int To)[] _appearanceMap;
    private readonly Dictionary<int, string> _spriteNames;
    private OriginalArmyMapUiSources((int From, int To)[] appearanceMap, Dictionary<int, string> spriteNames)
        => (_appearanceMap, _spriteNames) = (appearanceMap, spriteNames);

    public static OriginalArmyMapUiSources LoadCurrentTc(OriginalGameRepository repository)
    {
        _ = OriginalCeHexWorldCoordinates.VerifyInstalledExecutable(repository.SourceDirectory, "TC");
        using var stream = File.OpenRead(Path.Combine(repository.SourceDirectory, "SAN14CE_TC.exe"));
        using var pe = new PEReader(stream);
        var bytes = pe.GetSectionData(0x1A23E2C).GetContent(0, 19 * 20).AsSpan();
        var appearance = new (int From, int To)[19];
        for (int i = 0; i < appearance.Length; i++)
            appearance[i] = (BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(i * 20 + 4, 4)),
                BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(i * 20, 4)));
        int[] icons = [0x1B0, 0x1B6, 0x1B7, 0x1B8, 0x1BA, 0x1BB, 0x1B9,
            0x1BC, 0x1BD, 0x1B1, 0x1B2, 0x1B3, 0x1B4, 0x3F0, 0x1B5,
            0x231, 0x232, 0x233, 0x234, 0x235, 0x236, 0x237, 0x238, 0x1D1];
        var names = new Dictionary<int, string>();
        foreach (int id in icons)
        {
            ulong va = BinaryPrimitives.ReadUInt64LittleEndian(pe.GetSectionData(0x1A88BF0 + id * 8).GetContent(0, 8).AsSpan());
            var text = pe.GetSectionData(checked((int)(va - 0x140000000ul))).GetContent(0, 128).AsSpan();
            int length = text.IndexOf((byte)0);
            if (length < 0) throw new InvalidDataException("Original Army icon source name is unterminated.");
            names.Add(id, Encoding.ASCII.GetString(text[..length]));
        }
        return new(appearance, names);
    }

    // 8ACA40/8AD680 both use2A9C00(index,0). This is name resolution,
    // not the complete bank10/DLC/edit/fallback or exact source-owner policy.
    public OriginalArmyMapPortraitNames PortraitNames(ushort appearanceIndex)
    {
        int resolved = appearanceIndex;
        foreach (var row in _appearanceMap)
            if (row.From == appearanceIndex) { resolved = row.To; break; }
        bool remapped = resolved != appearanceIndex;
        string number = resolved.ToString("D4", CultureInfo.InvariantCulture);
        return new(appearanceIndex, resolved,
            "texture_chara_center_" + number + (remapped || appearanceIndex == 55 ? "b" : ""),
            "face_" + number + (remapped ? "b" : "") + "_ctr");
    }

    public string IconName(OriginalArmyMapIconSelection selection) => _spriteNames[selection.SpriteId];

    // Caller has accepted the actual player/District/233A90 route.
    public static OriginalArmyMapIconSelection DistrictIcon(byte number11)
        => new(number11 is >= 2 and <= 8 ? 0x230 + number11 : 0x231, 9);
    public static OriginalArmyMapIconSelection SpecialPlayerIcon() => new(0x1D1, 9);

    // actualFormation is22B2D0's chosen water1C/land1B, not Core FormationId.
    public static OriginalArmyMapIconSelection FormationIcon(byte actualFormation, byte formationClass7E)
        => new(actualFormation switch
        {
            2 => 0x1B6, 3 => 0x1B7, 4 => 0x1B8,
            5 => 0x1BA, 6 => 0x1BB, 7 => 0x1B9, 8 => 0x1BC, 9 => 0x1BD,
            10 => 0x1B1, 11 => 0x1B2, 12 => 0x1B3, 13 => 0x1B4,
            14 => 0x3F0, >= 17 and <= 20 => 0x1B5, _ => 0x1B0
        }, formationClass7E == 2 ? 0x2C : 0x0A);
}
