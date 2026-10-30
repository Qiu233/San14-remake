using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //200D30/2010B0/201400/201750/201A90, called once by the ordinary
    //outward command update. The outer dispatcher retains the task and owns
    //the transition to a separate return command after an early failure.
    internal (GameState State, PersonnelCaravanTask Task, bool ReturnEarly) ApplyForeignTradeJourneyEventCurrent(
        GameState state, SourceWorldContext current, PersonnelCaravanTask task, NativeMapSeasonDate date)
    {
        if (task.NativeKind != 17 || unchecked(PersonReturnCommands.CalendarOrdinal(date) -
            PersonReturnCommands.CalendarOrdinal(task.StartDate)) != 30) return (state, task, false);
        if (task.MemberIds.Length == 0) throw ForeignTradeReturnMissing("journey_event_member_head");
        if (task.SavedResult == 0)
        {
            var failed = PublishForeignTradeJourneyEventCurrent(state, current, task, date, task.MemberIds[0], 0x32FF, 0x3304, 7);
            var cancelled = task with { ReturnDisposition = 2 };
            failed = PublishForeignTradeCancellationCurrent(failed, current, cancelled, presentation: false, history: true);
            return (failed, cancelled, true);
        }
        switch (task.JourneyEvent)
        {
            case 2:
            {
                var working = PublishForeignTradeJourneyEventCurrent(state, current, task, date, task.MemberIds[0], 0x3300, 0x3308, 6);
                int percent = ForeignTradeParameter(working, "event-loss-percent");
                return (working, task with { GoldOffer = ForeignTradeEventPercent(task.GoldOffer, percent),
                    SuppliesOffer = ForeignTradeEventPercent(task.SuppliesOffer, percent) }, false);
            }
            case 3:
            {
                var working = PublishForeignTradeJourneyEventCurrent(state, current, task, date, task.MemberIds[0], 0x3301, 0x330C, 4);
                int percent = unchecked(100 + ForeignTradeParameter(working, "event-extra-percent"));
                return (working, task with { GoldOffer = ForeignTradeEventPercent(task.GoldOffer, percent),
                    SuppliesOffer = ForeignTradeEventPercent(task.SuppliesOffer, percent) }, false);
            }
            case 4:
            {
                var working = PublishForeignTradeJourneyEventCurrent(state, current, task, date, task.MemberIds[0], 0x3302, 0x3310, 4);
                int due = unchecked(PersonReturnCommands.CalendarOrdinal(task.DueDate) - ForeignTradeParameter(working, "event-shorter-days"));
                return (working, task with { DueDate = PersonReturnCommands.DateFromOrdinal(due) }, false);
            }
            case 5:
                return (ApplyForeignTradeJourneyGrowthCurrent(state, current, task, date), task, false);
            default:
                return (state, task, false);
        }
    }

    private static int ForeignTradeEventPercent(int amount, int percent) =>
        unchecked((int)(unchecked((uint)unchecked(amount * percent)) / 100u));

    private GameState PublishForeignTradeJourneyEventCurrent(GameState state, SourceWorldContext current,
        PersonnelCaravanTask task, NativeMapSeasonDate date, int speaker, int announcementRoot, int speechRoot, int sound)
    {
        int head = ForeignTradeReturnNeed(current.ResolveOfficerSource(task.MemberIds[0]), "event_head_Person");
        var force = current.ReadPersonForce(head) ?? throw ForeignTradeReturnMissing("event_Force");
        int ruler = TalentRulerCurrent(current, force);
        var country = ReadForeignTradeCountry(task.CountryId);
        var effects = new List<CampaignEffectRequest>();
        var notice = FormatForeignTradeMessage(state, current, announcementRoot,
            new(Person0: ruler, Person1: task.JourneyEvent == 5 ? speaker : 0, String0: country.Name,
                Integers: [unchecked((short)date.Year), unchecked((sbyte)date.Month)]));
        PersonnelAdministrationAnnouncement(effects, notice.Text);
        var speech = FormatForeignTradeMessage(notice.State, current, speechRoot,
            new(Person0: speaker, Person1: ruler, String0: country.Name, Integers: [task.MemberIds.Length]));
        PersonnelAdministrationSound(effects, sound);
        effects.Add(CreateStrategicCommandPresentation(current, notice.State, speaker, speech));
        var owner = speech.State.CampaignEffectRequests ?? throw ForeignTradeReturnMissing("event_presentation_owner");
        return speech.State with { CampaignEffectRequests = owner.Append(CampaignEffectSource.ForeignTradeCommands,
            speech.State.NativeMapSeason?.AdvancedCalendarSteps ?? throw ForeignTradeReturnMissing("event_calendar_steps"), effects) };
    }

    private GameState ApplyForeignTradeJourneyGrowthCurrent(GameState state, SourceWorldContext current,
        PersonnelCaravanTask task, NativeMapSeasonDate date)
    {
        var candidates = new List<int>();
        foreach (int id in task.MemberIds)
        {
            int physical = ForeignTradeReturnNeed(current.ResolveOfficerSource(id), "event_growth_Person");
            if (!ForeignTradeReturnNeed(current.ReadPersonNativeValidity(physical), "event_growth_validity")) continue;
            var person = state.Officers.GetValueOrDefault(id) ?? throw ForeignTradeReturnMissing("event_growth_owner");
            var basic = person.BaseAbilities ?? throw ForeignTradeReturnMissing("event_growth_base_abilities");
            if (new[] { basic.Leadership, basic.Strength, basic.Intelligence, basic.Politics, basic.Charm }.Any(value => value < 100))
                candidates.Add(id);
        }
        if (candidates.Count == 0) return state;
        var sampler = ReadArmyDestructionSampler(state, current) ?? throw ForeignTradeReturnMissing("event_growth_World450_45C");
        ForeignTradeEventShuffle(candidates, task.MemberIds[0], sampler);
        int selected = candidates[0];
        var working = PublishForeignTradeJourneyEventCurrent(state, current, task, date, selected, 0x3303, 0x3314, 5);
        var owner = working.Officers.GetValueOrDefault(selected) ?? throw ForeignTradeReturnMissing("event_selected_owner");
        var basicAbilities = owner.BaseAbilities ?? throw ForeignTradeReturnMissing("event_selected_base_abilities");
        int[] raw = [basicAbilities.Leadership, basicAbilities.Strength, basicAbilities.Intelligence, basicAbilities.Politics, basicAbilities.Charm];
        var dimensions = Enumerable.Range(0, 5).Where(index => raw[index] < 100).ToList();
        if (dimensions.Count == 0) return working;
        int minimum = dimensions.Min(index => raw[index]);
        int dimension;
        if (dimensions.Count == 1) dimension = dimensions[0];
        else
        {
            ForeignTradeEventShuffle(dimensions, selected, sampler);
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            //This is the source comparison after its shuffled order, including
            //the politics fallback when every effective value equals minRaw.
            dimension = 3;
            foreach (int index in dimensions)
            {
                int effective = ForeignTradeReturnNeed(abilities.ResolveDimension(working, selected, index,
                    (force, effect) => _baseCapacityInputs.ReadPanelModifier(working, force, effect)), "event_effective_ability");
                if (effective != minimum) { dimension = index; break; }
            }
        }
        int physicalPerson = ForeignTradeReturnNeed(current.ResolveOfficerSource(selected), "event_selected_Person");
        if (!ForeignTradeGrowthPersonPermitted(current, selected, physicalPerson)) return working;
        int before = raw[dimension];
        if (before >= 100) return working;
        //2B1880 asks for the gap between two source table rows, rather than
        //directly changing a base byte or replacing the officer's current XP.
        int requested = unchecked((int)_growthSource.ReadColumn(before + 1, dimension) -
            (before == 0 ? 0 : (int)_growthSource.ReadColumn(before, dimension)));
        var growth = _growthSource.TryApplyWithinArmyOperation(working, current, _growthQueries, selected, dimension, requested, 0);
        if (growth.Outcome == PersonGrowthOutcome.CurrentInputsUnresolved) throw ForeignTradeReturnMissing("event_2BA4E0_growth");
        return growth.State;
    }

    private static bool ForeignTradeGrowthPersonPermitted(SourceWorldContext current, int id, int physical)
    {
        if (!ForeignTradeReturnNeed(current.ReadPersonNativeValidity(physical), "event_growth_selected_validity")) return false;
        int home = ForeignTradeReturnNeed(current.ReadPersonFirstLocation(physical), "event_growth_home");
        int force = ForeignTradeReturnNeed(current.ReadPersonForce(physical)?.PhysicalSlot, "event_growth_Force");
        if (home is >= 1 and <= 61 && force is >= 46 and <= 50) return false;
        for (int i = 0; i < 5; i++)
            if (unchecked((uint)(id - (4111 + i * 20))) <= 9 && home is >= 1 and <= 61 && force is not (>= 46 and <= 50))
                return false;
        return true;
    }

    private static void ForeignTradeEventShuffle(List<int> values, int salt, TacticSourceSamplerWorld sampler)
    {
        int count = values.Count;
        if (count <= 1) return;
        for (int rounds = (count / 2) * count; rounds > 0; rounds--)
        {
            int first = TacticEffectSourceSamplerMath.Sample(count, 1, salt, 0, 0, 0, sampler);
            int second = count - 1 - TacticEffectSourceSamplerMath.Sample(count, 1, unchecked(salt + 1), 0, 0, 0, sampler);
            (values[first], values[second]) = (values[second], values[first]);
            salt = unchecked(salt + 2);
        }
    }

    //The two real presentation groups surround the friendship calculation.
    //The first call (null gain) prepares arrival; the second follows the actual
    //friendship/level writes and prepares their resulting report and history.
    internal GameState PublishForeignTradeJourneyArrivalCurrent(GameState state, SourceWorldContext current,
        PersonnelCaravanTask task, int? friendshipGain = null, int? hostileCountry = null, int hostileLoss = 0)
    {
        if (task.MemberIds.Length == 0) throw ForeignTradeReturnMissing("arrival_member_head");
        int headId = task.MemberIds[0];
        int head = ForeignTradeReturnNeed(current.ResolveOfficerSource(headId), "arrival_head_pointer");
        var force = current.ReadPersonForce(head) ?? throw ForeignTradeReturnMissing("arrival_Force");
        int forceId = ForeignTradeReturnNeed(force.PhysicalSlot, "arrival_Force_pointer");
        int ruler = TalentRulerCurrent(current, force);
        byte districtSlot = ForeignTradeReturnNeed(current.ReadPersonDistrictSlot(head), "arrival_DistrictE0");
        var district = current.ReadDistrictTableReference(districtSlot < 52 ? districtSlot : 0)
            ?? throw ForeignTradeReturnMissing("arrival_District_pointer");
        bool primary = TalentDistrictIsPlayerRulerCurrent(state, current, district);
        var country = ReadForeignTradeCountry(task.CountryId);
        var working = state;
        var effects = new List<CampaignEffectRequest>();
        if (friendshipGain is null)
        {
            if (!primary) return state;
            PersonnelAdministrationSound(effects, 8);
            Speech(0x1394, 0x32AB, new(Person0: 0x1394, Person1: ruler, String0: country.Name));
            if (task.CountryId is >= 1 and <= 4)
            {
                var date = working.NativeMapSeason?.ThirdDate ?? throw ForeignTradeReturnMissing("arrival_date");
                int root = task.CountryId switch { 1 => 0x32AC, 2 => 0x32AE, 3 => 0x32B0, _ => 0x32B2 };
                Announcement(root, new(Person0: ruler, String0: country.Name,
                    Integers: [unchecked((short)date.Year), unchecked((sbyte)date.Month)]));
                PersonnelAdministrationSound(effects, 5);
                Announcement(root + 1, new(Person0: ruler, Person1: headId, String0: country.Name,
                    Integers: [task.MemberIds.Length]));
            }
        }
        else
        {
            if (primary)
            {
                PersonnelAdministrationSound(effects, 4);
                Announcement(0x32B4, new(Person0: ruler, String0: country.Name,
                    Integers: [ForeignTradeCurrentSource.ReadFriendship(working, forceId, task.CountryId), friendshipGain.Value]));
                if (hostileCountry is >= 1 and <= 4)
                {
                    var hostile = ReadForeignTradeCountry(hostileCountry.Value);
                    PersonnelAdministrationSound(effects, 6);
                    Announcement(0x32B5, new(Person0: ruler, String0: country.Name, String1: hostile.Name,
                        Integers: [ForeignTradeCurrentSource.ReadFriendship(working, forceId, hostileCountry.Value), hostileLoss]));
                }
                int rulerPerson = ForeignTradeReturnNeed(current.ResolveOfficerSource(ruler), "arrival_ruler_pointer");
                Speech(headId, 0x32B6, new(Person0: headId, Person1: ruler, String0: country.Name,
                    Base0: ForeignTradeReturnNeed(current.ReadPersonFirstLocation(rulerPerson), "arrival_ruler_BaseE2")));
            }
            if (TalentForceIsPlayerCurrent(working, current, force))
            {
                var main = FormatForeignTradeMessage(working, current, 0x32BC, new(Person0: ruler, String0: country.Name));
                var supplemental = FormatForeignTradeMessage(main.State, current, 0x32AB,
                    new(Person0: 0x1394, Person1: ruler, String0: country.Name, Integers: [task.MemberIds.Length]));
                int day = (ReadArmyDestructionSampler(supplemental.State, current)
                    ?? throw ForeignTradeReturnMissing("arrival_history_day450")).Day450;
                working = WriteHistory(supplemental.State, current, new(1, districtSlot, 0, day, main.Text,
                    supplemental.Text, 0x1394, MarkBit0: 1, MarkBit4: 0), effects);
            }
        }
        if (effects.Count == 0) return working;
        var owner = working.CampaignEffectRequests ?? throw ForeignTradeReturnMissing("arrival_presentation_owner");
        return working with { CampaignEffectRequests = owner.Append(CampaignEffectSource.ForeignTradeCommands,
            working.NativeMapSeason?.AdvancedCalendarSteps ?? throw ForeignTradeReturnMissing("arrival_calendar_steps"), effects) };

        void Announcement(int root, HistoryMessageOperands operands)
        {
            var message = FormatForeignTradeMessage(working, current, root, operands);
            working = message.State;
            PersonnelAdministrationAnnouncement(effects, message.Text);
        }
        void Speech(int speaker, int root, HistoryMessageOperands operands)
        {
            var source = working;
            var message = FormatForeignTradeMessage(source, current, root, operands);
            working = message.State;
            effects.Add(CreateStrategicCommandPresentation(current, source, speaker, message));
        }
    }

    //1DA630 prepares this actual messenger subtitle before entering the
    //return state. A pending report must bypass this method on later updates.
    internal GameState PublishForeignTradeReturnArrivalCurrent(GameState state, SourceWorldContext current,
        PersonnelCaravanTask task)
    {
        if (task.MemberIds.Length == 0) throw ForeignTradeReturnMissing("return_notice_member_head");
        int head = ForeignTradeReturnNeed(current.ResolveOfficerSource(task.MemberIds[0]), "return_notice_head");
        var force = current.ReadPersonForce(head) ?? throw ForeignTradeReturnMissing("return_notice_Force");
        if (!TalentForceIsPlayerCurrent(state, current, force)) return state;
        int ruler = TalentRulerCurrent(current, force);
        var notice = FormatForeignTradeMessage(state, current, 0x32BD,
            new(Person0: 0x1394, Person1: ruler, String0: ReadForeignTradeCountry(task.CountryId).Name));
        var effects = new List<CampaignEffectRequest>();
        PersonnelAdministrationSound(effects, 8);
        effects.Add(CreateStrategicCommandPresentation(current, state, 0x1394, notice));
        var owner = notice.State.CampaignEffectRequests ?? throw ForeignTradeReturnMissing("return_notice_owner");
        return notice.State with { CampaignEffectRequests = owner.Append(CampaignEffectSource.ForeignTradeCommands,
            notice.State.NativeMapSeason?.AdvancedCalendarSteps ?? throw ForeignTradeReturnMissing("return_notice_calendar"), effects) };
    }

    //1E7620. Presentation and history are independently requested by the
    //actual source caller. Explicit1F22A0 only schedules return and does not
    //invoke this report; failed200D30 requests history without presentation.
    internal GameState PublishForeignTradeCancellationCurrent(GameState state, SourceWorldContext current,
        PersonnelCaravanTask task, bool presentation, bool history)
    {
        if ((!presentation && !history) || task.MemberIds.Length == 0) return state;
        int headId = task.MemberIds[0];
        int head = ForeignTradeReturnNeed(current.ResolveOfficerSource(headId), "cancellation_head");
        var force = current.ReadPersonForce(head) ?? throw ForeignTradeReturnMissing("cancellation_Force");
        if (!TalentForceIsPlayerCurrent(state, current, force) ||
            (ForeignTradeReturnNeed(current.ReadWorldTransferServiceFlags(), "cancellation_World16A8") & 0x100) != 0) return state;
        int ruler = TalentRulerCurrent(current, force);
        string country = ReadForeignTradeCountry(task.CountryId).Name;
        var operands = new HistoryMessageOperands(Person0: headId, Person1: ruler, String0: country,
            Integers: [task.MemberIds.Length]);
        var working = state;
        var effects = new List<CampaignEffectRequest>();
        if (presentation)
        {
            var primary = ForcePrimaryDistrictSource.Resolve(current, force);
            int primaryId = ForeignTradeReturnNeed(primary.PhysicalSlot, "cancellation_primary_District");
            if (ForeignTradeReturnNeed(current.ReadDistrictNativeValidity(primaryId), "cancellation_primary_validity") &&
                primaryId == task.SourceDistrictId)
            {
                var notice = FormatForeignTradeMessage(working, current, 0x32F9, operands);
                effects.Add(CreateStrategicCommandPresentation(current, working, headId, notice, conditional: true));
                working = notice.State;
            }
        }
        if (history)
        {
            var main = FormatForeignTradeMessage(working, current, 0x32FE, new(String0: country));
            var supplemental = FormatForeignTradeMessage(main.State, current, 0x32F9, operands);
            var source = BaseLookup.Find(task.SourceLocationId, current);
            var district = current.ReadBaseDistrictReference(source) ?? throw ForeignTradeReturnMissing("cancellation_source_Base_District");
            int districtId = ForeignTradeReturnNeed(district.PhysicalSlot, "cancellation_history_District");
            int day = (ReadArmyDestructionSampler(supplemental.State, current)
                ?? throw ForeignTradeReturnMissing("cancellation_history_day450")).Day450;
            working = WriteHistory(supplemental.State, current, new(1, districtId, 0, day, main.Text,
                supplemental.Text, headId, MarkBit0: 1, MarkBit4: 0), effects);
        }
        if (effects.Count == 0) return working;
        var owner = working.CampaignEffectRequests ?? throw ForeignTradeReturnMissing("cancellation_effect_owner");
        return working with { CampaignEffectRequests = owner.Append(CampaignEffectSource.ForeignTradeCommands,
            working.NativeMapSeason?.AdvancedCalendarSteps ?? throw ForeignTradeReturnMissing("cancellation_calendar"), effects) };
    }
}
