using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record AutomaticArmyAllModifierPrefix(int CasterId, int StageProduct);
internal sealed record AutomaticArmyAllModifierApplication(GameState State, bool TargetResultMarker);

public sealed partial class TacticRemakeCurrentContext
{
    internal int ReadAutomaticAllModifierParameter(GameState state, string key)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw AllModifierMissing("current_parameters");
        //Current CE SC1766E0/2ECAA0 and TC176980/2ECB80 share this
        //wrapper and writer contract. Read the bound world's saved/current
        //integer parameter; locale does not change this mechanism.
        return !bank.Float32Values.ContainsKey(key) && bank.Int32Values.TryGetValue(key, out int value)
            ? value : throw AllModifierMissing(key);
    }

    /// <summary>176980's negative/all-five branch: per-recipient16B820,
    /// waiting multiplier,16D6C0 and negated zero-extended16D750. There is no
    /// success trial, trait gate, special-Force multiplier or chain query.</summary>
    internal (int Modifier, int Duration) ResolveAutomaticArmyAllModifierInput(GameState state,
        UnitState caster, UnitState target, TacticSourceRow source, S14TacticEffectInstruction instruction,
        AutomaticArmyAllModifierPrefix prefix)
    {
        if (instruction.Opcode != 22 || instruction.Index is < 0 or > 1 || prefix.CasterId != caster.Id)
            throw AllModifierMissing("instruction_or_prefix");
        return ResolveAutomaticArmyModifierInput(state, caster, target, source, instruction,
            prefix.StageProduct, sourceForce: null);
    }

    //176610 and176980 share this arithmetic. Only the single-dimension
    //wrapper consumes Force22/23; all-five has no such parameter read.
    private (int Modifier, int Duration) ResolveAutomaticArmyModifierInput(GameState state,
        UnitState caster, UnitState target, TacticSourceRow source, S14TacticEffectInstruction instruction,
        int stageProduct, int? sourceForce)
    {
        bool all = instruction.Opcode is 21 or 22;
        bool positive = instruction.Opcode is >= 5 and <= 9 or 21;
        var sourceArmy = Pointer(caster);
        float factor;
        if (positive)
        {
            //16B6D0 reads both dimensions before its selector, unlike the
            //negative16B820 source/recipient difference wrapper.
            int strength = 0, intelligence = 0;
            if (K(_definitions.ReadArmyUnitNativeValidity(sourceArmy), "source_ability_Army_validity"))
            { strength = Ability(sourceArmy, 1); intelligence = Ability(sourceArmy, 2); }
            byte selector = Selector();
            int divisor = P(selector == 1 ? SimulationParameterKeys.Tactics.StrengthEffectDivisor
                : SimulationParameterKeys.Tactics.IntelligenceEffectDivisor);
            if (divisor == 0) throw AllModifierMissing("zero_source_divisor");
            factor = (float)(selector == 1 ? strength : intelligence) / (float)divisor;
            factor += 1f;
        }
        else
        {
            var targetArmy = Pointer(target);
            int acting = K(_definitions.ReadArmyUnitNativeValidity(sourceArmy), "source_ability_Army_validity")
                ? Ability(sourceArmy, Selector() == 1 ? 1 : 2) : 0;
            //16E820 consumes its own selector before checking the recipient.
            int receiving = Ability(targetArmy, Selector() == 1 ? 1 : 2);
            byte selector = Selector();
            int divisor = P(selector == 1 ? SimulationParameterKeys.Tactics.StrengthDifferenceDivisor
                : SimulationParameterKeys.Tactics.IntelligenceDifferenceDivisor);
            if (divisor == 0) throw AllModifierMissing("zero_difference_divisor");
            factor = (float)unchecked(acting - receiving) / (float)divisor;
            factor += 1f;
            float floor = (float)P(SimulationParameterKeys.Tactics.EffectFactorFloorPercent) / 100f;
            factor = factor > floor ? factor : floor;
        }
        float waiting = 1f;
        if (K(_definitions.ReadArmyUnitNativeValidity(sourceArmy), "waiting_source_Army_validity"))
        {
            var native = caster.NativeTacticRuntime ?? throw AllModifierMissing("source_waiting_runtime");
            if (native.Waiting21 == 0)
                waiting = (float)P(native.Waiting22 == 0 ? SimulationParameterKeys.Tactics.Waiting0SharedFactorPercent
                    : SimulationParameterKeys.Tactics.Waiting1SharedFactorPercent) / 100f;
        }
        float panel = 1f;
        if (!all)
        {
            int slot = sourceForce ?? throw AllModifierMissing("source_Force_prefix");
            var force = _definitions.ReadForceTableReference(slot < 52 ? slot : 0)
                ?? throw AllModifierMissing("source_Force_pointer");
            bool firstValid = K(_definitions.ReadForceNativeValidity(force), "source_Force_first_validity");
            if (firstValid && positive && Category() == 3)
                panel = (float)K(_definitions.ReadBaseForceModifier(force, 0x22), "positive_Force22") / 100f + 1f;
            else if (K(_definitions.ReadForceNativeValidity(force), "source_Force_second_validity") && !positive && Category() == 4)
                panel = (float)K(_definitions.ReadBaseForceModifier(force, 0x23), "negative_Force23") / 100f + 1f;
        }
        float duration = instruction.EffectValue;
        duration *= factor;
        duration *= (float)stageProduct / 100f + 1f;
        duration *= waiting;
        if (!all) duration *= panel;
        int requestedDuration = duration is >= int.MinValue and < 2147483648f ? (int)duration : int.MinValue;
        int modifier = instruction.Raw68;
        return (positive ? modifier : unchecked(-modifier), requestedDuration);

        byte Category()
        {
            var tactic = _definitions.ReadSkillTacticReference(source.Slot)
                ?? throw AllModifierMissing("category_Tactic_pointer");
            return K(_definitions.ReadSkillTacticRawByte(tactic, 0x40), "category40");
        }

        byte Selector() => FactorSelector(source) ?? throw AllModifierMissing("current_selector6A");
        int P(string key) => ReadAutomaticAllModifierParameter(state, key);
        ArmyUnitReference Pointer(UnitState army) => _definitions.ReadArmyUnitTableReference(
            K(army.NativeArmyOrigin?.NativeSlotIndex, "Army_physical")) ?? throw AllModifierMissing("Army_pointer");
        int Ability(ArmyUnitReference army, int dimension)
        {
            if (!K(_definitions.ReadArmyUnitNativeValidity(army), "ability_Army_validity")) return 0;
            ushort raw = K(_definitions.ReadArmyUnitCommanderId(army), "ability_Commander12");
            var person = raw <= 6000 ? _definitions.ReadPersonLookupReference(raw) : _definitions.ReadTransferPersonTableReference(0);
            int physical = K(person?.PhysicalSlot, "ability_Commander_pointer");
            if (!K(_definitions.ReadPersonNativeValidity(physical), "ability_Person_validity")) return 0;
            int id = K(_definitions.ReadPersonIdentity(physical), "ability_Person10");
            int? value = _abilities is TacticRemakeCurrentAbilities owned ? owned.ResolveDimension(state, id, dimension)
                : dimension == 1 ? _abilities?.Resolve(state, id)?.Strength : _abilities?.Resolve(state, id)?.Intelligence;
            return value ?? throw AllModifierMissing("ability_dimension" + dimension);
        }
        static T K<T>(T? value, string owner) where T : struct => value ?? throw AllModifierMissing(owner);
    }
    private static InvalidDataException AllModifierMissing(string owner) => new("automatic_Army_all_modifier." + owner);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>176980 prefix before even an empty vector. The actual Army
    /// source Force getter precedes the once-per-instruction stage product.</summary>
    internal AutomaticArmyAllModifierPrefix BeginBorrowedAutomaticArmyAllModifiers(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, int casterId, int stageOrdinal)
    {
        var caster = RequireLatestAutomaticActorImageCurrent(state, current, casterId);
        var army = current.ReadArmyUnitTableReference(AutomaticAllModifierKnown(caster.NativeArmyOrigin?.NativeSlotIndex, "source_physical"))
            ?? throw AutomaticAllModifierMissing("source_Army_pointer");
        if (!AutomaticAllModifierKnown(current.ReadArmyUnitNativeValidity(army), "source_prefix_Army_validity"))
            throw AutomaticAllModifierMissing("invalid_source_Army_fallback_Base");
        _ = current.ReadArmyUnitRawForceSlot(army); //22CD80's actual derived identity, not Force193 or a fabricated default.
        int scale = context.ReadAutomaticAllModifierParameter(state, SimulationParameterKeys.Tactics.EffectStageScale);
        return new(casterId, unchecked(stageOrdinal * scale));
    }

    /// <summary>Consume one actual Army from the caller's ordered Hex walk.
    /// The caller owns capture identity, result17A1C0, current/pool/sampler,
    /// activation tail and final publication. This operation never reselects.</summary>
    internal AutomaticArmyAllModifierApplication ApplyBorrowedAutomaticArmyAllModifiers(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, int casterId, int targetId,
        TacticSourceRow source, S14TacticEffectInstruction instruction, AutomaticArmyAllModifierPrefix prefix,
        NativeArmyCombatCycleSampler sampler, NativeMapSeasonDate operationDate, List<CampaignEffectRequest> effects)
    {
        if (instruction.Opcode != 22 || instruction.Index is < 0 or > 1 || prefix.CasterId != casterId)
            throw AutomaticAllModifierMissing("instruction_or_prefix");
        var target = state.Units.GetValueOrDefault(targetId) ?? throw AutomaticAllModifierMissing("target_Army_image");
        var army = current.ReadArmyUnitTableReference(AutomaticAllModifierKnown(target.NativeArmyOrigin?.NativeSlotIndex, "target_physical"))
            ?? throw AutomaticAllModifierMissing("target_Army_pointer");
        if (!AutomaticAllModifierKnown(current.ReadArmyUnitNativeValidity(army), "target_Army_validity")) return new(state, false);
        var caster = RequireLatestAutomaticActorImageCurrent(state, current, casterId);
        var amount = context.ResolveAutomaticArmyAllModifierInput(state, caster, target, source, instruction, prefix);
        var native = target.NativeTacticRuntime ?? throw AutomaticAllModifierMissing("target_runtime");
        if (native.StatModifiers4A is not { Length: 6 } || native.StatDurations50 is not { Length: 6 })
            throw AutomaticAllModifierMissing("target_six_modifier_duration_pairs");
        var modifiers = native.StatModifiers4A.ToArray();
        var durations = native.StatDurations50.ToArray();
        //2ECB80 passes param5=0,param6=255,param7=0 to all five2EC6A0
        //calls. Their individual history branches are not reached.
        for (int dimension = 0; dimension < 5; dimension++)
        {
            if (!AutomaticAllModifierKnown(current.ReadArmyUnitNativeValidity(army), "modifier_Army_validity")) continue;
            (modifiers[dimension], durations[dimension]) = MergeAutomaticArmyModifier(
                modifiers[dimension], durations[dimension], amount.Modifier, amount.Duration);
        }
        var changed = target with { NativeTacticRuntime = native with { StatModifiers4A = modifiers, StatDurations50 = durations } };
        var working = state with { Units = new(state.Units) { [targetId] = changed } };
        //The outer combined report gate is reached even if all five stores
        //leave their bytes unchanged. No amount/delta shortcut replaces it.
        byte rawDistrict = ReadFacilityHistoryDistrict(current, army);
        var district = current.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0)
            ?? throw AutomaticAllModifierMissing("report_District_pointer");
        if (BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district, AutomaticAllModifierMissing))
        {
            var formatted = FormatHistoryReport(working, current, 0x322E,
                new(Army0: army, String0: source.Name, String1: ReadHistoryDataText(1, 0x24F)));
            working = formatted.State;
            int hex = AutomaticAllModifierKnown(current.ReadArmyUnitRawHex2A(army), "report_Army2A");
            int day = sampler.Read(working)?.Day450 ?? throw AutomaticAllModifierMissing("report_World450");
            byte finalDistrict = ReadFacilityHistoryDistrict(current, army);
            working = WriteHistory(working, current, new ReportCommand(5, finalDistrict, hex, day,
                formatted.Text, CampaignHistoryWriter.Supplemental(working), 5012, MarkBit4: 1), effects);
        }
        return new(working, true);
    }

    /// <summary>2EC6A0's signed32 merge, before its separate byte caps.
    /// Incoming strength is not narrowed or clamped before its energy term.</summary>
    internal static (sbyte Modifier, byte Duration) MergeAutomaticArmyModifier(sbyte beforeModifier,
        byte beforeDuration, int requestedModifier, int requestedDuration)
    {
        unchecked
        {
            int selected = beforeModifier, duration = beforeDuration;
            if (beforeModifier == 0)
            {
                if (beforeDuration == 0) { selected = requestedModifier; duration = requestedDuration; }
            }
            else if (beforeDuration != 0 && requestedDuration != 0)
            {
                int numerator;
                bool sameSign = beforeModifier > 0 && requestedModifier > 0 || beforeModifier < 0 && requestedModifier < 0;
                if (sameSign)
                {
                    int a = Sign(beforeModifier) * beforeModifier, b = Sign(requestedModifier) * requestedModifier;
                    if (Abs(a) < Abs(b)) selected = requestedModifier;
                    numerator = a * a * beforeDuration + requestedDuration * b * b;
                }
                else
                {
                    int a = Sign(beforeModifier) * beforeDuration * beforeModifier * beforeModifier;
                    int b = Sign(requestedModifier) * requestedDuration * requestedModifier * requestedModifier;
                    if (Abs(a) < Abs(b)) selected = requestedModifier;
                    numerator = Abs(a + b);
                }
                //Native uses CDQ/IDIV, not unsigned division or floating point.
                duration = numerator / (selected * selected);
            }
            return ((sbyte)Math.Clamp(selected, -10, 10), (byte)Math.Clamp(duration, 0, 240));
        }
        static int Sign(int value) => value > 0 ? 1 : -1;
        static int Abs(int value) => unchecked((value ^ (value >> 31)) - (value >> 31));
    }
    private static T AutomaticAllModifierKnown<T>(T? value, string owner) where T : struct => value ?? throw AutomaticAllModifierMissing(owner);
    private static InvalidDataException AutomaticAllModifierMissing(string owner) => new("automatic_Army_all_modifier." + owner);
}
