using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record BaseOriginAutomaticAbnormalApplication(GameState State, bool TargetResultMarker,
    AutomaticArmySingleAbnormalReceipt[] ArmyCounters, BaseTacticAbnormalReceipt? BaseReceipt,
    int? FailureFeedbackKind, int? NotificationDistrict, bool GeneralImmunityNotice, bool CombinedApplicationNotice)
{
    internal PlayableTacticEffect[] ArmyEffects { get; init; } = [];
}

public sealed partial class TacticRemakeCurrentContext
{
    //175FF0/178500 retain this Hex before walking the ordered instruction
    //vector. Read the current Base receiver, not an earlier pair descriptor.
    internal int ReadBaseOriginAutomaticAbnormalPrefix(GameState state, BaseReference origin,
        S14TacticEffectInstruction instruction)
    {
        if (!Bound(state) || instruction.Opcode is not (2 or 3 or 4 or 23))
            throw OriginAutomaticMissing("abnormal_instruction_context");
        _ = OriginNeed(_definitions.ReadBaseNativeValidity(origin), "abnormal_source_validity");
        int raw = _definitions.ReadBaseCurrentHexIndex(origin);
        //175FF0 reads22CAD0,178500 reads virtualB0. The Base path has no
        //Army36 cache, linked Army or Army waiting operands.
        int force = OriginNeed(_definitions.ReadBaseForceSlot(origin), "abnormal_prefix_ForceB0");
        if (instruction.Opcode == 23)
            _ = _definitions.ReadForceTableReference(force < 52 ? force : 0)
                ?? throw OriginAutomaticMissing("abnormal_prefix_Force_pointer");
        _ = OriginParameter(state, SimulationParameterKeys.Tactics.EffectStageScale);
        return raw;
    }

    /// <summary>Actual Base-caster175FF0/178500. The governor supplies the
    /// source ability and early effect98 gate; the status writer receives a
    /// genuinely null source Army. No Army identity or cached traits are made.
    /// This writes the borrowed current and returns the ordered notification
    /// requirements. The caller owns feedback/report publication and markers.</summary>
    internal BaseOriginAutomaticAbnormalApplication ApplyBaseOriginAutomaticAbnormalCurrent(
        GameState state, SourceWorldContext current, BaseReference origin,
        FieldHexOccupantReceipt target, TacticSourceRow source, S14TacticEffectInstruction instruction,
        int sourceHexAtInstructionStart, TacticSourceSamplerWorld? sampler)
    {
        if (!ReferenceEquals(current, _definitions) || !Bound(state) ||
            instruction.Index is < 0 or > 1 || instruction.Opcode is not (2 or 3 or 4 or 23))
            throw OriginAutomaticMissing("abnormal_current_instruction");
        //Current CE SC175D50/178260 and TC175FF0/178500 have the same
        //receiver branches and null-source-Army contract. SC16F700 also
        //uses the qualified gate's -1 lower coefficient (rdata1344CDC).
        if (target.NativeKind is 5 or 6)
        {
            var targetBase = new BaseReference(target.NativeKind == 5 ? BaseKind.City : BaseKind.Gate, target.NativePhysicalSlot);
            var applied = ApplyAutomaticBaseAbnormalCore(state, null, origin, targetBase, source,
                instruction, 0, sourceHexAtInstructionStart, sampler);
            var receipt = applied.Receipt;
            return new(applied.State, receipt.TargetResultMarker, [], receipt, null, receipt.NotificationDistrict,
                receipt.GeneralImmunityNotice, receipt.CombinedApplicationNotice);
        }
        if (target.NativeKind != 27) throw OriginAutomaticMissing("abnormal_target_kind" + target.NativeKind);
        int targetId = target.GameplayArmyId ?? throw OriginAutomaticMissing("abnormal_target_ArmyId");
        var receiver = new ArmyUnitReference(target.NativePhysicalSlot);
        if (!OriginNeed(current.ReadArmyUnitNativeValidity(receiver), "abnormal_target_validity"))
            return new(state, false, [], null, null, null, false, false);
        var image = state.Units.GetValueOrDefault(targetId) ?? throw OriginAutomaticMissing("abnormal_target_image");
        var native = image.NativeTacticRuntime ?? throw OriginAutomaticMissing("abnormal_target_counters");
        if (!OriginGate())
        {
            int? feedback = instruction.Opcode != 23 && native.Abnormal23 == 0 && native.Abnormal24 == 0 && native.Abnormal25 == 0
                ? 20 + instruction.Opcode - 2 : null;
            return new(state, false, [], null, feedback, null, false, false);
        }
        //16B820 repeats the selected source/target ability queries after the
        //gate. Base16D900 returns1; both wrappers apply Force193 mask4 for
        //Army recipients (Base recipients use it only for opcode23).
        float factor = OriginAbilityFactor(state, origin, target, source);
        int sourceForce = OriginNeed(current.ReadBaseForceSlot(origin), "abnormal_source_Force");
        var force = current.ReadForceTableReference(sourceForce < 52 ? sourceForce : 0)
            ?? throw OriginAutomaticMissing("abnormal_source_Force_pointer");
        int forcePhysical = OriginNeed(current.ReadForcePhysicalSlot(force), "abnormal_source_Force_physical");
        byte forceFlags = state.NativeArmySimulation.Forces.GetValueOrDefault(forcePhysical)?.Flags193
            ?? throw OriginAutomaticMissing("abnormal_source_Force193");
        int special = (forceFlags & 4) != 0 ? P(SimulationParameterKeys.Tactics.SpecialForceEffectPercent) : 100;
        float amount = instruction.EffectValue;
        amount *= factor;
        amount *= (float)unchecked(0 * P(SimulationParameterKeys.Tactics.EffectStageScale)) / 100f + 1f;
        amount *= 1f;
        amount *= special;
        amount /= 100f;
        int requested = OriginTruncate(amount);
        var working = state;
        var receipts = new List<AutomaticArmySingleAbnormalReceipt>();
        var effects = new List<PlayableTacticEffect>();
        int[] indices = instruction.Opcode == 23 ? [0, 1, 2] : [instruction.Opcode - 2];
        foreach (int index in indices) ApplyOne(index);
        int? combinedDistrict = instruction.Opcode == 23 ? NotificationDistrict() : receipts[0].NotificationDistrict;
        bool general = instruction.Opcode == 23 && combinedDistrict.HasValue && receipts.Any(row => row.Status?.CauseCode == 21);
        bool combined = instruction.Opcode == 23 && combinedDistrict.HasValue &&
            !receipts.Any(row => row.Status?.CauseCode is 21 or 22 or 23 or 24);
        return new(working, true, receipts.ToArray(), null, null, combinedDistrict, general, combined)
            { ArmyEffects = effects.ToArray() };

        bool OriginGate()
        {
            int governor = OriginGovernor(origin);
            if (OriginNeed(current.ReadPersonNativeValidity(governor), "gate_Governor_validity") &&
                OriginNeed(TacticRemakeCurrentTraits.CommanderHasEffect(current, governor, 98), "gate_Governor_effect98")) return true;
            int acting = OriginAbility(state, origin, OriginSelector(source) == 1 ? 1 : 2);
            byte selected = OriginSelector(source);
            int receiving = OriginPersonAbility(state, OriginCommander(receiver), selected == 1 ? 1 : 2, 0);
            int baseline = P(SimulationParameterKeys.Tactics.AbnormalGateBaseline);
            int higher = acting > receiving ? P(SimulationParameterKeys.Tactics.AbnormalGateHigherAbilityScale) : 0;
            int lower = acting <= receiving ? P(SimulationParameterKeys.Tactics.AbnormalGateLowerAbilityScale) : 1;
            //16F9A0's99 correction is guarded by source-Army validity; a
            //Base source does not read that cache or its scalar coefficient.
            var gate = new TacticEffectGateSourceInput(acting, receiving, baseline, higher, lower, 0, 0);
            var tactic = current.ReadSkillTacticReference(source.Slot) ?? throw OriginAutomaticMissing("gate_Tactic_pointer");
            int tacticPhysical = OriginNeed(current.ReadSkillTacticPhysicalSlot(tactic), "gate_Tactic_physical");
            int from = Hex(current.ReadBaseCurrentHexIndex(origin));
            int to = Hex(OriginNeed(current.ReadArmyUnitRawHex2A(receiver), "gate_target_Hex2A"));
            return TacticEffectSourceGateMath.AllowsTc(gate, tacticPhysical, from, to, sampler)
                ?? throw OriginAutomaticMissing("gate_World450_45C");
        }
        void ApplyOne(int index)
        {
            image = working.Units.GetValueOrDefault(targetId) ?? throw OriginAutomaticMissing("status_target_image");
            native = image.NativeTacticRuntime ?? throw OriginAutomaticMissing("status_target_counters");
            byte before = index == 0 ? native.Abnormal23 : index == 1 ? native.Abnormal24 : native.Abnormal25;
            int commander = OriginCommander(receiver);
            bool generalImmune = OriginNeed(TacticRemakeCurrentTraits.CommanderImmune(current, commander, 21), "status_immunity21");
            bool? specific = generalImmune ? null : OriginNeed(TacticRemakeCurrentTraits.CommanderImmune(current,
                commander, 22 + index), "status_specific_immunity");
            TacticAbnormalSourceInput input;
            if (generalImmune || specific == true)
                input = new(index, requested, before, true, generalImmune, specific, null, null, null, null, null, null, null, null);
            else
            {
                int[] traits = TacticRemakeCurrentTraits.ArmyContributions(current, commander,
                    ArmyTraitSourceAdapter.Encode(working, image)) ?? throw OriginAutomaticMissing("status_target_Army_traits");
                //2EED60 param2 is null, so local source98 and its multiplier
                //are both literal0, with no governor/Army trait query.
                input = new(index, requested, before, true, false, false, traits[9], traits[10 + index], 0,
                    P(SimulationParameterKeys.Tactics.AbnormalGeneralResistanceRate),
                    P(index == 0 ? SimulationParameterKeys.Tactics.AbnormalType0ResistanceRate
                        : index == 1 ? SimulationParameterKeys.Tactics.AbnormalType1ResistanceRate
                        : SimulationParameterKeys.Tactics.AbnormalType2ResistanceRate), 0, null, null);
            }
            var adjustment = TacticEffectSourceAbnormal.ResolveAdjustment(input) ?? throw OriginAutomaticMissing("status_adjustment");
            int? district = (!adjustment.CounterWritten && instruction.Opcode != 23) || adjustment.HasTraitAdjustment
                ? NotificationDistrict() : null;
            if (adjustment.CounterWritten)
                input = input with { CurrentUpperBound = P(SimulationParameterKeys.Tactics.AbnormalCounterUpperBound), CapturedOwner = new(false, null) };
            var result = TacticEffectSourceAbnormal.Resolve(input, native.ProvocationOwner) ?? throw OriginAutomaticMissing("status_result");
            if (adjustment.HasTraitAdjustment && !district.HasValue) result = result with { CauseCode = 0 };
            NativeTacticAbnormalAssignment assigned = new(index == 0 ? result.CounterAfter : native.Abnormal23,
                index == 1 ? result.CounterAfter : native.Abnormal24, index == 2 ? result.CounterAfter : native.Abnormal25, result.OwnerAfter);
            var effect = new PlayableTacticEffect(PlayableTacticEffectKind.NativeAbnormalChange,
                result.CounterWritten ? result.CounterAfter - before : 0) { NativeAbnormalAfter = assigned };
            image = TacticEffectSourceRules.ApplyReceipt(image, effect);
            working = working with { Units = new(working.Units) { [targetId] = image } };
            bool ownerWritten = false;
            if (index == 2 && result.CounterWritten && result.AdjustedStrength > 0)
            {
                var captured = CaptureOwner(image);
                result = TacticEffectSourceAbnormal.Resolve(input with { CapturedOwner = captured }, native.ProvocationOwner)
                    ?? throw OriginAutomaticMissing("status_owner_result");
                if (adjustment.HasTraitAdjustment && !district.HasValue) result = result with { CauseCode = 0 };
                assigned = assigned with { ProvocationOwner = result.OwnerAfter, OverrideTarget = captured.HasEntity ? captured.OverrideTarget : null };
                ownerWritten = captured.HasEntity;
                effect = effect with { NativeAbnormalAfter = assigned };
                image = TacticEffectSourceRules.ApplyReceipt(working.Units[targetId], effect);
                working = working with { Units = new(working.Units) { [targetId] = image } };
                if (captured.SourceResidue is { } residue)
                    working = working with { ResourceState = CampaignSourceKnowledge.From(working.ResourceState).WithArmyOverride(residue) };
            }
            working = ArmyOverrideTargetSourceAdapter.ReconcileOwner(working, image);
            var raw = new RegistryObjectReference(RegistryKind.Army, target.NativePhysicalSlot);
            if (result.CounterWritten) current.SetTacticAbnormalRawByte(raw, 0x23 + index, result.CounterAfter);
            if (ownerWritten) current.SetArmyCommandOverride(target.NativePhysicalSlot, ArmyOverrideTargetSourceAdapter.Read(working, image));
            bool starts = before == 0 && result.CounterWritten && result.UnclampedSum > 0;
            if (instruction.Opcode != 23 && starts && !district.HasValue) district = NotificationDistrict();
            receipts.Add(new(targetId, index, true, true, requested, null, result, district,
                adjustment.HasTraitAdjustment && district.HasValue, starts, result.IncreasingNotification ? 23 + index : null));
            effects.Add(effect);
        }
        int? NotificationDistrict()
        {
            int commander = OriginCommander(receiver);
            byte raw = OriginNeed(current.ReadPersonNativeValidity(commander), "notice_Commander_validity")
                ? OriginNeed(current.ReadPersonDistrictSlot(commander), "notice_CommanderE0") : (byte)0;
            var district = current.ReadDistrictTableReference(raw <= 51 ? raw : 0) ?? throw OriginAutomaticMissing("notice_District_pointer");
            return BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district, OriginAutomaticMissing)
                ? OriginNeed(district.PhysicalSlot, "notice_District_physical") : null;
        }
        TacticCapturedOwnerSource CaptureOwner(UnitState recipient)
        {
            var found = FieldHexOccupantSource.ReadCurrent(working, current, Hex(sourceHexAtInstructionStart));
            var entity = found.Receipt ?? throw OriginAutomaticMissing(found.UnresolvedOwner ?? "status_owner16EB20");
            if (entity.NativeKind == 255) return new(false, null);
            NativeTacticOwnerReference owner;
            if (entity.NativeKind == 27)
            {
                int id = entity.GameplayArmyId ?? throw OriginAutomaticMissing("owner_ArmyId");
                var actual = working.Units.GetValueOrDefault(id) ?? throw OriginAutomaticMissing("owner_Army_image");
                owner = new(actual.ForceId, DeploymentTargetKind.Unit, id);
            }
            else if (entity.NativeKind is 5 or 6)
            {
                int location = entity.BaseLocationId ?? throw OriginAutomaticMissing("owner_Base_location");
                byte force = OriginNeed(current.ReadObjectForceSlot13(entity.CapturedObject!), "owner_Base_Force13");
                owner = new(force, DeploymentTargetKind.Location, location);
            }
            else throw OriginAutomaticMissing("owner_kind" + entity.NativeKind);
            var fields = new ArmyOverrideTargetSourceFields(checked((byte)entity.NativeKind), checked((ushort)entity.NativePhysicalSlot));
            var decoded = ArmyOverrideTargetSourceAdapter.Decode(fields, working.ReadArmyLifecycle() is { } arena ? new(arena) : null);
            return new(true, owner) { OverrideTarget = decoded, SourceResidue = ArmyOverrideTargetSourceAdapter.Residue(
                ArmyOverrideTargetSourceAdapter.Owner(working, recipient), fields, decoded) };
        }
        int Hex(int raw) => OriginNeed(current.ReadHexTableReference(raw is >= 0 and < 48400 ? raw : 0)?.PhysicalSlot, "abnormal_Hex_physical");
        int P(string key) => OriginParameter(state, key);
    }
}
