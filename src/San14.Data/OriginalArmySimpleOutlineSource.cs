using San14.Core;

namespace San14.Data;

public enum OriginalArmySimpleOutlineColorKind { Preserve, DistrictNumber, ForceFill, SpritePalette, PackedLiteral }
public readonly record struct OriginalArmySimpleOutlineColorChoice(OriginalArmySimpleOutlineColorKind Kind,
    uint Value, float RgbMultiplier = 1);
public sealed record OriginalArmySimpleOutlineSelection(bool UsesOutline,
    OriginalArmySimpleOutlineColorChoice Primary, OriginalArmySimpleOutlineColorChoice Disabled);
public sealed record OriginalArmySimpleOutlineResult(OriginalArmySimpleOutlineSelection? Selection, string? Missing);

/// <summary>1B7110 on an actual operable MapInfoMode0/filter15=0 Map. Title
///is outside this entry point. Current raw fields are read in source order;
///Force49 is the same immutable source style used by the normal flag fill.</summary>
public static class OriginalArmySimpleOutlineSource
{
    public static OriginalArmySimpleOutlineResult ReadOrdinaryMap(GameState world,
        OriginalEventInitializationContext current, OriginalDistrictReference? district)
    {
        if (district is null) return Missing("current.Army_simple_District_pointer");
        //A known null source pointer returns0 without writing either color.
        if (district.PhysicalSlot is not int districtPhysical)
            return Selected(false, new(OriginalArmySimpleOutlineColorKind.Preserve, 0));
        if (current.ReadCurrentPlayerForce()?.PhysicalSlot is not int player || player is < 0 or > 51)
            return Missing("current.Army_simple_Player_force_physical");
        if (current.ReadDistrictRawByte(districtPhysical, 0x10) is not byte forceRaw)
            return Missing("current.Army_simple_District10");
        var force = current.ReadForceTableReference(forceRaw <= 51 ? forceRaw : 0);
        if (force?.PhysicalSlot is not int forcePhysical) return Missing("current.Army_simple_District_Force_pointer");

        //This branch tests physical identity, not District validity or233A90.
        //2AAD40 alone applies its independent validity/fallback palette.
        if (player is >= 1 and <= 51 && forcePhysical == player)
        {
            if (current.ReadDistrictNativeValidity(district) is not bool valid)
                return Missing("current.Army_simple_District_color_validity");
            if (!valid) return Selected(true, new(OriginalArmySimpleOutlineColorKind.ForceFill, 61));
            return current.ReadDistrictRawByte(districtPhysical, 0x11) is byte number
                ? Selected(true, new(OriginalArmySimpleOutlineColorKind.DistrictNumber, number))
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
                    ? Selected(true, new(OriginalArmySimpleOutlineColorKind.ForceFill, color))
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
                if ((flags & 2) != 0) return Selected(true, new(OriginalArmySimpleOutlineColorKind.SpritePalette, 62));
            }
            else
            {
                if (selected == forcePhysical) return Selected(true, new(OriginalArmySimpleOutlineColorKind.SpritePalette, 62));
                if (ReadForceByte(world, forcePhysical, 0x12) is not byte flags)
                    return Missing("current.Army_simple_Force12");
                if ((flags & 2) != 0) return Selected(true, new(OriginalArmySimpleOutlineColorKind.SpritePalette, 8));
            }
        }
        byte[]? relation = forcePhysical == 0 ? world.NativeForceUnregisterCurrent?.PlaceholderRelationsEA
            : world.NativeArmySimulation?.Forces.GetValueOrDefault(forcePhysical)?.RelationsEA;
        if (relation is not { Length: 52 }) return Missing("current.Army_simple_Force_EA_player");
        if ((relation[player] & 0x7F) != 0)
            return Selected(true, new(OriginalArmySimpleOutlineColorKind.SpritePalette, 7));
        return Selected(false, new(OriginalArmySimpleOutlineColorKind.PackedLiteral, uint.MaxValue));
    }

    private static byte? ReadForceByte(GameState world, int physical, int offset)
    {
        //Explicit reset images are actual receiver writes, not scenario defaults.
        if (world.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(physical, out var reset) == true)
            return reset[offset];
        var force = world.NativeArmySimulation?.Forces.GetValueOrDefault(physical);
        return offset == 0x194 ? force?.Flags194 : force?.Flags12;
    }
    private static OriginalArmySimpleOutlineResult Selected(bool outline, OriginalArmySimpleOutlineColorChoice primary)
        => new(new(outline, primary, primary.Kind == OriginalArmySimpleOutlineColorKind.Preserve
            ? primary : primary with { RgbMultiplier = 0.4f }), null);
    private static OriginalArmySimpleOutlineResult Missing(string field) => new(null, field);
}
