using San14.Core;

namespace San14.Data;

/// <summary>The reached ordinary empty-Hex edge of TC2D99C0/2DA620, including
/// its mandatory waypoint and development tails. It does not choose a route.</summary>
public sealed class ArmyMarchCrossingSource(ArmyCampaignRuleAdapter package)
{
    /// <summary>2D99C0's self-occupied, insufficient-credit branch after the
    ///3014C0 minimum early return. No crossing, direction or changed-list write.</summary>
    public ArmyMovementResolution? TryAccumulateAtCurrentHex(GameState state, UnitState army, ushort credit, int cost)
    {
        if (state.Units.GetValueOrDefault(army.Id) != army || army.NativeTacticRuntime?.Status10 != 1 ||
            ArmyMarchSourceAdapter.ReadCurrentHex(state, army) is not ushort at ||
            ArmyMarchSourceAdapter.EncodeDestination(army.March?.NextStep) != at ||
            army.NativeArmyOrigin?.NativeSlotIndex is not int physical || physical is < 1 or > 500 ||
            cost <= 0 || credit >= cost || package.CaptureCurrentDefinitions(state) is not { } current) return null;
        current.BindArmyMapOccupancy(state);
        if (current.ReadHexTableReference(at) is not { PhysicalSlot: not null } hex ||
            current.ReadHexRaw1C(hex) != physical) return null;
        return package.ApplyOrdinaryMarchDevelopment(state, current, army) is { } complete ? new(credit, complete) : null;
    }

    public ArmyMovementResolution? TryCross(GameState state, UnitState army, ushort credit, int cost,
        NativeArmyProgressStageMoment? moment = null)
    {
        var current = package.CaptureCurrentDefinitions(state);
        return current is null ? null : TryCrossCurrent(state, current, army, credit, cost, moment);
    }

    /// <summary>Consumes the same operation's actual definitions and saved map
    /// index. All writes remain private until occupancy, flags and registration
    /// succeed; callers must discard this operation view on failure.</summary>
    private ArmyMovementResolution? TryCrossCurrent(GameState state, SourceWorldContext current,
        UnitState army, ushort credit, int cost, NativeArmyProgressStageMoment? moment)
    {
        if (state.Units.GetValueOrDefault(army.Id) != army || army.NativeTacticRuntime?.Status10 != 1 ||
            army.OverrideTarget?.Kind is not (ArmyOverrideTargetKind.None or ArmyOverrideTargetKind.ArmyReceiver) ||
            ArmyConfigurationSourceAdapter.ReadAction(state, army) is not byte action || action is not (0 or 1 or 8 or 9 or 13) ||
            ArmyMarchSourceAdapter.ReadCurrentHex(state, army) is not ushort from || from != army.HexId ||
            ArmyMarchSourceAdapter.EncodeDestination(army.March?.NextStep) is not ushort to ||
            OrdinaryArmyRouteSource.ReadCurrentTarget(state, army) is not ushort target ||
            target >= ArmyMarchSourceAdapter.SourceHexCount || target == from ||
            ArmyDirectionSource.ReadAdjacentDirection36(from, to) is not byte direction ||
            cost <= 0 || credit < cost || state.ArmyMapOccupancy?.HasArmyMarker(to) != false ||
            army.NativeArmyOrigin?.NativeSlotIndex is not int physical || physical is < 1 or > 500 ||
            state.ReadArmyLifecycle() is not { } arena)
            return null;

        //Final raw3A Hex arrival has no move/stay-specific completion writer.
        //An explicit raw2C arrival instead consumes the intermediate queue.
        //Preserve action, target, Next==new current and credit remainder. The
        //next stage7 excludes this reached final goal through2C7350.
        bool waypointArrival = ArmyMarchSourceAdapter.EncodeDestination(army.March?.Target) == to;
        if (target == to && !waypointArrival && (action is not (1 or 13) || army.March?.Target != ArmyMarchDestination.None ||
            OwnedArmyLifecycleTargets.Read(state, arena.Slots[physical].ReceiverId) is not { Kind38: 9, Identity3A: ushort goal } ||
            goal != to)) return null;

        var oldHex = current.ReadHexTableReference(from);
        var nextHex = current.ReadHexTableReference(to);
        if (oldHex?.PhysicalSlot is not int oldPhysical || nextHex?.PhysicalSlot is not int nextPhysical ||
            current.ReadHexObjectLink(nextHex) != 0 ||
            current.ReadCaptureHexWord16(oldPhysical) != 0 || current.ReadCaptureHexWord16(nextPhysical) != 0 ||
            !TryReadTerrain(oldHex, out byte oldFlags) || !TryReadTerrain(nextHex, out byte nextFlags) ||
            (oldFlags & 2) != (nextFlags & 2)) return null;

        if (current.ResolveOfficerSource(army.OfficerId) is not int commander ||
            TacticRemakeCurrentTraits.CommanderImmune(current, commander, 102) is null) return null;
        //The known-zero destination attachment makes both effect102 outcomes
        //write nothing. No Object arrival or action9 report is reached here.
        current.BindArmyMapOccupancy(state);
        OwnedArmyDestructorFlags.BindCurrent(arena, current);
        if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 0, true)) return null;
        var occupancy = ArmyCurrentHexOccupancySource.TryApplyCurrent(current, physical, from, to);
        if (occupancy.Receipt is null) return null;
        int remainder = credit - cost;
        var moved = PlayableUnitPosition.AtHex(army, to) with
        {
            NativeDirection36 = direction,
            March = army.March! with { MovementCredit = remainder }
        };
        if (waypointArrival)
        {
            if (ArmyWaypointArrivalSource.Consume(state, moved) is not { } advanced) return null;
            moved = moved with { March = advanced };
        }
        var working = (state with
        {
            Units = new(state.Units) { [army.Id] = moved },
            ArmyMapOccupancy = current.ExportArmyMapOccupancy()
        }).WithArmyLifecycle(OwnedArmyDestructorFlags.PublishCurrent(arena, current));
        var changed = ArmyChangedListsSource.Append(working, physical, current);
        //29DB90 sees the newly written position. A failed setter notification
        //must roll back occupancy, direction, credit and pending together.
        if (changed.State is not { } registered ||
            package.ApplyOrdinaryMarchDevelopment(registered, current, registered.Units[army.Id]) is not { } complete) return null;
        if (action is 8 or 9)
        {
            try { complete = package.AppendFacilityConstructionMarchTail(complete, current, complete.Units[army.Id], moment); }
            catch (InvalidDataException) { return null; }
        }
        return new(remainder, complete);

        bool TryReadTerrain(HexReference hex, out byte flags)
        {
            flags = 0;
            if (current.ReadHexSurfaceCode(hex) is not byte code ||
                current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0)?.PhysicalSlot is not int terrain ||
                PersonArmyCaptureInputs.ReadTerrainFlags2C(state, terrain) is not byte raw) return false;
            flags = raw;
            return true;
        }
    }

}
