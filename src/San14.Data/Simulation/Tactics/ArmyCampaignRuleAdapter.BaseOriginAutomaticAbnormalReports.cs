using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The numeric leaf has already applied each counter. This consumes only
    //the original notification branches, before the caller's17A1C0 marker.
    internal GameState ApplyBaseOriginAutomaticAbnormalReportsCurrent(GameState state,
        SourceWorldContext current, BaseReference origin, FieldHexOccupantReceipt target,
        TacticSourceRow source, S14TacticEffectInstruction instruction,
        BaseOriginAutomaticAbnormalApplication applied, NativeArmyCombatCycleSampler sampler,
        NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        var working = state;
        if (target.NativeKind == 27)
        {
            int id = target.GameplayArmyId ?? throw OriginAbnormalReportMissing("target_ArmyId");
            var army = working.Units.GetValueOrDefault(id) ?? throw OriginAbnormalReportMissing("target_Army_image");
            var receiver = new ArmyUnitReference(target.NativePhysicalSlot);
            var feedback = new ArmyTraitFeedback(working, current, date, effects, () => working,
                speechConsumer: CreateArmyTraitSpeechConsumer(current, () => working, next => working = next));
            if (applied.FailureFeedbackKind is int failed) feedback.NoAbnormalApplied(receiver, failed);
            foreach (var counter in applied.ArmyCounters)
                if (counter.IncreasingFeedbackKind is int increased) feedback.AbnormalIncreased(receiver, increased);
            working = feedback.Publish(working);
            if (instruction.Opcode != 23)
            {
                foreach (var counter in applied.ArmyCounters)
                    if (counter.NotificationDistrict is int district && counter.Status is { } status &&
                        (!status.CounterWritten || counter.StartsStatus))
                        working = ApplyAutomaticArmyAbnormalReport(working, current, null, army, district,
                            counter.NativeIndex, status.CauseCode, source.Name, ReadDay, effects);
                return working;
            }
            if (applied.NotificationDistrict is not int selectedDistrict) return working;
            if (applied.GeneralImmunityNotice)
                return ApplyAutomaticArmyAbnormalReport(working, current, null, army, selectedDistrict,
                    0, 21, source.Name, ReadDay, effects);
            bool hasSpecific = applied.ArmyCounters.Any(row => row.Status?.CauseCode is 22 or 23 or 24);
            if (!hasSpecific)
                Append(0x3223, 5, selectedDistrict, Need(current.ReadArmyUnitRawHex2A(receiver), "Army_Hex2A"),
                    new(Army0: receiver, String0: source.Name, String1: Phrase(0), String2: Phrase(2), String3: Phrase(1)));
            bool generalAttributed = false;
            foreach (var counter in applied.ArmyCounters)
            {
                if (counter.Status is not { } status) continue;
                int cause = generalAttributed && status.CauseCode is 9 or 98 ? 0 : status.CauseCode;
                if (cause == 98) throw OriginAbnormalReportMissing("null_Army_source98_cause");
                if (!hasSpecific && cause == 9)
                {
                    //2EAE60 reports the common resistance once with322C;
                    //it does not reuse a per-status phrase as that operand.
                    working = ApplyAutomaticArmyAbnormalReport(working, current, null, army, selectedDistrict,
                        counter.NativeIndex, cause, source.Name, ReadDay, effects,
                        suppressAppliedNotice: true, combinedAttribution: true);
                    generalAttributed = true;
                }
                else
                {
                    bool emitMain = !hasSpecific && cause == 0 && counter.StartsStatus;
                    working = ApplyAutomaticArmyAbnormalReport(working, current, null, army, selectedDistrict,
                        counter.NativeIndex, cause, source.Name, ReadDay, effects, suppressAppliedNotice: !emitMain);
                }
            }
            return working;
        }
        if (target.NativeKind is not (5 or 6) || applied.BaseReceipt is not { } baseReceipt) return working;
        var targetBase = baseReceipt.Target;
        foreach (var counter in baseReceipt.Counters)
        {
            if (!counter.IncreasingVisual) continue;
            ushort location = Need(current.ReadBaseObjectLink(targetBase), "feedback_Base4E");
            int governor = Governor(targetBase);
            int person = Need(current.ReadPersonIdentity(governor), "feedback_Governor10");
            int hex = current.ReadBaseCurrentHexIndex(targetBase);
            effects.Add(new(null, null) { BaseAbnormalFeedback = new(location, person, hex,
                counter.NativeIndex == 0 ? 15 : 16) });
        }
        if (instruction.Opcode != 23)
        {
            foreach (var counter in baseReceipt.Counters)
                if (counter.NotificationDistrict is int district && (!counter.CounterWritten || counter.StartsStatus))
                    BaseNotice(targetBase, district, counter.NativeIndex, counter.CauseCode, false, false);
            return working;
        }
        if (baseReceipt.NotificationDistrict is not int baseDistrict) return working;
        if (baseReceipt.GeneralImmunityNotice)
        {
            BaseNotice(targetBase, baseDistrict, 0, 21, false, false);
            return working;
        }
        bool specificImmunity = baseReceipt.Counters.Any(row => row.CauseCode is 22 or 24);
        if (!specificImmunity)
            Append(0x3222, 5, baseDistrict, current.ReadBaseCurrentHexIndex(targetBase),
                new(Base0: Need(current.ReadBaseObjectLink(targetBase), "combined_Base4E"),
                    String0: source.Name, String1: Phrase(0), String2: Phrase(2)), mark5: 1);
        bool commonAttributed = false;
        foreach (var counter in baseReceipt.Counters)
        {
            int cause = commonAttributed && counter.CauseCode is 9 or 98 ? 0 : counter.CauseCode;
            if (cause == 98) throw OriginAbnormalReportMissing("null_Army_source98_cause");
            if (!specificImmunity && cause == 9)
            {
                BaseNotice(targetBase, baseDistrict, counter.NativeIndex, cause, true, true);
                commonAttributed = true;
            }
            else
                BaseNotice(targetBase, baseDistrict, counter.NativeIndex, cause,
                    specificImmunity || !counter.StartsStatus, false);
        }
        return working;

        void BaseNotice(BaseReference at, int district, int index, int cause, bool suppressMain, bool combinedAttribution)
        {
            //2F3280's actual player-primary-District and index0/2 gates.
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(working, current)
                ?? throw OriginAbnormalReportMissing("player_Force");
            var primary = ForcePrimaryDistrictSource.Resolve(current, player);
            if (!Need(current.ReadDistrictNativeValidity(primary), "primary_District_validity") ||
                Need(primary.PhysicalSlot, "primary_District") != district || index is not (0 or 2)) return;
            ushort location = Need(current.ReadBaseObjectLink(at), "report_Base4E");
            int hex = current.ReadBaseCurrentHexIndex(at);
            int governor = Governor(at);
            if (cause is 21 or 22 or 24)
            {
                var trait = FirstImmunity(governor, cause);
                if (trait is null) return;
                Append(cause == 21 ? 0x3226 : 0x3227, 0, district, hex,
                    new(Base0: location, String0: current.ReadPersonalityName(trait),
                        String1: cause == 21 ? null : Phrase(cause == 22 ? 0 : 2)));
                return;
            }
            if (!suppressMain)
                Append(0x3224, 5, district, hex, new(Base0: location, String0: source.Name, String1: Phrase(index)));
            if (cause is not (9 or 10 or 12)) return;
            if (!Need(current.ReadPersonNativeValidity(governor), "attribution_Governor_validity")) return;
            int total = TacticRemakeCurrentTraits.CommanderEffectSum(current, governor, cause)
                ?? throw OriginAbnormalReportMissing("attribution_Person_effect");
            PersonalityReference? selected = null;
            int selectedValue = 0;
            foreach (var trait in Traits(governor))
            {
                int value = Contribution(trait, cause);
                if (value == 0 || (value > 0) != (total > 0) || Abs(value) <= Abs(selectedValue)) continue;
                selected = trait; selectedValue = value;
            }
            if (selected is null || !Need(current.ReadSkillPersonalityValidity(selected), "attribution_trait_validity")) return;
            string sign = ReadHistoryDataText(1, selectedValue > 0 ? 0x4BA : 0x4B9);
            string? name = current.ReadPersonalityName(selected);
            if (combinedAttribution)
                Append(0x322A, 0, district, hex, new(Base0: location, String0: name,
                    String1: Phrase(0), String2: Phrase(2), String3: sign));
            else
                Append(0x322B, 0, district, hex, new(Base0: location, String0: name,
                    String1: Phrase(cause == 9 ? index : cause - 10), String2: sign));
        }
        IEnumerable<PersonalityReference> Traits(int person)
        {
            for (int slot = 0; slot < 9; slot++)
            {
                var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, person, slot)
                    ?? throw OriginAbnormalReportMissing("selected_trait_pointer");
                if (Need(current.ReadSkillPersonalityValidity(trait), "selected_trait_validity")) yield return trait;
            }
        }
        PersonalityReference? FirstImmunity(int person, int cause)
        {
            if (!Need(current.ReadPersonNativeValidity(person), "immunity_Person_validity")) return null;
            foreach (var trait in Traits(person))
                if (Need(current.ReadPersonalityByte(trait, 0xB6), "immunity_B6") == cause ||
                    Need(current.ReadPersonalityByte(trait, 0xBA), "immunity_BA") == cause) return trait;
            return null;
        }
        int Contribution(PersonalityReference trait, int effect)
        {
            for (int pair = 0; pair < 2; pair++)
                if (Need(current.ReadPersonalityByte(trait, pair == 0 ? 0xB6 : 0xBA), "trait_code") == effect)
                    return Math.Clamp((int)unchecked((short)Need(current.ReadPersonalityWord(trait,
                        pair == 0 ? 0xB8 : 0xBC), "trait_value")), -100, 2000);
            return 0;
        }
        int Governor(BaseReference at)
        {
            ushort raw = Need(current.ReadArmyReturnBaseGovernor2E(at), "Governor2E");
            return Need((raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Governor_pointer");
        }
        void Append(int root, int category, int district, int hex, HistoryMessageOperands operands, int mark5 = 0)
        {
            var text = FormatHistoryReport(working, current, root, operands);
            working = text.State;
            working = WriteHistory(working, current, new(category, district, hex, ReadDay(), text.Text,
                CampaignHistoryWriter.Supplemental(working), 5012, MarkBit4: 1, MarkBit5: mark5), effects);
        }
        int ReadDay() => sampler.Read(working)?.Day450 ?? throw OriginAbnormalReportMissing("World450");
        string Phrase(int index) => ReadHistoryDataText(1, index switch
            { 0 => 0x25C, 1 => 0x260, 2 => 0x25E, _ => throw OriginAbnormalReportMissing("status_phrase") });
        T Need<T>(T? value, string name) where T : struct => value ?? throw OriginAbnormalReportMissing(name);
        int Abs(int value) => value < 0 ? unchecked(-value) : value;
    }

    private static InvalidDataException OriginAbnormalReportMissing(string name) => new("automatic_Base_origin_abnormal_reports." + name);
}
