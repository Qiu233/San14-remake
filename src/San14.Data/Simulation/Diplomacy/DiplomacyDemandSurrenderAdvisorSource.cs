using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2F39D0(kind7) deliberately evaluates3249B0 independently of the later
    //arrival trial. A page refresh formats text but never submits a command.
    internal DiplomacyAdvisorResult ReadDemandSurrenderAdvisorCurrent(GameState state,
        SourceWorldContext current, int forceId, int executorId, int recipientId)
        => ReadDemandSurrenderAdvisorCoreCurrent(state, current, forceId, executorId, recipientId, out _);

    //1F5570 runs the real formatter again after registration/resource writes;
    //preparation-page advice is not the command's persisted source operand.
    internal (GameState State, int Outcome) ReadDemandSurrenderAdvisorOutcomeAtDispatchCurrent(
        GameState state, SourceWorldContext current, PrepareDiplomaticOrder order)
    {
        var result = ReadDemandSurrenderAdvisorCoreCurrent(state, current, order.ForceId,
            order.ExecutorOfficerId, order.TargetForceId, out int outcome);
        //1F5570 repeats the actual accuracy query after formatting. The
        //formatter's first result does not own this dispatch-time decision.
        int accuracy = ReadDemandSurrenderAdvisorAccuracyCurrent(result.State, current,
            DiplomacyForce(current, order.ForceId));
        return (result.State, accuracy >= 100 ? outcome : -1);
    }

    private DiplomacyAdvisorResult ReadDemandSurrenderAdvisorCoreCurrent(GameState state,
        SourceWorldContext current, int forceId, int executorId, int recipientId, out int outcomeAtDispatch)
    {
        outcomeAtDispatch = -1;
        //The complete current CE_SC formatter and accuracy helper are
        //independently matched to the established CE_TC rule contract.
        if (Seed.Language is not ("TC" or "SC"))
            throw DiplomacyUiSource.Missing("surrender_advisor_locale_" + Seed.Language);
        var force = DiplomacyForce(current, forceId);
        int advisor = ReadDemandSurrenderAdvisorPersonCurrent(current, force);
        if (!DNeed(current.ReadPersonNativeValidity(advisor), "surrender_advisor_validity"))
            return new(state, null, null);
        int accuracy = ReadDemandSurrenderAdvisorAccuracyCurrent(state, current, force);
        var sampler = ReadArmyDestructionSampler(state, current) ?? throw DiplomacyMissing("surrender_advisor_sampler");
        bool accurate = TacticEffectSourceSamplerMath.Sample(100, 10, executorId, recipientId, 0, 0, sampler) < accuracy;
        var q = new DiplomacyQuery(this, state, current, executorId, recipientId, 7, 0, null);
        bool accepted = q.DemandSurrenderTrial().Accepted;
        //The formatter always writes its actual trial outcome. Dispatch
        //then repeats the accuracy query before retaining this signed Int32
        //in command storage5C through the real v128/v130 accessors.
        outcomeAtDispatch = accepted ? 1 : 0;
        bool positive = accepted == accurate;
        int advisorId = DNeed(current.ReadPersonIdentity(advisor), "surrender_advisor_ID");
        var text = FormatDiplomacyReportCurrent(state, current, positive ? 0x2D20 : 0x2D3F,
            new(Person0: advisorId, Person1: q.Id(q.Ruler(q.Own)), Person2: executorId,
                Person3: q.Id(q.Ruler(q.Other))));
        return new(text.State, text.Text, string.IsNullOrEmpty(text.Text) ? null : advisorId)
        { PositivePrediction = positive };

    }

    private int ReadDemandSurrenderAdvisorAccuracyCurrent(GameState state, SourceWorldContext current,
        ForceReference force)
    {
        if (!DNeed(current.ReadForceNativeValidity(force), "surrender_advisor_Force_validity")) return 0;
        int advisor = ReadDemandSurrenderAdvisorPersonCurrent(current, force);
        if (!DNeed(current.ReadPersonNativeValidity(advisor), "surrender_advisor_fresh_validity")) return 0;
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        int id = DNeed(current.ReadPersonIdentity(advisor), "surrender_advisor_fresh_ID");
        //26BCB0/CE_SC26B9B0 deliberately performs both full INT queries.
        int first = DNeed(abilities.ResolveDimension(state, id, 2,
            (f, effect) => _baseCapacityInputs.ReadPanelModifier(state, f, effect)), "surrender_advisor_first_INT");
        int second = DNeed(abilities.ResolveDimension(state, id, 2,
            (f, effect) => _baseCapacityInputs.ReadPanelModifier(state, f, effect)), "surrender_advisor_second_INT");
        return Math.Clamp(unchecked(60 + Math.Max(0, first - 70) + Math.Max(0, second - 90)), 0, 100);
    }

    private static int ReadDemandSurrenderAdvisorPersonCurrent(SourceWorldContext current, ForceReference force)
        => DiplomacyLookupPerson(current, DNeed(current.ReadAssignmentForceAdvisor52(
            DNeed(current.ReadForcePhysicalSlot(force), "surrender_advisor_Force_physical")), "surrender_advisor_Force52"));
}
