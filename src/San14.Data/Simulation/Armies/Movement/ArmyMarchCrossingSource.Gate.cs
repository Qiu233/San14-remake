using San14.Core;

namespace San14.Data;

public sealed partial class ArmyMarchCrossingSource
{
    //Only classifies the actual lookup receiver. Native validity is consumed
    //later, at3014C0 or after2D99C0's credit write, in its original position.
    internal bool? ReadGateStepCandidate(GameState state, ushort next)
    {
        var current = operationCurrent ?? package.CaptureCurrentDefinitions(state);
        if (current is null || current.ReadHexTableReference(next < 48400 ? next : 0) is not { } hex ||
            current.ReadHexObjectLink(hex) is not ushort link) return null;
        var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0);
        return obj is null ? null : obj.PhysicalSlot is >= 52 and <= 61;
    }

    private ArmyMovementResolution? TryResolveGateDestinationCurrent(GameState state, SourceWorldContext current,
        UnitState army, ushort credit, int cost, ushort next, NativeArmyProgressStageMoment? moment,
        ushort? effectiveTarget)
    {
        var requested = GateHex(next);
        var gate = GateObject(requested);
        if (!GateNeed(current.ReadObjectNativeValidity(gate), "Gate_validity")) return new(credit, state);
        if (gate.PhysicalSlot is not (>= 52 and <= 61)) throw GateMissing("Gate_receiver");
        int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw GateMissing("Army_receiver");
        var actor = new ArmyUnitReference(physical);
        ushort at = GateNeed(current.ReadArmyUnitRawHex2A(actor), "Army_current2A");
        ushort center = GateNeed(current.ReadObjectHexIndex(gate), "Gate_current10");
        int force = current.ReadArmyUnitRawForceSlot(actor);

        if (!Hostile(GateNeed(current.ReadObjectForceSlot13(gate), "Gate_Force13")))
        {
            int currentPhysical = GateNeed(GateHex(at).PhysicalSlot, "Army_current_Hex_receiver");
            byte direction = ReadCollisionDirection(checked((ushort)currentPhysical), center);
            int neighbor = ReadCollisionNeighbor(center, direction);
            HexReference? candidate = neighbor < 48400 ? GateHex(neighbor) : null;
            bool useCandidate = false;
            if (candidate?.PhysicalSlot is not null)
            {
                if (Passage(army, candidate, true))
                    useCandidate = army.NativeTacticRuntime?.Status10 != 2 || !Hostile(HexForce(candidate));
                else if (Passage(army, candidate, false))
                {
                    short occupant = unchecked((short)GateNeed(current.ReadHexRaw1C(candidate), "candidate_occupant1C"));
                    if (occupant < 0) throw GateMissing("candidate_negative_Army_lookup");
                    var other = current.ReadArmyUnitTableReference(occupant <= 500 ? occupant : 0)
                        ?? throw GateMissing("candidate_Army_pointer");
                    if (GateNeed(current.ReadArmyUnitNativeValidity(other), "candidate_Army_validity"))
                    {
                        int otherPhysical = GateNeed(other.PhysicalSlot, "candidate_Army_receiver");
                        var second = GateArmyImage(state, otherPhysical);
                        ushort otherNext = GateNeed(ArmyMarchSourceAdapter.EncodeDestination(second.March?.NextStep), "candidate_Army_next48");
                        var otherNextHex = GateHex(otherNext < 48400 ? otherNext : 0);
                        ObjectReference? otherGate = otherNextHex.PhysicalSlot is null ? null : GateObject(otherNextHex);
                        bool friendly = GateNeed(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state,
                            current.ReadArmyUnitForceReference(actor), current.ReadArmyUnitForceReference(other)), "candidate_Army_relation");
                        if (friendly && otherGate is not null &&
                            GateNeed(current.ReadObjectNativeValidity(otherGate), "candidate_Next_Object_validity") &&
                            otherGate.PhysicalSlot is >= 52 and <= 61 &&
                            package.ReadMarchCollisionPassageCurrent(state, current, second, at, false))
                        {
                            int firstCost = ReadGateStepCost(state, army);
                            int secondCost = ReadGateStepCost(state, second);
                            ushort otherCredit = otherPhysical == physical ? credit
                                : GateNeed(ArmyMarchSourceAdapter.EncodeCredit(second.March), "candidate_Army_credit");
                            if (credit >= firstCost && otherCredit >= secondCost)
                                return TryExchangeOrdinaryArmies(state, current, army, second, credit,
                                    firstCost, otherCredit, secondCost, moment, effectiveTarget);
                        }
                    }
                }
            }

            if (!useCandidate)
            {
                //The source picks its first geometric candidate, then checks
                //passability once. It does not search onward after rejection.
                int ringCenter = candidate?.PhysicalSlot
                    ?? throw GateMissing("direction_Hex_null_native_index");
                candidate = null;
                foreach (int slot in GateSixRing(ringCenter))
                {
                    if (slot >= 48400) continue;
                    var hex = GateHex(slot);
                    if (hex.PhysicalSlot is null ||
                        GateNeed(current.ReadHexSurfaceCode(hex), "fallback_surface") == 17 ||
                        ArmyRouteSearch.Distance(slot, center) != 1) continue;
                    candidate = hex; break;
                }
                if (candidate is not null && Passage(army, candidate, true))
                    useCandidate = army.NativeTacticRuntime?.Status10 != 2 || !Hostile(HexForce(candidate));
            }

            if (useCandidate && candidate?.PhysicalSlot is int chosen)
            {
                if (chosen == center) return GateWait(state, army);
                //local870 is the original Gate-step cost. Replacing48 does not
                //recompute it or modify the main target/cached route/waypoints.
                army = army with { March = (army.March ?? throw GateMissing("replacement_March")) with
                    { NextStep = ArmyMarchDestination.AtHex(chosen), MovementCredit = credit } };
                var rewritten = package.WriteMarchCollisionImageCurrent(state, army);
                if (credit < cost) return new(credit, rewritten);
                var committed = CommitDecidedMarchPositionCurrent(rewritten, current, army, credit, cost,
                    true, moment, effectiveTarget);
                return CompleteMarchCallerAfterStepCurrent(committed, current, physical, moment, effectiveTarget);
            }
            if (requested.PhysicalSlot == center) return GateWait(state, army);
        }

        //An enemy Gate does not acquire the friendly replacement branch.
        //The original final2C6A10 decides its genuine wait/cross outcome.
        if (!Passage(army, requested, true)) return GateWait(state, army);
        if (credit < cost) return new(credit, state);
        var accumulated = army with { March = (army.March ?? throw GateMissing("funded_March")) with { MovementCredit = credit } };
        var accepted = CommitDecidedMarchPositionCurrent(package.WriteMarchCollisionImageCurrent(state, accumulated),
            current, accumulated, credit, cost, true, moment, effectiveTarget);
        return CompleteMarchCallerAfterStepCurrent(accepted, current, physical, moment, effectiveTarget);

        HexReference GateHex(int raw) => current.ReadHexTableReference(raw < 48400 ? raw : 0)
            ?? throw GateMissing("Hex_pointer");
        ObjectReference GateObject(HexReference hex)
        {
            int raw = GateNeed(current.ReadHexObjectLink(hex), "Hex_Object1A");
            return current.ReadObjectTableReference(raw <= 3000 ? raw : 0) ?? throw GateMissing("Object_pointer");
        }
        byte HexForce(HexReference hex) => GateNeed(current.ReadHexForceSlot(hex), "candidate_Hex_force");
        bool Hostile(byte other) => GateNeed(ArmyLogisticsPreparation.ReadNativeRelationGate(state, force, other), "relation");
        bool Passage(UnitState image, HexReference hex, bool flag) =>
            package.ReadMarchCollisionPassageCurrent(state, current, image,
                GateNeed(hex.PhysicalSlot, "passage_Hex_receiver"), flag);
        int ReadGateStepCost(GameState world, UnitState image)
        {
            ushort destination = GateNeed(ArmyMarchSourceAdapter.EncodeDestination(image.March?.NextStep), "cost_next48");
            byte surface = GateNeed(current.ReadHexSurfaceCode(GateHex(destination)), "cost_Hex_surface");
            var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0)
                ?? throw GateMissing("cost_Terrain_pointer");
            return ArmyMarchTerrainCost.Read(world, current, terrain) ?? throw GateMissing("cost_Terrain_value");
        }
        ArmyMovementResolution? GateWait(GameState world, UnitState image) =>
            WaitAfterMarchCollisionCurrent(world, current, image, credit, cost, moment, effectiveTarget);
    }

    private ArmyMovementResolution? WaitAfterMarchCollisionCurrent(GameState state, SourceWorldContext current,
        UnitState actor, ushort credit, int cost, NativeArmyProgressStageMoment? moment, ushort? effectiveTarget)
    {
        if (actor.March is not { } march) throw GateMissing("wait_March");
        if (ArmyMarchSourceAdapter.EncodeCredit(march) != credit)
        {
            actor = actor with { March = march with { MovementCredit = credit } };
            state = package.WriteMarchCollisionImageCurrent(state, actor);
        }
        return package.ApplyOrdinaryMarchDevelopment(state, current, actor) is { } waited
            ? new(credit, waited) : Missing<ArmyMovementResolution>(state, actor, "collision_wait_development", moment, credit, cost, effectiveTarget);
    }

    private ArmyMovementResolution? CompleteMarchCallerAfterStepCurrent(GameState state, SourceWorldContext current,
        int physical, NativeArmyProgressStageMoment? moment, ushort? effectiveTarget)
    {
        var actor = GateArmyImage(state, physical);
        ushort remaining = GateNeed(ArmyMarchSourceAdapter.EncodeCredit(actor.March), "after_commit_credit");
        var returned = package.CompleteMovementBaseArrival(state, current, actor, moment);
        var reached = GateArmyImage(returned, physical);
        var complete = package.ApplyOrdinaryMarchDevelopment(returned, current, reached)
            ?? throw GateMissing("after_commit_development");
        reached = GateArmyImage(complete, physical);
        complete = package.ApplyObjectMarchExtinguishCurrent(complete, current, reached, moment);
        reached = GateArmyImage(complete, physical);
        if (ArmyConfigurationSourceAdapter.ReadAction(complete, reached) is 8 or 9)
            complete = package.AppendFacilityConstructionMarchTail(complete, current, reached, moment);
        return new(remaining, complete);
    }

    private static UnitState GateArmyImage(GameState state, int physical)
    {
        var slot = (state.ReadArmyLifecycle() ?? throw GateMissing("image_lifecycle")).ReceiverSlots()[physical];
        return (slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : slot.PendingImage)
            ?? throw GateMissing("image_receiver");
    }

    //2B4C70 packs only valid coordinates into the caller's six prefilled slots.
    private static int[] GateSixRing(int center)
    {
        (int X, int Y)[][] delta =
        [ [(-1, 0), (0, -1), (1, 0), (-1, 1), (0, 1), (1, 1)],
          [(-1, -1), (0, -1), (1, -1), (-1, 0), (0, 1), (1, 0)] ];
        int x = center % 220, y = center / 220, count = 0;
        int[] result = [48400, 48400, 48400, 48400, 48400, 48400];
        Step(4);
        foreach (int direction in new[] { 2, 3, 6, 5, 4, 1 })
        {
            Step(direction);
            if ((uint)x < 220 && (uint)y < 220) result[count++] = y * 220 + x;
        }
        return result;
        void Step(int direction)
        {
            var shift = delta[Math.Abs(x) % 2][direction - 1];
            x += shift.X; y += shift.Y;
        }
    }

    private static T GateNeed<T>(T? value, string field) where T : struct => value ?? throw GateMissing(field);
    private static InvalidDataException GateMissing(string field) => new("Army.march-Gate." + field);
}
