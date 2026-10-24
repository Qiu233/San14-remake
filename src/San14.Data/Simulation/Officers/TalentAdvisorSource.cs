using San14.Core;

namespace San14.Data;

internal sealed record TalentAdvisorSampleResult(GameState State, int Sample)
{
    internal bool PositiveAdvice { get; init; }
    internal string? AdviceText { get; init; }
    internal int? AdvisorOfficerId { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    //2F39D0: initial advice is a real query plus196CC0 formatting. Its
    //Function3 choices update the owned App32 state even if the text is unused.
    internal TalentAdvisorSampleResult ResolveTalentAdvisorSampleCurrent(GameState state,
        SourceWorldContext current, int executorPhysical, int? targetPhysical,
        TalentOrderKind kind, Func<TacticSourceSamplerWorld> readSampler)
    {
        if (Seed.Language != "TC") throw TalentCommandMissing("advisor_SC_owner");
        var force = current.ReadPersonForce(executorPhysical) ?? throw TalentCommandMissing("advisor_executor_Force");
        var advisor = Advisor(force);
        if (!Valid(advisor)) return new(state, -1);
        //26BCB0 has its own Force validity and independent repeated advisor
        //lookup/current-INT reads. These are not the recruit Charm query.
        int accuracy = 0;
        if (TalentNeed(current.ReadForceNativeValidity(force), "advisor_Force_validity"))
        {
            var fresh = Advisor(force);
            if (Valid(fresh))
            {
                var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
                int id = TalentNeed(current.ReadPersonIdentity(TalentNeed(fresh.PhysicalSlot, "advisor_pointer")), "advisor_ID10");
                int first = TalentNeed(abilities.ResolveDimension(state, id, 2,
                    (f, effect) => _baseCapacityInputs.ReadPanelModifier(state, f, effect)), "advisor_first_INT");
                int second = TalentNeed(abilities.ResolveDimension(state, id, 2,
                    (f, effect) => _baseCapacityInputs.ReadPanelModifier(state, f, effect)), "advisor_second_INT");
                accuracy = Math.Clamp(unchecked(60 + Math.Max(0, first - 70) + Math.Max(0, second - 90)), 0, 100);
            }
        }
        int executorId = TalentNeed(current.ReadPersonIdentity(executorPhysical), "advisor_executor_ID10");
        int target = targetPhysical ?? throw TalentCommandMissing("advisor_target");
        int secondSalt;
        if (kind == TalentOrderKind.Recruit) secondSalt = TalentNeed(current.ReadPersonIdentity(target), "advisor_target_ID10");
        else if (kind == TalentOrderKind.Search) secondSalt = target;
        else throw TalentCommandMissing("advisor_kind");
        int correctness = TacticEffectSourceSamplerMath.Sample(100, 10, executorId, secondSalt, 0, 0, readSampler());
        bool accurate = correctness < accuracy;
        int sample = -1, root;
        HistoryMessageOperands operands;
        int advisorId = TalentNeed(current.ReadPersonIdentity(TalentNeed(advisor.PhysicalSlot, "retained_advisor_pointer")), "retained_advisor_ID10");
        if (kind == TalentOrderKind.Recruit)
        {
            var trial = TryRecruitCurrent(state, current, target, executorPhysical, -1, readSampler);
            sample = trial.SampleAfter;
            root = trial.Accepted == accurate ? 0x2C82 : 0x2CA3;
            operands = new(Person0: advisorId, Person1: TalentRulerCurrent(current,
                current.ReadPersonForce(executorPhysical) ?? throw TalentCommandMissing("advisor_fresh_Force")),
                Person2: executorId, Person3: secondSalt);
        }
        else
        {
            var area = current.ReadAreaTableReference(target <= 500 ? target : 0) ?? throw TalentCommandMissing("advisor_Area_pointer");
            var people = TrySearchCurrent(state, current, executorPhysical, area, -1, readSampler);
            if (people.Receipt is not { } personResult) throw TalentCommandMissing(people.UnresolvedOwner ?? "advisor_Person_trial");
            bool found = personResult.FoundPersonPhysical is int foundPhysical &&
                TalentNeed(current.ReadPersonNativeValidity(foundPhysical), "advisor_found_Person_validity");
            if (found) sample = personResult.UsedSample;
            else
            {
                var item = TrySearchItemCurrent(state, current, executorPhysical, area, -1, readSampler);
                if (item.UnresolvedOwner is not null) throw TalentCommandMissing(item.UnresolvedOwner);
                if (item.Receipt is not null) { found = true; sample = item.SampleAfter; }
                else
                {
                    var gold = TrySearchGoldCurrent(state, current, executorPhysical, area, -1, readSampler);
                    if (gold.UnresolvedOwner is not null) throw TalentCommandMissing(gold.UnresolvedOwner);
                    found = gold.Receipt?.Kind == TalentSearchFallbackKind.Gold;
                    if (found) sample = gold.SampleAfter;
                }
            }
            root = found == accurate ? 0x2CB3 : 0x2CC3;
            operands = new(Person0: advisorId, Person1: TalentRulerCurrent(current,
                current.ReadPersonForce(executorPhysical) ?? throw TalentCommandMissing("advisor_fresh_Force")), Person2: executorId);
        }
        var formatted = FormatTalentReportCurrent(state, current, root, operands);
        return new(formatted.State, sample) { PositiveAdvice = root is 0x2C82 or 0x2CB3,
            AdviceText = formatted.Text, AdvisorOfficerId = advisorId };

        TransferPersonReference Advisor(ForceReference f)
        {
            ushort raw = TalentNeed(current.ReadAssignmentForceAdvisor52(TalentNeed(current.ReadForcePhysicalSlot(f), "advisor_Force_pointer")), "Force52");
            return (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
                ?? throw TalentCommandMissing("advisor_lookup");
        }
        bool Valid(TransferPersonReference p) => p.PhysicalSlot is int physical && TalentNeed(current.ReadPersonNativeValidity(physical), "advisor_validity");
    }
}
