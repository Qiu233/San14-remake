using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //1E5E30 is reached only by the explicit report-cancellation branch of
    //1E9F50; invalid-member/changed-home cancellation remains silent.
    private GameState AppendStrategicCancellationCurrent(GameState state,
        SourceWorldContext current, PersonnelStrategicTask task)
    {
        if (!task.ReportToPlayer ||
            (StrategyNeed(current.ReadWorldTransferServiceFlags(), "cancel_World16A8") & 0x100) != 0) return state;
        if (task.MemberIds.Length == 0) return state;
        int executor = task.MemberIds[0];
        var source = current.ReadForceTableReference(task.SourceForceId <= 51 ? task.SourceForceId : 0)
            ?? throw StrategyMissing("cancel_source_Force");
        var main = new HistoryMessageOperands(Person0: executor, Person1: TalentRulerCurrent(current, source));
        HistoryMessageOperands attachment;
        int mainRoot, attachmentRoot;
        switch (task.Kind)
        {
            case 10:
                var target = current.ReadForceTableReference(task.Target.Id <= 51 ? task.Target.Id : 0)
                    ?? throw StrategyMissing("cancel_target_Force");
                int targetRuler = TalentRulerCurrent(current, target);
                int targetId = StrategyNeed(current.ReadForcePhysicalSlot(target), "cancel_target_Force_id");
                main = main with { Person2 = targetRuler, Force0 = targetId };
                attachment = new(Person0: targetRuler, Force0: targetId);
                mainRoot = 0x30A4; attachmentRoot = 0x30C6; break;
            case 11:
                var city = current.ReadCityTableReference(task.Target.Id <= 51 ? task.Target.Id : 0)
                    ?? throw StrategyMissing("cancel_target_City");
                ushort location = StrategyNeed(current.ReadBaseObjectLink(city), "cancel_City_location");
                main = main with { Base0 = location }; attachment = new(Base0: location);
                mainRoot = 0x30AD; attachmentRoot = 0x30C7; break;
            case 12:
                main = main with { Person2 = task.Target.Id }; attachment = new(Person0: task.Target.Id);
                mainRoot = 0x30B2; attachmentRoot = 0x30C8; break;
            case 24:
                int person = current.ResolveOfficerSource(task.Target.Id) ?? throw StrategyMissing("cancel_rumor_Person");
                main = main with { Person2 = task.Target.Id, Person3 = TalentRulerCurrent(current,
                    current.ReadPersonForce(person) ?? throw StrategyMissing("cancel_rumor_Force")) };
                attachment = new(Person0: executor, Person1: task.Target.Id);
                mainRoot = 0x345C; attachmentRoot = 0x3467; break;
            case 13 or 14 or 20:
                main = main with { Area0 = task.Target.Id }; attachment = new(Area0: task.Target.Id);
                (mainRoot, attachmentRoot) = task.Kind switch
                { 13 => (0x30B7, 0x30C9), 14 => (0x30BC, 0x30CA), _ => (0x30C1, 0x30CB) }; break;
            default: throw StrategyMissing("cancel_kind");
        }
        var text = FormatStrategicCommandReport(state, current, mainRoot, main);
        var supplemental = FormatStrategicCommandReport(text.State, current, attachmentRoot, attachment);
        var effects = new List<CampaignEffectRequest>();
        int day = (ReadArmyDestructionSampler(supplemental.State, current)
            ?? throw StrategyMissing("cancel_day450")).Day450;
        var working = WriteHistory(supplemental.State, current, new(4, task.SourceDistrictId, 0, day,
            text.Text, supplemental.Text, executor, MarkBit0: 1, MarkBit4: 0), effects);
        effects.Add(CreateStrategicCommandPresentation(current, state, executor, text));
        return working with { CampaignEffectRequests = (working.CampaignEffectRequests
            ?? throw StrategyMissing("cancel_pending_owner")).Append(CampaignEffectSource.StrategicCommands,
                working.NativeMapSeason?.AdvancedCalendarSteps ?? throw StrategyMissing("cancel_calendar"), effects) };
    }
}
