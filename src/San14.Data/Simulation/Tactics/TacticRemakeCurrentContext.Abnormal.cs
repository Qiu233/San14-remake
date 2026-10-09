using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>Gameplay completion of the reached2F2880 trait-adjustment
    /// notification. Its actual templates call only string/name formatters and
    /// append report types0/5. Original presentation records are not materialized
    /// by this provider; their independent CRT stream is not claimed unchanged.</summary>
    public ulong? FinishArmyAbnormalAdjustment(GameState state, UnitState caster, UnitState target,
        TacticSourceRow source, S14TacticEffectInstruction instruction, int nativeIndex,
        TacticAbnormalSourceAdjustment adjustment)
    {
        if (instruction.Opcode != 23 || nativeIndex is < 0 or > 2 || ArmyValid(state, target) != true)
            return null;
        if (!adjustment.CounterWritten || !adjustment.HasTraitAdjustment ||
            adjustment.CauseCode is not (0 or 9 or 10 or 11 or 12 or 98))
            throw new InvalidDataException("Current abnormal adjustment notification differs from its source branch.");
        return state.RandomState;
    }

    /// <summary>Gameplay completion after the native counter/owner writes.
    /// Visual tags23..25 consume a separate CRT presentation stream, and the
    /// actual status-text templates append only report/UI data. Core already
    /// emits its typed effect report; this receipt preserves gameplay RNG only.</summary>
    public ulong? FinishArmyAbnormalStatus(GameState state, UnitState caster, UnitState target,
        TacticSourceRow source, S14TacticEffectInstruction instruction, int nativeIndex,
        TacticAbnormalSourceResult result)
    {
        if (instruction.Opcode != 23 || nativeIndex is < 0 or > 2 || ArmyValid(state, target) != true ||
            target.NativeTacticRuntime is not { } runtime) return null;
        byte counter = nativeIndex switch { 0 => runtime.Abnormal23, 1 => runtime.Abnormal24, _ => runtime.Abnormal25 };
        if (counter != result.CounterAfter || runtime.ProvocationOwner != result.OwnerAfter ||
            result.CauseCode is not (0 or 9 or 10 or 11 or 12 or 21 or 22 or 23 or 24 or 98))
            throw new InvalidDataException("Current abnormal completion belongs to another counter, owner or notification branch.");
        return state.RandomState;
    }

    public TacticEffectQueryResult<TacticAbnormalStrengthSourceInput>? ResolveArmyAbnormalStrength(
        GameState state, UnitState caster, UnitState target, TacticSourceRow source,
        S14TacticEffectInstruction instruction)
    {
        if (instruction.Opcode != 23 || ArmyValid(state, caster) != true || ArmyValid(state, target) != true ||
            CommanderPhysical(state, caster) is not int commander ||
            TacticRemakeCurrentTraits.CommanderHasEffect(_definitions, commander, 98) is not bool early)
            return null;
        byte? selector = FactorSelector(source);
        if (selector is null) return null;
        OfficerAbilities? acting = _abilities?.Resolve(state, caster.OfficerId);
        OfficerAbilities? receiving = _abilities?.Resolve(state, target.OfficerId);
        if (acting is null || receiving is null) return null;
        int sourceAbility = selector == 1 ? acting.Strength : acting.Intelligence;
        int targetAbility = selector == 1 ? receiving.Strength : receiving.Intelligence;
        if (!early)
        {
            int[]? traits = TacticRemakeCurrentTraits.ArmyContributions(_definitions,
                commander, caster.NativeArmyConfiguration?.TraitSlots64);
            int? baseline = Parameter(state, SimulationParameterKeys.Tactics.AbnormalGateBaseline), higher = Parameter(state, SimulationParameterKeys.Tactics.AbnormalGateHigherAbilityScale),
                lower = Parameter(state, SimulationParameterKeys.Tactics.AbnormalGateLowerAbilityScale), traitRate = Parameter(state, SimulationParameterKeys.Tactics.AbnormalGateTraitRate);
            if (traits is null || baseline is null || higher is null || lower is null || traitRate is null ||
                _definitions.ReadHexTableReference(caster.HexId)?.PhysicalSlot != caster.HexId ||
                _definitions.ReadHexTableReference(target.HexId)?.PhysicalSlot != target.HexId) return null;
            bool? allowed = TacticEffectSourceGateMath.Allows(new(sourceAbility, targetAbility,
                baseline.Value, higher.Value, lower.Value, traits[99], traitRate.Value), source.Slot,
                caster.HexId, target.HexId, ReadCurrentSamplerWorld(state));
            if (allowed is null) return null;
            if (!allowed.Value)
                return new(new(new(instruction.EffectValue, 0f, 0, 0, 0f), false, 0), state.RandomState);
        }
        if (ResolveChain(state, caster, source) is not { ChainCount: 0 } ||
            caster.NativeTacticRuntime is not { } runtime ||
            state.NativeArmySimulation!.Forces.GetValueOrDefault(caster.ForceId)?.Flags193 is not byte flags)
            return null;
        int? divisor = Parameter(state, selector == 1 ? SimulationParameterKeys.Tactics.StrengthDifferenceDivisor : SimulationParameterKeys.Tactics.IntelligenceDifferenceDivisor), floor = Parameter(state, SimulationParameterKeys.Tactics.EffectFactorFloorPercent),
            stage = Parameter(state, SimulationParameterKeys.Tactics.EffectStageScale), shared = runtime.Waiting21 != 0 ? 100 :
                Parameter(state, runtime.Waiting22 == 0 ? SimulationParameterKeys.Tactics.Waiting0SharedFactorPercent : SimulationParameterKeys.Tactics.Waiting1SharedFactorPercent),
            special = (flags & 2) != 0 ? Parameter(state, SimulationParameterKeys.Tactics.SpecialForceEffectPercent) : 100;
        if (divisor is null or 0 || floor is null || stage is null || shared is null || special is null) return null;
        float factor = (float)unchecked(sourceAbility - targetAbility) / (float)divisor.Value;
        factor += 1f;
        factor = MathF.Max(factor, (float)floor.Value / 100f);
        return new(new(new(instruction.EffectValue, factor, 0, stage.Value, (float)shared.Value / 100f),
            true, special.Value), state.RandomState);
    }

    private int? CommanderPhysical(GameState state, UnitState army)
    {
        if (ArmyValid(state, army) != true || army.OfficerId is < 0 or > 6000 ||
            _definitions.PersonalityDefinitionSource?.ResourceUri != _catalog.Source.ResourceUri ||
            _definitions.PersonalityDefinitionSource.Sha256 != _catalog.Source.Sha256) return null;
        TransferPersonReference? reference = _definitions.ReadPersonLookupReference(army.OfficerId);
        return reference?.PhysicalSlot is int physical && _definitions.ReadPersonIdentity(physical) is not null
            ? physical : null;
    }

    public TacticEffectQueryResult<TacticAbnormalSourceInput>? ResolveArmyAbnormalStatus(
        GameState state, UnitState caster, UnitState target, TacticSourceRow source,
        S14TacticEffectInstruction instruction, int nativeIndex, int requestedStrength)
    {
        if (nativeIndex is < 0 or > 2 || target.NativeTacticRuntime is not { } runtime) return null;
        bool? valid = ArmyValid(state, target);
        if (valid is null) return null;
        byte counter = nativeIndex switch { 0 => runtime.Abnormal23, 1 => runtime.Abnormal24, _ => runtime.Abnormal25 };
        if (!valid.Value) return Receipt(false, null, null, null, null, null, null, null, null);
        int? targetPerson = CommanderPhysical(state, target);
        if (targetPerson is null) return null;
        bool? general = TacticRemakeCurrentTraits.CommanderImmune(_definitions, targetPerson.Value, 21);
        if (general is null) return null;
        if (general.Value) return Receipt(true, true, null, null, null, null, null, null, null);
        bool? specific = TacticRemakeCurrentTraits.CommanderImmune(_definitions, targetPerson.Value, 22 + nativeIndex);
        if (specific is null) return null;
        if (specific.Value) return Receipt(true, false, true, null, null, null, null, null, null);
        int[]? targetValues = TacticRemakeCurrentTraits.ArmyContributions(_definitions,
            targetPerson.Value, target.NativeArmyConfiguration?.TraitSlots64);
        int? sourcePerson = CommanderPhysical(state, caster);
        int[]? sourceValues = sourcePerson is null ? null : TacticRemakeCurrentTraits.ArmyContributions(
            _definitions, sourcePerson.Value, caster.NativeArmyConfiguration?.TraitSlots64);
        int? generalRate = Parameter(state, SimulationParameterKeys.Tactics.AbnormalGeneralResistanceRate);
        int? specificRate = Parameter(state, nativeIndex switch { 0 => SimulationParameterKeys.Tactics.AbnormalType0ResistanceRate, 1 => SimulationParameterKeys.Tactics.AbnormalType1ResistanceRate, _ => SimulationParameterKeys.Tactics.AbnormalType2ResistanceRate });
        int? sourceRate = Parameter(state, SimulationParameterKeys.Tactics.AbnormalSourceTraitRate);
        if (targetValues is null || sourceValues is null || generalRate is null || specificRate is null || sourceRate is null) return null;
        return Receipt(true, false, false, targetValues[9], targetValues[10 + nativeIndex],
            sourceValues[98], generalRate, specificRate, sourceRate);

        TacticEffectQueryResult<TacticAbnormalSourceInput> Receipt(bool nativeValid,
            bool? generalImmunity, bool? specificImmunity, int? targetGeneral, int? targetSpecific,
            int? sourceTrait, int? generalMultiplier, int? specificMultiplier, int? sourceMultiplier) =>
            new(new(nativeIndex, requestedStrength, counter, nativeValid, generalImmunity, specificImmunity,
                targetGeneral, targetSpecific, sourceTrait, generalMultiplier, specificMultiplier, sourceMultiplier,
                null, null), state.RandomState);
    }

    public TacticEffectQueryResult<TacticCapturedOwnerSource>? ResolveArmyAbnormalCapturedOwner(
        GameState state, UnitState caster, UnitState target, TacticSourceRow source,
        S14TacticEffectInstruction instruction)
    {
        if (ArmyValid(state, caster) != true || !TryOccupant(state, caster.HexId, out Occupant? occupant)) return null;
        if (occupant is null) return new(new(false, null), state.RandomState);
        if (occupant.Army is { } army)
        {
            if (army.NativeArmyOrigin?.NativeSlotIndex is not int ordinal || ordinal is < 1 or > 500) return null;
            var fields = new ArmyOverrideTargetSourceFields(27, (ushort)ordinal);
            var decoded = ArmyOverrideTargetSourceAdapter.Decode(fields,
                state.NativeArmyLifecycle is { } arena ? new(arena) : null);
            return new(new(true, new(army.ForceId, DeploymentTargetKind.Unit, army.Id))
            { OverrideTarget = decoded, SourceResidue = ArmyOverrideTargetSourceAdapter.Residue(
                ArmyOverrideTargetSourceAdapter.Owner(state, target), fields, decoded) }, state.RandomState);
        }
        if (occupant.BaseLocation is int location && occupant.CapturedObject is { } obj &&
            _definitions.ReadObjectForceSlot13(obj) is byte force)
        {
            if (location is < 1 or > 61) return null;
            return new(new(true, new(force, DeploymentTargetKind.Location, location))
            { OverrideTarget = location <= 51
                ? new(ArmyOverrideTargetKind.City, new(ArmyOverrideDestinationKind.City, location))
                : new(ArmyOverrideTargetKind.Gate, new(ArmyOverrideDestinationKind.Gate, location)) }, state.RandomState);
        }
        return null;
    }
}
