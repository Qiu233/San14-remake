using System.Collections.Immutable;
using System.Globalization;

namespace San14.Data;

/// <summary>Pure fields of the full tactic balloon shared by the gallery and
/// the current deployment query. Physical effect slots never collapse.</summary>
public sealed record TacticBalloonContent(string Category, string TargetCaption,
    string Target, string WaitingCaption, string WaitingDays,
    IReadOnlyList<TacticBalloonEffect> Effects)
{
    public string LayoutName => "11_00_01_offset_05";
}
public sealed record TacticBalloonEffect(int Slot, int Opcode, string Caption,
    int IconSpriteId, string IconSpriteName, int SpritePalette, int TextPreset);

/// <summary>Resolved content and the actual disabled-control explanation.
/// A null explanation is a known absence, independent of body readiness.</summary>
public sealed record DeploymentTacticBalloon(TacticBalloonContent Body, string? DisabledReason);

internal static class TacticBalloonContentSource
{
    internal static TacticBalloonContent Create(byte category, byte targetFlags, ushort waiting,
        byte firstOpcode, byte secondOpcode, Func<int, int, string> data)
    {
        if (category > 2) throw new InvalidDataException("tactic.balloon.category");
        var effects = ImmutableArray.CreateBuilder<TacticBalloonEffect>(2);
        if (Effect(0, firstOpcode, data) is { } first) effects.Add(first);
        if (Effect(1, secondOpcode, data) is { } second) effects.Add(second);
        return new(category == 0 ? "" : data(1, category == 1 ? 0x4B1 : 0x4B2),
            data(1, 0x4B4), data(1, (targetFlags & 1) != 0 ? 0x338 : 0x339),
            data(1, 0x6C5), (waiting / TacticEffectSourceWaiting.OrdinaryUpdatesPerDay)
                .ToString(CultureInfo.InvariantCulture), effects.ToImmutable());
    }

    // Shared opcode caption/icon and beneficial/harmful presentation mapping.
    // Empty or out-of-range physical slots are hidden by the source consumer.
    private static TacticBalloonEffect? Effect(int slot, byte opcode, Func<int, int, string> data)
    {
        if (opcode is < 1 or > 23) return null;
        var (sprite, name) = opcode switch
        {
            1 => (0x1EB, "000_cmn_icon_st_damage"),
            2 => (0x1EF, "000_cmn_icon_st_konran"),
            3 => (0x1E9, "000_cmn_icon_st_asidome"),
            4 => (0x1EA, "000_cmn_icon_st_chohatu"),
            5 or 10 => (0x15D, "000_cmn_icon_bt_kidou"),
            6 or 11 => (0x15F, "000_cmn_icon_bt_kougun"),
            7 or 12 => (0x161, "000_cmn_icon_bt_koujyou"),
            8 or 13 => (0x157, "000_cmn_icon_bt_bougyo"),
            9 or 14 => (0x15A, "000_cmn_icon_bt_hajyou"),
            15 or 16 => (0x1E1, "000_cmn_icon_siki"),
            17 => (0x1EC, "000_cmn_icon_st_firing"),
            18 => (0x1F1, "000_cmn_icon_taikyu_damage"),
            19 => (0x1D7, "000_cmn_icon_recovery"),
            20 => (0x19D, "000_cmn_icon_heishi_recovery"),
            21 or 22 => (0x147, "000_cmn_icon_all_abilities"),
            23 => (0x1ED, "000_cmn_icon_st_grant"),
            _ => throw new InvalidDataException("tactic.balloon.opcode")
        };
        bool beneficial = opcode is >= 5 and <= 9 or 15 or >= 19 and <= 21;
        return new(slot, opcode, data(0, 0x151 + opcode), sprite, name,
            beneficial ? 11 : 12, beneficial ? 15 : 16);
    }
}
