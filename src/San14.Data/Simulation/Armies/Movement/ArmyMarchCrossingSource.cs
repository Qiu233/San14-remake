using San14.Core;

namespace San14.Data;

/// <summary>The reached ordinary empty-Hex edge of TC2D99C0/2DA620, including
/// its mandatory waypoint and development tails. It does not choose a route.</summary>
public sealed class ArmyMarchCrossingSource(ArmyCampaignRuleAdapter package, SourceWorldContext? operationCurrent = null)
{
    internal bool? ReadDirectBaseEntryStep(GameState state, UnitState army, ushort next) =>
        operationCurrent is null ? null : ReadDirectBaseEntryStep(state, operationCurrent, army, next);

    private static bool? ReadDirectBaseEntryStep(GameState state, SourceWorldContext current, UnitState army, ushort next)
    {
        if (ArmyConfigurationSourceAdapter.ReadAction(state, army) is not byte action || action is not (2 or 3 or 11 or 12)) return false;
        if (ArmyMarchSourceAdapter.ReadFirstWaypoint(state, army) is not ushort waypoint) return null;
        if (waypoint != ArmyMarchSourceAdapter.SourceHexCount) return false;
        if (army.NativeArmyOrigin?.NativeSlotIndex is not int physical || state.ReadArmyLifecycle() is not { } arena ||
            OwnedArmyLifecycleTargets.Read(state, arena.ReceiverSlots()[physical].ReceiverId) is not
                { Kind38: byte kind, Identity3A: ushort identity } || current.ReadHexTableReference(next) is not { } hex ||
            current.ReadHexObjectLink(hex) is not ushort link ||
            current.ReadObjectTableReference(link <= 3000 ? link : 0) is not { } obj ||
            current.ReadObjectNativeValidity(obj) is not bool valid) return null;
        if (!valid || obj.PhysicalSlot is not int objectPhysical) return false;
        //232D00/22DFF0 classify the actual Object block. No nearest Base or
        //reverse search by a coincident Hex is substituted for this identity.
        if (objectPhysical is >= 52 and <= 61)
            return action is 3 or 12 && kind == 6 && identity == objectPhysical - 51;
        if (objectPhysical is < 1 or > 51 || action is not (2 or 11) || kind != 5 || identity != objectPhysical)
            return false;
        //The City's six surrounding cells also bind the City Object.2D99C0
        //bypasses its obstacle test only for this selected City and actual
        //matching Force pointer (0916E0 reads Object13 without a validity gate).
        if (current.ReadObjectForceSlot13(obj) is not byte cityForce) return null;
        var owner = current.ReadForceTableReference(cityForce <= 51 ? cityForce : 0);
        var actorForce = current.ReadArmyUnitForceReference(new(physical));
        return owner is null || actorForce.PhysicalSlot is null ? null : owner == actorForce;
    }

    /// <summary>2D99C0's occupied-Army branch. An enemy, a nonreciprocal
    /// friendly step or an underfunded reciprocal step waits at its actual
    /// position and reaches29DB90. A funded reciprocal pair reaches its
    /// distinct2F60C0 consumer; it is not an empty-Hex crossing.</summary>
    public ArmyMovementResolution? TryWaitForOccupiedArmy(GameState state, UnitState army, ushort credit, int cost,
        NativeArmyProgressStageMoment? moment = null, ushort? effectiveTarget = null)
    {
        if (state.Units.GetValueOrDefault(army.Id) != army || cost <= 0 ||
            (operationCurrent ?? package.CaptureCurrentDefinitions(state)) is not { } current) return Missing<ArmyMovementResolution>(state, army, "wait.entry_fields", moment, credit, cost, target: effectiveTarget);
        current.BindArmyMapOccupancy(state);
        if (ArmyMarchSourceAdapter.ReadCurrentHex(state, army) is not ushort at ||
            ArmyMarchSourceAdapter.EncodeDestination(army.March?.NextStep) is not ushort next ||
            next == at || current.ReadHexTableReference(next) is not { } hex ||
            current.ReadHexRaw1C(hex) is not ushort raw || unchecked((short)raw) is not (>= 1 and <= 500) ||
            current.ReadArmyUnitTableReference(raw) is not { } other ||
            current.ReadArmyUnitTableReference(army.NativeArmyOrigin?.NativeSlotIndex ?? 0) is not { } own)
            return Missing<ArmyMovementResolution>(state, army, "wait.occupant_fields", moment, credit, cost, target: effectiveTarget);
        bool? friendly = ArmyMarchDevelopmentSource.ReadFriendlyRelation(state,
            current.ReadArmyUnitForceReference(own), current.ReadArmyUnitForceReference(other));
        if (friendly is null) return Missing<ArmyMovementResolution>(state, army, "wait.relation", moment, credit, cost, target: effectiveTarget);
        if (friendly.Value)
        {
            if (other.PhysicalSlot is not int otherPhysical || otherPhysical is < 0 or > 500) return Missing<ArmyMovementResolution>(state, army, "wait.occupant_physical", moment, credit, cost, target: effectiveTarget);
            var receiver = state.ReadArmyLifecycle()?.ReceiverSlots()[otherPhysical];
            var occupant = receiver?.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : receiver?.PendingImage;
            if (occupant is null || ArmyMarchSourceAdapter.EncodeDestination(occupant.March?.NextStep) is not ushort otherNext)
                return Missing<ArmyMovementResolution>(state, army, "wait.occupant_image", moment, credit, cost, target: effectiveTarget);
            if (otherNext == at && credit >= cost)
            {
                if (ArmyMarchSourceAdapter.EncodeCredit(occupant.March) is not ushort otherCredit ||
                    current.ReadHexTableReference(otherNext) is not { } otherHex ||
                    current.ReadHexSurfaceCode(otherHex) is not byte surfaceCode ||
                    current.ReadSurfaceTypeTableReference(surfaceCode <= 30 ? surfaceCode : 0) is not { } surface ||
                    ArmyMarchTerrainCost.Read(state, current, surface) is not int otherCost)
                    return Missing<ArmyMovementResolution>(state, army, "wait.reciprocal_cost", moment, credit, cost, target: effectiveTarget);
                if (otherCredit >= otherCost)
                    return TryExchangeOrdinaryArmies(state, current, army, occupant, credit, cost, otherCredit, otherCost, moment, effectiveTarget);
            }
        }
        return package.ApplyOrdinaryMarchDevelopment(state, current, army) is { } complete ? new(credit, complete) : Missing<ArmyMovementResolution>(state, army, "wait.development", moment, credit, cost, target: effectiveTarget);
    }

    /// <summary>The ordinary same-terrain2F60C0 edge: both Next words are
    /// written first, then2DA620 executes for the caller and for its partner.
    /// Only the original2D99C0 caller reaches the development tail.</summary>
    private ArmyMovementResolution? TryExchangeOrdinaryArmies(GameState state, SourceWorldContext current,
        UnitState first, UnitState second, ushort firstCredit, int firstCost, ushort secondCredit, int secondCost,
        NativeArmyProgressStageMoment? moment, ushort? effectiveTarget)
    {
        if (first.NativeTacticRuntime?.Status10 != 1 || state.Units.GetValueOrDefault(second.Id) != second || second.NativeTacticRuntime?.Status10 != 1 ||
            ArmyConfigurationSourceAdapter.ReadAction(state, second) is not (1 or 13) ||
            first.NativeArmyOrigin?.NativeSlotIndex is not int firstPhysical ||
            second.NativeArmyOrigin?.NativeSlotIndex is not int secondPhysical ||
            ArmyMarchSourceAdapter.ReadCurrentHex(state, first) is not ushort firstHex ||
            ArmyMarchSourceAdapter.ReadCurrentHex(state, second) is not ushort secondHex ||
            ArmyMarchSourceAdapter.EncodeDestination(first.March?.NextStep) != secondHex ||
            first.March is null || second.March is null || firstCost <= 0 || secondCost <= 0 ||
            current.ReadHexTableReference(firstHex) is not { PhysicalSlot: int firstHexPhysical } oldFirst ||
            current.ReadHexTableReference(secondHex) is not { PhysicalSlot: int secondHexPhysical } oldSecond ||
            current.ReadHexObjectLink(oldFirst) != 0 || current.ReadHexObjectLink(oldSecond) != 0 ||
            current.ReadCaptureHexWord16(firstHexPhysical) != 0 || current.ReadCaptureHexWord16(secondHexPhysical) != 0 ||
            ArmyDirectionSource.ReadAdjacentDirection36(firstHex, secondHex) is not byte firstDirection ||
            ArmyDirectionSource.ReadAdjacentDirection36(secondHex, firstHex) is not byte secondDirection ||
            ReadTerrainFlags(oldFirst) is not byte firstFlags || ReadTerrainFlags(oldSecond) is not byte secondFlags ||
            (firstFlags & 2) != (secondFlags & 2))
            return Missing<ArmyMovementResolution>(state, first, "exchange.scope_or_terrain", moment, firstCredit, firstCost, target: effectiveTarget);
        first = first with { March = first.March with { NextStep = ArmyMarchDestination.AtHex(secondHex) } };
        second = second with { March = second.March with { NextStep = ArmyMarchDestination.AtHex(firstHex) } };
        GameState? working = state with { Units = new(state.Units) { [first.Id] = first, [second.Id] = second } };
        OwnedArmyDestructorFlags.BindCurrent(working.ReadArmyLifecycle()!, current);
        working = CommitPosition(working, first, firstPhysical, secondHex, secondHexPhysical,
            firstDirection, firstCredit - firstCost);
        if (working is null) return Missing<ArmyMovementResolution>(state, first, "exchange.first_commit", moment, firstCredit, firstCost, target: effectiveTarget);
        working = CommitPosition(working, second, secondPhysical, firstHex, firstHexPhysical,
            secondDirection, secondCredit - secondCost);
        if (working is null) return Missing<ArmyMovementResolution>(state, first, "exchange.second_commit", moment, firstCredit, firstCost, target: effectiveTarget);
        var completed = package.ApplyOrdinaryMarchDevelopment(working, current, working.Units[first.Id]);
        return completed is null ? Missing<ArmyMovementResolution>(working, first, "exchange.development", moment, firstCredit, firstCost, target: effectiveTarget) : new(firstCredit - firstCost, completed);

        byte? ReadTerrainFlags(HexReference hex)
        {
            if (current.ReadHexSurfaceCode(hex) is not byte code ||
                current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0)?.PhysicalSlot is not int physical) return null;
            return PersonArmyCaptureInputs.ReadTerrainFlags2C(state, physical);
        }
        GameState? CommitPosition(GameState input, UnitState actor, int physical, ushort destination,
            int destinationPhysical, byte direction, int remaining)
        {
            var moved = PlayableUnitPosition.AtHex(actor, destination) with
            { NativeDirection36 = direction, March = actor.March! with { MovementCredit = remaining } };
            //2F60C0 does not take2D99C0's old-cell clear. Each2DA620 writes
            //its destination in order, temporarily overwriting the partner.
            current.SetHexRaw1C(destinationPhysical, checked((ushort)physical));
            current.SetArmyUnitRawHex2A(physical, ArmyMarchSourceAdapter.ReadCurrentHex(input, moved));
            if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 0, true))
                return Missing<GameState>(input, actor, "exchange.position_flag", moment, remaining, target: effectiveTarget);
            if (ArmyMarchSourceAdapter.EncodeDestination(moved.March!.Target) == destination)
            {
                var advanced = ArmyWaypointArrivalSource.Consume(input, moved);
                if (advanced is null) return Missing<GameState>(input, actor, "exchange.waypoint", moment, remaining, target: effectiveTarget);
                moved = moved with { March = advanced };
            }
            var positioned = (input with { Units = new(input.Units) { [actor.Id] = moved },
                ArmyMapOccupancy = current.ExportArmyMapOccupancy() })
                .WithArmyLifecycle(OwnedArmyDestructorFlags.PublishCurrent(input.ReadArmyLifecycle()!, current));
            return (operationCurrent is null ? ArmyChangedListsSource.Append(positioned, physical, current) : ArmyChangedListsSource.AppendCurrent(positioned, physical, current)).State
                ?? Missing<GameState>(positioned, moved, "exchange.changed_list", moment, remaining, target: effectiveTarget);
        }
    }

    /// <summary>2D99C0's self-occupied, insufficient-credit branch after the
    ///3014C0 minimum early return. No crossing, direction or changed-list write.</summary>
    public ArmyMovementResolution? TryAccumulateAtCurrentHex(GameState state, UnitState army, ushort credit, int cost,
        NativeArmyProgressStageMoment? moment = null, ushort? effectiveTarget = null)
    {
        if (state.Units.GetValueOrDefault(army.Id) != army || army.NativeTacticRuntime?.Status10 is not (1 or 2) ||
            ArmyMarchSourceAdapter.ReadCurrentHex(state, army) is not ushort at ||
            ArmyMarchSourceAdapter.EncodeDestination(army.March?.NextStep) != at ||
            army.NativeArmyOrigin?.NativeSlotIndex is not int physical || physical is < 1 or > 500 ||
            cost <= 0 || credit >= cost || (operationCurrent ?? package.CaptureCurrentDefinitions(state)) is not { } current) return Missing<ArmyMovementResolution>(state, army, "accumulate.entry_fields", moment, credit, cost, target: effectiveTarget);
        current.BindArmyMapOccupancy(state);
        if (current.ReadHexTableReference(at) is not { PhysicalSlot: not null } hex ||
            current.ReadHexRaw1C(hex) != physical) return Missing<ArmyMovementResolution>(state, army, "accumulate.occupancy", moment, credit, cost, target: effectiveTarget);
        return package.ApplyOrdinaryMarchDevelopment(state, current, army) is { } complete ? new(credit, complete) : Missing<ArmyMovementResolution>(state, army, "accumulate.development", moment, credit, cost, target: effectiveTarget);
    }

    public ArmyMovementResolution? TryCross(GameState state, UnitState army, ushort credit, int cost,
        NativeArmyProgressStageMoment? moment = null, ushort? effectiveTarget = null)
    {
        var current = operationCurrent ?? package.CaptureCurrentDefinitions(state);
        return current is null ? Missing<ArmyMovementResolution>(state, army, "cross.current_owner", moment, credit, cost, target: effectiveTarget)
            : TryCrossCurrent(state, current, army, credit, cost, moment, effectiveTarget);
    }

    /// <summary>Consumes the same operation's actual definitions and saved map
    /// index. All writes remain private until occupancy, flags and registration
    /// succeed; callers must discard this operation view on failure.</summary>
    private ArmyMovementResolution? TryCrossCurrent(GameState state, SourceWorldContext current,
        UnitState army, ushort credit, int cost, NativeArmyProgressStageMoment? moment, ushort? effectiveTarget)
    {
        if (state.Units.GetValueOrDefault(army.Id) != army || army.NativeTacticRuntime?.Status10 is not (1 or 2) ||
            army.OverrideTarget?.Kind is not (ArmyOverrideTargetKind.None or ArmyOverrideTargetKind.ArmyReceiver) ||
            ArmyConfigurationSourceAdapter.ReadAction(state, army) is not byte action || action is not (0 or 1 or 2 or 3 or 4 or 5 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 16 or 17) ||
            ArmyMarchSourceAdapter.ReadCurrentHex(state, army) is not ushort from || from != army.HexId ||
            ArmyMarchSourceAdapter.EncodeDestination(army.March?.NextStep) is not ushort to ||
            OrdinaryArmyRouteSource.ReadCurrentTarget(state, army, current) is not ushort target ||
            target >= ArmyMarchSourceAdapter.SourceHexCount || target == from ||
            ArmyDirectionSource.ReadAdjacentDirection36(from, to) is not byte direction ||
            cost <= 0 || credit < cost || state.ArmyMapOccupancy?.HasArmyMarker(to) != false ||
            army.NativeArmyOrigin?.NativeSlotIndex is not int physical || physical is < 1 or > 500 ||
            state.ReadArmyLifecycle() is not { } arena)
            return Missing<ArmyMovementResolution>(state, army, "cross.entry_fields", moment, credit, cost, target: effectiveTarget);

        //The ordinary Make/Target packet stores Attack4/5/7 against an
        //actual City5/Gate6/Army27 receiver.2B54A0 above resolves those native
        //fields (or the real Army override/first waypoint), never March.Target
        //or a displayed destination.2D99C0's empty edge and2DA620 have no
        //attack-action exclusion. Keep type2 and unclosed target forms out.
        if (action is 4 or 5 or 7)
        {
            var attackTarget = OrdinaryArmyRouteSource.ReadEffectiveSearchTarget(state, army);
            if (army.NativeTacticRuntime.Status10 != 1 || attackTarget is null ||
                (army.OverrideTarget.Kind == ArmyOverrideTargetKind.ArmyReceiver
                    ? attackTarget.Value.Kind != 27
                    : attackTarget.Value.Kind != (action == 4 ? 5 : action == 5 ? 6 : 27)))
                return Missing<ArmyMovementResolution>(state, army, "cross.attack_receiver_scope", moment, credit, cost, target: effectiveTarget);
        }

        if (action == 17 && (army.NativeTacticRuntime.Status10 != 1 ||
            OwnedArmyLifecycleTargets.Read(state, arena.Slots[physical].ReceiverId) is not { Kind38: 27 }))
            return Missing<ArmyMovementResolution>(state, army, "cross.false_report_receiver_scope", moment, credit, cost, target: effectiveTarget);

        //Demolish uses the same empty-cell crossing stores. Keep this newly
        //admitted action confined to a real ordinary Army's Object main target;
        //the separate construction-march tail below still belongs only to8/9.
        if (action == 10 && (army.NativeTacticRuntime.Status10 != 1 ||
            OwnedArmyLifecycleTargets.Read(state, arena.Slots[physical].ReceiverId) is not { Kind38: 11 }))
            return Missing<ArmyMovementResolution>(state, army, "cross.demolition_receiver_scope", moment, credit, cost, target: effectiveTarget);

        //Fire has the same source empty-cell position/credit/waypoint tail.
        //Keep its newly admitted command family ordinary and Hex-targeted;
        //existing Object/attachment/final-goal gates still apply unchanged.
        if (action == 16 && (army.NativeTacticRuntime.Status10 != 1 ||
            OwnedArmyLifecycleTargets.Read(state, arena.Slots[physical].ReceiverId) is not { Kind38: 9 }))
            return Missing<ArmyMovementResolution>(state, army, "cross.fire_receiver_scope", moment, credit, cost, target: effectiveTarget);
        if (action == 14 && (army.NativeTacticRuntime.Status10 != 1 ||
            OwnedArmyLifecycleTargets.Read(state, arena.Slots[physical].ReceiverId) is not { Kind38: 9 }))
            return Missing<ArmyMovementResolution>(state, army, "cross.occupation_receiver_scope", moment, credit, cost, target: effectiveTarget);

        //Final raw3A Hex arrival has no move/stay-specific completion writer.
        //An explicit raw2C arrival instead consumes the intermediate queue.
        //Preserve action, target, Next==new current and credit remainder. The
        //next stage7 excludes this reached final goal through2C7350.
        bool waypointArrival = ArmyMarchSourceAdapter.ReadFirstWaypoint(state, army) == to;
        bool baseEntry = ReadDirectBaseEntryStep(state, current, army, to) == true;
        if (target == to && !waypointArrival && !baseEntry && (action is not (1 or 13 or 14) || army.March?.Target != ArmyMarchDestination.None ||
            OwnedArmyLifecycleTargets.Read(state, arena.Slots[physical].ReceiverId) is not { Kind38: 9, Identity3A: ushort goal } ||
            goal != to)) return Missing<ArmyMovementResolution>(state, army, "cross.final_goal_scope", moment, credit, cost, target: effectiveTarget);

        var oldHex = current.ReadHexTableReference(from);
        var nextHex = current.ReadHexTableReference(to);
        if (oldHex?.PhysicalSlot is not int oldPhysical || nextHex?.PhysicalSlot is not int nextPhysical ||
            !baseEntry && current.ReadHexObjectLink(nextHex) != 0 ||
            current.ReadCaptureHexWord16(oldPhysical) != 0 ||
            current.ReadCaptureHexWord16(nextPhysical) is not ushort attachment || attachment != 0 && !baseEntry ||
            !TryReadTerrain(oldHex, out _) || !TryReadTerrain(nextHex, out _))
            return Missing<ArmyMovementResolution>(state, army, "cross.hex_object_attachment_or_terrain", moment, credit, cost, target: effectiveTarget);
        //2DA620 admits both directions across Terrain2C's water bit. It
        //retains Army1B/1C and debits only the already reached BDDD0 cost.
        //The common position/150/waypoint/097E10 tail below is identical;
        //subsequent formation queries select1B/1C from the newly stored Hex.
        //The source's conditional render+148 ->1C92B0 rebuild belongs to
        //the presentation owner; no graphics object is stored in GameState.

        if (current.ResolveOfficerSource(army.OfficerId) is not int commander ||
            TacticRemakeCurrentTraits.CommanderImmune(current, commander, 102) is null) return Missing<ArmyMovementResolution>(state, army, "cross.commander_trait", moment, credit, cost, target: effectiveTarget);
        //A zero attachment has no effect102 store. A real valid City/Gate also
        //short-circuits that wall-clear branch through232780/232D00, even when its
        //independent Hex16 is nonzero. Other attached destinations stay out.
        current.BindArmyMapOccupancy(state);
        OwnedArmyDestructorFlags.BindCurrent(arena, current);
        if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 0, true)) return Missing<ArmyMovementResolution>(state, army, "cross.position_flag", moment, credit, cost, target: effectiveTarget);
        var occupancy = ArmyCurrentHexOccupancySource.TryApplyCurrent(current, physical, from, to);
        if (occupancy.Receipt is null) return Missing<ArmyMovementResolution>(state, army, "cross.occupancy_write", moment, credit, cost, target: effectiveTarget);
        int remainder = credit - cost;
        var moved = PlayableUnitPosition.AtHex(army, to) with
        {
            NativeDirection36 = direction,
            March = army.March! with { MovementCredit = remainder }
        };
        var working = (state with
        {
            Units = new(state.Units) { [army.Id] = moved },
            ArmyMapOccupancy = current.ExportArmyMapOccupancy()
        }).WithArmyLifecycle(OwnedArmyDestructorFlags.PublishCurrent(arena, current));
        if (waypointArrival)
        {
            var advanced = ArmyWaypointArrivalSource.ConsumeCurrent(working, moved);
            if (advanced is null) return Missing<ArmyMovementResolution>(state, army, "cross.waypoint", moment, credit, cost, target: effectiveTarget);
            working = advanced;
        }
        //The same operation's later actors/cuts consume this actual store;
        //a fresh capture must not be required to observe the new terrain.
        current.SetArmyUnitRawHex2A(physical, ArmyMarchSourceAdapter.ReadCurrentHex(working, moved));
        var changed = (operationCurrent is null ? ArmyChangedListsSource.Append(working, physical, current) : ArmyChangedListsSource.AppendCurrent(working, physical, current));
        //29DB90 sees the newly written position. A failed setter notification
        //must roll back occupancy, direction, credit and pending together.
        if (changed.State is not { } registered) return Missing<ArmyMovementResolution>(state, army, "cross.changed_list", moment, credit, cost, target: effectiveTarget);
        GameState returned;
        try { returned = package.CompleteMovementBaseArrival(registered, current, registered.Units[army.Id], moment); }
        catch (InvalidDataException)
        {
            //The operation driver returns the reached owner in its failure
            //result. Keep the strict standalone entry's nullable contract.
            if (operationCurrent is not null) throw;
            return null;
        }
        //29F220 can retire this active projection into pending70.29DB90
        //still reads that real receiver image before stage26 resets it.
        var afterArrival = returned.Units.GetValueOrDefault(army.Id)
            ?? returned.ReadArmyLifecycle()?.ReceiverSlots()[physical].PendingImage;
        if (afterArrival is null || package.ApplyOrdinaryMarchDevelopment(returned, current, afterArrival) is not { } complete)
            return Missing<ArmyMovementResolution>(returned, afterArrival ?? army, "cross.arrival_or_development", moment, credit, cost, target: effectiveTarget);
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

    private T? Missing<T>(GameState state, UnitState army, string branch,
        NativeArmyProgressStageMoment? moment, int? credit = null, int? cost = null, ushort? target = null) where T : class
    {
        if (operationCurrent is not null)
            throw ArmyMarchSourceRuntime.MissingBranch(state, army, moment, branch, target,
                attemptedCredit: credit, destinationCost: cost);
        return null;
    }
}
