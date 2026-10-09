using San14.Core;

namespace San14.Data;

/// <summary>Reached current scalars, not a new saved owner. TargetCityPhysical
/// is the pointer-table receiver selected by raw38/3A; GoalHex is its linked
/// Object10, not the Army's route48 or the first waypoint2C.</summary>
public sealed record ArmyCityEntryPositionInputs(int ArmyPhysicalSlot, ushort? Commander12,
    byte? Status10, byte? Action37, ushort? CurrentHex2A, NativeArmyLifecycleTargetState? Target38,
    int? TargetCityPhysical, bool? TargetCityValid, ushort? TargetLocation4E, ushort? GoalHex2B5610,
    int? ArmyForceB0, int? TargetForceB0, bool? CommanderValid = null);

public sealed record ArmyCityEntryEligibilityReceipt(bool Eligible, int ArmyPhysicalSlot,
    byte? Status10, byte? Action37, BaseReference? TargetBase, int? LocationId,
    ushort? CurrentHex2A, ushort? GoalHex2B5610, bool Reports2E, string SourceBranch);
public sealed record ArmyCityEntryEligibilityResult(ArmyCityEntryEligibilityReceipt? Receipt,
    string? UnresolvedOwner)
{
    public bool? Eligible => Receipt?.Eligible;
}

/// <summary>Only2D99C0's friendly City arrival suffix after an actual2DA620
/// position commit. This does not grant stage7 eligibility, credit, routing48,
/// movement, or29F220 publication. It creates no context/pool and writes nothing.</summary>
public static class ArmyCityEntryEligibilitySource
{
    public const string Revision = "san14:ce-army-city-after-committed-move-20261008";

    /// <summary>For callers already holding these actual source projections.
    /// No map capture/IO or Core action-to-native action conversion is performed.</summary>
    public static ArmyCityEntryEligibilityResult ReadPosition(ArmyCityEntryPositionInputs input)
    {
        if (input.ArmyPhysicalSlot is < 1 or > 500) return Missing("Army_physical1_500");
        if (input.Commander12 is not ushort commander) return Missing("Army12");
        if (commander == 0) return Result(false, "32FB90.invalid-Army-Commander");
        if (input.Status10 is not byte status) return Missing("Army10");
        if (status == 0) return Result(false, "32FB90.invalid-Army-Status");
        if (status is not (1 or 2)) return Missing("scope.ordinary_or_transport_Army10");
        if (input.Action37 is not byte action) return Missing("Army37");
        if (action is not (2 or 11)) return Result(false, "2DA05D.non-City-entry-action");
        if (input.Target38?.Kind38 is not byte kind || input.Target38.Identity3A is not ushort identity)
            return Missing("Army38_3A");
        if (kind != 5) return Missing("scope.City-action_target_not_City5");
        if (input.TargetCityPhysical is not int physical) return Missing("TargetCity_pointer");
        if (input.TargetCityValid is not bool valid) return Missing("TargetCity_validity");
        if (!valid || physical == 0) return Result(false, "scope.invalid-target-City");
        if (physical is < 1 or > 51) return Missing("TargetCity_physical1_51");
        if (input.GoalHex2B5610 is not ushort goal || goal >= 48400) return Missing("TargetCity_Object10");
        if (input.CurrentHex2A is not ushort at || at >= 48400) return Missing("Army2A");
        var ring = HexNeighborhood.RadiusOneCandidates(goal);
        if (!ring.Contains(at))
        {
            //2D99C0 tests six stack slots even at the map edge. The producer
            //does not write every slot when2B4C70 returns fewer than six cells.
            if (ring.Count != 6) return Missing("2B4C70_unwritten_boundary_stack_cells");
            return Result(false, "2DA073.outside-City-six-ring");
        }
        if (input.TargetLocation4E is not ushort location) return Missing("TargetCity4E");
        if (input.CommanderValid is not bool commanderValid) return Missing("Commander_Person_validity");
        if (!commanderValid) return Result(false, "29F220.invalid-Commander-Person", action == 2);
        if (input.ArmyForceB0 is not int armyForce) return Missing("ArmyForceB0");
        if (input.TargetForceB0 is not int targetForce) return Missing("TargetCityForceB0");
        return Result(armyForce == targetForce, armyForce == targetForce
            ? "2DA073→29F220.friendly-City-six-ring" : "29F220.different-Force", action == 2);

        ArmyCityEntryEligibilityResult Result(bool eligible, string branch, bool reports2E = false) => new(new(eligible,
            input.ArmyPhysicalSlot, input.Status10, input.Action37,
            input.TargetCityPhysical is int city ? new(BaseKind.City, city) : null,
            input.TargetLocation4E, input.CurrentHex2A, input.GoalHex2B5610,
            reports2E, branch), null);
    }

    /// <summary>The supplied current view is shared by the caller's batch.
    /// Position/action/target are read from the supplied current Unit and saved
    /// arena, so a static or earlier context cannot replace latest raw2A/38/3A.</summary>
    public static ArmyCityEntryEligibilityResult ReadAfterCommittedMove(GameState state,
        SourceWorldContext current, UnitState army)
    {
        if (army.NativeArmyOrigin?.NativeSlotIndex is not int physical || physical is < 1 or > 500 ||
            state.NativeArmyLifecycle is not { } arena || physical >= arena.Slots.Length)
            return Missing("saved_Army_slot");
        var slot = arena.Slots[physical];
        if (slot.PhysicalSlot != physical || slot.ActiveUnitId != army.Id || slot.Commander12 != army.OfficerId)
            return Missing("saved_active_Army_receiver");
        var fields = new ArmyCityEntryPositionInputs(physical, slot.Commander12, slot.Status10,
            army.NativeArmyConfiguration?.Action37, army.NativeMarch?.CurrentHex2A ?? army.NativeCurrentHex2A,
            arena.Targets38 is { } targets && physical < targets.Length ? targets[physical] : null,
            null, null, null, null, null, null);
        var prefix = ReadPosition(fields);
        if (prefix.UnresolvedOwner != "current.city_entry_TargetCity_pointer") return prefix;
        ushort identity = fields.Target38!.Identity3A!.Value;
        var target = current.ReadCityTableReference(identity <= 51 ? identity : 0);
        if (target is null) return prefix;
        fields = fields with { TargetCityPhysical = target.PhysicalSlot ?? 0,
            TargetCityValid = current.ReadBaseNativeValidity(target), TargetLocation4E = current.ReadBaseObjectLink(target) };
        if (fields.TargetCityValid is true && fields.TargetLocation4E is ushort objectIdentity)
        {
            var obj = current.ReadObjectTableReference(objectIdentity <= 3000 ? objectIdentity : 0);
            fields = fields with { GoalHex2B5610 = obj is null ? null : current.ReadObjectHexIndex(obj) };
        }
        var geometry = ReadPosition(fields);
        if (geometry.UnresolvedOwner != "current.city_entry_Commander_Person_validity") return geometry;
        //Read-only definition captures do not restore every Army header. Use
        //the actual saved Commander12 followed by the same Person/District raw
        //lookup as22C0A0, without importing or mutating the Army table.
        int? force = null;
        bool? personValid = null;
        var person = current.ReadPersonLookupReference(slot.Commander12!.Value <= 6000 ? slot.Commander12.Value : 0);
        if (person is not null)
        {
            if (person.PhysicalSlot is not int personPhysical || personPhysical == 0) personValid = false;
            else
            {
                personValid = current.ReadPersonNativeValidity(personPhysical);
                if (personValid is true) force = current.ReadPersonOldForceSlot(personPhysical);
            }
        }
        return ReadPosition(fields with { CommanderValid = personValid,
            ArmyForceB0 = force, TargetForceB0 = personValid is true ? current.ReadBaseForceSlot(target) : null });
    }

    private static ArmyCityEntryEligibilityResult Missing(string field) =>
        new(null, "current.city_entry_" + field);
}
