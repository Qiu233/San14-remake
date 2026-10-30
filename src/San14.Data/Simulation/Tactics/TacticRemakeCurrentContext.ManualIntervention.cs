using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record ManualTacticCapturedUse(S14TacticEffectProgram Program,
    TacticInstructionTargetsSource[] Instructions);

public sealed partial class TacticRemakeCurrentContext
{
    private sealed record ManualEmptyChainCapture(int ActorUnitId, int TacticId, ushort WaitingContribution);
    //Only this detached activation context holds the pre-effect chain result.
    //It is neither a world flag nor a reusable cache for later commands.
    private ManualEmptyChainCapture? _manualEmptyChain;
    internal bool? ResolveManualPrimaryHex(GameState state, int hex, out UnitState? primary)
    {
        primary = null;
        if (!Bound(state) || hex is < 0 or >= 48400 ||
            _definitions.ReadHexTableReference(hex)?.PhysicalSlot != hex || !TryOccupant(state, hex, out var occupant)) return null;
        if (occupant?.BaseLocation is not null) return null;
        if (occupant?.Army is not { } army) return false;
        bool? valid = ArmyValid(state, army);
        if (valid == true) primary = army;
        return valid;
    }

    internal bool? ReadManualTacticValidity(GameState state, int tacticId)
    {
        if (!Bound(state)) return null;
        var reference = _definitions.ReadSkillTacticReference(tacticId is >= 0 and <= 200 ? tacticId : 0);
        return reference is null ? null : _definitions.ReadSkillTacticValidity(reference);
    }

    /// <summary>16F230's manual eligibility, separate from automatic17D450.
    /// The caller supplies the reached World period count, never a tick ordinal.</summary>
    internal bool? ReadManualInterventionEligibility(GameState state, UnitState caster,
        int tacticId, int pastPeriods)
    {
        if (!Bound(state) || caster.NativeArmyOrigin?.NativeSlotIndex is not int physical ||
            state.ReadArmyLifecycle()?.ReceiverSlots() is not { } slots || physical < 0 || physical >= slots.Length ||
            slots[physical].PhysicalSlot != physical || slots[physical].ActiveUnitId != caster.Id) return null;
        //The operation's generic definition capture does not eagerly bind all
        //Army headers. Import this actual saved header only if still unbound;
        //an explicitly bound unknown is not repaired from another owner.
        _definitions.BindUnspecifiedOwnedArmyQueryHeader(slots[physical]);
        if (_definitions.ReadArmyUnitCommanderId(new(physical)) is not ushort commander ||
            state.CommanderSelection?.ActiveCount is not int count) return null;
        int previous = 0;
        for (int ordinal = 0; ordinal < count; ordinal++)
        {
            if (state.CommanderSelection.Candidates is not { } candidates || ordinal >= candidates.Length ||
                candidates[ordinal].OfficerId is not int selected || selected is < 0 or > ushort.MaxValue) return null;
            if (selected != commander) continue;
            if (candidates[ordinal].UninterpretedValue is not int value) return null;
            previous = value;
            break;
        }
        if (Parameter(state, SimulationParameterKeys.Tactics.ManualInterventionMinimumPeriods) is not int minimum)
            return null;
        if (unchecked(pastPeriods - previous) < minimum) return false;
        var person = _definitions.ReadPersonLookupReference(commander < 6001 ? commander : 0);
        if (person?.PhysicalSlot is not int personPhysical ||
            _definitions.ReadArmyReturnPersonFlags196(personPhysical) is not ushort flags ||
            caster.NativeTacticRuntime is not { } native) return null;
        if ((flags & 0x20) != 0 || native.Abnormal23 != 0) return false;
        bool? available = ReadManualInterventionTargetAvailability(state, caster, tacticId);
        return available is not true ? available : native.Waiting20 == 0;
    }

    /// <summary>16F360's generic Hex scanner. The explicit execution target
    /// remains separate from its final no-primary availability fallback.</summary>
    private bool? ReadManualInterventionTargetAvailability(GameState state, UnitState caster, int tacticId)
    {
        if (EffectiveFormation(state, caster) is not int formation ||
            ReadArmyNativePrimaryRange(state, caster) is not int range ||
            caster.NativeTacticRuntime is not { } native ||
            caster.NativeArmyOrigin?.NativeSlotIndex is not int physical ||
            ArmyMarchSourceAdapter.ReadCurrentHex(state, caster) is not ushort sourceHex ||
            _definitions.ReadHexTableReference(sourceHex)?.PhysicalSlot != sourceHex ||
            ReadCurrentProgram(state, _catalog.Rows[tacticId]) is not { } program || !ManualProgram(program)) return null;
        if (formation is < 1 or > 20) return false;
        byte expectedKind = 255;
        ushort expectedId = 0;
        if (native.Abnormal25 != 0)
        {
            var reference = _definitions.ReadRegistryTableReference(RegistryKind.Army, physical);
            var army = _definitions.ReadArmyUnitTableReference(physical);
            if (reference is null || army is null ||
                _definitions.ReadTacticClearByte(reference, 0x3C) is not byte kind ||
                _definitions.ReadTacticArmyTarget3E(army) is not ushort identity) return null;
            expectedKind = kind; expectedId = identity;
        }
        foreach (int hex in NativeHexOrder(sourceHex, range).Skip(1))
        {
            var selected = FieldHexOccupantSource.ReadCurrent(state, _definitions, hex);
            if (selected.Receipt is not { } occupant) return null;
            if (occupant.NativeKind is 255 or 11) continue;
            //16F360's Gate footprint can lie within the ring while its
            //actual linked Object center is farther away. Keep this extra gate.
            if (occupant.NativeKind == 6)
            {
                var gate = _definitions.ReadGateTableReference(occupant.NativePhysicalSlot);
                if (gate is null || _definitions.ReadBaseNativeValidity(gate) is not bool valid) return null;
                if (valid && LinkedDistance(sourceHex, _definitions.ReadBaseCurrentHexIndex(gate)) > range) continue;
            }
            if (native.Abnormal25 != 0 &&
                (occupant.NativeKind != expectedKind || occupant.NativePhysicalSlot != expectedId)) continue;
            var vectors = CaptureManualInstructionHexVectorsCurrent(state, caster, tacticId, program, sourceHex, hex);
            if (vectors is null) return null;
            if (vectors.Any(vector => vector.OrderedTargets.Length != 0)) return true;
        }
        //The no-primary fallback tests exact caster Hex membership. It
        //does not authorize an empty Hex as174020's execution target.
        var fallback = CaptureManualInstructionHexVectorsCurrent(state, caster, tacticId, program, sourceHex, null);
        return fallback is null ? null : fallback.Any(vector => vector.OrderedTargets.Any(node => node.HexId == sourceHex));
    }

    internal ManualTacticCapturedUse? CaptureManualInterventionUse(GameState state,
        UnitState caster, UnitState primary, int tacticId)
    {
        if (!Bound(state) || ArmyValid(state, caster) != true || ArmyValid(state, primary) != true ||
            ReadCurrentProgram(state, _catalog.Rows[tacticId]) is not { } program || !ManualProgram(program)) return null;
        var vectors = CaptureManualVectors(state, caster, primary, tacticId, program);
        return vectors is null ? null : new(program, vectors);
    }

    private TacticInstructionTargetsSource[]? CaptureManualVectors(GameState state,
        UnitState caster, UnitState? primary, int tacticId, S14TacticEffectProgram program)
    {
        //Compatibility for the old Army-only effect-query API. The actual
        //manual executor consumes the generic captured plan instead.
        if (ArmyMarchSourceAdapter.ReadCurrentHex(state, caster) is not ushort sourceHex) return null;
        var vectors = CaptureManualInstructionHexVectorsCurrent(state, caster, tacticId, program, sourceHex, primary?.HexId);
        if (vectors is null || vectors.Any(vector => vector.OrderedTargets.Any(node => node.Entity.NativeKind is 5 or 6))) return null;
        return vectors.Select(vector => new TacticInstructionTargetsSource(vector.OrderedTargets
            .Where(node => node.Entity.NativeKind == 27).Select(node => node.Entity.GameplayArmyId!.Value).ToArray(),
            true, state.RandomState)).ToArray();
    }

    internal bool HasManualEmptyChain(GameState state, UnitState caster, int tacticId)
    {
        if (ArmyValid(state, caster) != true ||
            ArmyChainCurrentCandidates.Read(state, _definitions, caster).Count != 0) return false;
        if (_programs.ReadCurrentWaitingContribution(_catalog.Rows[tacticId]) is not ushort contribution) return false;
        _manualEmptyChain = new(caster.Id, tacticId, contribution);
        return true;
    }

    internal NativeTacticWaitingAssignment? ResolveManualActivationWaiting(GameState state, UnitState caster, int tacticId)
    {
        if (EffectiveFormation(state, caster) is not int formation ||
            ResolveChain(state, caster, _catalog.Rows[tacticId]) is not { ChainCount: 0 } chain ||
            state.NativeArmySimulation!.Forces.GetValueOrDefault(caster.ForceId)?.Flags193 is not byte flags) return null;
        int? first = Parameter(state, SimulationParameterKeys.Tactics.PrimaryWaitingCap),
            second = Parameter(state, SimulationParameterKeys.Tactics.SecondaryWaitingCap),
            third = Parameter(state, SimulationParameterKeys.Tactics.TertiaryWaitingCap);
        int? adjustment = formation switch
        {
            1 => Parameter(state, SimulationParameterKeys.Tactics.Formation1ManualWaitingAdjustment),
            2 => Parameter(state, SimulationParameterKeys.Tactics.Formation2ManualWaitingAdjustment),
            14 => Parameter(state, SimulationParameterKeys.Tactics.Formation14ManualWaitingAdjustment), _ => 0
        };
        int? reduction = (flags & 0x40) != 0 ? Parameter(state, SimulationParameterKeys.Tactics.ForceFlagWaitingReduction) : 0;
        if (first is not >= 0 or > 255 || second is not >= 0 or > 255 || third is not >= 0 or > 255 ||
            adjustment is null || reduction is null) return null;
        //174020 has no automatic category-policy subtraction. The shared pure
        //arithmetic is valid with that independent term explicitly zero.
        var assigned = TacticEffectSourceWaiting.AfterActivation(new(chain.SumOfNativeWaitingContributions,
            0, adjustment.Value, reduction.Value, 0, first.Value, second.Value, third.Value));
        return new(assigned.Primary20, assigned.Second21, assigned.Third22, $"manual-current:{Revision}");
    }

    internal TacticUseSourceResolution? ResolveManualCapturedEffects(GameState state, UnitState caster,
        int tacticId, ManualTacticCapturedUse use) => new TacticEffectSourceRules(_catalog, this,
            new(new HexTravelMap(_map))).ResolveCapturedInstructionUse(state, caster, tacticId, use.Program, use.Instructions);

    private static bool ManualProgram(S14TacticEffectProgram program) => program.Instructions.Count == 2 &&
        program.Instructions.Select(instruction => instruction.Index).SequenceEqual([0, 1]) &&
        program.Instructions.All(instruction => instruction.Opcode <= 23);
}
