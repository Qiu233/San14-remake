using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record BaseTacticEffectApplicationResult(GameState State, bool Applied,
    BaseTacticEffectApplicationReceipt? Receipt);

//The caller attaches the real target-list node's captured Hex to the17A1C0
//semantic marker. A current Base Hex is not a substitute for that list node.
internal sealed record BaseTacticEffectApplicationReceipt(int SourceArmyId, BaseReference Target,
    int TacticSlot, int InstructionIndex, byte Opcode, int StageOrdinal,
    int? RequestedMoraleIncrease, byte? MoraleBefore, byte? MoraleAfter, bool MoraleWritten,
    byte? Counter0After, byte? Counter1After, byte? Counter2After);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>1763B0's actual Base branch. The operation owns current and
    /// context; this method neither captures/exports a world nor appends a
    /// duplicate instruction marker. These two opcodes do not sample RNG.</summary>
    internal BaseTacticEffectApplicationResult ApplyBorrowedBaseTacticMorale(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, int casterId,
        TacticSourceRow source, BaseReference target, S14TacticEffectInstruction instruction,
        int stageOrdinal, NativeArmyCombatCycleSampler sampler)
    {
        ArgumentNullException.ThrowIfNull(sampler);
        var caster = BaseTacticCaster(state, current, context, casterId, source, target, instruction, 15);
        //The native instruction computes its source-ability/stage/waiting
        //prefix before traversing its target list, even for an invalid target.
        var scale = context.ResolveBorrowedBaseTacticMoraleScale(state, caster, source,
            instruction, stageOrdinal);
        return ApplyBorrowedBaseTacticMorale(state, current, context, casterId, source, target,
            instruction, stageOrdinal, sampler, scale);
    }

    /// <summary>Per-Base writer after the caller has resolved the actual
    /// instruction prefix once, including when its complete vector is empty.
    /// It does not requery source abilities, selector, stage or waiting.</summary>
    internal BaseTacticEffectApplicationResult ApplyBorrowedBaseTacticMorale(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, int casterId,
        TacticSourceRow source, BaseReference target, S14TacticEffectInstruction instruction,
        int stageOrdinal, NativeArmyCombatCycleSampler sampler, TacticEffectScaleSourceInput scale)
    {
        ArgumentNullException.ThrowIfNull(sampler); ArgumentNullException.ThrowIfNull(scale);
        _ = BaseTacticCaster(state, current, context, casterId, source, target, instruction, 15);
        return ApplyBorrowedBaseTacticMoraleTargetCurrent(state, current, casterId, source, target,
            instruction, stageOrdinal, scale);
    }

    private BaseTacticEffectApplicationResult ApplyBorrowedBaseTacticMoraleTargetCurrent(GameState state,
        SourceWorldContext current, int casterId, TacticSourceRow source, BaseReference target,
        S14TacticEffectInstruction instruction, int stageOrdinal, TacticEffectScaleSourceInput scale)
    {
        if (scale.EffectValue != instruction.EffectValue || scale.StageOrdinal != stageOrdinal)
            throw new InvalidDataException("Base morale scale belongs to another instruction or stage.");
        int amount = TacticEffectSourceMath.Scale(scale);
        bool valid = BaseTacticNeed(current.ReadBaseNativeValidity(target), "morale_Base_validity");
        if (!valid) return new(state, false, null);

        int forceSlot = BaseTacticNeed(current.ReadBaseForceSlot(target), "morale_Base_Force_virtualB0");
        if (forceSlot < 0) throw BaseTacticMissing("morale_Base_negative_Force_index");
        var force = current.ReadForceTableReference(forceSlot <= 51 ? forceSlot : 0)
            ?? throw BaseTacticMissing("morale_Base_Force_pointer22CAD0");
        int upper = BasePostLoad.ReadMoraleUpper(force, current);
        byte before = BaseTacticNeed(current.ReadBaseMorale44(target), "morale_Base44");
        int requested = Math.Min(upper, unchecked((int)before + amount));
        //The actual241790 owner performs two fresh Force queries, the
        //special46..50 skip and a distinct fresh2690D0 bound/clamp.
        bool written = BasePostLoad.WriteMoraleCurrent(target, requested, current);
        var working = PublishCurrentBaseCombatQuantities(state, current, target, false, false, written);
        byte after = BaseTacticNeed(current.ReadBaseMorale44(target), "morale_Base44_after241790");
        return new(working, true, new(casterId, target, source.Slot, instruction.Index,
            instruction.Opcode, stageOrdinal, amount, before, after, written, null, null, null));
    }

    /// <summary>1782F0's actual Base branch. The three231480 calls are
    /// interleaved with writes49/4A/4B. Base4C and all other fields survive;
    /// the Army25-positive override reset is not a Base operation.</summary>
    internal BaseTacticEffectApplicationResult ApplyBorrowedBaseTacticClear(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, int casterId,
        TacticSourceRow source, BaseReference target, S14TacticEffectInstruction instruction,
        int stageOrdinal, NativeArmyCombatCycleSampler sampler)
    {
        ArgumentNullException.ThrowIfNull(sampler);
        _ = BaseTacticCaster(state, current, context, casterId, source, target, instruction, 19);
        return ApplyBorrowedBaseTacticClearTargetCurrent(state, current, casterId, source, target,
            instruction, stageOrdinal);
    }

    private BaseTacticEffectApplicationResult ApplyBorrowedBaseTacticClearTargetCurrent(GameState state,
        SourceWorldContext current, int casterId, TacticSourceRow source, BaseReference target,
        S14TacticEffectInstruction instruction, int stageOrdinal)
    {
        bool valid = BaseTacticNeed(current.ReadBaseNativeValidity(target), "clear_Base_validity");
        if (!valid) return new(state, false, null);
        var fields = current.ReadCaptureBaseThreatFields(target)
            ?? throw BaseTacticMissing("clear_Base49_4A_4B_current_owner");
        var working = state;
        byte[] after = new byte[3];
        for (int index = 0; index < 3; index++)
        {
            //231480 ignores its formal index, but is called afresh after
            //each preceding write. A negative cap is truncated to byte.
            int upper = BaseTacticReadParameter(working, SimulationParameterKeys.Tactics.AbnormalCounterUpperBound);
            byte value = upper < 0 ? unchecked((byte)upper) : (byte)0;
            fields = index switch { 0 => fields with { Byte49 = value },
                1 => fields with { Byte4A = value }, _ => fields with { Byte4B = value } };
            current.SetCaptureBaseThreatFields(target, fields);
            working = PublishOneBaseTacticCounter(working, target, 0x49 + index, value);
            after[index] = value;
        }
        return new(working, true, new(casterId, target, source.Slot, instruction.Index,
            instruction.Opcode, stageOrdinal, null, null, null, false, after[0], after[1], after[2]));
    }

    private static UnitState BaseTacticCaster(GameState state, SourceWorldContext current,
        TacticRemakeCurrentContext context, int casterId, TacticSourceRow source,
        BaseReference target, S14TacticEffectInstruction instruction, byte expectedOpcode)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(target); ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(instruction);
        if (instruction.Opcode != expectedOpcode || instruction.Index is < 0 or > 1 || source.Slot is < 0 or > 200)
            throw new InvalidDataException("Base tactic support writer needs the reached actual instruction.");
        var caster = RequireLatestAutomaticActorImageCurrent(state, current, casterId);
        if (!context.IsBoundCurrentState(state) || context.ArmyValid(state, caster) != true)
            throw BaseTacticMissing("source_Army_current_identity");
        return caster;
    }

    private static GameState PublishOneBaseTacticCounter(GameState state, BaseReference target,
        int offset, byte value)
    {
        var simulation = state.NativeArmySimulation ?? throw BaseTacticMissing("clear_saved_Parameters");
        var parameters = simulation.Parameters;
        if (parameters.Float32Values.ContainsKey(CaptureBaseThreatInputs.VersionKey) ||
            !parameters.Int32Values.TryGetValue(CaptureBaseThreatInputs.VersionKey, out int version) || version != 1)
            throw BaseTacticMissing("clear_saved_Base_runtime_version");
        string key = CaptureBaseThreatInputs.Key(target, offset);
        if (parameters.Float32Values.ContainsKey(key)) throw BaseTacticMissing("clear_saved_counter_integer_owner");
        var values = new Dictionary<string, int>(parameters.Int32Values) { [key] = value };
        return state with { NativeArmySimulation = simulation with
            { Parameters = parameters with { Int32Values = values } } };
    }

    private static int BaseTacticReadParameter(GameState state, string key)
    {
        var parameters = state.NativeArmySimulation?.Parameters ?? throw BaseTacticMissing("current_Parameters");
        return !parameters.Float32Values.ContainsKey(key) && parameters.Int32Values.TryGetValue(key, out int value)
            ? value : throw BaseTacticMissing(key);
    }
    private static T BaseTacticNeed<T>(T? value, string owner) where T : struct =>
        value ?? throw BaseTacticMissing(owner);
    private static InvalidDataException BaseTacticMissing(string owner) =>
        new($"Current Army-to-Base tactic support owner is unresolved: {owner}.");
}

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>16B6D0 then stage49A84 then16D900, for an actual Army
    /// caster. Unlike the older Army helper this does not assert chain0 or
    /// hardcode frame+8 to0: neither is a query made by1763B0's prefix.</summary>
    internal TacticEffectScaleSourceInput ResolveBorrowedBaseTacticMoraleScale(GameState state,
        UnitState caster, TacticSourceRow source, S14TacticEffectInstruction instruction, int stageOrdinal)
    {
        if (ArmyValid(state, caster) != true) throw SupportMissing("source_Army_validity");
        //16B6D0 reads both dimensions before decoding selector6A/slot9.
        int strength = Ability(1), intelligence = Ability(2);
        byte selector = FactorSelector(source) ?? throw SupportMissing("source_current_selector6A");
        string divisorKey = selector == 1 ? SimulationParameterKeys.Tactics.StrengthEffectDivisor
            : SimulationParameterKeys.Tactics.IntelligenceEffectDivisor;
        int divisor = Read(divisorKey);
        if (divisor == 0) throw SupportMissing(divisorKey + ":zero");
        float factor = (float)(selector == 1 ? strength : intelligence) / (float)divisor;
        factor += 1f;
        int stage = Read(SimulationParameterKeys.Tactics.EffectStageScale);
        var native = caster.NativeTacticRuntime ?? throw SupportMissing("source_waiting21_22");
        float shared = native.Waiting21 != 0 ? 1f :
            (float)Read(native.Waiting22 == 0 ? SimulationParameterKeys.Tactics.Waiting0SharedFactorPercent
                : SimulationParameterKeys.Tactics.Waiting1SharedFactorPercent) / 100f;
        return new(instruction.EffectValue, factor, stageOrdinal, stage, shared);

        int Ability(int dimension)
        {
            int? value = _abilities is TacticRemakeCurrentAbilities current
                ? current.ResolveDimension(state, caster.OfficerId, dimension)
                : dimension == 1 ? _abilities?.Resolve(state, caster.OfficerId)?.Strength
                    : _abilities?.Resolve(state, caster.OfficerId)?.Intelligence;
            return value ?? throw SupportMissing("source_current_ability" + dimension);
        }
        int Read(string key)
        {
            var parameters = state.NativeArmySimulation?.Parameters ?? throw SupportMissing("current_Parameters");
            return !parameters.Float32Values.ContainsKey(key) && parameters.Int32Values.TryGetValue(key, out int value)
                ? value : throw SupportMissing(key);
        }
        static InvalidDataException SupportMissing(string owner) =>
            new($"Current Base tactic morale prefix is unresolved: {owner}.");
    }
}
