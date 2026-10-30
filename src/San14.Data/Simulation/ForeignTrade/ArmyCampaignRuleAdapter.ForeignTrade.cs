using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IForeignTradeCommandRules
{
    public CommandResult DispatchForeignTrade(GameState state, ForceState force,
        DispatchForeignTradeOrder order, CommandContext context)
    {
        if (order.ForceId != force.Id || order.CountryId is < 1 or > 4 || order.ExecutorOfficerIds is null ||
            order.ExecutorOfficerIds.Length is < 1 or > 5 ||
            order.ExecutorOfficerIds.Distinct().Count() != order.ExecutorOfficerIds.Length ||
            order.Gold < 0 || order.Supplies < 0 || order.Troops < 0 || order.ItemId is < 1 or > 201 ||
            order.Gold == 0 && order.Supplies == 0 && order.Troops == 0 && order.ItemId is null)
            return CommandResult.Reject("The caravan selection or cargo is invalid.", "foreign-trade.packet");
        try
        {
            var world = CaptureOwnedArmyReturnWorld(state); var current = world.Context;
            _baseCapacityInputs.BindCurrent(state, current);
            _personAssignmentPrecallInputs.BindCurrent(state, current);
            var forceRef = current.ReadForceTableReference(force.Id) ?? throw ForeignTradeCommandMissing("dispatch_Force");
            var districtRef = ForcePrimaryDistrictSource.Resolve(current, forceRef);
            int primaryDistrict = ForeignTradeNeed(districtRef.PhysicalSlot, "dispatch_primary_District");
            if (!NormalTurnForeignTradeAvailable(state, current, forceRef, districtRef, primaryDistrict))
                return CommandResult.Reject("Foreign trade is unavailable to this force.", "foreign-trade.admission");
            var candidateRows = ReadForeignTradeExecutorsCurrent(state, current, force.Id, primaryDistrict);
            if (order.ExecutorOfficerIds.Any(id => !candidateRows.Any(row => row.Id == id)))
                return CommandResult.Reject("A caravan executor is unavailable.", "foreign-trade.executor");
            var owner = state.PersonnelCommands ?? throw ForeignTradeCommandMissing("dispatch_command_owner");
            var tasks = owner.Caravans ?? throw ForeignTradeCommandMissing("dispatch_caravan_owner");
            if (tasks.Values.Any(task => task.SourceForceId == force.Id && task.CountryId == order.CountryId && !task.IsRetiring))
                return CommandResult.Reject("A caravan is already travelling to this country.", "foreign-trade.pending_country");
            if (!PersonReturnPathSource.ReadForeignTradeEligibility(state, current, forceRef, order.CountryId, ForeignTradeApplication()))
                return CommandResult.Reject("The destination is unavailable.", "foreign-trade.country");
            if (owner.AvailableDispatchSlots.Length == 0) throw ForeignTradeCommandMissing("dispatch_slot_capacity");
            int firstId = order.ExecutorOfficerIds[0];
            int first = candidateRows.First(row => row.Id == firstId).Physical;
            ushort firstHome = ForeignTradeNeed(current.ReadPersonFirstLocation(first), "dispatch_first_home");
            var oldBase = BaseLookup.Find(firstHome, current);
            int sourceDistrict = ForeignTradeNeed(current.ReadBaseDistrictSlot(oldBase), "dispatch_source_District");
            var capital = new DiplomacyCommandSource(state, current).Capital(force.Id);
            var treasury = BaseCurrencyAdditionSource.CurrencyCity(capital, current);
            int capitalId = ForeignTradeNeed(current.ReadBaseObjectLink(treasury), "dispatch_capital_City_location");
            int gold = ForeignTradeNeed(current.ReadBaseResource(treasury, BaseResourceKind.Gold), "dispatch_Gold");
            int supplies = ForeignTradeNeed(current.ReadBaseResource(treasury, BaseResourceKind.Supplies), "dispatch_Food");
            int troops = ForeignTradeNeed(current.ReadBaseResource(capital, BaseResourceKind.Troops), "dispatch_Troops");
            int bookCost = order.ExecutorOfficerIds.Length;
            int books = state.Districts.GetValueOrDefault(sourceDistrict)?.OrdersRemaining ?? throw ForeignTradeCommandMissing("dispatch_books");
            if (books - CommandBookAvailability.Reserved(state, sourceDistrict) < bookCost ||
                gold < order.Gold || supplies < order.Supplies || troops < order.Troops)
                return CommandResult.Reject("There are insufficient caravan resources.", "foreign-trade.resources");
            if (order.ItemId is int itemId && !ReadForeignTradeItemsCurrent(state, current, force.Id).Any(row => row.ItemId == itemId))
                return CommandResult.Reject("The offered item is unavailable.", "foreign-trade.item");
            foreach (int id in order.ExecutorOfficerIds)
            {
                int physical = candidateRows.First(row => row.Id == id).Physical;
                var associated = current.ReadPersonLinkedEntity(physical) ?? throw ForeignTradeCommandMissing("dispatch_associated");
                if (ForeignTradeNeed(current.ReadLinkedEntityNativeValidity(associated), "dispatch_associated_validity"))
                    return CommandResult.Reject("A caravan executor has another command.", "foreign-trade.executor_task");
            }
            var date = state.NativeMapSeason?.ThirdDate ?? throw ForeignTradeCommandMissing("dispatch_date");
            var country = ReadForeignTradeCountry(order.CountryId);
            var hostCity = current.ReadCityTableReference(country.HostCityId < 52 ? country.HostCityId : 0)
                ?? throw ForeignTradeCommandMissing("dispatch_foreign_port");
            int duration = 0;
            var baseDistances = new Dictionary<(int, int), int>();
            foreach (int id in order.ExecutorOfficerIds)
                duration = Math.Max(duration, ReadForeignTradeTravelDaysCurrent(state, current,
                    candidateRows.First(row => row.Id == id).Physical, order.CountryId, baseDistances));
            var capitalHex = current.ReadHexTableReference(current.ReadBaseCurrentHexIndex(capital))
                ?? throw ForeignTradeCommandMissing("dispatch_capital_Hex");
            int capitalArea = ForeignTradeNeed(current.ReadHexAreaSlot(capitalHex), "dispatch_capital_Area");
            int startHex = ForeignTradeNeed(current.ReadAreaHexIndex(new(capitalArea <= 500 ? capitalArea : 0)), "dispatch_capital_representative_Hex");
            var task = new PersonnelCaravanTask(owner.NextTaskId, owner.AvailableDispatchSlots[0], [], 17,
                order.CountryId, force.Id, sourceDistrict, firstHome, startHex,
                current.ReadBaseCurrentHexIndex(hostCity), capitalId, date,
                PersonReturnCommands.DateFromOrdinal(unchecked(PersonReturnCommands.CalendarOrdinal(date) + duration)),
                order.ItemId ?? 0, order.Gold, order.Supplies, order.Troops, 0, 0, 0, false, false, false)
            { ReportToPlayer = TalentDistrictIsPlayerRulerCurrent(state, current, districtRef) };
            owner = owner with { NextTaskId = checked(owner.NextTaskId + 1), AvailableDispatchSlots = owner.AvailableDispatchSlots[1..],
                Caravans = new(tasks) { [task.Id] = task } };
            var handle = current.OwnedIndexes?.AssociatedTaskHandle ?? throw ForeignTradeCommandMissing("dispatch_association_index");
            if (!current.AppendPersonReturnAssociated(new(handle.Identity), task.Id)) throw ForeignTradeCommandMissing("dispatch_registration");
            current.SetOwnedPersonCommandManager(owner);
            current.SetLinkedEntityRawState58(task.Id, task.DispatchSlot); current.SetLinkedEntityMemberIds(task.Id, []);
            current.SetAssociatedPayloadPostLoadCallback(task.Id, PostLoadCallback.NoOp);
            var working = state with { PersonnelCommands = owner };
            var members = new List<int>();
            foreach (int id in order.ExecutorOfficerIds)
            {
                int physical = candidateRows.First(row => row.Id == id).Physical;
                //1F4630 moves each actual member to the capital using271320,
                //then marks that person busy at the foreign port's Area.
                var moved = ArmyReturnCommanderHomeUpdater.ApplyPersonCurrent(physical, capital, current, world.Pool,
                    new PersonReturnGovernorQueries(working, _governorInputs, current), assignment =>
                    {
                        if (assignment.NativeReturn is null) throw ForeignTradeCommandMissing(assignment.UnresolvedOwner ?? "dispatch_assignment");
                        var effects = new List<CampaignEffectRequest>();
                        working = ApplyPersonAssociationRemovalReports(working, current, physical, assignment,
                            () => ReadArmyDestructionSampler(working, current)?.Day450 ?? throw ForeignTradeCommandMissing("dispatch_assignment_day"), effects);
                        if (effects.Count != 0) working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                            ?? throw ForeignTradeCommandMissing("dispatch_effects")).Append(CampaignEffectSource.ForceAssociationRemoval,
                            working.NativeMapSeason!.AdvancedCalendarSteps, effects) };
                    });
                var movedPacket = moved.Packet ?? throw ForeignTradeCommandMissing(moved.UnresolvedOwner ?? "dispatch_move_capital");
                if (movedPacket.AssignmentPrecall is not null) working = PersonAssignmentPrecallInputs.PublishCurrent(working, current);
                working = ArmyReturnCommanderHomeUpdater.PublishPersonCurrent(working, current, movedPacket);
                ushort flags = ForeignTradeNeed(current.ReadArmyReturnPersonFlags196(physical), "dispatch_person_flags");
                current.WriteArmyReturnPersonFlags196(physical, (ushort)(flags | 1));
                var portHex = current.ReadHexTableReference(current.ReadBaseCurrentHexIndex(hostCity))
                    ?? throw ForeignTradeCommandMissing("dispatch_port_Hex");
                int area = ForeignTradeNeed(current.ReadHexAreaSlot(portHex), "dispatch_port_Area");
                current.WritePersonSecondLocation(physical, area is >= 1 and <= 500 ? (ushort)(area + 561) : (ushort)0);
                working = OfficerLocationSourceAdapter.PublishPosition(working, id, current);
                members.Add(id); task = task with { MemberIds = members.ToArray() };
                owner = current.ReadOwnedPersonCommandManager() ?? throw ForeignTradeCommandMissing("dispatch_latest_manager");
                owner = owner with { Caravans = new(owner.Caravans!) { [task.Id] = task } };
                current.SetOwnedPersonCommandManager(owner);
                current.SetLinkedEntityMemberIds(task.Id, members.Select(value => (uint?)value).ToArray());
                working = working with { PersonnelCommands = owner };
            }
            working = SpendStrategicCommandBooksCurrent(working, current, sourceDistrict, bookCost);
            working = SpendForeignTradeCargoCurrent(working, current, capital, order);
            if (order.ItemId is int offered) working = ReserveDiplomacyItemCurrent(working, current, offered, firstId);
            //2F39D0's real sample/advice point precedes the independent trip event.
            var advice = PrepareForeignTradeDispatchAdviceCurrent(working, current, force.Id, order);
            working = advice.State;
            var sampler = ReadArmyDestructionSampler(working, current) ?? throw ForeignTradeCommandMissing("dispatch_sampler");
            int roll = TacticEffectSourceSamplerMath.Sample(100, 10, PersonReturnCommands.CalendarOrdinal(date), firstId, 0, 0, sampler);
            byte journeyEvent = roll <= 10 ? (byte)2 : roll <= 40 ? (byte)3 : roll <= 60 ? (byte)4 : roll <= 70 ? (byte)5 : (byte)0;
            task = task with { SavedResult = advice.Sample, JourneyEvent = journeyEvent };
            owner = current.ReadOwnedPersonCommandManager() ?? throw ForeignTradeCommandMissing("dispatch_final_manager");
            owner = owner with { Caravans = new(owner.Caravans!) { [task.Id] = task } };
            current.SetOwnedPersonCommandManager(owner); working = working with { PersonnelCommands = owner };
            working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
            working = OwnedArmyLifecycleDetachedInputs.PublishCurrent(working, current);
            working = PersonReturnCommands.PublishCurrent(working, current);
            var departure = FormatForeignTradeMessage(working, current, 0x5BEE,
                new(Person0: firstId, Person1: TalentRulerCurrent(current, forceRef), String0: country.Name));
            var request = CreateStrategicCommandPresentation(current, state, firstId, departure, conditional: true);
            working = departure.State with { CampaignEffectRequests = (departure.State.CampaignEffectRequests
                ?? throw ForeignTradeCommandMissing("dispatch_presentation_owner")).Append(CampaignEffectSource.ForeignTradeCommands,
                departure.State.NativeMapSeason!.AdvancedCalendarSteps, [request]) };
            return CommandResult.Accept(working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = world.Pool.ExportOwnedState() }));
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    private static GameState SpendForeignTradeCargoCurrent(GameState state, SourceWorldContext current, BaseReference capital,
        DispatchForeignTradeOrder order)
    {
        var city = BaseCurrencyAdditionSource.CurrencyCity(capital, current);
        int cityId = ForeignTradeNeed(city.PhysicalSlot, "cargo_City");
        var owner = state.Cities.GetValueOrDefault(cityId) ?? throw ForeignTradeCommandMissing("cargo_City_owner");
        var capacities = BasePostLoad.ReadResourceCapacities(city, current);
        int gold = Math.Clamp(unchecked(ForeignTradeNeed(current.ReadBaseResource(city, BaseResourceKind.Gold), "cargo_Gold") - order.Gold), 0, capacities.Gold);
        int supplies = Math.Clamp(unchecked(ForeignTradeNeed(current.ReadBaseResource(city, BaseResourceKind.Supplies), "cargo_Food") - order.Supplies), 0, capacities.Supplies);
        int troops = Math.Clamp(unchecked(ForeignTradeNeed(current.ReadBaseResource(capital, BaseResourceKind.Troops), "cargo_Troops") - order.Troops), 0, BasePostLoad.ReadResourceCapacities(capital, current).Troops);
        current.SetBaseResource(city, BaseResourceKind.Gold, gold); current.SetBaseResource(city, BaseResourceKind.Supplies, supplies);
        current.SetBaseResource(capital, BaseResourceKind.Troops, troops);
        var paid = state with { Cities = new(state.Cities) { [cityId] = owner with
            { Gold = gold, Supplies = supplies, Troops = capital.Kind == BaseKind.City ? troops : owner.Troops } } };
        if (capital.Kind == BaseKind.Gate)
        {
            int gateId = ForeignTradeNeed(capital.PhysicalSlot, "cargo_Gate");
            var gate = paid.Gates.GetValueOrDefault(gateId) ?? throw ForeignTradeCommandMissing("cargo_Gate_owner");
            paid = paid with { Gates = new(paid.Gates) { [gateId] = gate with { Troops = troops } } };
        }
        return paid;
    }
}
