using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

//Operation-local query results only. The original vectors contain Hex pointers;
//the captured entity annotates selection and is not a future dispatch receiver.
internal sealed record AutomaticTacticCurrentHexTarget(int HexId, FieldHexOccupantReceipt Entity);
internal sealed record TacticInstructionHexTargetsSource(int InstructionIndex,
    AutomaticTacticCurrentHexTarget[] OrderedTargets);
internal sealed record AutomaticTacticBaseCandidateSource(int TacticSlot,
    S14TacticEffectProgram Program, TacticInstructionHexTargetsSource[] Instructions);
internal sealed record AutomaticTacticBaseCurrentCandidates(int SourceArmyId,
    NormalCombatParticipant Primary, int SourceDescriptorHex, int PrimaryDescriptorHex,
    int[] ActualAvailableSlots, AutomaticTacticBaseCandidateSource[] CandidatesAfterTargetFiltering);

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>TC172EB0/16CAC0/16BF20 current Army-to-Base candidates.
    /// Preserves original available order/repeats, primary-kind5-only flag
    /// filtering and two independent complete ordered Hex vectors. No score,
    /// rate, effect, manager allocation, waiting or random write is performed.</summary>
    internal AutomaticTacticBaseCurrentCandidates? ResolveAutomaticBaseCurrentCandidates(
        GameState state, NormalCombatMixedPair pair)
    {
        if (!Bound(state) || pair.Actor.Kind != 27 || pair.Target.Kind is not (5 or 6) ||
            pair.Actor.UnitId is not int id || !state.Units.TryGetValue(id, out var caster) ||
            caster.NativeArmyOrigin?.NativeSlotIndex != pair.Actor.PhysicalSlot || ArmyValid(state, caster) != true ||
            _definitions.ReadBaseNativeValidity(new(pair.Target.Kind == 5 ? BaseKind.City : BaseKind.Gate,
                pair.Target.PhysicalSlot)) != true ||
            _definitions.ArmyReturnTransferMode is not { Present: false } ||
            EffectiveFormation(state, caster) is not int formation ||
            AvailableTactics(state, caster, formation) is not int[] available) return null;
        //Both descriptor Hex pointers are resolved/validated before candidate
        //production. This bounded owned geometry rejects unresolved aliases.
        if (!ActualHex(pair.Actor.HexId) || !ActualHex(pair.Target.HexId)) return null;
        var retained = new List<AutomaticTacticBaseCandidateSource>();
        foreach (int slot in available)
        {
            var row = _catalog.Rows[slot];
            if (pair.Target.Kind == 5)
            {
                if (ReadCurrentTargetFlags(state, row) is not byte flags) return null;
                if ((flags & 1) == 0) continue;
            }
            if (ReadCurrentProgram(state, row) is not { } program || program.Instructions.Count != 2 ||
                !program.Instructions.Select(i => i.Index).SequenceEqual([0, 1])) return null;
            var vectors = new TacticInstructionHexTargetsSource[2];
            foreach (var instruction in program.Instructions)
            {
                var selected = ResolveAutomaticBaseInstructionTargets(state, caster, row, instruction,
                    pair.Actor.HexId, pair.Target.HexId);
                if (selected is null) return null;
                vectors[instruction.Index] = selected;
            }
            if (vectors.All(vector => vector.OrderedTargets.Length == 0)) continue;
            retained.Add(new(slot, program, vectors));
        }
        return new(caster.Id, pair.Target with { }, pair.Actor.HexId, pair.Target.HexId,
            available.ToArray(), retained.ToArray());

        bool ActualHex(int hex) => hex is >= 0 and < 48400 &&
            _definitions.ReadHexTableReference(hex) is { PhysicalSlot: int physical } reference &&
            physical == hex;
    }

    /// <summary>One TC16BF20 instruction, all native opcodes0..23. Source and
    /// primary centers are the captured descriptor Hexes, not a fabricated Army.
    /// Base footprints are deduplicated by actual captured Object identity.
    /// A selected-centered vector without its exact center is wholly cleared.</summary>
    internal TacticInstructionHexTargetsSource? ResolveAutomaticBaseInstructionTargets(
        GameState state, UnitState caster, TacticSourceRow row,
        S14TacticEffectInstruction instruction, int sourceDescriptorHex, int primaryDescriptorHex)
        => ResolveAutomaticInstructionHexTargets(state, caster, row, instruction,
            sourceDescriptorHex, primaryDescriptorHex);

    //16BF20 is shared by Army and Base primary participants. Keep its actual
    //ordered Hex vectors and entity admission in one implementation.
    internal TacticInstructionHexTargetsSource? ResolveAutomaticInstructionHexTargets(
        GameState state, UnitState caster, TacticSourceRow row,
        S14TacticEffectInstruction instruction, int sourceDescriptorHex, int primaryDescriptorHex)
    {
        if (!Bound(state) || ArmyValid(state, caster) != true ||
            instruction.Opcode > 23 || caster.NativeArmyOrigin?.NativeSlotIndex is not int sourceSlot)
            return null;
        int opcode = instruction.Opcode;
        if (opcode == 0) return new(instruction.Index, []);
        bool primaryCentered = opcode is 1 or 2 or 3 or 4 or 10 or 11 or 12 or 13 or 14 or 16 or 17 or 18 or 22 or 23;
        if (sourceDescriptorHex is < 0 or >= 48400 || primaryDescriptorHex is < 0 or >= 48400) return null;
        var source = _definitions.ReadArmyUnitTableReference(sourceSlot);
        if (source is null) return null;
        int sourceForce = _definitions.ReadArmyUnitRawForceSlot(source);
        return ResolveAutomaticInstructionHexTargets(state, sourceForce, row, instruction,
            sourceDescriptorHex, primaryDescriptorHex);
    }

    //16BF20 receives real source/primary Hexes. Its receiver filtering uses
    //the source Force, independent of whether the actor is an Army or Base.
    internal TacticInstructionHexTargetsSource? ResolveAutomaticInstructionHexTargets(
        GameState state, int sourceForce, TacticSourceRow row,
        S14TacticEffectInstruction instruction, int sourceDescriptorHex, int primaryDescriptorHex)
    {
        if (!Bound(state) || instruction.Opcode > 23) return null;
        int opcode = instruction.Opcode;
        if (opcode == 0) return new(instruction.Index, []);
        bool primaryCentered = opcode is 1 or 2 or 3 or 4 or 10 or 11 or 12 or 13 or 14 or 16 or 17 or 18 or 22 or 23;
        if (sourceDescriptorHex is < 0 or >= 48400 || primaryDescriptorHex is < 0 or >= 48400) return null;
        if (opcode == 17)
        {
            var primaryHex = _definitions.ReadHexTableReference(primaryDescriptorHex);
            if (primaryHex is null || _definitions.ReadHexForceSlot(primaryHex) is not byte territoryForce) return null;
            bool? hostileCenter = ArmyLogisticsPreparation.ReadNativeRelationGate(state, sourceForce, territoryForce);
            if (hostileCenter is null) return null;
            if (!hostileCenter.Value) return new(instruction.Index, []);
        }
        var result = new List<AutomaticTacticCurrentHexTarget>();
        var seenObjects = new HashSet<int>();
        int center = primaryCentered ? primaryDescriptorHex : sourceDescriptorHex;
        foreach (int hex in NativeHexOrder(center, instruction.Raw64))
        {
            var hexReference = _definitions.ReadHexTableReference(hex);
            if (hexReference is null) return null;
            if (hexReference.PhysicalSlot is null) continue;
            if (hexReference.PhysicalSlot != hex) return null;
            var read = FieldHexOccupantSource.ReadCurrent(state, _definitions, hex);
            if (read.Receipt is not { } entity) return null;
            int kind = entity.NativeKind;
            if (kind == 255 && opcode != 17) continue;
            int force = 0;
            UnitState? targetArmy = null;
            BaseReference? targetBase = null;
            if (kind is 5 or 6)
            {
                if (ReadCurrentTargetFlags(state, row) is not byte flags) return null;
                if ((flags & 1) == 0) continue;
                if (entity.CapturedObject is not { } captured ||
                    _definitions.ReadObjectNativeValidity(captured) is not bool objectValid) return null;
                if (!objectValid) continue;
                if (_definitions.ReadObjectPhysicalSlot(captured) is not int objectIdentity) return null;
                if (seenObjects.Contains(objectIdentity)) continue;
                targetBase = kind == 5 ? _definitions.ReadCityTableReference(entity.NativePhysicalSlot)
                    : _definitions.ReadGateTableReference(entity.NativePhysicalSlot);
                if (targetBase is null || _definitions.ReadBaseNativeValidity(targetBase) is not bool valid) return null;
                if (!valid) continue;
                if (entity.BaseLocationId is not int location || BaseExcluded(state, location) is not bool excluded) return null;
                if (excluded) continue;
                seenObjects.Add(objectIdentity);
                if (_definitions.ReadObjectForceSlot13(captured) is not byte baseForce) return null;
                force = baseForce;
            }
            else if (kind == 27)
            {
                if (entity.GameplayArmyId is not int targetId || !state.Units.TryGetValue(targetId, out targetArmy) ||
                    _definitions.ReadArmyUnitTableReference(entity.NativePhysicalSlot) is not { } army ||
                    _definitions.ReadArmyUnitNativeValidity(army) is not bool valid) return null;
                if (!valid) continue;
                if (ArmyExcluded(state, targetId) is not bool excluded) return null;
                if (excluded) continue;
                force = _definitions.ReadArmyUnitRawForceSlot(army);
            }
            else if (opcode != 17) continue;

            bool? accepted = opcode switch
            {
                1 or 2 or 3 or 4 or 16 => Hostile(),
                5 or 6 or 7 or 8 or 9 or 21 => force == sourceForce && kind == 27,
                10 or 11 or 12 or 13 or 14 or 22 => HostileArmy(),
                15 => force == sourceForce,
                17 => hex != sourceDescriptorHex,
                18 => HostileNonArmy(),
                19 => Abnormal(),
                20 => Healing(),
                23 => Hostile(), //Common Base admission above already requires current44 bit0.
                _ => null
            };
            if (accepted is null) return null;
            if (accepted.Value) result.Add(new(hex, entity));

            bool? Hostile() => ArmyLogisticsPreparation.ReadNativeRelationGate(state, sourceForce, force);
            bool? HostileArmy() { var hostile = Hostile(); return hostile is null ? null : hostile.Value && kind == 27; }
            bool? HostileNonArmy() { var hostile = Hostile(); return hostile is null ? null : hostile.Value && kind is 5 or 6 or 11; }
            bool? Abnormal()
            {
                if (force != sourceForce) return false;
                if (targetArmy is not null)
                {
                    var reference = _definitions.ReadRegistryTableReference(RegistryKind.Army, entity.NativePhysicalSlot);
                    if (reference is null) return null;
                    for (int offset = 0x23; offset <= 0x25; offset++)
                    {
                        if (_definitions.ReadTacticClearByte(reference, offset) is not byte abnormal) return null;
                        if (abnormal != 0) return true;
                    }
                    return false;
                }
                if (targetBase is null) return false;
                var fields = _definitions.ReadCaptureBaseThreatFields(targetBase);
                byte?[] bytes = [fields?.Byte49, fields?.Byte4A, fields?.Byte4B];
                foreach (byte? value in bytes) { if (value is null) return null; if (value.Value != 0) return true; }
                return false;
            }
            bool? Healing()
            {
                if (force != sourceForce || kind != 27) return false;
                var army = _definitions.ReadArmyUnitTableReference(entity.NativePhysicalSlot);
                if (army is null || _definitions.ReadArmyUnitCommanderId(army) is not ushort commander) return null;
                var person = _definitions.ReadPersonLookupReference(commander <= 6000 ? commander : 0);
                if (person is null) return null;
                bool? valid = person.PhysicalSlot is int physical ? _definitions.ReadPersonNativeValidity(physical) : false;
                if (valid is null) return null;
                if (!valid.Value) return false;
                if (targetArmy?.WoundedTroops is not int wounded) return null;
                if (wounded == 0) return false;
                int? capacity = ReadArmyHealingSelectionCapacity(state, targetArmy);
                return capacity is null ? null : targetArmy.Troops < capacity.Value;
            }
        }
        if (primaryCentered && !result.Any(target => target.HexId == primaryDescriptorHex)) result.Clear();
        return new(instruction.Index, result.ToArray());
    }
}
