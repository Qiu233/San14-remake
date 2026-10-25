using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState AppendStrategicWaitCreationReportsCurrent(GameState state, SourceWorldContext current,
        PersonnelIndependenceTask task, PersonnelStrategicTask previous, NativeMapSeasonDate date)
        => AppendStrategicWaitSituationCurrent(state, current, task, date, 0x34AC, 0x34B1,
            () => TalentTravelSource.ResolvePosition(state, current, previous.StartHexId, previous.EndHexId,
                previous.SourceDistrictId, previous.StartDate, previous.DueDate, date));

    private GameState AppendStrategicWaitCancellationCurrent(GameState state, SourceWorldContext current,
        PersonnelIndependenceTask task, bool timeout, NativeMapSeasonDate date)
    {
        if (!task.ReportToPlayer ||
            (StrategyNeed(current.ReadWorldTransferServiceFlags(), "wait_cancel_World16A8") & 0x100) != 0) return state;
        if (!timeout) return AppendStrategicCancellationCurrent(state, current, StrategicWaitJourneyView(task));
        return AppendStrategicWaitSituationCurrent(state, current, task, date, 0x34B2, 0x30C7,
            () => TalentTravelSource.ResolvePosition(state, current, task.StartHexId, task.EndHexId,
                task.SourceDistrictId, task.StartDate, task.DueDate, date));
    }

    //1DDB40's wait announcement and1E7190's timeout share their source-time
    //speaker/City operands. The announcement and history format independently,
    //in the same order as their separate native calls.
    private GameState AppendStrategicWaitSituationCurrent(GameState state, SourceWorldContext current,
        PersonnelIndependenceTask task, NativeMapSeasonDate date, int introductionRoot, int historyRoot,
        Func<HexReference> readPosition)
    {
        int executorId = task.MemberIds[0];
        int executor = current.ResolveOfficerSource(executorId) ?? throw StrategyMissing("wait_report_executor");
        int rawDistrict = StrategyNeed(current.ReadPersonDistrictSlot(executor), "wait_report_DistrictE0");
        var district = current.ReadDistrictTableReference(rawDistrict < 52 ? rawDistrict : 0)
            ?? throw StrategyMissing("wait_report_District_pointer");
        var force = current.ReadPersonForce(executor) ?? throw StrategyMissing("wait_report_executor_Force");
        var city = current.ReadCityTableReference(task.TargetCityId < 52 ? task.TargetCityId : 0)
            ?? throw StrategyMissing("wait_report_City");
        ushort location = StrategyNeed(current.ReadBaseObjectLink(city), "wait_report_City_location");
        int ruler = TalentRulerCurrent(current, current.ReadForceTableReference(task.SourceForceId < 52 ? task.SourceForceId : 0)
            ?? throw StrategyMissing("wait_report_source_Force"));
        var introduction = new HistoryMessageOperands(Person0: executorId, Person1: ruler,
            Person2: task.CapturedGovernorId, Base0: location);
        var effects = new List<CampaignEffectRequest>();
        var working = state;
        if (StrategyNeed(current.ReadDistrictNativeValidity(district), "wait_report_District_validity") &&
            TalentDistrictIsPlayerRulerCurrent(working, current, district))
        {
            var line = FormatStrategicCommandReport(working, current, introductionRoot, introduction);
            working = line.State;
            effects.Add(CreateStrategicCommandPresentation(current, state, executorId, line));
        }
        if (StrategyNeed(current.ReadForceNativeValidity(force), "wait_report_Force_validity") &&
            TalentForceIsPlayerCurrent(working, current, force))
        {
            var operands = historyRoot == 0x34B1
                ? new HistoryMessageOperands(Person0: executorId, Person1: task.CapturedGovernorId, Base0: location)
                : new HistoryMessageOperands(Base0: location);
            var main = FormatStrategicCommandReport(working, current, historyRoot, operands);
            var extra = FormatStrategicCommandReport(main.State, current, introductionRoot, introduction);
            working = extra.State;
            int hex = StrategyNeed(readPosition().PhysicalSlot, "wait_report_current_Hex");
            int day = (ReadArmyDestructionSampler(working, current) ?? throw StrategyMissing("wait_report_day450")).Day450;
            working = WriteHistory(working, current, new(4,
                StrategyNeed(district.PhysicalSlot, "wait_report_District_identity"), hex, day,
                main.Text, extra.Text, executorId, MarkBit0: 0, MarkBit4: 1), effects);
        }
        return effects.Count == 0 ? working : working with { CampaignEffectRequests =
            (working.CampaignEffectRequests ?? throw StrategyMissing("wait_report_pending_owner")).Append(
                CampaignEffectSource.StrategicCommands,
                working.NativeMapSeason?.AdvancedCalendarSteps ?? throw StrategyMissing("wait_report_calendar"), effects) };
    }
}
