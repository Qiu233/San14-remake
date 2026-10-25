using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyNormalTurnIndependenceReportCurrent(GameState state, SourceWorldContext current,
        int originalForce, int rebel, int root, int person, List<CampaignEffectRequest> effects)
        => ApplyForceIndependenceReportCurrent(state,current,originalForce,rebel,root,person,effects,normalTurn:true);

    private GameState ApplyForceIndependenceReportCurrent(GameState state, SourceWorldContext current,
        int originalForce, int rebel, int root, int person, List<CampaignEffectRequest> effects, bool normalTurn)
    {
        int home = StrategyNeed(current.ReadPersonFirstLocation(rebel), "independence_report_Home");
        int Id(int value) => StrategyNeed(current.ReadPersonIdentity(value), "independence_report_identity");
        ForceReference Force(int value) => current.ReadPersonForce(value) ?? throw StrategyMissing("independence_report_Force");
        int Ordinal(ForceReference force) => StrategyNeed(current.ReadForcePhysicalSlot(force), "independence_report_ordinal");
        int Person(int id) => StrategyNeed((current.ReadPersonLookupReference(id <= 6000 ? id : 0))?.PhysicalSlot, "independence_report_lookup");
        int Ruler(ForceReference force) => Person(StrategyNeed(current.ReadForceRecord(Ordinal(force))?.RawRuler10, "independence_report_ruler"));
        HistoryTextResult Format(int value, HistoryMessageOperands args) => normalTurn
            ? FormatNormalTurnReport(state,current,value,args) : FormatStrategicCommandReportCurrent(state,current,value,args);
        HistoryMessageOperands operands = root switch
        {
            0x5E43 => new(Person0:Id(rebel),Force0:originalForce),
            0x5E49 or 0x5E4A => new(Person0:Id(rebel),Force0:originalForce,Base0:(ushort)home),
            0x5E4B or 0x5E59 => new(Person0:Id(person),Person1:Id(rebel)),
            0x5E58 => new(Person0:Id(person),Person1:Id(rebel),Force0:Ordinal(Force(person))),
            0x5E6F => new(Person0:Id(person),Person1:Id(Ruler(Force(person))),Person2:Id(rebel)),
            _ => throw StrategyMissing("independence_report_root")
        };
        if (root == 0x5E4B)
            effects.Add(new(null,null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue,6,null,null,null,null) });
        var source = state; var formatted = Format(root,operands); state = formatted.State;
        if (root == 0x5E49)
            effects.Add(new(null,null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.Announcement,null,null,null,null,formatted.Text) });
        else if (root is not (0x5E4A or 0x5E58))
            effects.Add(CreateStrategicCommandPresentation(current,source,Id(person),formatted));
        if (root is not (0x5E4A or 0x5E58)) return state;

        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state,current) ?? throw StrategyMissing("independence_history_player");
        int ruler = Ruler(player);
        if (!StrategyNeed(current.ReadPersonNativeValidity(ruler), "independence_history_ruler_validity")) return state;
        int speaker = 5012;
        if (root == 0x5E4A)
        {
            int advisor = Person(StrategyNeed(current.ReadAssignmentForceAdvisor52(Ordinal(player)), "independence_history_advisor"));
            if (StrategyNeed(current.ReadPersonNativeValidity(advisor), "independence_history_advisor_validity")) speaker = Id(advisor);
        }
        var detailOperands = root == 0x5E4A
            ? new HistoryMessageOperands(Person0:speaker,Person1:Id(ruler),Person2:Id(rebel),Base0:(ushort)home,Force0:originalForce)
            : new HistoryMessageOperands(Person0:5012,Person1:Id(ruler),Person2:Id(person),Person3:Id(rebel),Force0:Ordinal(Force(person)));
        var detail = Format(root == 0x5E4A ? 0x525B : 0x526C, detailOperands); state = detail.State;
        int day = (ReadArmyDestructionSampler(state,current) ?? throw StrategyMissing("independence_history_Day450")).Day450;
        var date = state.NativeMapSeason?.ThirdDate ?? throw StrategyMissing("independence_history_date");
        int hex = StrategyNeed(PersonReturnCommands.ResolvePersonPosition(state,current,rebel,date).PhysicalSlot, "independence_history_Hex");
        if (current.ReadOwnedPersonCommandManager() is { } commands) state = state with { PersonnelCommands = commands };
        var original = current.ReadForceTableReference(originalForce) ?? throw StrategyMissing("independence_history_original");
        int district = StrategyNeed(ForcePrimaryDistrictSource.Resolve(current,original).PhysicalSlot, "independence_history_primary");
        return WriteHistory(state,current,new(1,district,hex,day,formatted.Text,detail.Text,
            root == 0x5E4A ? speaker : 5012,MarkBit0:1,MarkBit4:0),effects);
    }
}
