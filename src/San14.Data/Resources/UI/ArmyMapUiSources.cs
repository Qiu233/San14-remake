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
        var appearance = PersonPortraitTransitionCatalog.Rules
            .Select(row => (From: row.PortraitWhenTrue, To: row.PortraitWhenFalse)).ToArray();
        var names = new Dictionary<int, string>
        {
            [0x21D]="000_cmn_mrk_base_disable",
            [0x1C3]="000_cmn_icon_kyoten", [0x198]="000_cmn_icon_gate", [0x1D6]="000_cmn_icon_port",
            [0x221]="000_cmn_mrk_base_red", [0x222]="000_cmn_mrk_base_yellow",
            [0x21F]="000_cmn_mrk_base_green", [0x21C]="000_cmn_mrk_base_cyan",
            [0x1C1]="000_cmn_icon_kane_fuchi",
            [0x1A0]="000_cmn_icon_hyourou_fuchi",
            [0x19C]="000_cmn_icon_heishi_fuchi",
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
            [0x238]="000_cmn_mrk_gundan_8", [0x1D1]="000_cmn_icon_map_shussei",
            [0x1F0]="000_cmn_icon_st_syusei_fuchi"
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

    //930280(form,0) uses the source non-outline table8AAA50.
    public static ArmyMapIconSelection FormationIconWithoutOutline(byte actualFormation, byte formationClass7E)
        => new(actualFormation switch
        {
            2 => 0x1A8, 3 => 0x1A9, 4 => 0x1AA,
            5 => 0x1AC, 6 => 0x1AD, 7 => 0x1AB, 8 => 0x1AE, 9 => 0x1AF,
            10 => 0x1A3, 11 => 0x1A4, 12 => 0x1A5, 13 => 0x1A6,
            14 => 0x3EF, >= 17 and <= 20 => 0x1A7, _ => 0x1A2
        }, formationClass7E == 2 ? 0x2C : 0x0A);

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
