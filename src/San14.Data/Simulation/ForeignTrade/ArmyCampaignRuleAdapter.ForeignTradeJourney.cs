using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPersonnelCaravanExecution
{
    PersonnelCaravanAdvanceResult IPersonnelCaravanExecution.AdvanceCaravanCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, PersonnelCaravanTask task,
        NativeMapSeasonDate date, IArmyReturnGovernorCurrentQueries queries, bool borrowedCurrent)
    {
        if (date.Hour != (task.NativeKind == 17 ? 8 : 22)) return new(state, false);
        if (task.PendingReport is not null) return new(state, false);
        bool emptyAtEntry = task.MemberIds.Length == 0;
        (state, task) = FilterForeignTradeMembersCurrent(state, current, task, date);
        if (task.Cancelled || emptyAtEntry) return new(StoreForeignTradeTaskCurrent(state, current, task), true);
        task = task with { Started = true };
        state = StoreForeignTradeTaskCurrent(state, current, task);
        if (task.NativeKind == 17 && task.MemberIds.Length != 0 && TalentForceIsPlayerCurrent(state, current,
            current.ReadPersonForce(ForeignTradeNeed(current.ResolveOfficerSource(task.MemberIds[0]), "journey_head"))
                ?? throw CaravanMissing("journey_actor_force")))
        {
            var journey = ApplyForeignTradeJourneyEventCurrent(state, current, task, date);
            state = journey.State; task = journey.Task;
            state = StoreForeignTradeTaskCurrent(state, current, task);
            if (journey.ReturnEarly) return new(ScheduleForeignTradeReturnCurrent(state, current, task, date), true);
        }
        int remaining = Math.Max(0, unchecked(PersonReturnCommands.CalendarOrdinal(task.DueDate) - PersonReturnCommands.CalendarOrdinal(date)));
        if (remaining > 1) return new(StoreForeignTradeTaskCurrent(state, current, task with { NeedsRefresh = true }), false);
        (state, task) = FilterForeignTradeMembersCurrent(state, current, task, date);
        if (task.Cancelled || task.MemberIds.Length == 0) return new(StoreForeignTradeTaskCurrent(state, current, task), true);
        if (task.NativeKind == 17)
        {
            var control = ReadTutorialControlState(state);
            if (TacticRemakeCurrentBattleControl.ReadHeader(control) is int header && header != 0 &&
                TacticRemakeCurrentBattleControl.ReadBitmapPresence(control) == true)
                state = SetTutorialLessonEventCurrent(state, 49, true);
            state = PublishForeignTradeJourneyArrivalCurrent(state, current, task);
            var arrival = ResolveForeignTradeArrivalCurrent(state, current, task);
            state = arrival.State; task = arrival.Task;
            state = PublishForeignTradeJourneyArrivalCurrent(state, current, task, arrival.Gain, arrival.HostileCountry, arrival.HostileLoss);
            state = ConsumeForeignTradeOfferedItemCurrent(state, current, task.OfferedItemId);
            task = task with { GoldOffer = 0, SuppliesOffer = 0, ReturnDisposition = 1 };
            state = StoreForeignTradeTaskCurrent(state, current, task);
            state = ScheduleForeignTradeReturnCurrent(state, current, task, date);
            state = ApplyForeignTradeMemberRewardsCurrent(state, current, task, 17);
            return new(state, true);
        }
        int head = ForeignTradeNeed(current.ResolveOfficerSource(task.MemberIds[0]), "return_head");
        var force = current.ReadPersonForce(head) ?? throw CaravanMissing("return_force");
        bool player = TalentForceIsPlayerCurrent(state, current, force);
        state = PublishForeignTradeReturnArrivalCurrent(state, current, task);
        var settled = SettleForeignTradeReturnCurrent(state, current, task);
        state = settled.State;
        if (player)
            return new(StoreForeignTradeTaskCurrent(state, current, task with { PendingReport = settled.Report }), false);
        var target = new DiplomacyCommandSource(state, current).Capital(ForeignTradeNeed(force.PhysicalSlot, "return_force_slot"));
        var returned = ApplyPersonReturnArrivalCurrent(state, current, pool, task.Id, target, queries, borrowedCurrent, date);
        return new(returned.State ?? throw CaravanMissing(returned.UnresolvedOwner ?? "return_arrival"), true);

    }

    internal GameState CancelForeignTradeAssociatedCurrent(GameState state, SourceWorldContext current,
        PersonnelCaravanTask task, NativeMapSeasonDate date)
    {
        //Both caravan17 and18 share virtual90=1F1CC0: refund first,
        //then2351B0/235730 schedules ordinary Return15. The overseas18
        //initializer belongs to normal arrival, not cancellation.
        var refund = RefundForeignTradeCargoCurrent(state, current, task, date);
        state = StoreForeignTradeTaskCurrent(refund.State, current, refund.Task);
        state = PersonReturnCommands.ScheduleTalentCompletionReturnCurrent(state, current,
            task.Id, task.MemberIds, date, task.ReportToPlayer);
        var owner = current.ReadOwnedPersonCommandManager() ?? throw CaravanMissing("cancel_manager");
        return state with { PersonnelCommands = PersonReturnCommands.QueueRetirementCurrent(owner, current, task.Id) };
    }

    internal static GameState StoreForeignTradeTaskCurrent(GameState state, SourceWorldContext current, PersonnelCaravanTask task)
    {
        var owner = current.ReadOwnedPersonCommandManager() ?? state.PersonnelCommands ?? throw CaravanMissing("task_manager");
        var tasks = owner.Caravans ?? throw CaravanMissing("task_owner");
        owner = owner with { Caravans = new(tasks) { [task.Id] = task } };
        current.SetOwnedPersonCommandManager(owner);
        current.SetLinkedEntityMemberIds(task.Id, task.MemberIds.Select(id => (uint?)id).ToArray());
        return state with { PersonnelCommands = owner };
    }

    private (GameState State, PersonnelCaravanTask Task) FilterForeignTradeMembersCurrent(GameState state,
        SourceWorldContext current, PersonnelCaravanTask task, NativeMapSeasonDate date)
    {
        var removed = new HashSet<int>(); var changed = new List<int>();
        foreach (int id in task.MemberIds)
        {
            int person = ForeignTradeNeed((current.ReadPersonLookupReference(id is >= 0 and <= 6000 ? id : 0))?.PhysicalSlot, "filter_person");
            if (!ForeignTradeNeed(current.ReadPersonNativeValidity(person), "filter_validity") ||
                ForeignTradeNeed(current.ReadPersonStatus(person), "filter_status") == 9) { removed.Add(id); continue; }
            if (ForeignTradeNeed(current.ReadPersonOldForceSlot(person), "filter_force") == task.SourceForceId) continue;
            removed.Add(id);
            if (ForeignTradeNeed(current.ReadPersonStatus(person), "filter_serving_status") is >= 1 and <= 4) changed.Add(id);
        }
        if (removed.Count != 0)
        {
            task = task with { MemberIds = task.MemberIds.Where(id => !removed.Contains(id)).ToArray() };
            state = StoreForeignTradeTaskCurrent(state, current, task);
            foreach (int id in changed)
            {
                int person = ForeignTradeNeed(current.ResolveOfficerSource(id), "filter_changed_person");
                var associated = current.ReadPersonLinkedEntity(person) ?? throw CaravanMissing("filter_changed_association");
                if (ForeignTradeNeed(current.ReadLinkedEntityNativeValidity(associated), "filter_changed_association_validity")) continue;
                var hex = current.ReadHexTableReference(task.EndHexId) ?? throw CaravanMissing("filter_country_hex");
                int area = ForeignTradeNeed(current.ReadHexAreaSlot(hex), "filter_country_area");
                current.WritePersonSecondLocation(person, area is >= 1 and <= 500 ? (ushort)(area + 561) : (ushort)0);
                state = PersonReturnCommands.ScheduleTalentCompletionReturnCurrent(state, current, task.Id, [id], date, task.ReportToPlayer);
            }
        }
        return (state, task);
    }

    private (GameState State, PersonnelCaravanTask Task, int Gain, int? HostileCountry, int HostileLoss)
        ResolveForeignTradeArrivalCurrent(GameState state, SourceWorldContext current, PersonnelCaravanTask task)
    {
        int forceId = task.SourceForceId, countryId = task.CountryId;
        int food = unchecked((int)((long)task.SuppliesOffer * 10 / ForeignTradeParameter(state, "food-to-gold-divisor")));
        int itemValue = 0;
        var item = current.ReadRegistryTableReference(RegistryKind.Item, task.OfferedItemId is >= 0 and <= 201 ? task.OfferedItemId : 0)
            ?? throw CaravanMissing("arrival_item");
        if (ForeignTradeNeed(current.ReadRegistryObjectNativeValidity(item), "arrival_item_validity"))
        {
            var offeredItem = state.Items?.GetValueOrDefault(task.OfferedItemId) ?? throw CaravanMissing("arrival_item_owner");
            itemValue = Math.Min(50, ForeignTradeNeed(state.ItemDefinitions.GetValueOrDefault(offeredItem.DefinitionId)?.LoyaltyValue, "arrival_item_value"));
        }
        int[] ability = new int[5]; int traits = 0;
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        foreach (int id in task.MemberIds)
        {
            int person = ForeignTradeNeed(current.ResolveOfficerSource(id), "arrival_person");
            if (!ForeignTradeNeed(current.ReadPersonNativeValidity(person), "arrival_person_validity")) continue;
            for (int dimension = 0; dimension < 5; dimension++)
                ability[dimension] = unchecked(ability[dimension] + ForeignTradeNeed(abilities.ResolveDimension(state, id, dimension,
                    (owner, effect) => _baseCapacityInputs.ReadPanelModifier(state, owner, effect)), "arrival_ability"));
            if (ForeignTradeNeed(TacticRemakeCurrentTraits.CommanderHasEffect(current, person, 113), "arrival_trait113"))
                traits = unchecked(traits + ForeignTradeNeed(TacticRemakeCurrentTraits.CommanderEffectSum(current, person, 113), "arrival_trait113_value"));
        }
        float offered = (float)ForeignTradeParameter(state, "outward-friendship-troop-scale") * task.TroopOffer;
        offered += food; offered += task.GoldOffer;
        offered += (float)ForeignTradeParameter(state, "outward-friendship-item-scale") * itemValue;
        int baseBenefit = unchecked(ForeignTradeParameter(state, "outward-friendship-base") +
            (int)MathF.Sqrt(offered) / ForeignTradeParameter(state, "outward-friendship-offer-divisor") +
            (ability[3] / ForeignTradeParameter(state, "outward-friendship-politics-divisor") +
             unchecked(ability[0] + ability[1] + ability[2] + ability[4]) / ForeignTradeParameter(state, "outward-friendship-other-ability-divisor")) /
            ForeignTradeParameter(state, "outward-friendship-ability-divisor"));
        int benefit = unchecked(baseBenefit + traits);
        if (ForeignTradeNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "arrival_settings") == 0)
        {
            int percent = ForeignTradeNeed(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 40), "arrival_advanced40");
            benefit = Math.Min(unchecked(benefit * percent) / 100, 1000 - ForeignTradeCurrentSource.ReadFriendship(state, forceId, countryId));
        }
        state = ForeignTradeCurrentSource.WriteFriendship(state, forceId, countryId,
            unchecked(ForeignTradeCurrentSource.ReadFriendship(state, forceId, countryId) + benefit));
        int loss = Math.Min(baseBenefit / ForeignTradeParameter(state, "outward-hostile-loss-divisor"), ForeignTradeParameter(state, "outward-hostile-loss-cap"));
        var relations = state.ForeignCountryRelations?.Countries.GetValueOrDefault(countryId)?.Relations ?? throw CaravanMissing("arrival_country_relations");
        int? hostile = null;
        for (int peer = 1; peer < 5; peer++)
        {
            if (peer == countryId || relations[peer] != 2) continue;
            int before = ForeignTradeCurrentSource.ReadFriendship(state, forceId, peer);
            if (before >= ForeignTradeParameter(state, "outward-hostile-warning-minimum")) hostile = peer;
            state = ForeignTradeCurrentSource.WriteFriendship(state, forceId, peer, unchecked(before - loss));
            if (hostile is not null) break;
        }
        var force = current.ReadForceTableReference(forceId) ?? throw CaravanMissing("arrival_force");
        bool player = TalentForceIsPlayerCurrent(state, current, force);
        var country = ReadForeignTradeCountry(countryId);
        var rewardItem = current.ReadRegistryTableReference(RegistryKind.Item, country.FirstItemId) ?? throw CaravanMissing("arrival_reward_item");
        int itemStatus = ForeignTradeNeed(current.ReadRegistryItemRawField50(rewardItem), "arrival_reward_item_status");
        int oldStage = ForeignTradeCurrentSource.ReadStage(state, forceId, countryId);
        int points = ForeignTradeCurrentSource.ReadFriendship(state, forceId, countryId);
        int stage = oldStage; byte reward = 0;
        if (points >= ForeignTradeParameter(state, "first-friendship-threshold") && oldStage == 0) { stage = 1; if (!player) reward = 1; }
        if (points >= ForeignTradeParameter(state, "second-friendship-threshold") && oldStage == 1 && (!player || itemStatus != 4)) stage = 2;
        int firstBit = new[] { 8, 0, 2, 4, 6 }[countryId], secondBit = new[] { 8, 1, 3, 5, 7 }[countryId];
        int flags = ForeignTradeCurrentSource.ReadFlags(state, forceId);
        bool firstOwned = firstBit < 8 && (flags & (1 << firstBit)) != 0, secondOwned = secondBit < 8 && (flags & (1 << secondBit)) != 0;
        if (points >= ForeignTradeParameter(state, "third-friendship-threshold") && oldStage == 2 && firstOwned) stage = 3;
        if (points >= ForeignTradeParameter(state, "fourth-friendship-threshold") && oldStage > 2 && secondOwned) stage = 4;
        state = ForeignTradeCurrentSource.WriteStage(state, forceId, countryId, stage);
        if (stage >= 4) reward = 4;
        else if (stage >= 3 && !secondOwned) reward = 3;
        else if (stage >= 2 && !firstOwned) reward = 2;
        else if (player && stage != 0 && !firstOwned && itemStatus == 4) reward = 1;
        return (state, task with { RewardStage = reward }, benefit, hostile, loss);
    }

    private static GameState ConsumeForeignTradeOfferedItemCurrent(GameState state, SourceWorldContext current, int itemId)
    {
        var item = current.ReadRegistryTableReference(RegistryKind.Item, itemId is >= 0 and <= 201 ? itemId : 0) ?? throw CaravanMissing("consume_item");
        if (!ForeignTradeNeed(current.ReadRegistryObjectNativeValidity(item), "consume_item_validity")) return state;
        var items = state.Items ?? throw CaravanMissing("consume_items");
        var before = items.GetValueOrDefault(itemId) ?? throw CaravanMissing("consume_item_owner");
        var lifecycle = before.Lifecycle ?? throw CaravanMissing("consume_item_lifecycle");
        byte flags = ForeignTradeNeed(lifecycle.RetainedFlags, "consume_item_flags");
        current.SetCaptureItemOwner4A(itemId, 0); current.SetRegistryItemRawField50(itemId, 5);
        //The source writes50=5 before324580, so its same-status early exit
        //preserves the existing item registry node.
        return state with { Items = new(items) { [itemId] = before with { Holder = new(null), Lifecycle = lifecycle with
            { RetainedFlags = (byte)(flags & 0xF7), Status = ItemStatusKnowledge.Exact(5) } } } };
    }

    internal GameState ScheduleForeignTradeReturnCurrent(GameState state, SourceWorldContext current,
        PersonnelCaravanTask task, NativeMapSeasonDate date)
    {
        if (task.MemberIds.Length == 0) return state;
        var refund = RefundForeignTradeCargoCurrent(state, current, task, date);
        task = refund.Task;
        state = StoreForeignTradeTaskCurrent(refund.State, current, task);
        //224A60 builds the packet after1F2600 has refunded and cleared cargo.
        int[] members = task.MemberIds.Where(id => ForeignTradeNeed(current.ReadPersonStatus(
            ForeignTradeNeed(current.ResolveOfficerSource(id), "return_init_member")), "return_init_status") is >= 1 and <= 4).ToArray();
        if (members.Length == 0) return state;
        var owner = current.ReadOwnedPersonCommandManager() ?? throw CaravanMissing("return_init_manager");
        if (owner.AvailableDispatchSlots.Length == 0) throw CaravanMissing("return_init_allocation");
        int first = ForeignTradeNeed(current.ResolveOfficerSource(members[0]), "return_init_head");
        int home = ForeignTradeNeed(current.ReadPersonFirstLocation(first), "return_init_home");
        var homeBase = BaseLookup.Find(home, current);
        int district = ForeignTradeNeed(current.ReadBaseDistrictSlot(homeBase), "return_init_district");
        int oldArea = ForeignTradeNeed(current.ReadPersonSecondLocation(first), "return_init_old_area");
        var portHex = current.ReadHexTableReference(task.EndHexId) ?? throw CaravanMissing("return_init_port_hex");
        int portArea = ForeignTradeNeed(current.ReadHexAreaSlot(portHex), "return_init_port_area");
        int start = ForeignTradeNeed(current.ReadAreaHexIndex(new(portArea <= 500 ? portArea : 0)), "return_init_start");
        var force = current.ReadPersonForce(first) ?? throw CaravanMissing("return_init_force");
        int forceId = ForeignTradeNeed(force.PhysicalSlot, "return_init_force_slot");
        int leadership = 0, trait = 0;
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        int entryCityId = new[] { 0, 31, 44, 20, 18 }[task.CountryId];
        var entry = current.ReadCityTableReference(entryCityId) ?? throw CaravanMissing("return_init_entry_city");
        var entryHex = current.ReadHexTableReference(current.ReadBaseCurrentHexIndex(entry)) ?? throw CaravanMissing("return_init_entry_hex");
        int entryArea = ForeignTradeNeed(current.ReadHexAreaSlot(entryHex), "return_init_entry_area");
        foreach (int id in members)
        {
            int person = ForeignTradeNeed(current.ResolveOfficerSource(id), "return_init_person");
            leadership = unchecked(leadership + ForeignTradeNeed(abilities.ResolveDimension(state, id, 0,
                (own, effect) => _baseCapacityInputs.ReadPanelModifier(state, own, effect)), "return_init_leadership"));
            if (ForeignTradeNeed(TacticRemakeCurrentTraits.CommanderHasEffect(current, person, 114), "return_init_trait114"))
                trait = Math.Max(trait, ForeignTradeNeed(TacticRemakeCurrentTraits.CommanderEffectSum(current, person, 114), "return_init_trait114_value"));
            ushort flags = ForeignTradeNeed(current.ReadArmyReturnPersonFlags196(person), "return_init_flags");
            current.WriteArmyReturnPersonFlags196(person, (ushort)(flags | 1));
            current.WritePersonSecondLocation(person, entryArea is >= 1 and <= 500 ? (ushort)(entryArea + 561) : (ushort)0);
            state = OfficerLocationSourceAdapter.PublishPosition(state, id, current);
        }
        int days = task.ReturnDisposition == 1 ? ReadForeignTradeCountry(state, task.CountryId).ReturnDays : ForeignTradeParameter(state, "return-short-days");
        days = unchecked(Math.Max(1, days - leadership / ForeignTradeParameter(state, "return-leadership-divisor")) - trait);
        if ((PersonReturnPathSource.ReadForceAdvantage(state, current, force, 5).Value ?? throw CaravanMissing("return_init_advantage5")) != 0)
            days -= ForeignTradeParameter(state, "return-state-advantage-reduction");
        if (ForeignTradeNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "return_init_settings") != 0)
        {
            if (ForeignTradeNeed(BattleRemakeCurrentSettings.ReadInt32(state, 0xFC), "return_init_difficulty") == 0)
                days -= ForeignTradeParameter(state, "return-low-difficulty-reduction");
        }
        else days = unchecked(days * ForeignTradeNeed(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 13), "return_init_advanced13")) / 100;
        var returning = new PersonnelCaravanTask(owner.NextTaskId, owner.AvailableDispatchSlots[0], members, 18,
            task.CountryId, forceId, district, home, start, task.EndHexId, oldArea, date,
            PersonReturnCommands.DateFromOrdinal(unchecked(PersonReturnCommands.CalendarOrdinal(date) + days)),
            task.OfferedItemId, task.GoldOffer, task.SuppliesOffer, task.TroopOffer, task.RewardStage, task.JourneyEvent,
            task.ReturnDisposition, false, false, false) { ReportToPlayer = task.ReportToPlayer };
        owner = owner with { NextTaskId = checked(owner.NextTaskId + 1), AvailableDispatchSlots = owner.AvailableDispatchSlots[1..],
            Caravans = new(owner.Caravans ?? throw CaravanMissing("return_init_owner")) { [returning.Id] = returning } };
        var handle = current.OwnedIndexes?.AssociatedTaskHandle ?? throw CaravanMissing("return_init_association");
        if (!current.AppendPersonReturnAssociated(new(handle.Identity), returning.Id)) throw CaravanMissing("return_init_registration");
        state = state with { PersonnelCommands = owner }; current.SetOwnedPersonCommandManager(owner);
        PersonReturnCommands.BindCurrent(state, current);
        return state;
    }

    public CommandResult CompleteForeignTradeReport(GameState state, ForceState force,
        CompleteForeignTradeReportOrder order, CommandContext context)
    {
        try
        {
            var task = state.PersonnelCommands?.Caravans?.GetValueOrDefault(order.TaskId);
            if (task is null || task.NativeKind != 18 || task.IsRetiring || task.PendingReport is null ||
                task.SourceForceId != force.Id || order.ForceId != force.Id)
                return CommandResult.Reject("The caravan report is no longer pending.", "foreign-trade.report_state");
            var world = CaptureOwnedArmyReturnWorld(state); var current = world.Context;
            _baseCapacityInputs.BindCurrent(state, current); _personAssignmentPrecallInputs.BindCurrent(state, current);
            var target = new DiplomacyCommandSource(state, current).Capital(force.Id);
            var arrived = ApplyPersonReturnArrivalCurrent(state, current, world.Pool, task.Id, target,
                new PersonReturnGovernorQueries(state, _governorInputs, current), false, state.NativeMapSeason?.ThirdDate);
            var working = arrived.State ?? throw CaravanMissing(arrived.UnresolvedOwner ?? "report_arrival");
            working = StoreForeignTradeTaskCurrent(working, current, task with { PendingReport = null });
            var owner = PersonReturnCommands.QueueRetirementCurrent(working.PersonnelCommands!, current, task.Id);
            working = PersonReturnCommands.PublishCurrent(working with { PersonnelCommands = owner }, current);
            working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
            working = OwnedArmyLifecycleDetachedInputs.PublishCurrent(working, current);
            return CommandResult.Accept(working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = world.Pool.ExportOwnedState() }));
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }
}
