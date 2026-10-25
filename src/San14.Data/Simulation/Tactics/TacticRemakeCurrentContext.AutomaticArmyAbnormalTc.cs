using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>Current CE SC175D50/TC175FF0's source-Army/target-Army
    /// opcode23 strength input. The paired bodies and their gate/factor
    /// consumers share these operations, including the explicit -1 lower
    /// coefficient. The existing status0..2 owner consumes this result.</summary>
    internal TacticEffectQueryResult<TacticAbnormalStrengthSourceInput> ResolveTcAutomaticArmyAbnormalStrength(
        GameState state, UnitState caster, UnitState target, TacticSourceRow source,
        S14TacticEffectInstruction instruction, int stageOrdinal)
    {
        if (instruction.Opcode != 23 || instruction.Index is < 0 or > 1)
            throw new InvalidDataException("TC Army abnormal strength needs the reached opcode23 instruction.");
        if (!Bound(state) || ArmyValid(state, caster) != true || ArmyValid(state, target) != true)
            throw MissingTc("current_Army_receivers");
        //175FF0 prepares its stage multiplier at instruction entry, not
        //after a successful target gate. The caller also reaches this prefix
        //for a wholly empty instruction vector.
        int stageStep = P(SimulationParameterKeys.Tactics.EffectStageScale);
        int sourcePerson = CommanderPhysical(state, caster) ?? throw MissingTc("caster_Commander");
        bool early = K(TacticRemakeCurrentTraits.CommanderHasEffect(_definitions, sourcePerson, 98),
            "caster_effect98");
        if (!early)
        {
            //16F9A0 reads16D840 then16E820. Only their selected dimension
            //is consumed; the both-dimension16B6D0 prefix belongs to15/20.
            int sourceAbility = SelectedAbility(caster), targetAbility = SelectedAbility(target);
            int baseline = P(SimulationParameterKeys.Tactics.AbnormalGateBaseline);
            //The other coefficient is an unconsumed formal of the shared
            //math record, not a fallback for an unknown reached parameter.
            int higher = sourceAbility > targetAbility
                ? P(SimulationParameterKeys.Tactics.AbnormalGateHigherAbilityScale) : 0;
            int lower = sourceAbility <= targetAbility
                ? P(SimulationParameterKeys.Tactics.AbnormalGateLowerAbilityScale) : 1;
            int[] traits = TacticRemakeCurrentTraits.ArmyContributions(_definitions,
                sourcePerson, ArmyTraitSourceAdapter.Encode(state, caster))
                ?? throw MissingTc("caster_Army_trait99");
            var gate = new TacticEffectGateSourceInput(sourceAbility, targetAbility, baseline,
                higher, lower, traits[99], P(SimulationParameterKeys.Tactics.AbnormalGateTraitRate));
            var tactic = _definitions.ReadSkillTacticReference(source.Slot)
                ?? throw MissingTc("current_Tactic_pointer");
            int physical = K(_definitions.ReadSkillTacticPhysicalSlot(tactic), "current_Tactic_physical");
            int sourceHex = ArmyHex(caster), targetHex = ArmyHex(target);
            float failure = TacticEffectSourceGateMath.FailurePercentTc(gate);
            TacticSourceSamplerWorld? world = failure <= 0f ? null : ReadCurrentSamplerWorld(state);
            bool allowed = TacticEffectSourceGateMath.AllowsTc(gate, physical, sourceHex, targetHex, world)
                ?? throw MissingTc("gate_current_World450_45C");
            if (!allowed)
                return new(new(new(instruction.EffectValue, 0f, stageOrdinal, stageStep, 0f), false, 0),
                    state.RandomState);
        }
        //16B820 repeats its current source and target selected-ability
        //queries after the successful/early gate; it does not query a chain.
        int acting = SelectedAbility(caster), receiving = SelectedAbility(target);
        byte selector = FactorSelector(source) ?? throw MissingTc("factor_selector6A");
        int divisor = P(selector == 1 ? SimulationParameterKeys.Tactics.StrengthDifferenceDivisor
            : SimulationParameterKeys.Tactics.IntelligenceDifferenceDivisor);
        if (divisor == 0) throw MissingTc("factor_divisor_zero");
        float factor = (float)unchecked(acting - receiving) / (float)divisor;
        factor += 1f;
        float floor = (float)P(SimulationParameterKeys.Tactics.EffectFactorFloorPercent) / 100f;
        factor = factor > floor ? factor : floor; //MAXSS: second operand on equal/unordered.
        var runtime = caster.NativeTacticRuntime ?? throw MissingTc("caster_waiting21_22");
        float shared = runtime.Waiting21 != 0 ? 1f :
            (float)P(runtime.Waiting22 == 0 ? SimulationParameterKeys.Tactics.Waiting0SharedFactorPercent
                : SimulationParameterKeys.Tactics.Waiting1SharedFactorPercent) / 100f;
        //16F850(22C990(caster),2) is bit ordinal2, i.e. mask4.
        var army = _definitions.ReadArmyUnitTableReference(K(caster.NativeArmyOrigin?.NativeSlotIndex,
            "caster_physical")) ?? throw MissingTc("caster_pointer");
        int force = K(_definitions.ReadForcePhysicalSlot(_definitions.ReadArmyUnitForceReference(army)),
            "caster_current_Force22C990");
        byte flags = state.NativeArmySimulation.Forces.GetValueOrDefault(force)?.Flags193
            ?? throw MissingTc("caster_current_Force193");
        int special = (flags & 4) != 0 ? P(SimulationParameterKeys.Tactics.SpecialForceEffectPercent) : 100;
        return new(new(new(instruction.EffectValue, factor, stageOrdinal, stageStep, shared), true, special),
            state.RandomState);

        int SelectedAbility(UnitState army)
        {
            byte selected = FactorSelector(source) ?? throw MissingTc("current_selector6A");
            int dimension = selected == 1 ? 1 : 2;
            int? value = _abilities is TacticRemakeCurrentAbilities owned
                ? owned.ResolveDimension(state, army.OfficerId, dimension)
                : dimension == 1 ? _abilities?.Resolve(state, army.OfficerId)?.Strength
                    : _abilities?.Resolve(state, army.OfficerId)?.Intelligence;
            return value ?? throw MissingTc("current_selected_ability" + dimension);
        }
        int ArmyHex(UnitState unit)
        {
            var reference = _definitions.ReadArmyUnitTableReference(K(unit.NativeArmyOrigin?.NativeSlotIndex,
                "Army_physical")) ?? throw MissingTc("Army_pointer");
            int raw = K(_definitions.ReadArmyUnitRawHex2A(reference), "Army_live_Hex2A");
            var hex = _definitions.ReadHexTableReference(raw < 48400 ? raw : 0)
                ?? throw MissingTc("Army_live_Hex_pointer");
            return K(_definitions.ReadHexPhysicalSlot(hex), "Army_live_Hex22DEF0");
        }
        int P(string key)
        {
            var parameters = state.NativeArmySimulation.Parameters;
            return !parameters.Float32Values.ContainsKey(key) && parameters.Int32Values.TryGetValue(key, out int value)
                ? value : throw MissingTc(key);
        }
        static T K<T>(T? value, string owner) where T : struct => value ?? throw MissingTc(owner);
        static InvalidDataException MissingTc(string owner) =>
            new($"Current TC automatic Army abnormal strength owner is unresolved: {owner}.");
    }
}
