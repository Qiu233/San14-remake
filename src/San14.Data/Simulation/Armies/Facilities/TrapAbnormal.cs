using San14.Core;
using static San14.Data.TrapParameters;

namespace San14.Data;

internal sealed partial class TrapEffectApplication
{
    /// <summary>TC1924A0 for installed confusion/immobilization channels.
    /// The caster passed to2EED60 is a real null; neither a substitute Army
    /// nor source trait98/rate queries participate.</summary>
    private void ApplyAbnormal(ObjectReference facility, int kind, int channel, HexListHandle targets, byte index)
    {
        if (!Need(Current.ReadObjectNativeValidity(facility), "abnormal_Object_validity")) return;
        if (channel is < 0 or > 1 || index > 1) throw Missing("abnormal_channel_index");
        var record = Current.ReadPhysicalObject(Need(facility.PhysicalSlot, "abnormal_Object_pointer"))
            ?? throw Missing("abnormal_Object_record");
        var reference = Current.ReadObjectDefinitionReference(record.RawField12 <= 30 ? record.RawField12 : 0)
            ?? throw Missing("abnormal_definition_pointer");
        if (kind != Need(reference.PhysicalSlot, "abnormal_definition_identity")) throw Missing("abnormal_definition_alias");
        var definition = Current.ReadFacilityEffectDefinition(reference) ?? throw Missing("abnormal_definition_effects");
        var effect = definition.Channel(channel);
        if (effect.Effect != index + 2) throw Missing("abnormal_channel_effect");
        int chance = effect.Amount, duration = effect.Secondary;
        int sourceHexSalt = Need(Current.ReadObjectHexIndex(facility), "abnormal_source_Hex10");
        if (Need(Current.ReadObjectDefinitionRaw2C(reference), "abnormal_definition_category") == 4)
        {
            int raw = Need(Current.ReadObjectForceSlot13(facility), "abnormal_source_Force13");
            var force = Current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("abnormal_source_Force_pointer");
            if (Need(Current.ReadForceNativeValidity(force), "abnormal_source_Force_validity"))
                duration = unchecked(unchecked(ForcePolicy(force, 58) + 100) * duration) / 100;
        }
        int baseline = Read(State, index == 0 ? ConfusionChanceBaseline : ImmobilizationChanceBaseline);
        foreach (var hex in FacilityEffectTargets.ReadHexes(Current, targets))
        {
            int targetHex = Need(hex.PhysicalSlot, "abnormal_target_Hex_pointer");
            if (Current.ReadHexTableReference(targetHex) != hex) throw Missing("abnormal_Hex_alias_16EB20");
            var selected = FieldHexOccupantSource.ReadCurrent(State, Current, targetHex);
            var occupant = selected.Receipt ?? throw Missing(selected.UnresolvedOwner ?? "abnormal_16EB20");
            if (occupant.NativeKind != 27) continue;
            var army = Current.ReadArmyUnitTableReference(occupant.NativePhysicalSlot <= 500 ? occupant.NativePhysicalSlot : 0)
                ?? throw Missing("abnormal_Army_pointer");
            int commanderId = Need(Current.ReadArmyUnitCommanderId(army), "abnormal_Commander12");
            var commander = (commanderId <= 6000 ? Current.ReadPersonLookupReference(commanderId)
                : Current.ReadTransferPersonTableReference(0)) ?? throw Missing("abnormal_Commander_pointer");
            int person = Need(commander.PhysicalSlot, "abnormal_Commander_physical");
            if (!Need(Current.ReadPersonNativeValidity(person), "abnormal_Commander_validity")) continue;
            int officer = Need(Current.ReadPersonIdentity(person), "abnormal_Commander_identity");
            int intelligence = Abilities(officer, 2);
            //192722..51: IMUL I*I wraps before float32 conversion; truncate
            //the scaled baseline BEFORE the final wrapping integer +chance.
            float factor = (float)unchecked(intelligence * intelligence) / 10000f;
            factor = 1f - factor;
            factor *= (float)baseline;
            int threshold = unchecked(Truncate(factor) + chance);
            int hour = SamplerHour();
            int sample = TacticEffectSourceSamplerMath.Sample(100, 1, sourceHexSalt,
                targetHex, officer, hour, SamplerWorld());
            if (sample < threshold)
                ApplyTrapAbnormalHit(army, person, facility, kind, index, duration);
            //A valid Commander reaches this tail even on chance/immunity
            //failure; failed numerical effects still affect the diplomacy tail.
            AddVictimForce(Current.ReadArmyUnitForceReference(army));
        }
    }

    private void ApplyTrapAbnormalHit(ArmyUnitReference army, int person,
        ObjectReference facility, int definitionId, byte index, int duration)
    {
        var image = Image(army);
        var native = image.NativeTacticRuntime ?? throw Missing("abnormal_target_runtime");
        byte before = index == 0 ? native.Abnormal23 : native.Abnormal24;
        //1924A0 resolves the source Hex pointer/name before2EED60. The
        //source Hex is not a fake caster and index0/1 never captures an owner.
        ushort rawSource = Need(Current.ReadObjectHexIndex(facility), "abnormal_hit_source_Hex");
        _ = Current.ReadHexTableReference(rawSource < 48400 ? rawSource : 0)
            ?? throw Missing("abnormal_hit_source_Hex_pointer");
        string sourceName = Feedback.DefinitionName(definitionId);
        if (!Need(Current.ReadArmyUnitNativeValidity(army), "abnormal_hit_Army_validity")) return;
        bool general = Need(TacticRemakeCurrentTraits.CommanderImmune(Current, person, 21), "abnormal_immunity21");
        bool? specific = general ? null : Need(TacticRemakeCurrentTraits.CommanderImmune(Current, person, 22 + index),
            "abnormal_specific_immunity");
        TacticAbnormalSourceInput input;
        if (general || specific == true)
            input = new(index, duration, before, true, general, specific,
                null, null, null, null, null, null, null, null);
        else
        {
            int[] traits = TacticRemakeCurrentTraits.ArmyContributions(Current, person,
                ArmyTraitSourceAdapter.Encode(State, image)) ?? throw Missing("abnormal_target_traits127");
            if (traits.Length != 127) throw Missing("abnormal_target_traits127_count");
            int generalRate = Read(State, SimulationParameterKeys.Tactics.AbnormalGeneralResistanceRate);
            int specificRate = Read(State, index == 0 ? SimulationParameterKeys.Tactics.AbnormalType0ResistanceRate
                : SimulationParameterKeys.Tactics.AbnormalType1ResistanceRate);
            //The two zeros are2EED60's initialized null-caster contribution
            //and rate. Do not read the global source-trait multiplier here.
            input = new(index, duration, before, true, false, false, traits[9], traits[10 + index],
                0, generalRate, specificRate, 0, null, null);
        }
        var adjustment = TacticEffectSourceAbnormal.ResolveAdjustment(input) ?? throw Missing("abnormal_adjustment");
        if (!adjustment.CounterWritten)
        {
            if (sourceName.Length != 0)
                Feedback.AbnormalReport(army, facility, definitionId, index, adjustment.CauseCode, 0);
            return;
        }
        //The native trait-adjustment branch queries player District before
        //choosing a cause, even when an existing counter prevents a report.
        bool causeVisible = !adjustment.HasTraitAdjustment || Feedback.PlayerDistrict(army);
        int upper = Read(State, SimulationParameterKeys.Tactics.AbnormalCounterUpperBound);
        var result = TacticEffectSourceAbnormal.Resolve(input with { CurrentUpperBound = upper }, native.ProvocationOwner)
            ?? throw Missing("abnormal_resolution");
        int cause = causeVisible ? result.CauseCode : 0;
        native = index == 0 ? native with { Abnormal23 = result.CounterAfter }
            : native with { Abnormal24 = result.CounterAfter };
        WriteImage(army, image with { NativeTacticRuntime = native });
        if (result.IncreasingNotification)
            Feedback.Speech(army, index == 0 ? CombatSpeechEvent.ConfusionApplied : CombatSpeechEvent.ImmobilizationApplied, facility);
        //2EED60 uses the UNCLAMPED sum for this report gate. A zero cap can
        //still produce report/speech while1924A0's later started visual fails.
        if (before == 0 && result.UnclampedSum > 0 && sourceName.Length != 0)
            Feedback.AbnormalReport(army, facility, definitionId, index, cause, 0);
        if (before == 0 && result.CounterAfter != 0)
        {
            int raw = Need(Current.ReadArmyUnitRawHex2A(army), "abnormal_started_Army_Hex2A");
            var position = Current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("abnormal_started_Hex_pointer");
            int at = Need(position.PhysicalSlot, "abnormal_started_Hex_identity");
            Feedback.ArmyVisual(index == 0 ? ArmyTrapPresentationOperation.AbnormalConfusionStarted
                : ArmyTrapPresentationOperation.AbnormalImmobilizationStarted, army, facility, definitionId, at);
        }
    }
}
