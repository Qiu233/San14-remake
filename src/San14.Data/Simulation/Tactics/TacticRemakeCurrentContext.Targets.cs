using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class TacticRemakeCurrentContext
{
    private sealed record Occupant(UnitState? Army, int? BaseLocation, int ObjectIdentity,
        ObjectReference? CapturedObject);

    public TacticInstructionTargetsSource? ResolveInstructionTargets(GameState state,
        UnitState caster, UnitState primaryTarget, TacticSourceRow source,
        S14TacticEffectInstruction instruction)
        => ResolveInstructionTargetsAtCapturedHexes(state, caster, primaryTarget, source, instruction,
            caster.HexId, primaryTarget.HexId);

    /// <summary>172C10 captures descriptor Hex C before building its two
    /// instruction vectors. Its center is independent of the live word2A later
    /// used by the activation-rate sampler. Occupants remain actual current.</summary>
    internal TacticInstructionTargetsSource? ResolveInstructionTargetsAtCapturedHexes(GameState state,
        UnitState caster, UnitState primaryTarget, TacticSourceRow source,
        S14TacticEffectInstruction instruction, int sourceDescriptorHex, int primaryDescriptorHex)
    {
        if (!Bound(state) || ArmyValid(state, caster) != true ||
            ReadCurrentTargetFlags(state, source) is not byte flags ||
            instruction.Opcode is not (0 or 15 or 19 or 20 or 23)) return null;
        if (instruction.Opcode == 0) return new([], true, state.RandomState);
        if (instruction.Opcode == 23 && ArmyValid(state, primaryTarget) != true) return null;
        var ids = new List<int>();
        var seenObjects = new HashSet<int>();
        bool selectedBase = false;
        int center = instruction.Opcode == 23 ? primaryDescriptorHex : sourceDescriptorHex;
        foreach (int hex in NativeHexOrder(center, instruction.Raw64))
        {
            if (!TryOccupant(state, hex, out Occupant? occupant)) return null;
            if (occupant is null) continue;
            if (occupant.BaseLocation is int location)
            {
                //16E880 already chose the Base. Rejecting Base-target flags
                //does not expose an Army hidden at this same footprint Hex.
                if ((flags & 1) == 0 || !seenObjects.Add(occupant.ObjectIdentity)) continue;
                bool? excluded = BaseExcluded(state, location);
                if (excluded is null) return null;
                if (excluded == true) continue;
                if (occupant.CapturedObject is not { } captured ||
                    _definitions.ReadObjectForceSlot13(captured) is not byte force) return null;
                if (instruction.Opcode == 23)
                {
                    bool? hostile = ArmyLogisticsPreparation.ReadNativeRelationGate(state, caster.ForceId, force);
                    if (hostile is null) return null;
                    if (hostile.Value) selectedBase = true;
                    continue;
                }
                if (force != caster.ForceId) continue;
                if (instruction.Opcode == 15) selectedBase = true;
                else if (instruction.Opcode == 19)
                {
                    //Core Base state does not yet own native49/4A/4B. A same-
                    //force Base can qualify, so its absence cannot be asserted.
                    return null;
                }
                //20 rejects every Base even when native44 permits them.
                continue;
            }
            if (occupant.Army is not { } target) continue; //Native generic Object kind11.
            bool? armyExcluded = ArmyExcluded(state, target.Id);
            if (armyExcluded is null) return null;
            if (armyExcluded == true) continue;
            if (instruction.Opcode == 23)
            {
                bool? hostile = ArmyLogisticsPreparation.ReadNativeRelationGate(state, caster.ForceId, target.ForceId);
                if (hostile is null) return null;
                if (!hostile.Value) continue;
            }
            else if (target.ForceId != caster.ForceId) continue;
            NativeArmyTacticRuntime native = target.NativeTacticRuntime!;
            if (instruction.Opcode == 19 && native.Abnormal23 == 0 && native.Abnormal24 == 0 && native.Abnormal25 == 0)
                continue;
            if (instruction.Opcode == 20)
            {
                if (target.WoundedTroops is not int wounded) return null;
                if (wounded == 0) continue;
                int? capacity = ReadArmyHealingSelectionCapacity(state, target);
                if (capacity is null) return null;
                if (target.Troops >= capacity.Value) continue;
            }
            ids.Add(target.Id);
        }
        return new(ids.ToArray(), !selectedBase, state.RandomState);
    }

    private bool TryOccupant(GameState state, int hexId, out Occupant? occupant)
    {
        occupant = null;
        if (state.NativeArmyLifecycle is not null)
        {
            var read = FieldHexOccupantSource.ReadCurrent(state, _definitions, hexId);
            if (read.Receipt is not { } current) return false;
            if (current.NativeKind is 5 or 6)
                occupant = new(null, current.BaseLocationId,
                    _definitions.ReadObjectPhysicalSlot(current.CapturedObject!)!.Value, current.CapturedObject);
            else if (current.NativeKind == 27)
            {
                if (current.GameplayArmyId is not int id || state.Units.GetValueOrDefault(id) is not { } army) return false;
                occupant = new(army, null, 0, null);
            }
            else if (current.NativeKind == 11)
                occupant = new(null, null, current.NativePhysicalSlot, current.CapturedObject);
            return true;
        }
        //Earlier narrow captures without the saved native registry retain
        //their explicit projection. Owned campaigns use actual Hex1C above.
        HexReference? hex = _definitions.ReadHexTableReference(hexId);
        if (hex?.PhysicalSlot is not int actualHex || actualHex != hexId ||
            _definitions.ReadHexObjectLink(hex) is not ushort objectLink) return false;
        ObjectReference? obj = _definitions.ReadObjectTableReference(objectLink <= 3000 ? objectLink : 0);
        if (obj is null) return false;
        bool? objectValid = _definitions.ReadObjectNativeValidity(obj);
        if (objectValid is null) return false;
        if (objectValid == true)
        {
            int? physical = _definitions.ReadObjectPhysicalSlot(obj);
            if (physical is null) return false;
            if (physical is >= 1 and <= 51)
            {
                BaseReference? reference = _definitions.ReadCityTableReference(physical.Value);
                if (reference is null || _definitions.ReadBaseNativeValidity(reference) is not bool valid) return false;
                if (valid)
                {
                    if (reference.PhysicalSlot is not int cityId) return false;
                    occupant = new(null, cityId, physical.Value, obj); return true;
                }
            }
            else if (physical is >= 52 and <= 61)
            {
                BaseReference? reference = _definitions.ReadGateTableReference(physical.Value - 51);
                if (reference is null || _definitions.ReadBaseNativeValidity(reference) is not bool valid) return false;
                if (valid)
                {
                    if (reference.PhysicalSlot is not int slot) return false;
                    occupant = new(null, slot + 51, physical.Value, obj); return true;
                }
            }
        }
        //An owned native Hex has one Army occupant. A provisional stack or
        //unproved Army creation cannot be resolved by sorting gameplay IDs.
        UnitState[] armies = state.Units.Values.Where(unit => unit.HexId == hexId).ToArray();
        if (armies.Length > 1) return false;
        if (armies.Length == 0)
        {
            if (objectValid == true && _definitions.ReadObjectPhysicalSlot(obj) is int identity)
                occupant = new(null, null, identity, obj);
            return true;
        }
        bool? armyValid = ArmyValid(state, armies[0]);
        if (armyValid is null) return false;
        if (armyValid == true)
        {
            bool? excluded = ArmyExcluded(state, armies[0].Id);
            if (excluded is null) return false;
            if (excluded.Value) return true; //16E880 returnsFF, skipping generic Object.
            occupant = new(armies[0], null, 0, null);
        }
        else if (objectValid == true && _definitions.ReadObjectPhysicalSlot(obj) is int identity)
            occupant = new(null, null, identity, obj);
        return true;
    }

    public bool? ArmyManagerExcluded(GameState state, int unitId) =>
        Bound(state) ? ArmyExcluded(state, unitId) : null;

    private bool? ArmyExcluded(GameState state, int unitId)
    {
        if (state.PendingCombatSettlements is not null)
            return state.Units.GetValueOrDefault(unitId)?.NativeArmyOrigin?.NativeSlotIndex is int physical
                ? OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, _definitions, physical) : null;
        // Earlier saved/explicit narrow captures retain their current query
        // projection. Loading does not manufacture the new allocation owner.
        NativeArmyWorldState world = state.NativeArmySimulation!.World;
        if (world.PendingArmyIds is null || world.ObjectPairs is null) return null;
        if (world.PendingArmyIds.Contains(unitId)) return true;
        foreach (NativeArmyObjectPairState pair in world.ObjectPairs)
        {
            if (pair.BaseLocationId is int location && pair.SecondArmyId is int second)
            {
                bool? validBase = BaseValid(location);
                if (validBase is null) return null;
                if (validBase == true && state.Units.TryGetValue(second, out UnitState? army))
                {
                    bool? validArmy = ArmyValid(state, army);
                    if (validArmy is null) return null;
                    if (validArmy == true && army.ForceId is not (>= 46 and <= 51) && second == unitId) return true;
                }
            }
            if (pair.FirstArmyId is int first && state.Units.TryGetValue(first, out UnitState? initial))
            {
                bool? valid = ArmyValid(state, initial);
                if (valid is null) return null;
                if (valid == true && first == unitId) return true;
            }
        }
        return false;
    }

    private bool? BaseExcluded(GameState state, int location)
    {
        if (state.PendingCombatSettlements is not null)
            return OwnedArmyDestructionCurrent.ReadBaseExcluded2334B0(state, _definitions, location);
        if (state.NativeArmySimulation!.World.ObjectPairs is not { } pairs) return null;
        foreach (NativeArmyObjectPairState pair in pairs)
            if (pair.BaseLocationId is int target)
            {
                bool? valid = BaseValid(target);
                if (valid is null) return null;
                if (valid == true && target == location) return true;
            }
        return false;
    }

    private bool? BaseValid(int location)
    {
        BaseReference? reference = location is >= 1 and <= 51
            ? _definitions.ReadCityTableReference(location) : location is >= 52 and <= 61
                ? _definitions.ReadGateTableReference(location - 51) : null;
        return reference is null ? null : _definitions.ReadBaseNativeValidity(reference);
    }

    /// <summary>2A7B20 rings append at18CC0 flag1.16BC80 inserts its center
    /// at flag0, which prepends; output traversal therefore starts at center.</summary>
    public static int[] NativeHexOrder(int center, int radius)
    {
        if (center is < 0 or >= 48400 || radius is < 0 or > 255)
            throw new InvalidDataException("Owned native target ring geometry exceeds the inspected profile.");
        (int X, int Y)[][] directions =
        [
            [(-1, 0), (0, -1), (1, 0), (-1, 1), (0, 1), (1, 1)],
            [(-1, -1), (0, -1), (1, -1), (-1, 0), (0, 1), (1, 0)]
        ];
        int[] order = [2, 3, 6, 5, 4, 1];
        var result = new List<int> { center };
        for (int ring = 1; ring <= radius; ring++)
        {
            int x = center % 220, y = center / 220;
            for (int step = 0; step < ring; step++) Step(4);
            foreach (int direction in order)
                for (int step = 0; step < ring; step++)
                {
                    Step(direction);
                    if ((uint)x < 220 && (uint)y < 220) result.Add(y * 220 + x);
                }
            void Step(int direction)
            {
                (int dx, int dy) = directions[Math.Abs(x) % 2][direction - 1];
                x += dx; y += dy;
            }
        }
        return result.ToArray();
    }
}
