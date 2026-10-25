using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>331150 on the explicit new-campaign path. Self-save restore
    /// never enters this producer or repairs absent research knowledge.</summary>
    public GameState InitializeFreshDoctrineResearch(GameState state)
    {
        //The normal host has already entered Strategy while constructing its
        //native services. Use the actual fresh clock and allocation boundary;
        //do not temporarily change Phase to imitate an initialization state.
        if (_scenario.Metadata.FormatMarker != 0x66 || state.NativeArmySimulation is null ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.Forces.Values.Any(force => force.DoctrineResearch is not null))
            throw ResearchMissing("fresh_initialization_boundary");
        var reserves = state.ForceCreditReserves ?? throw ResearchMissing("fresh_credit_reserve_allocation");
        var current = CaptureWritableTransportRouteCurrent(state);
        var panels = ReadDoctrineResearchDefinitions(current);
        if (!panels.TryGetValue(1, out var root)) throw ResearchMissing("fresh_root_definition");
        var byLayout = panels.Values.OrderBy(panel => panel.Id).GroupBy(panel => panel.LayoutSlot)
            .ToDictionary(group => group.Key, group => group.Last().Id);
        var registered = current.ReadRegisteredPersonPhysicalSlots() ?? throw ResearchMissing("fresh_Person_registry");
        var forces = new Dictionary<int, ForceState>(state.Forces);
        var points = reserves.PointsByForce.ToBuilder();

        //331150 visits all52 force table pointers in table order, rather than
        //only the human-controlled force or an unordered domain dictionary.
        for (int raw = 0; raw < 52; raw++)
        {
            var force = current.ReadForceTableReference(raw) ?? throw ResearchMissing("fresh_Force_pointer");
            if (force.PhysicalSlot is not int physical) continue;
            if (physical != 0 && current.ReadForceRecord(physical) is null)
            {
                //The normal mutable capture intentionally does not interpret
                //a nullable Domain ruler as the native zero sentinel. Here,
                //and only on the explicit fresh path,237CC0's loaded record
                //still proves that unused receiver's actual ruler-word0.
                //A nonzero source or changed current owner remains unknown.
                var source = _scenario.Snapshot.Forces[physical];
                if (source.Slot != physical || source.RulerSlot != 0 ||
                    !forces.TryGetValue(physical, out var fresh) || fresh.RulerId is not null)
                    throw ResearchMissing("fresh_Force_ruler_binding");
                current.SetOwnedForceRulerSlot10(physical, 0);
            }
            if (!ResearchNeed(current.ReadForceNativeValidity(force), "fresh_Force_validity")) continue;
            var owner = forces.GetValueOrDefault(physical) ?? throw ResearchMissing("fresh_Force_owner");
            if (!points.TryGetValue(physical, out var initial) || initial.Length != 6)
                throw ResearchMissing("fresh_six_reserve_owner");

            //235680 clears all three working bitmaps, makes root1 available,
            //then reaches the shared2ED610(root1,initialization=true) writer.
            var baseline = new DoctrineResearchWorkspace(new byte[6], [], [], [1]);
            var working = baseline.Copy();
            int rulerDoctrine = 0, discount = 0;
            if (root.FirstDoctrineId != 0 || root.SecondDoctrineId != 0)
            {
                int rulerId = TalentRulerCurrent(current, force);
                var ruler = state.Officers.GetValueOrDefault(rulerId) ?? throw ResearchMissing("fresh_ruler_owner");
                rulerDoctrine = ResearchNeed(OfficerDoctrineRules.ReadDoctrineId(ruler.DoctrineId, ruler.AbilityState),
                    "fresh_ruler_doctrine");
                discount = ResearchParameter(state, "matching-doctrine-discount", 1);
            }
            DoctrineResearchSource.Apply(working, baseline, root, rulerDoctrine, discount, byLayout);
            //The fresh native constructor's old committed panels are empty.
            //224FB0/224E40's removed-effect branches therefore are not entered.

            //2321A0 follows root initialization and replaces all six temporary
            //reserves. Each officer addition is separately capped at99.
            Array.Clear(working.Points);
            foreach (int? entry in registered)
            {
                int personPhysical = ResearchNeed(entry, "fresh_Person_pointer");
                var affiliation = current.ReadPersonForce(personPhysical) ?? throw ResearchMissing("fresh_Person_Force");
                if (affiliation.PhysicalSlot != physical) continue;
                int status = ResearchNeed(current.ReadPersonStatus(personPhysical), "fresh_Person_status");
                if (status is < 2 or > 4) continue;
                int id = ResearchNeed(current.ReadPersonIdentity(personPhysical), "fresh_Person_identity");
                if (FreshResearchForeignPerson(id))
                {
                    int home = ResearchNeed(current.ReadPersonFirstLocation(personPhysical), "fresh_foreign_home");
                    if (home is >= 1 and <= 61 && physical is not (>= 46 and <= 50)) continue;
                }
                if (ResearchNeed(current.ReadPersonBehavior(personPhysical)?.UsesSpecialPersonRules, "fresh_Person_special")) continue;
                var person = state.Officers.GetValueOrDefault(id) ?? throw ResearchMissing("fresh_Person_owner");
                int doctrine = ResearchNeed(OfficerDoctrineRules.ReadDoctrineId(person.DoctrineId, person.AbilityState),
                    "fresh_Person_doctrine");
                if (doctrine is < 1 or > 6) continue;
                //03FBE0 returns the actual rank byte here: status1, foreign
                //exclusion and4000 have already been tested above.22C3F0 sums
                //CCreditRankData.DoctrinePointAddition for ranks1..that rank.
                int stage = ResearchNeed(person.MeritState?.RankStageId, "fresh_Person_credit_rank");
                if (stage is < 0 or > 6) throw ResearchMissing("fresh_Person_credit_rank_range");
                //This is an accumulation from rank0 to the current rank, not
                //a new promotion or a replacement of the person's merit data.
                int addition = ReadCreditRankupPointAddition(new CreditCommandPerson(id, personPhysical,
                    person.Name, 0, 0, stage, 0, 0, 0));
                working.Points[doctrine - 1] = (byte)Math.Clamp(unchecked(working.Points[doctrine - 1] + addition), 0, 99);
            }
            forces[physical] = owner with
            {
                PolicyEffects = new(working.Researched.ToArray(), working.Pending.ToArray()),
                DoctrineResearch = new(working.Available.ToImmutableHashSet())
            };
            points[physical] = working.Points.ToImmutableArray();
        }
        return state with { Forces = forces, ForceCreditReserves = reserves with { PointsByForce = points.ToImmutable() } };
    }

    private static bool FreshResearchForeignPerson(int id) =>
        FreshResearchForeignBand(4111,id) || FreshResearchForeignBand(4131,id) ||
        FreshResearchForeignBand(4151,id) || FreshResearchForeignBand(4171,id) || FreshResearchForeignBand(4191,id);
    //The five actual2C7250 bands, kept at this source boundary.
    private static bool FreshResearchForeignBand(int first, int id) => unchecked((uint)(id - first)) <= 9;
}
