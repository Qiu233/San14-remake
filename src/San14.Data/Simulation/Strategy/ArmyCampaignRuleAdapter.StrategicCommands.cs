using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IStrategicCommandRules
{
    public CommandResult ApplyStrategicCommand(GameState state, ForceState force,
        IssueStrategicCommandOrder order, CommandContext context)
    {
        try
        {
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            var rules = CaptureStrategicCommandRules(state, current, force.Id, order.Kind);
            var executor = rules.Executors.FirstOrDefault(row => row.Id == order.ExecutorId && row.Enabled == true);
            var target = rules.Targets.FirstOrDefault(row => row.Id == order.TargetId && row.Enabled == true);
            if (executor is null || target is null)
                return CommandResult.Reject("The strategic executor or target is unavailable.", "strategic.selection");
            var choice = QueryStrategicCommandSelectionCurrent(rules, order.ExecutorId, order.TargetId,
                order.SecondForceId, order.MilitaryIntelligence, order.IndependenceChoice);
            if (choice.UnresolvedOwner is string missing) return CommandResult.RuleUnavailable(missing, missing);
            if (order.Kind == 10 && !choice.SecondForces.Any(row => row.Id == order.SecondForceId && row.Enabled == true) ||
                order.Kind != 10 && order.SecondForceId is not null ||
                order.Kind == 11 && order.IndependenceChoice is not (0 or 10 or 30) ||
                order.Kind != 11 && order.IndependenceChoice != 0)
                return CommandResult.Reject("The strategic command packet is invalid.", "strategic.packet");
            int goldCost = choice.GoldCost ?? throw StrategyMissing("command_gold_cost");
            if (rules.Gold < goldCost || rules.Books - CommandBookAvailability.Reserved(state, rules.DistrictId) < rules.BookCost ||
                order.MilitaryIntelligence < 0 || order.MilitaryIntelligence > rules.MilitaryIntelligence)
                return CommandResult.Reject("There are insufficient strategic resources.", "strategic.resources");
            if (state.PersonnelCommands?.Strategies is null) throw StrategyMissing("saved_task_owner");
            var sourceDate = state.NativeMapSeason?.ThirdDate ?? throw StrategyMissing("command_date");
            int physical = executor.PhysicalSlot;
            ushort sourceLocation = StrategyNeed(current.ReadPersonFirstLocation(physical), "command_executor_E2");
            var source = BaseLookup.Find(sourceLocation, current);
            if (!StrategyNeed(current.ReadBaseNativeValidity(source), "command_source_validity"))
                return CommandResult.Reject("The executor's home has changed.", "strategic.home");
            int sourceDistrict = StrategyNeed(current.ReadBaseDistrictSlot(source), "command_source_District");
            int sourceForce = StrategyNeed(current.ReadBaseForceSlot(source), "command_source_Force");
            var identity = new StrategicCommandTarget(order.Kind switch
            {
                10 => StrategicCommandTargetKind.Force, 11 => StrategicCommandTargetKind.City,
                12 or 24 => StrategicCommandTargetKind.Person, _ => StrategicCommandTargetKind.Area
            }, order.TargetId);
            int targetForce = StrategicTargetForceCurrent(current, identity, target.PhysicalSlot);
            int? governor = order.Kind == 11 ? StrategyNeed(current.ReadArmyReturnBaseGovernor2E(
                new(BaseKind.City, target.PhysicalSlot)), "command_target_governor") : null;
            int saved = -1;
            if (choice.ChancePercent is > 99)
            {
                int trial = ResolveStrategicCommandTrialCurrent(state, current, order.Kind, physical,
                    target.PhysicalSlot, order.SecondForceId, order.MilitaryIntelligence, order.IndependenceChoice);
                if (trial > (order.Kind == 11 ? 1 : 0)) saved = trial;
            }
            var travel = StrategicCommandTravelSource.Resolve(state, current, physical, identity,
                order.SecondForceId, sourceDate);
            var task = new PersonnelStrategicTask(0, 0, [order.ExecutorId], order.Kind, force.Id,
                sourceLocation, sourceDistrict, identity, order.SecondForceId, governor,
                targetForce, sourceForce, travel.StartHexId, travel.EndHexId, sourceDate,
                PersonReturnCommands.DateFromOrdinal(unchecked(PersonReturnCommands.CalendarOrdinal(sourceDate) + travel.TravelDays)),
                saved, order.MilitaryIntelligence, unchecked((byte)order.IndependenceChoice), false, false)
            { ReportToPlayer = TalentDistrictIsPlayerRulerCurrent(state, current,
                current.ReadDistrictTableReference(rules.DistrictId) ?? throw StrategyMissing("command_report_District")) };
            var working = PersonReturnCommands.RegisterStrategicCurrent(state, current, task, physical, sourceDate);
            working = SpendStrategicCommandBooksCurrent(working, current, sourceDistrict, rules.BookCost);
            if (ReadApplicationInformationRestriction(working) is not false)
                throw StrategyMissing("command_application_book_service");
            int payerDistrict = StrategyNeed(current.ReadPersonDistrictSlot(physical), "command_executor_E0");
            var treasury = ReadPersonnelHireTreasuryCurrent(current, payerDistrict);
            working = BaseCurrencyAdditionSource.Add(working, current, treasury, -goldCost,
                city => BasePostLoad.ReadResourceCapacities(city, current).Gold).State;
            if (order.MilitaryIntelligence > 0)
                working = SpendDiplomacyDistrictCurrent(working, current,
                    current.ReadDistrictTableReference(sourceDistrict) ?? throw StrategyMissing("command_military_District"),
                    order.MilitaryIntelligence);
            if (StrategyNeed(current.ReadArmyReturnBaseGovernor2E(source), "command_source_governor") ==
                StrategyNeed(current.ReadPersonIdentity(physical), "command_executor_identity"))
                _ = ApplyPersonFreedomGovernorCurrent(working, current, source);
            working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
            working = PersonReturnCommands.PublishCurrent(working, current);
            working = AppendStrategicPreparationCurrent(working, current, order, task, state);
            return CommandResult.Accept(working.WithArmyLifecycle(working.ReadArmyLifecycle()! with
                { Indexes = world.Pool.ExportOwnedState() }));
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    private static int StrategicTargetForceCurrent(SourceWorldContext current,
        StrategicCommandTarget target, int physical)
    {
        if (target.Kind == StrategicCommandTargetKind.Force)
            return StrategyNeed(current.ReadForcePhysicalSlot(new(physical)), "target_Force_identity");
        if (target.Kind == StrategicCommandTargetKind.Person)
            return StrategyNeed(current.ReadForcePhysicalSlot(current.ReadPersonForce(physical)
                ?? throw StrategyMissing("target_Person_Force")), "target_Person_Force_identity");
        if (target.Kind == StrategicCommandTargetKind.City)
            return StrategyNeed(current.ReadBaseForceSlot(new(BaseKind.City, physical)), "target_City_Force");
        return StrategyNeed(current.ReadAreaCurrentForceSlot(new(physical)), "target_Area_Force");
    }

    internal static HexReference ResolveStrategicTaskPositionCurrent(GameState state,
        SourceWorldContext current, PersonnelStrategicTask task)
        => TalentTravelSource.ResolvePosition(state, current, task.StartHexId, task.EndHexId,
            task.SourceDistrictId, task.StartDate, task.DueDate,
            state.NativeMapSeason?.ThirdDate ?? throw StrategyMissing("position_date"));

    private static GameState SpendStrategicCommandBooksCurrent(GameState state,
        SourceWorldContext current, int districtId, int amount)
    {
        var district = state.Districts.GetValueOrDefault(districtId) ?? throw StrategyMissing("payment_District");
        int before = district.OrdersRemaining ?? throw StrategyMissing("payment_District14");
        int after = Math.Clamp(unchecked(before - amount), 0, 200);
        var saved = state.NativeForceUnregisterCurrent ?? throw StrategyMissing("payment_District_window_owner");
        var window = saved.DistrictWindows10.GetValueOrDefault(districtId)?.ToArray()
            ?? throw StrategyMissing("payment_District_window");
        if (window.Length < 5) throw StrategyMissing("payment_District_window_length");
        window[4] = (byte)after; current.SetDistrictRawByte(districtId, 0x14, (byte)after);
        return state with { Districts = new(state.Districts) { [districtId] = district with { OrdersRemaining = after } },
            NativeForceUnregisterCurrent = saved with
                { DistrictWindows10 = new(saved.DistrictWindows10) { [districtId] = window } } };
    }

    private GameState AppendStrategicPreparationCurrent(GameState state, SourceWorldContext current,
        IssueStrategicCommandOrder order, PersonnelStrategicTask task, GameState sourceState)
    {
        int ruler = TalentRulerCurrent(current, current.ReadForceTableReference(order.ForceId)
            ?? throw StrategyMissing("report_own_Force"));
        int root; var operands = new HistoryMessageOperands(Person0: order.ExecutorId, Person1: ruler);
        int OtherRuler(int force) => TalentRulerCurrent(current,
            current.ReadForceTableReference(force <= 51 ? force : 0) ?? throw StrategyMissing("report_target_Force"));
        switch (order.Kind)
        {
            case 10:
                root = 0x5A40; operands = operands with { Person2 = OtherRuler(order.TargetId),
                    Person3 = OtherRuler(order.SecondForceId ?? throw StrategyMissing("report_second_Force")) }; break;
            case 11:
                root = 0x5A5B; operands = operands with { Person2 = OtherRuler(task.TargetOriginalForceId), Person3 = task.TargetGovernorId }; break;
            case 12:
                root = 0x5A34; operands = operands with { Person2 = order.TargetId }; break;
            case 14:
                root = 0x5A4A; operands = operands with { Person2 = OtherRuler(task.TargetOriginalForceId), Area0 = order.TargetId }; break;
            case 13:
                bool valid = StrategyNeed(current.ReadForceNativeValidity(current.ReadForceTableReference(
                    task.TargetOriginalForceId <= 51 ? task.TargetOriginalForceId : 0)
                    ?? throw StrategyMissing("report_poison_Force")), "report_poison_Force_validity");
                root = valid ? 0x5A55 : 0x5A50;
                operands = operands with { Person2 = valid ? OtherRuler(task.TargetOriginalForceId) : null, Area0 = order.TargetId }; break;
            case 20:
                root = 0x5C05; operands = operands with { Person2 = OtherRuler(task.TargetOriginalForceId), Area0 = order.TargetId }; break;
            case 24:
                int target = current.ResolveOfficerSource(order.TargetId) ?? throw StrategyMissing("report_rumor_target");
                root = 0x5CFD; operands = operands with { Person2 = order.TargetId,
                    Person3 = TalentRulerCurrent(current, current.ReadPersonForce(target) ?? throw StrategyMissing("report_rumor_Force")) }; break;
            default: throw StrategyMissing("report_kind");
        }
        var text = FormatStrategicCommandReport(state, current, root, operands);
        var request = CreateStrategicCommandPresentation(current, sourceState, order.ExecutorId, text);
        return text.State with { CampaignEffectRequests = (text.State.CampaignEffectRequests
            ?? throw StrategyMissing("report_pending_owner")).Append(CampaignEffectSource.StrategicCommands,
                text.State.NativeMapSeason?.AdvancedCalendarSteps ?? throw StrategyMissing("report_calendar"), [request]) };
    }
}
