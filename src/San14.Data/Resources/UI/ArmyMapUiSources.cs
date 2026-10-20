using System.Globalization;

namespace San14.Data;

public sealed record ArmyMapPortraitNames(int AppearanceIndex, int ResolvedAppearanceIndex,
    string ResourceName, string SpriteName);
public readonly record struct ArmyMapIconSelection(int SpriteId, int Palette);

/// <summary>Loaded stock portrait name mapping and the actual Army icon table.
/// Current world conditions choose a route outside this static source owner.</summary>
public sealed class ArmyMapUiSources
{
    private readonly (int From, int To)[] _appearanceMap;
    private readonly Dictionary<int, string> _spriteNames;
    private ArmyMapUiSources((int From, int To)[] appearanceMap, Dictionary<int, string> spriteNames)
        => (_appearanceMap, _spriteNames) = (appearanceMap, spriteNames);

    public static ArmyMapUiSources Create()
    {
        // Appearance aliases and icon names are rules; the named sprites remain
        // in the original resource archive and are loaded by the UI asset owner.
        (int From, int To)[] appearance =
        [
            (2197,7), (2196,36), (2195,45), (2194,2), (2193,1),
            (2192,9), (2191,84), (2190,60), (2189,44), (2188,49),
            (2187,14), (2186,25), (2185,37), (2184,64), (2183,63),
            (2182,91), (2181,30), (2180,117), (2179,17)
        ];
        var names = new Dictionary<int, string>
        {
            [0x1A2]="000_cmn_icon_jin_1", [0x1A8]="000_cmn_icon_jin_2",
            [0x1A9]="000_cmn_icon_jin_3", [0x1AA]="000_cmn_icon_jin_4",
            [0x1AC]="000_cmn_icon_jin_6", [0x1AD]="000_cmn_icon_jin_7",
            [0x1AB]="000_cmn_icon_jin_5", [0x1AE]="000_cmn_icon_jin_8",
            [0x1AF]="000_cmn_icon_jin_9", [0x1A3]="000_cmn_icon_jin_10",
            [0x1A4]="000_cmn_icon_jin_11", [0x1A5]="000_cmn_icon_jin_12",
            [0x1A6]="000_cmn_icon_jin_13", [0x3EF]="000_cmn_icon_jin_15",
            [0x1A7]="000_cmn_icon_jin_14",
            [0x1B0]="000_cmn_icon_jin_fuchi_1", [0x1B6]="000_cmn_icon_jin_fuchi_2",
            [0x1B7]="000_cmn_icon_jin_fuchi_3", [0x1B8]="000_cmn_icon_jin_fuchi_4",
            [0x1BA]="000_cmn_icon_jin_fuchi_6", [0x1BB]="000_cmn_icon_jin_fuchi_7",
            [0x1B9]="000_cmn_icon_jin_fuchi_5", [0x1BC]="000_cmn_icon_jin_fuchi_8",
            [0x1BD]="000_cmn_icon_jin_fuchi_9", [0x1B1]="000_cmn_icon_jin_fuchi_10",
            [0x1B2]="000_cmn_icon_jin_fuchi_11", [0x1B3]="000_cmn_icon_jin_fuchi_12",
            [0x1B4]="000_cmn_icon_jin_fuchi_13", [0x3F0]="000_cmn_icon_jin_15_fuchi",
            [0x1B5]="000_cmn_icon_jin_fuchi_14", [0x231]="000_cmn_mrk_gundan_1",
            [0x232]="000_cmn_mrk_gundan_2", [0x233]="000_cmn_mrk_gundan_3",
            [0x234]="000_cmn_mrk_gundan_4", [0x235]="000_cmn_mrk_gundan_5",
            [0x236]="000_cmn_mrk_gundan_6", [0x237]="000_cmn_mrk_gundan_7",
            [0x238]="000_cmn_mrk_gundan_8", [0x1D1]="000_cmn_icon_map_shussei"
        };
        return new(appearance, names);
    }

    // 8ACA40/8AD680 both use2A9C00(index,0). This is name resolution,
    // not the complete bank10/DLC/edit/fallback or exact source-owner policy.
    public ArmyMapPortraitNames PortraitNames(ushort appearanceIndex)
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

    public string IconName(ArmyMapIconSelection selection) => _spriteNames[selection.SpriteId];

    // Caller has accepted the actual player/District/233A90 route.
    public static ArmyMapIconSelection DistrictIcon(byte number11)
        => new(number11 is >= 2 and <= 8 ? 0x230 + number11 : 0x231, 9);
    public static ArmyMapIconSelection SpecialPlayerIcon() => new(0x1D1, 9);

    // actualFormation is22B2D0's chosen water1C/land1B, not Core FormationId.
    public static ArmyMapIconSelection FormationIcon(byte actualFormation, byte formationClass7E)
        => new(actualFormation switch
        {
            2 => 0x1B6, 3 => 0x1B7, 4 => 0x1B8,
            5 => 0x1BA, 6 => 0x1BB, 7 => 0x1B9, 8 => 0x1BC, 9 => 0x1BD,
            10 => 0x1B1, 11 => 0x1B2, 12 => 0x1B3, 13 => 0x1B4,
            14 => 0x3F0, >= 17 and <= 20 => 0x1B5, _ => 0x1B0
        }, formationClass7E == 2 ? 0x2C : 0x0A);
}
