using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record AutomaticArmyMoraleDamagePrefix(int CasterId,
    ArmyUnitReference SourceArmy, HexReference SourceHex, float StageFactor, float WaitingFactor);
internal sealed record AutomaticArmyMoraleDamageApplication(GameState State,
    bool TargetResultMarker, bool NativeReturn);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>177990's instruction-entry stage, waiting and source Hex.
    /// The caller captures this once even for an empty ordered target vector.</summary>
    internal AutomaticArmyMoraleDamagePrefix BeginBorrowedAutomaticArmyMoraleDamage(
        GameState state, SourceWorldContext current, TacticRemakeCurrentContext context,
        int casterId, int stageOrdinal)
    {
        if (!context.IsBoundCurrentState(state) ||
            state.NativeArmySimulation?.Parameters.Language != "TC" ||
            !state.Units.TryGetValue(casterId, out var caster))
            throw AutomaticMoraleDamageMissing("current_TC_caster");
        int stage = AutomaticMoraleDamageParameter(state, SimulationParameterKeys.Tactics.EffectStageScale);
        float stageFactor = (float)unchecked(stageOrdinal * stage) / 100f + 1f;
        int physical = AutomaticMoraleDamageNeed(caster.NativeArmyOrigin?.NativeSlotIndex, "source_Army_physical");
        var army = current.ReadArmyUnitTableReference(physical)
            ?? throw AutomaticMoraleDamageMissing("source_Army_pointer");
        //16D900 tests the Army, then reads21 and only the reached22 branch.
        //This entry is the Army-origin wrapper; its Base-origin branch is separate.
        if (!AutomaticMoraleDamageNeed(current.ReadArmyUnitNativeValidity(army), "source_Army_validity"))
            throw AutomaticMoraleDamageMissing("source_Army_origin");
        var native = caster.NativeTacticRuntime ?? throw AutomaticMoraleDamageMissing("source_waiting21_22");
        float waiting = native.Waiting21 != 0 ? 1f :
            (float)AutomaticMoraleDamageParameter(state, native.Waiting22 == 0
                ? SimulationParameterKeys.Tactics.Waiting0SharedFactorPercent
                : SimulationParameterKeys.Tactics.Waiting1SharedFactorPercent) / 100f;
        //177990 resolves this after waiting and retains the pointer across callbacks.
        if (!AutomaticMoraleDamageNeed(current.ReadArmyUnitNativeValidity(army), "source_Hex_Army_validity"))
            throw AutomaticMoraleDamageMissing("source_Base_origin");
        ushort raw = AutomaticMoraleDamageNeed(current.ReadArmyUnitRawHex2A(army), "source_Hex2A");
        var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0)
            ?? throw AutomaticMoraleDamageMissing("source_Hex_pointer");
        return new(casterId, army, hex, stageFactor, waiting);
    }

    /// <summary>177990's one valid Army receiver. Marker publication and the
    /// manager busy write remain with the ordered-vector caller. NativeReturn
    /// is2709A0's return, including successful queue admission, not an ACK.</summary>
    internal AutomaticArmyMoraleDamageApplication ApplyBorrowedAutomaticArmyMoraleDamage(
        GameState state, SourceWorldContext current, TacticRemakeCurrentContext context,
        int casterId, int targetId, TacticSourceRow source, S14TacticEffectInstruction instruction,
        AutomaticArmyMoraleDamagePrefix prefix, NativeArmyCombatCycleSampler sampler,
        NativeMapSeasonDate operationDate, List<CampaignEffectRequest> effects)
    {
        if (instruction.Opcode != 16 || instruction.Index is < 0 or > 1 || prefix.CasterId != casterId)
            throw AutomaticMoraleDamageMissing("instruction_prefix_owner");
        if (!state.Units.TryGetValue(targetId, out var image))
            throw AutomaticMoraleDamageMissing("target_Army_image");
        var target = current.ReadArmyUnitTableReference(AutomaticMoraleDamageNeed(
            image.NativeArmyOrigin?.NativeSlotIndex, "target_Army_physical"))
            ?? throw AutomaticMoraleDamageMissing("target_Army_pointer");
        if (!AutomaticMoraleDamageNeed(current.ReadArmyUnitNativeValidity(target), "target_Army_validity"))
            return new(state, false, false);
        int adjustment = context.ResolveBorrowedAutomaticArmyMoraleDamage(state, source,
            instruction, prefix, target);
        var applied = ArmyMoraleZeroRoutingCurrent.Apply(state, current, target, adjustment,
            TrapMobility(), Immediate, prefix.SourceHex);
        var working = applied.State ?? throw AutomaticMoraleDamageMissing(
            applied.UnresolvedOwner ?? "target2709A0");
        int nativeReturn = applied.NativeReturn ?? throw AutomaticMoraleDamageMissing("target2709A0_return");
        return new(working, true, nativeReturn != 0);

        GameState Immediate(GameState value, UnitDefeatRequest request)
        {
            //31F160 full falls through here. Keep its exact source-Hex winner,
            //the enclosing operation's pool, sampler and pre-writer date.
            var pool = current.ReadOwnedCurrentListPool() ?? throw AutomaticMoraleDamageMissing("immediate_pool");
            if (current.ReadOwnedPersonCommandManager() is null)
            {
                if (value.PersonnelCommands is null) throw AutomaticMoraleDamageMissing("immediate_Person_manager");
                PersonReturnCommands.BindCurrent(value, current);
            }
            value = value with { PersonnelCommands = current.ReadOwnedPersonCommandManager()! };
            BindActorlessDestructionInputs(value, current);
            var changed = new HashSet<int>();
            var result = new OwnedArmyDestructionStage(this).ApplyBattleDefeatCurrent(value,
                current, pool, request, sampler.Read(value) ?? throw AutomaticMoraleDamageMissing("immediate_sampler"),
                operationDate, effects, Refresh, person => changed.Add(person));
            if (changed.Count == 0) return result;
            var people = new Dictionary<int, OfficerState>(result.Officers);
            foreach (int physical in changed)
            {
                int id = AutomaticMoraleDamageNeed(current.ReadPersonIdentity(physical), "changed_Person_identity");
                var person = people.GetValueOrDefault(id) ?? throw AutomaticMoraleDamageMissing("changed_Person_image");
                var activation = OfficerTraitActivationSourceAdapter.Read(current, physical);
                if (activation is not { DurationSlotCount: 9, ActiveSlots: not null })
                    throw AutomaticMoraleDamageMissing("changed_Person_activation");
                people[id] = person with { TraitActivation = activation };
            }
            return result with { Officers = people };

            void Refresh(ArmyUnitReference receiver) => effects.Add(new(null, null)
            { ArmyTraitViewRefresh = new(ArmyDestructionQueue.ResolveUnitId(value, receiver.PhysicalSlot)
                ?? throw AutomaticMoraleDamageMissing("immediate_trait_Army")) });
        }
    }

    private static int AutomaticMoraleDamageParameter(GameState state, string key)
    {
        var parameters = state.NativeArmySimulation?.Parameters ?? throw AutomaticMoraleDamageMissing("Parameters");
        return !parameters.Float32Values.ContainsKey(key) && parameters.Int32Values.TryGetValue(key, out int value)
            ? value : throw AutomaticMoraleDamageMissing(key);
    }
    private static T AutomaticMoraleDamageNeed<T>(T? value, string field) where T : struct =>
        value ?? throw AutomaticMoraleDamageMissing(field);
    private static InvalidDataException AutomaticMoraleDamageMissing(string field) =>
        new("automatic_Army_morale_damage." + field);
}

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>16B820's independent source/target selected-ability queries,
    /// then16D6C0 and float32 scaling. Truncate before signed negation.</summary>
    internal int ResolveBorrowedAutomaticArmyMoraleDamage(GameState state, TacticSourceRow source,
        S14TacticEffectInstruction instruction, AutomaticArmyMoraleDamagePrefix prefix, ArmyUnitReference target)
    {
        if (!Bound(state) || state.NativeArmySimulation?.Parameters.Language != "TC")
            throw MoraleAmountMissing("current_TC_context");
        int acting = SourceAbility();
        //16E820 decodes its selector before testing its target Army pointer.
        byte targetSelector = FactorSelector(source) ?? throw MoraleAmountMissing("target_selector6A");
        int receiving = Need(_definitions.ReadArmyUnitNativeValidity(target), "ability_target_Army_validity")
            ? Ability(target, targetSelector == 1 ? 1 : 2) : 0;
        byte selector = FactorSelector(source) ?? throw MoraleAmountMissing("factor_selector6A");
        int divisor = P(selector == 1 ? SimulationParameterKeys.Tactics.StrengthDifferenceDivisor
            : SimulationParameterKeys.Tactics.IntelligenceDifferenceDivisor);
        if (divisor == 0) throw MoraleAmountMissing("factor_divisor_zero");
        float factor = (float)unchecked(acting - receiving) / (float)divisor;
        factor += 1f;
        float floor = (float)P(SimulationParameterKeys.Tactics.EffectFactorFloorPercent) / 100f;
        factor = factor > floor ? factor : floor;
        float scaled = instruction.EffectValue;
        scaled *= factor;
        scaled *= prefix.StageFactor;
        scaled *= prefix.WaitingFactor;
        int amount = !float.IsFinite(scaled) || scaled < int.MinValue || scaled >= 2147483648f
            ? int.MinValue : (int)scaled;
        return unchecked(-amount);

        int SourceAbility()
        {
            if (!Need(_definitions.ReadArmyUnitNativeValidity(prefix.SourceArmy), "ability_source_Army_validity")) return 0;
            byte selected = FactorSelector(source) ?? throw MoraleAmountMissing("ability_selector6A");
            return Ability(prefix.SourceArmy, selected == 1 ? 1 : 2);
        }
        int Ability(ArmyUnitReference army, int dimension)
        {
            ushort raw = Need(_definitions.ReadArmyUnitCommanderId(army), "ability_Commander12");
            var person = raw <= 6000 ? _definitions.ReadPersonLookupReference(raw)
                : _definitions.ReadTransferPersonTableReference(0);
            int physical = Need(person?.PhysicalSlot, "ability_Person_pointer");
            if (!Need(_definitions.ReadPersonNativeValidity(physical), "ability_Person_validity")) return 0;
            int id = Need(_definitions.ReadPersonIdentity(physical), "ability_Person_identity");
            int? value = _abilities is TacticRemakeCurrentAbilities owned
                ? owned.ResolveDimension(state, id, dimension)
                : dimension == 1 ? _abilities?.Resolve(state, id)?.Strength
                    : _abilities?.Resolve(state, id)?.Intelligence;
            return value ?? throw MoraleAmountMissing("ability_dimension" + dimension);
        }
        int P(string key)
        {
            var parameters = state.NativeArmySimulation!.Parameters;
            return !parameters.Float32Values.ContainsKey(key) && parameters.Int32Values.TryGetValue(key, out int value)
                ? value : throw MoraleAmountMissing(key);
        }
        static T Need<T>(T? value, string field) where T : struct => value ?? throw MoraleAmountMissing(field);
        static InvalidDataException MoraleAmountMissing(string field) => new("automatic_Army_morale_damage.amount." + field);
    }
}
