using San14.Core;

namespace San14.Data;

public enum ArmySimpleOutlineColorKind { Preserve, DistrictNumber, ForceFill, SpritePalette, PackedLiteral }
public readonly record struct ArmySimpleOutlineColorChoice(ArmySimpleOutlineColorKind Kind,
    uint Value, float RgbMultiplier = 1);
public sealed record ArmySimpleOutlineSelection(bool UsesOutline,
    ArmySimpleOutlineColorChoice Primary, ArmySimpleOutlineColorChoice Disabled);
public sealed record ArmySimpleOutlineResult(ArmySimpleOutlineSelection? Selection, string? Missing);

/// <summary>Explicit non-Title profiles of1B7110. Each entry supplies the
/// actual player-District eligibility. Current fields are read in source order;
///Force49 is the same immutable source style used by the normal flag fill.</summary>
public static class ArmySimpleOutlineSource
{
    public static ArmySimpleOutlineResult ReadOrdinaryMap(GameState world,
        SourceWorldContext current, DistrictReference? district)
        => ReadNonTitleMap(world, current, district, allowPlayerDistrict: true);

    /// <summary>Scenery's explicit MapInfo mode0/category15=4/non-Title
    /// profile. Configured category15 disables the player-District branch;
    /// all later current-field reads retain the ordinary source ordering.</summary>
    public static ArmySimpleOutlineResult ReadConfiguredSceneryMap(GameState world,
        SourceWorldContext current, DistrictReference? district)
        => ReadNonTitleMap(world, current, district, allowPlayerDistrict: false);

    private static ArmySimpleOutlineResult ReadNonTitleMap(GameState world,
        SourceWorldContext current, DistrictReference? district, bool allowPlayerDistrict)
    {
        if (district is null) return Missing("current.Army_simple_District_pointer");
        //A known null source pointer returns0 without writing either color.
        if (district.PhysicalSlot is not int districtPhysical)
            return Selected(false, new(ArmySimpleOutlineColorKind.Preserve, 0));
        if (current.ReadCurrentPlayerForce()?.PhysicalSlot is not int player || player is < 0 or > 51)
            return Missing("current.Army_simple_Player_force_physical");
        if (current.ReadDistrictForceId(districtPhysical) is not byte forceRaw)
            return Missing("current.Army_simple_District10");
        var force = current.ReadForceTableReference(forceRaw <= 51 ? forceRaw : 0);
        if (force?.PhysicalSlot is not int forcePhysical) return Missing("current.Army_simple_District_Force_pointer");

        //This branch tests physical identity, not District validity or233A90.
        //2AAD40 alone applies its independent validity/fallback palette.
        if (allowPlayerDistrict && player is >= 1 and <= 51 && forcePhysical == player)
        {
            if (current.ReadDistrictNativeValidity(district) is not bool valid)
                return Missing("current.Army_simple_District_color_validity");
            if (!valid) return Selected(true, new(ArmySimpleOutlineColorKind.ForceFill, 61));
            return current.ReadDistrictNumber(districtPhysical) is byte number
                ? Selected(true, new(ArmySimpleOutlineColorKind.DistrictNumber, number))
                : Missing("current.Army_simple_District11");
        }

        byte? attachedRaw = ReadForceByte(world, forcePhysical, 0x194);
        if (attachedRaw is not byte attached) return Missing("current.Army_simple_Force194");
        if (attached != 0)
        {
            var attachedForce = current.ReadForceTableReference(attached <= 51 ? attached : 0);
            if (attachedForce is null || current.ReadForceNativeValidity(attachedForce) is not bool valid)
                return Missing("current.Army_simple_attached_Force_validity");
            if (valid)
                return current.ReadForceReportColor(attachedForce) is byte color
                    ? Selected(true, new(ArmySimpleOutlineColorKind.ForceFill, color))
                    : Missing("source.Army_simple_attached_Force_color49_style");
        }
        //The46..50 CTitleState branch is false in this explicitly scoped Map.
        if (world.NativeArmySimulation?.World.Flags165C is not byte view)
            return Missing("current.Army_simple_World165C");
        if (view != 0)
        {
            if (world.NativeArmySimulation.World.SelectedForce1656 is not byte selected)
                return Missing("current.Army_simple_World1656");
            if (selected == player)
            {
                if (ReadForceByte(world, forcePhysical, 0x12) is not byte flags)
                    return Missing("current.Army_simple_Force12");
                if ((flags & 2) != 0) return Selected(true, new(ArmySimpleOutlineColorKind.SpritePalette, 62));
            }
            else
            {
                if (selected == forcePhysical) return Selected(true, new(ArmySimpleOutlineColorKind.SpritePalette, 62));
                if (ReadForceByte(world, forcePhysical, 0x12) is not byte flags)
                    return Missing("current.Army_simple_Force12");
                if ((flags & 2) != 0) return Selected(true, new(ArmySimpleOutlineColorKind.SpritePalette, 8));
            }
        }
        byte[]? relation = forcePhysical == 0 ? world.NativeForceUnregisterCurrent?.PlaceholderRelationsEA
            : world.NativeArmySimulation?.Forces.GetValueOrDefault(forcePhysical)?.RelationsEA;
        if (relation is not { Length: 52 }) return Missing("current.Army_simple_Force_EA_player");
        if ((relation[player] & 0x7F) != 0)
            return Selected(true, new(ArmySimpleOutlineColorKind.SpritePalette, 7));
        return Selected(false, new(ArmySimpleOutlineColorKind.PackedLiteral, uint.MaxValue));
    }

    private static byte? ReadForceByte(GameState world, int physical, int offset)
    {
        //Explicit reset images are actual receiver writes, not scenario defaults.
        if (world.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(physical, out var reset) == true)
            return reset[offset];
        var force = world.NativeArmySimulation?.Forces.GetValueOrDefault(physical);
        return offset == 0x194 ? force?.Flags194 : force?.Flags12;
    }
    private static ArmySimpleOutlineResult Selected(bool outline, ArmySimpleOutlineColorChoice primary)
        => new(new(outline, primary, primary.Kind == ArmySimpleOutlineColorKind.Preserve
            ? primary : primary with { RgbMultiplier = 0.4f }), null);
    private static ArmySimpleOutlineResult Missing(string field) => new(null, field);
}
