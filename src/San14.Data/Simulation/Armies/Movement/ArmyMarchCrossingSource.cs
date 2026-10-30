using San14.Core;

namespace San14.Data;

/// <summary>The reached ordinary empty-Hex edge of TC2D99C0/2DA620, including
/// its mandatory waypoint and development tails. It does not choose a route.</summary>
public sealed partial class ArmyMarchCrossingSource(ArmyCampaignRuleAdapter package, SourceWorldContext? operationCurrent = null)
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
            current.ReadHexTableReference(next) is not { } hex ||
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
                ushort? currentOtherCredit = otherPhysical == own.PhysicalSlot
                    ? credit : ArmyMarchSourceAdapter.EncodeCredit(occupant.March);
                if (currentOtherCredit is not ushort otherCredit ||
                    current.ReadHexTableReference(otherNext) is not { } otherHex ||
                    current.ReadHexSurfaceCode(otherHex) is not byte surfaceCode ||
                    current.ReadSurfaceTypeTableReference(surfaceCode <= 30 ? surfaceCode : 0) is not { } surface ||
                    ArmyMarchTerrainCost.Read(state, current, surface) is not int otherCost)
                    return Missing<ArmyMovementResolution>(state, army, "wait.reciprocal_cost", moment, credit, cost, target: effectiveTarget);
                if (otherCredit >= otherCost)
                    return TryExchangeOrdinaryArmies(state, current, army, occupant, credit, 0, otherCredit, 0, moment, effectiveTarget);
            }
        }
        return WaitAfterMarchCollisionCurrent(state, current, army, credit, cost, moment, effectiveTarget);
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

    internal ArmyMarchObjectStepKind? ReadObjectStep(GameState state, ushort next)
    {
        var current = operationCurrent ?? package.CaptureCurrentDefinitions(state);
        return current is null ? null : ArmyMarchObjectStepSource.Read(current, next);
    }

    //2D99C0 reaches this after credit accumulation and after the Army occupant
    //branch. Invalid objects return with that credit; blocked ones run29DB90.
    internal ArmyMovementResolution? TryResolveObjectDestination(GameState state, UnitState army,
        ushort credit, int cost, NativeArmyProgressStageMoment? moment, ushort? effectiveTarget)
    {
        var current = operationCurrent ?? package.CaptureCurrentDefinitions(state);
        if (current is null || ArmyMarchSourceAdapter.EncodeDestination(army.March?.NextStep) is not ushort next)
            return Missing<ArmyMovementResolution>(state, army, "Object_destination_inputs", moment, credit, cost, effectiveTarget);
        var kind = ArmyMarchObjectStepSource.Read(current, next);
        if (kind is null) return Missing<ArmyMovementResolution>(state, army, "Object_destination_kind", moment, credit, cost, effectiveTarget);
        if (kind == ArmyMarchObjectStepKind.Invalid) return new(credit, state);
        bool objectPassage = kind == ArmyMarchObjectStepKind.GenericPassable;
        if (kind == ArmyMarchObjectStepKind.Gate && ReadDirectBaseEntryStep(state, current, army, next) != true)
            return TryResolveGateDestinationCurrent(state, current, army, credit, cost, next, moment, effectiveTarget);
        if (kind is ArmyMarchObjectStepKind.GenericBlocked or ArmyMarchObjectStepKind.City &&
            ReadDirectBaseEntryStep(state, current, army, next) != true)
        {
            var hex = current.ReadHexTableReference(next) ?? throw ArmyMarchSourceRuntime.MissingBranch(state, army, moment, "Object_passage_Hex");
            if (!package.ReadObjectMarchPassageCurrent(state, current, army, hex))
                return WaitAfterMarchCollisionCurrent(state, current, army, credit, cost, moment, effectiveTarget);
            objectPassage = true;
        }
        if (credit < cost) return new(credit, state);
        return TryCrossCurrent(state, current, army, credit, cost, moment, effectiveTarget, objectPassage);
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
        UnitState army, ushort credit, int cost, NativeArmyProgressStageMoment? moment, ushort? effectiveTarget,
        bool sourceObjectPassage = false)
    {
        bool objectTarget = OrdinaryArmyRouteSource.UsesObjectCommandMovement(state, army);
        if (state.Units.GetValueOrDefault(army.Id) != army || army.NativeTacticRuntime?.Status10 is not (1 or 2) ||
            !OrdinaryArmyRouteSource.HasSupportedMovementOverride(army) ||
            ArmyConfigurationSourceAdapter.ReadAction(state, army) is not byte action ||
            action is not (0 or 1 or 2 or 3 or 4 or 5 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 16 or 17) &&
                !(objectTarget && action is 6 or 15) ||
            ArmyMarchSourceAdapter.ReadCurrentHex(state, army) is not ushort from || from != army.HexId ||
            ArmyMarchSourceAdapter.EncodeDestination(army.March?.NextStep) is not ushort to ||
            OrdinaryArmyRouteSource.ReadCurrentTarget(state, army, current) is not ushort target ||
            target >= ArmyMarchSourceAdapter.SourceHexCount ||
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
                (OrdinaryArmyRouteSource.HasTypedObjectOverride(army) ? attackTarget.Value.Kind != 11 :
                    army.OverrideTarget?.Kind == ArmyOverrideTargetKind.ArmyReceiver
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

        bool baseEntry = ReadDirectBaseEntryStep(state, current, army, to) == true;
        var destination = current.ReadHexTableReference(to);
        if (destination is null || !baseEntry && !sourceObjectPassage && current.ReadHexObjectLink(destination) != 0)
            return Missing<ArmyMovementResolution>(state, army, "cross.Object_passage_owner", moment, credit, cost, effectiveTarget);
        //The movement decision is complete. Reuse the same full2DA620 writer
        //as Gate replacements and reciprocal pairs; it owns fire/credit/pose,
        //flags, waypoints and changed registration, without caller tails.
        var accumulated = army with { March = army.March! with { MovementCredit = credit } };
        var prepared = package.WriteMarchCollisionImageCurrent(state, accumulated);
        var committed = CommitDecidedMarchPositionCurrent(prepared, current, accumulated, credit, cost,
            true, moment, effectiveTarget);
        return CompleteMarchCallerAfterStepCurrent(committed, current, physical, moment, effectiveTarget);
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
