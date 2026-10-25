using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>235680 and its reached2321A0 for a newly created Force.
    /// Uses the existing research kernel and the caller's current; does not
    /// reseed other Forces or reopen a campaign initialization boundary.</summary>
    internal GameState InitializeDoctrineResearchForceCurrent(GameState state,
        SourceWorldContext current, int physical)
    {
        var force = current.ReadForceTableReference(physical) ?? throw ResearchMissing("created_Force_pointer");
        if (!ResearchNeed(current.ReadForceNativeValidity(force), "created_Force_validity")) return state;
        var panels = ReadDoctrineResearchDefinitions(current);
        var root = panels.GetValueOrDefault(1) ?? throw ResearchMissing("created_root_definition");
        var byLayout = panels.Values.OrderBy(panel => panel.Id).GroupBy(panel => panel.LayoutSlot)
            .ToDictionary(group => group.Key, group => group.Last().Id);
        var baseline = new DoctrineResearchWorkspace(new byte[6], [], [], [1]);
        var working = baseline.Copy();
        int doctrine = 0, discount = 0;
        if (root.FirstDoctrineId != 0 || root.SecondDoctrineId != 0)
        {
            int id = TalentRulerCurrent(current, force);
            var ruler = state.Officers.GetValueOrDefault(id) ?? throw ResearchMissing("created_ruler_owner");
            doctrine = ResearchNeed(OfficerDoctrineRules.ReadDoctrineId(ruler.DoctrineId, ruler.AbilityState), "created_ruler_doctrine");
            discount = ResearchParameter(state, "matching-doctrine-discount", 1);
        }
        DoctrineResearchSource.Apply(working, baseline, root, doctrine, discount, byLayout);
        //2D8050 tests the independent tutorial object before2321A0.
        var application = ReadApplicationGameplay() ?? throw ResearchMissing("created_application_owner");
        bool present = ResearchNeed(application.TutorialServicePresent, "created_tutorial_service_presence");
        int user = state.CampaignEventWorld?.CurrentUserIndex ?? throw ResearchMissing("created_current_user");
        if (!present || user == -1)
        {
            Array.Clear(working.Points);
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw ResearchMissing("created_Person_registry"))
            {
                int person = ResearchNeed(entry, "created_Person_pointer");
                if ((current.ReadPersonForce(person) ?? throw ResearchMissing("created_Person_Force")).PhysicalSlot != physical) continue;
                int status = ResearchNeed(current.ReadPersonStatus(person), "created_Person_status");
                if (status is < 2 or > 4) continue;
                int id = ResearchNeed(current.ReadPersonIdentity(person), "created_Person_identity");
                if (FreshResearchForeignPerson(id) && ResearchNeed(current.ReadPersonFirstLocation(person), "created_foreign_Home") is >= 1 and <= 61
                    && physical is not (>= 46 and <= 50)) continue;
                if (ResearchNeed(current.ReadPersonBehavior(person)?.UsesSpecialPersonRules, "created_Person_special")) continue;
                var owner = state.Officers.GetValueOrDefault(id) ?? throw ResearchMissing("created_Person_owner");
                int category = ResearchNeed(OfficerDoctrineRules.ReadDoctrineId(owner.DoctrineId, owner.AbilityState), "created_Person_doctrine");
                if (category is < 1 or > 6) continue;
                int stage = ResearchNeed(owner.MeritState?.RankStageId, "created_Person_credit_rank");
                if (stage is < 0 or > 6) throw ResearchMissing("created_Person_credit_rank_range");
                int addition = ReadCreditRankupPointAddition(new CreditCommandPerson(id, person, owner.Name, 0, 0, stage, 0, 0, 0));
                working.Points[category - 1] = (byte)Math.Clamp(unchecked(working.Points[category - 1] + addition), 0, 99);
            }
        }
        state = PublishDoctrineResearchWorkspace(state, physical, working);
        return ClampResourcesAfterDoctrineResearch(state, current, baseline, working, physical);
    }
}
