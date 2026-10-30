using San14.Core;

namespace San14.Data;

public sealed partial class ArmyMarchCrossingSource
{
    //2F60C0 receives costs, not actions or target categories. The caller
    //has already decided friendliness/reciprocity/funding. Zero costs are
    //read after BOTH Next writes, before either2DA620 runs.
    private ArmyMovementResolution? TryExchangeOrdinaryArmies(GameState state, SourceWorldContext current,
        UnitState first, UnitState second, ushort firstCredit, int firstCost, ushort secondCredit, int secondCost,
        NativeArmyProgressStageMoment? moment, ushort? effectiveTarget)
    {
        try
        {
            if (ReadCollisionImage(state, first) != first ||
                ReadCollisionImage(state, second) != second || first.March is null || second.March is null ||
                first.NativeArmyOrigin?.NativeSlotIndex is not int firstPhysical ||
                second.NativeArmyOrigin?.NativeSlotIndex is not int secondPhysical ||
                current.ReadArmyUnitRawHex2A(new(firstPhysical)) is not ushort firstHex ||
                current.ReadArmyUnitRawHex2A(new(secondPhysical)) is not ushort secondHex)
                return Missing<ArmyMovementResolution>(state, first, "exchange.current_receivers", moment,
                    firstCredit, firstCost, target: effectiveTarget);

            first = first with { March = first.March with
                { NextStep = DecodeExchangeNext(secondHex), MovementCredit = firstCredit } };
            var working = package.WriteMarchCollisionImageCurrent(state, first);
            if (second.Id != first.Id)
                working = package.WriteMarchCollisionImageCurrent(working, second with
                    { March = second.March with { NextStep = DecodeExchangeNext(firstHex) } });
            else
                working = package.WriteMarchCollisionImageCurrent(working, first with
                    { March = first.March with { NextStep = DecodeExchangeNext(firstHex) } });
            if (firstCost == 0) firstCost = ReadExchangeDestinationCost(working, ReadCollisionImage(working, first));
            if (secondCost == 0) secondCost = ReadExchangeDestinationCost(working, ReadCollisionImage(working, second));

            working = CommitDecidedMarchPositionCurrent(working, current, ReadCollisionImage(working, first),
                firstCredit, firstCost, false, moment, effectiveTarget);
            var partner = ReadCollisionImage(working, second);
            //When the occupied receiver is the caller itself, the second
            //2DA620 reads the credit left by the first call, not its old word.
            ushort partnerCredit = second.Id == first.Id
                ? ArmyMarchSourceAdapter.EncodeCredit(partner.March)
                    ?? throw ArmyMarchSourceRuntime.MissingBranch(working, partner, moment, "exchange.partner_credit")
                : secondCredit;
            working = CommitDecidedMarchPositionCurrent(working, current, partner,
                partnerCredit, secondCost, false, moment, effectiveTarget);
            var caller = ReadCollisionImage(working, first);
            //Both original callers return to2D9EAD: only the original
            //caller's29DB90 runs. No Base arrival, effect102 or build tail.
            var completed = package.ApplyOrdinaryMarchDevelopment(working, current, caller);
            ushort? remaining = ArmyMarchSourceAdapter.EncodeCredit(caller.March);
            return completed is null || remaining is null
                ? Missing<ArmyMovementResolution>(working, caller, "exchange.development_or_credit", moment,
                    firstCredit, firstCost, target: effectiveTarget)
                : new(remaining.Value, completed);

            int ReadExchangeDestinationCost(GameState input, UnitState actor)
            {
                if (ArmyMarchSourceAdapter.EncodeDestination(actor.March?.NextStep) is not ushort next ||
                    current.ReadHexTableReference(next < ArmyMarchSourceAdapter.SourceHexCount ? next : 0) is not { } hex ||
                    current.ReadHexSurfaceCode(hex) is not byte code ||
                    current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0) is not { } surface ||
                    ArmyMarchTerrainCost.Read(input, current, surface) is not int cost)
                    throw ArmyMarchSourceRuntime.MissingBranch(input, actor, moment, "exchange.zero_cost_Terrain",
                        effectiveTarget);
                return cost;
            }
        }
        catch (InvalidDataException) when (operationCurrent is null) { return null; }
    }

    //The caller owns the movement decision. This is only2DA620, plus the
    //explicit2D99C0 old-cell clear when requested; it never runs arrival or
    //the enclosing caller's development/effect102/construction tails.
    private GameState CommitDecidedMarchPositionCurrent(GameState state, SourceWorldContext current,
        UnitState actor, ushort credit, int cost, bool clearOldOccupant,
        NativeArmyProgressStageMoment? moment, ushort? effectiveTarget)
    {
        if (ReadCollisionImage(state, actor) != actor || actor.March is null ||
            actor.NativeArmyOrigin?.NativeSlotIndex is not int physical || physical is < 1 or > 500 ||
            current.ReadArmyUnitRawHex2A(new(physical)) is not ushort from ||
            ArmyMarchSourceAdapter.EncodeDestination(actor.March.NextStep) is not ushort to)
            throw MissingCommit("current_fields");
        current.BindArmyMapOccupancy(state);
        var oldHex = current.ReadHexTableReference(from < ArmyMarchSourceAdapter.SourceHexCount ? from : 0) ?? throw MissingCommit("old_Hex_pointer");
        var destination = current.ReadHexTableReference(to < ArmyMarchSourceAdapter.SourceHexCount ? to : 0) ?? throw MissingCommit("next_Hex_pointer");

        //2D99C0's2AF290/282940 precedes2DA620's validity/terrain returns.
        //2F60C0 does not clear either old cell between its two commits.
        if (clearOldOccupant && oldHex.PhysicalSlot is int clearPhysical)
        {
            if (current.ReadHexRaw1C(oldHex) is not ushort previous) throw MissingCommit("old_occupant");
            if (unchecked((short)previous) == physical)
            {
                current.SetHexRaw1C(clearPhysical, 0);
                state = state with { ArmyMapOccupancy = current.ExportArmyMapOccupancy() };
            }
        }
        //A known null native pointer is a completed source return. Missing
        //directory knowledge remains distinct and was rejected above.
        if (oldHex.PhysicalSlot is null || destination.PhysicalSlot is null) return state;
        if (current.ReadHexSurfaceCode(oldHex) is not byte oldCode ||
            current.ReadHexSurfaceCode(destination) is not byte newCode)
            throw MissingCommit("Hex_surface");
        var oldTerrain = current.ReadSurfaceTypeTableReference(oldCode <= 30 ? oldCode : 0)
            ?? throw MissingCommit("old_Terrain_pointer");
        var newTerrain = current.ReadSurfaceTypeTableReference(newCode <= 30 ? newCode : 0)
            ?? throw MissingCommit("next_Terrain_pointer");
        if (oldTerrain.PhysicalSlot is null || newTerrain.PhysicalSlot is null) return state;

        int oldPhysical = oldHex.PhysicalSlot.Value, destinationPhysical = destination.PhysicalSlot.Value;
        if (current.ReadCaptureHexWord16(oldPhysical) is not ushort oldFire) throw MissingCommit("old_Hex16");
        if (oldFire != 0)
        {
            byte oldFlags = PersonArmyCaptureInputs.ReadTerrainFlags2C(state, oldTerrain.PhysicalSlot.Value)
                ?? throw MissingCommit("old_Terrain2C");
            if ((oldFlags & 2) != 0 && oldPhysical != destinationPhysical)
            {
                byte newFlags = PersonArmyCaptureInputs.ReadTerrainFlags2C(state, newTerrain.PhysicalSlot.Value)
                    ?? throw MissingCommit("next_Terrain2C");
                state = package.ApplyObjectMarchFireTransferCurrent(state, current, actor, oldHex, destination,
                    oldFlags, newFlags, moment);
            }
        }
        var arena = state.ReadArmyLifecycle() ?? throw MissingCommit("lifecycle");
        OwnedArmyDestructorFlags.BindCurrent(arena, current);
        ushort remaining = unchecked((ushort)Math.Max(0, unchecked(credit - cost)));
        byte direction = ReadCollisionDirection(from, to);
        ushort writtenPosition = Math.Min(to, (ushort)(ArmyMarchSourceAdapter.SourceHexCount - 1));
        //241D70 overwrites the destination before Army2A/150. An exchange's
        //first commit deliberately still leaves the partner in the old cell.
        current.SetHexRaw1C(destinationPhysical, checked((ushort)physical));
        current.SetArmyUnitRawHex2A(physical, writtenPosition);
        if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 0, true)) throw MissingCommit("position_flag");
        var moved = PlayableUnitPosition.AtHex(actor, writtenPosition) with
        { PositionKnowledge = ArmyPositionKnowledge.Confirmed, NativeDirection36 = direction,
            March = actor.March with { MovementCredit = remaining } };
        var positioned = package.WriteMarchCollisionImageCurrent(state with
            { ArmyMapOccupancy = current.ExportArmyMapOccupancy() }, moved);
        positioned = positioned.WithArmyLifecycle(OwnedArmyDestructorFlags.PublishCurrent(
            positioned.ReadArmyLifecycle()!, current));
        if (ArmyMarchSourceAdapter.ReadFirstWaypoint(positioned, moved) is not ushort firstWaypoint)
            throw MissingCommit("first_waypoint");
        if (firstWaypoint == writtenPosition)
            positioned = ArmyWaypointArrivalSource.ConsumeCurrent(positioned, moved, package.WriteMarchCollisionImageCurrent)
                ?? throw MissingCommit("waypoint_consumption");
        return (operationCurrent is null ? ArmyChangedListsSource.Append(positioned, physical, current)
            : ArmyChangedListsSource.AppendCurrent(positioned, physical, current)).State
            ?? throw MissingCommit("changed_list");

        InvalidDataException MissingCommit(string field) => ArmyMarchSourceRuntime.MissingBranch(state, actor,
            moment, "decided_commit." + field, effectiveTarget, attemptedCredit: credit, destinationCost: cost);
    }
    private static ArmyMarchDestination DecodeExchangeNext(ushort currentPosition)
        => ArmyMarchSourceAdapter.DecodeDestination(currentPosition == ArmyMarchSourceAdapter.SourceHexCount
            ? currentPosition : Math.Min(currentPosition, (ushort)(ArmyMarchSourceAdapter.SourceHexCount - 1)));

    //2AEBC0 consumes original ushort operands, before2DA620 clamps Army2A.
    //Its missing-neighbor sentinel can itself match a raw None target.
    private static byte ReadCollisionDirection(ushort from, ushort to)
    {
        for (byte direction = 0; direction <= 6; direction++)
            if (ReadCollisionNeighbor(from, direction) == to) return direction;
        return 0;
    }

    //Shared2A7B60 leaf. The last-row even-column4/6 source fallback is
    //horizontal, unlike the general bounded-grid helper's None result.
    private static int ReadCollisionNeighbor(ushort from, byte direction)
    {
        if (direction == 0) return from;
        const int columns = 220;
        int x = from % columns, y = from / columns, last = columns - 1;
        bool odd = (x & 1) != 0;
        if (from < ArmyMarchSourceAdapter.SourceHexCount && direction is >= 1 and <= 6 &&
            !(y == last && !odd && direction is 4 or 6))
            return PersonReturnPathSource.Neighbor(from, direction);
        return direction switch
        {
            1 => x > 0 ? !odd ? from - 1 : y > 0 ? from - columns - 1 : ArmyMarchSourceAdapter.SourceHexCount
                : ArmyMarchSourceAdapter.SourceHexCount,
            2 => y > 0 ? from - columns : ArmyMarchSourceAdapter.SourceHexCount,
            3 => x < last ? !odd ? from + 1 : y > 0 ? from - columns + 1 : ArmyMarchSourceAdapter.SourceHexCount
                : ArmyMarchSourceAdapter.SourceHexCount,
            4 => x > 0 ? odd || y > last - 1 ? from - 1 : from + columns - 1
                : ArmyMarchSourceAdapter.SourceHexCount,
            5 => y < last ? from + columns : ArmyMarchSourceAdapter.SourceHexCount,
            6 => x < last ? odd || y > last - 1 ? from + 1 : from + columns + 1
                : ArmyMarchSourceAdapter.SourceHexCount,
            _ => ArmyMarchSourceAdapter.SourceHexCount
        };
    }

    private static UnitState ReadCollisionImage(GameState state, UnitState expected)
    {
        if (state.ReadArmyLifecycle() is not { } arena ||
            expected.NativeArmyOrigin?.NativeSlotIndex is not int physical || physical is < 1 or > 500 ||
            arena.Receivers.Find(arena.Slots[physical].ReceiverId) is not { } receiver)
            throw new InvalidDataException("Army.march-collision.image_receiver");
        return receiver.ActiveUnitId == expected.Id
            ? state.Units.GetValueOrDefault(expected.Id)
                ?? throw new InvalidDataException("Army.march-collision.active_image")
            : receiver.PendingImage is { } pending && pending.Id == expected.Id ? pending
                : throw new InvalidDataException("Army.march-collision.pending_image");
    }

}
