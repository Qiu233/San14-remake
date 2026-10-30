namespace San14.Data;

/// <summary>1B7110 restricted to the reached normal static-title prefix.
/// 336750/326230 leaves the actual World871E8 player3A and view165C zero;
/// 227070 leaves Force194 zero and the static237CC0 branch omits that field.
/// The still-serialized EA[0] is consumed from the actual selected scenario.
/// Preview selection is never a gameplay player or an outline input.</summary>
public static class TitleForceFlagStyleSource
{
    public const string NormalOutlineSprite = "000_cmn_prt_flag_default_base";
    public const string HighlightOutlineSprite = "000_cmn_prt_flag_default_outline";

    public static ArmySimpleOutlineSelection ReadNormalStaticTitle(ForcePreviewRecord force)
    {
        ArgumentNullException.ThrowIfNull(force);
        if (force.Slot is < 0 or > 51)
            throw new InvalidDataException("Title flag requires its actual Force-table physical slot.");
        // This branch precedes view/relations and returns the SAME dimmed
        // 80FFFFFF color to both the normal and disabled outline nodes.
        if (force.Slot is >= 46 and <= 50)
        {
            var special = new ArmySimpleOutlineColorChoice(ArmySimpleOutlineColorKind.PackedLiteral,
                Dim(0x80FFFFFF));
            return new(true, special, special);
        }
        if (force.RawIndexedBytesEA is not { Length: 52 } relations)
            throw new InvalidDataException("Title flag requires the actual static Force EA[0] source byte.");
        bool outline = (relations[0] & 0x7F) != 0;
        var normal = outline
            ? new ArmySimpleOutlineColorChoice(ArmySimpleOutlineColorKind.SpritePalette, 7)
            : new ArmySimpleOutlineColorChoice(ArmySimpleOutlineColorKind.PackedLiteral, uint.MaxValue);
        return new(outline, normal, normal with { RgbMultiplier = 0.4f });
    }

    /// <summary>1B0CE0 preserves alpha and truncates each RGB*0.4 channel.</summary>
    public static uint Dim(uint packed) => (packed & 0xFF000000) |
        ((uint)((packed >> 16 & 255) * 0.4f) << 16) |
        ((uint)((packed >> 8 & 255) * 0.4f) << 8) |
        (uint)((packed & 255) * 0.4f);
}
