using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record AutomaticArmySingleAbnormalPrefix(int CasterId, ushort SourceHex,
    byte SourceForceSlot, int StageProduct);
internal sealed record AutomaticArmySingleAbnormalReceipt(int TargetArmyId, int NativeIndex,
    bool TargetValid, bool GateAllowed, int? RequestedStrength, int? FailureFeedbackKind,
    TacticAbnormalSourceResult? Status, int? NotificationDistrict,
    bool TraitAdjustmentAttributed, bool StartsStatus, int? IncreasingFeedbackKind);
internal sealed record AutomaticArmySingleAbnormalResolution(TacticTargetResolution Resolution,
    bool TargetResultMarker, AutomaticArmySingleAbnormalReceipt Receipt);

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>178500's Army-origin prefix, before the ordered vector:
    /// raw2A,22CD80 and the stage product. No abilities or gate operands are
    /// read for an empty vector. This is independent of175FF0's Force pointer.</summary>
    internal AutomaticArmySingleAbnormalPrefix BeginAutomaticArmySingleAbnormal(GameState state,
        UnitState caster, int stageOrdinal)
    {
        if (!Bound(state) || ArmyValid(state, caster) != true ||
            caster.NativeArmyOrigin?.NativeSlotIndex is not int physical)
            throw SingleAbnormalMissing("current_caster");
        if (state.NativeArmySimulation!.Parameters.Language is not ("TC" or "SC"))
            throw SingleAbnormalMissing("wrapper_not_qualified");
        var army = _definitions.ReadArmyUnitTableReference(physical) ?? throw SingleAbnormalMissing("caster_pointer");
        ushort hex = SingleNeed(_definitions.ReadArmyUnitRawHex2A(army), "source_Hex2A");
        byte force = _definitions.ReadArmyUnitRawForceSlot(army);
        int stage = SingleParameter(state, SimulationParameterKeys.Tactics.EffectStageScale);
        return new(caster.Id, hex, force, unchecked(stageOrdinal * stage));
    }

    /// <summary>TC178500 ->2EED60 for one actual Army receiver and opcode2,
    ///3 or4. Returns the real numerical effect and semantic notifications.
    /// The caller applies its receipt once, publishes ResourceStateAfter and
    /// synchronizes the same current23..25/3C3E before visiting another node.</summary>
    internal AutomaticArmySingleAbnormalResolution ResolveAutomaticArmySingleAbnormal(GameState state,
        UnitState caster, UnitState target, TacticSourceRow source,
        S14TacticEffectInstruction instruction, AutomaticArmySingleAbnormalPrefix prefix)
    {
        if (instruction.Opcode is not (2 or 3 or 4) || instruction.Index is < 0 or > 1 || prefix.CasterId != caster.Id)
            throw SingleAbnormalMissing("instruction_prefix_owner");
        int index = instruction.Opcode - 2;
        bool valid = SingleNeed(ArmyValid(state, target), "target_validity");
        if (!valid) return NoEffect(false, false, null);
        if (!Bound(state) || ArmyValid(state, caster) != true ||
            state.NativeArmySimulation!.Parameters.Language is not ("TC" or "SC"))
            throw SingleAbnormalMissing("current_caster");
        var native = target.NativeTacticRuntime ?? throw SingleAbnormalMissing("target_counters");
        int commander = NativeCommander(caster);
        bool commanderValid = SingleNeed(_definitions.ReadPersonNativeValidity(commander), "caster_Commander_validity");
        bool early = commanderValid && SingleNeed(TacticRemakeCurrentTraits.CommanderHasEffect(_definitions,
            commander, 98), "caster_effect98");
        if (!early)
        {
            //16F9A0 selects and reads only the reached coefficient branch.
            int acting = SelectedAbility(caster), receiving = SelectedAbility(target);
            int baseline = P(SimulationParameterKeys.Tactics.AbnormalGateBaseline);
            int higher = acting > receiving ? P(SimulationParameterKeys.Tactics.AbnormalGateHigherAbilityScale) : 0;
            int lower = acting <= receiving ? P(SimulationParameterKeys.Tactics.AbnormalGateLowerAbilityScale) : 1;
            int[] traits = TacticRemakeCurrentTraits.ArmyContributions(_definitions, commander,
                ArmyTraitSourceAdapter.Encode(state, caster)) ?? throw SingleAbnormalMissing("caster_traits99");
            var gate = new TacticEffectGateSourceInput(acting, receiving, baseline, higher, lower,
                traits[99], P(SimulationParameterKeys.Tactics.AbnormalGateTraitRate));
            var tactic = _definitions.ReadSkillTacticReference(source.Slot) ?? throw SingleAbnormalMissing("Tactic_pointer");
            int tacticPhysical = SingleNeed(_definitions.ReadSkillTacticPhysicalSlot(tactic), "Tactic_physical");
            int from = LiveArmyHex(caster), to = LiveArmyHex(target);
            float failure = TacticEffectSourceGateMath.FailurePercentTc(gate);
            var world = failure <= 0f ? null : ReadCurrentSamplerWorld(state);
            bool allowed = TacticEffectSourceGateMath.AllowsTc(gate, tacticPhysical, from, to, world)
                ?? throw SingleAbnormalMissing("gate_World450_45C");
            if (!allowed)
            {
                //178500 emits20/21/22 only when all three existing counters
                //are zero. This is a real1B4AB0 request, not an applied marker.
                int? feedback = native.Abnormal23 == 0 && native.Abnormal24 == 0 && native.Abnormal25 == 0
                    ? 20 + index : null;
                return NoEffect(true, false, feedback);
            }
        }
        //16B820 re-reads both selected abilities and then selector6A after
        //the allowed gate. No both-dimension16B6D0 or neighbor-chain query.
        int sourceAbility = SelectedAbility(caster), targetAbility = SelectedAbility(target);
        byte selector = FactorSelector(source) ?? throw SingleAbnormalMissing("factor_selector");
        int divisor = P(selector == 1 ? SimulationParameterKeys.Tactics.StrengthDifferenceDivisor
            : SimulationParameterKeys.Tactics.IntelligenceDifferenceDivisor);
        if (divisor == 0) throw SingleAbnormalMissing("factor_divisor_zero");
        float factor = (float)unchecked(sourceAbility - targetAbility) / (float)divisor;
        factor += 1f;
        float floor = (float)P(SimulationParameterKeys.Tactics.EffectFactorFloorPercent) / 100f;
        factor = factor > floor ? factor : floor;
        var sourceRuntime = caster.NativeTacticRuntime ?? throw SingleAbnormalMissing("caster_waiting");
        float waiting = sourceRuntime.Waiting21 != 0 ? 1f : (float)P(sourceRuntime.Waiting22 == 0
            ? SimulationParameterKeys.Tactics.Waiting0SharedFactorPercent
            : SimulationParameterKeys.Tactics.Waiting1SharedFactorPercent) / 100f;
        int force = prefix.SourceForceSlot <= 51 ? prefix.SourceForceSlot : 0;
        byte flags = state.NativeArmySimulation!.Forces.GetValueOrDefault(force)?.Flags193
            ?? throw SingleAbnormalMissing("captured_source_Force193");
        int special = (flags & 4) != 0 ? P(SimulationParameterKeys.Tactics.SpecialForceEffectPercent) : 100;
        float amount = instruction.EffectValue;
        amount *= factor;
        amount *= (float)prefix.StageProduct / 100f + 1f;
        amount *= waiting;
        amount *= (float)special;
        amount /= 100f;
        int requested = SingleTruncate(amount);
        var query = StatusInput(requested);
        var input = query.Value;
        var adjustment = TacticEffectSourceAbnormal.ResolveAdjustment(input)
            ?? throw SingleAbnormalMissing("status_adjustment");
        int? district = null;
        if (!adjustment.CounterWritten || adjustment.HasTraitAdjustment) district = NotificationDistrict();
        if (adjustment.CounterWritten)
            input = input with { CurrentUpperBound = P(SimulationParameterKeys.Tactics.AbnormalCounterUpperBound),
                CapturedOwner = new(false, null) };
        var result = TacticEffectSourceAbnormal.Resolve(input, native.ProvocationOwner)
            ?? throw SingleAbnormalMissing("status_counter_result");
        if (adjustment.HasTraitAdjustment && !district.HasValue) result = result with { CauseCode = 0 };
        NativeTacticAbnormalAssignment assigned = new(index == 0 ? result.CounterAfter : native.Abnormal23,
            index == 1 ? result.CounterAfter : native.Abnormal24,
            index == 2 ? result.CounterAfter : native.Abnormal25, result.OwnerAfter);
        var effect = new PlayableTacticEffect(PlayableTacticEffectKind.NativeAbnormalChange,
            result.CounterWritten ? result.CounterAfter - input.CurrentCounter : 0) { NativeAbnormalAfter = assigned };
        var stagedTarget = TacticEffectSourceRules.ApplyReceipt(target, effect);
        var staged = state with { RandomState = query.RandomState };
        if (index == 2 && result.CounterWritten && result.AdjustedStrength > 0)
        {
            //The byte is staged before16EB20 at the wrapper's captured Hex.
            //None preserves3C/3E and the former typed owner; it is not casterId.
            staged = staged with { Units = new(staged.Units) { [target.Id] = stagedTarget } };
            var capture = CaptureOwner(staged, stagedTarget);
            result = TacticEffectSourceAbnormal.Resolve(input with { CapturedOwner = capture }, native.ProvocationOwner)
                ?? throw SingleAbnormalMissing("captured_owner_result");
            if (adjustment.HasTraitAdjustment && !district.HasValue) result = result with { CauseCode = 0 };
            assigned = assigned with { ProvocationOwner = result.OwnerAfter,
                OverrideTarget = capture.HasEntity ? capture.OverrideTarget : null };
            effect = effect with { NativeAbnormalAfter = assigned };
            stagedTarget = TacticEffectSourceRules.ApplyReceipt(target, effect);
            if (capture.SourceResidue is { } residue)
                staged = staged with { ResourceState = CampaignSourceKnowledge.From(staged.ResourceState).WithArmyOverride(residue) };
        }
        bool starts = input.CurrentCounter == 0 && result.CounterWritten && result.UnclampedSum > 0;
        if (starts && !district.HasValue) district = NotificationDistrict();
        var resolution = new TacticTargetResolution([effect], staged.RandomState)
            { ResourceStateAfter = ArmyOverrideTargetSourceAdapter.ReconcileOwner(staged, stagedTarget).ResourceState };
        return new(resolution, true, new(target.Id, index, true, true, requested, null, result, district,
            adjustment.HasTraitAdjustment && district.HasValue, starts, result.IncreasingNotification ? 23 + index : null));

        AutomaticArmySingleAbnormalResolution NoEffect(bool targetValid, bool gateAllowed, int? feedback) =>
            new(new([], state.RandomState), false, new(target.Id, index, targetValid, gateAllowed,
                null, feedback, null, null, false, false, null));
        int SelectedAbility(UnitState army)
        {
            byte selected = FactorSelector(source) ?? throw SingleAbnormalMissing("current_selector6A");
            int dimension = selected == 1 ? 1 : 2;
            //22E390 resolves the actual current Army12. Its invalid Person
            //returns literal0 before health, merit, caps or any provider read.
            int person = NativeCommander(army);
            if (!SingleNeed(_definitions.ReadPersonNativeValidity(person), "ability_Person_validity")) return 0;
            int identity = SingleNeed(_definitions.ReadPersonIdentity(person), "ability_Person_identity");
            int? value = _abilities is TacticRemakeCurrentAbilities owned
                ? owned.ResolveDimension(state, identity, dimension)
                : dimension == 1 ? _abilities?.Resolve(state, identity)?.Strength
                    : _abilities?.Resolve(state, identity)?.Intelligence;
            return value ?? throw SingleAbnormalMissing("current_ability_dimension" + dimension);
        }
        int NativeCommander(UnitState army)
        {
            int physical = SingleNeed(army.NativeArmyOrigin?.NativeSlotIndex, "Commander_Army_physical");
            var reference = _definitions.ReadArmyUnitTableReference(physical) ?? throw SingleAbnormalMissing("Commander_Army_pointer");
            ushort raw = SingleNeed(_definitions.ReadArmyUnitCommanderId(reference), "Commander_Army12");
            var person = raw <= 6000 ? _definitions.ReadPersonLookupReference(raw) : _definitions.ReadTransferPersonTableReference(0);
            return SingleNeed(person?.PhysicalSlot, "Commander_Person_pointer");
        }
        TacticEffectQueryResult<TacticAbnormalSourceInput> StatusInput(int strength)
        {
            //2C3B50 resolves Army12 before2C3BC0's native Person gate.
            //Do not route this through a domain OfficerId surrogate.
            int receiving = NativeCommander(target);
            bool general = SingleNeed(TacticRemakeCurrentTraits.CommanderImmune(_definitions, receiving, 21), "target_immunity21");
            bool? specific = general ? null : SingleNeed(TacticRemakeCurrentTraits.CommanderImmune(_definitions,
                receiving, 22 + index), "target_specific_immunity");
            byte counter = index == 0 ? native.Abnormal23 : index == 1 ? native.Abnormal24 : native.Abnormal25;
            if (general || specific == true)
                return new(new(index, strength, counter, true, general, specific, null, null, null,
                    null, null, null, null, null), state.RandomState);
            //26A740 itself has no Person-validity short circuit in this
            //mode: its Army36 then selected-Person entries retain that source
            //behavior, independently of2C3BC0's immunity gate.
            int[] receivingTraits = TacticRemakeCurrentTraits.ArmyContributions(_definitions, receiving,
                ArmyTraitSourceAdapter.Encode(state, target)) ?? throw SingleAbnormalMissing("target_traits127");
            int sourcePerson = NativeCommander(caster);
            int[] sourceTraits = TacticRemakeCurrentTraits.ArmyContributions(_definitions, sourcePerson,
                ArmyTraitSourceAdapter.Encode(state, caster)) ?? throw SingleAbnormalMissing("source_traits127");
            return new(new(index, strength, counter, true, false, false,
                receivingTraits[9], receivingTraits[10 + index], sourceTraits[98],
                P(SimulationParameterKeys.Tactics.AbnormalGeneralResistanceRate),
                P(index == 0 ? SimulationParameterKeys.Tactics.AbnormalType0ResistanceRate
                    : index == 1 ? SimulationParameterKeys.Tactics.AbnormalType1ResistanceRate
                    : SimulationParameterKeys.Tactics.AbnormalType2ResistanceRate),
                P(SimulationParameterKeys.Tactics.AbnormalSourceTraitRate), null, null), state.RandomState);
        }
        int Hex(int raw)
        {
            var hex = _definitions.ReadHexTableReference(raw is >= 0 and < 48400 ? raw : 0)
                ?? throw SingleAbnormalMissing("Hex_pointer");
            return SingleNeed(_definitions.ReadHexPhysicalSlot(hex), "Hex_physical");
        }
        int LiveArmyHex(UnitState army)
        {
            var reference = _definitions.ReadArmyUnitTableReference(SingleNeed(army.NativeArmyOrigin?.NativeSlotIndex,
                "Army_physical")) ?? throw SingleAbnormalMissing("Army_pointer");
            return Hex(SingleNeed(_definitions.ReadArmyUnitRawHex2A(reference), "Army_live_Hex2A"));
        }
        int? NotificationDistrict()
        {
            foreach (var army in new[] { target, caster })
            {
                int person = NativeCommander(army);
                //22BB10 short-circuits an invalid Person to District0 and
                //does not consume the invalid record's residual E0.
                byte districtRaw = SingleNeed(_definitions.ReadPersonNativeValidity(person), "notice_Person_validity")
                    ? SingleNeed(_definitions.ReadPersonDistrictSlot(person), "notice_PersonE0") : (byte)0;
                var district = _definitions.ReadDistrictTableReference(districtRaw <= 51 ? districtRaw : 0)
                    ?? throw SingleAbnormalMissing("notice_District_pointer");
                if (BaseCaptureRewardSource.ReadPlayerRulerDistrict(_definitions, district, SingleAbnormalMissing))
                    return SingleNeed(district.PhysicalSlot, "notice_District_physical");
            }
            return null;
        }
        TacticCapturedOwnerSource CaptureOwner(GameState working, UnitState receiving)
        {
            var selected = FieldHexOccupantSource.ReadCurrent(working, _definitions, Hex(prefix.SourceHex));
            var entity = selected.Receipt ?? throw SingleAbnormalMissing(selected.UnresolvedOwner ?? "16EB20_owner");
            if (entity.NativeKind == 255) return new(false, null);
            NativeTacticOwnerReference owner;
            if (entity.NativeKind == 27)
            {
                int id = entity.GameplayArmyId ?? throw SingleAbnormalMissing("owner_Army_identity");
                var actual = working.Units.GetValueOrDefault(id) ?? throw SingleAbnormalMissing("owner_Army_image");
                owner = new(actual.ForceId, DeploymentTargetKind.Unit, id);
            }
            else if (entity.NativeKind is 5 or 6)
            {
                int location = entity.BaseLocationId ?? throw SingleAbnormalMissing("owner_Base_identity");
                byte ownerForce = SingleNeed(_definitions.ReadObjectForceSlot13(entity.CapturedObject!), "owner_Base_Force13");
                owner = new(ownerForce, DeploymentTargetKind.Location, location);
            }
            else throw SingleAbnormalMissing("owner_kind" + entity.NativeKind + "_typed_ProvocationOwner");
            var fields = new ArmyOverrideTargetSourceFields(checked((byte)entity.NativeKind), checked((ushort)entity.NativePhysicalSlot));
            var decoded = ArmyOverrideTargetSourceAdapter.Decode(fields,
                working.ReadArmyLifecycle() is { } arena ? new(arena) : null);
            return new(true, owner) { OverrideTarget = decoded, SourceResidue = ArmyOverrideTargetSourceAdapter.Residue(
                ArmyOverrideTargetSourceAdapter.Owner(working, receiving), fields, decoded) };
        }
        int P(string key) => SingleParameter(state, key);
    }

    private static int SingleParameter(GameState state, string key)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw SingleAbnormalMissing("parameter_owner");
        return !bank.Float32Values.ContainsKey(key) && bank.Int32Values.TryGetValue(key, out int value)
            ? value : throw SingleAbnormalMissing(key);
    }
    private static T SingleNeed<T>(T? value, string owner) where T : struct => value ?? throw SingleAbnormalMissing(owner);
    private static int SingleTruncate(float value) => !float.IsFinite(value) || value >= 2147483648f || value < -2147483648f
        ? int.MinValue : (int)value;
    private static InvalidDataException SingleAbnormalMissing(string owner) => new("automatic_Army_single_abnormal." + owner);
}
