using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public StrategicCommandDraft OpenStrategicCommandDraft(GameState state, int forceId, int kind)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var rules = CaptureStrategicCommandRules(state, current, forceId, kind);
            var project = PersonnelDevelopmentOfficerProjector(state, current);
            var title = ReadHistoryDataText(0, kind + 0x127);
            (int, string)[] choices = kind == 11 ? [(0, ReadHistoryDataText(1, 0xB4)),
                (10, ReadHistoryDataText(1, 0xB5)), (30, ReadHistoryDataText(1, 0xB6))] : [];
            return new(forceId, kind, title, choices, Read, Advice);

            AreaAdministrationQuery<StrategicCommandAdvicePresentation> Advice(int executor, int target,
                int? second, int military, int independence, CampaignAppRandomState random)
            {
                try
                {
                    int actorPhysical = rules.Executors.First(row => row.Id == executor).PhysicalSlot;
                    int targetPhysical = rules.Targets.First(row => row.Id == target).PhysicalSlot;
                    int? secondPhysical = second is int other ? rules.SecondForces.First(row => row.Id == other).PhysicalSlot : null;
                    var prepared = ReadStrategicCommandAdviceCurrent(state with { CampaignAppRandom32 = random },
                        current, forceId, kind, actorPhysical, targetPhysical, secondPhysical, military, independence);
                    if (prepared.UnresolvedOwner is string missing)
                        return AreaAdministrationQuery<StrategicCommandAdvicePresentation>.Unknown(missing);
                    PersonnelDevelopmentOfficer? advisor = null;
                    if (prepared.AdvisorOfficerId is int id)
                    {
                        int physical = current.ResolveOfficerSource(id) ?? throw StrategyMissing("advice_source_Person");
                        advisor = project(id, physical, state.Officers.GetValueOrDefault(id)?.Name
                            ?? throw StrategyMissing("advice_source_Person_name"));
                    }
                    return AreaAdministrationQuery<StrategicCommandAdvicePresentation>.Known(
                        new(prepared.AdviceText, advisor, prepared.State.CampaignAppRandom32));
                }
                catch (InvalidDataException error)
                { return AreaAdministrationQuery<StrategicCommandAdvicePresentation>.Unknown(error.Message); }
            }

            StrategicCommandPageSelection Read(int? executor, int? target, int? second, int military, int independence)
            {
                var preview = QueryStrategicCommandSelectionCurrent(rules, executor, target, second, military, independence);
                var actors = preview.Executors.Select(row => project(row.Id, row.PhysicalSlot, row.Name) with
                { Enabled = row.Enabled, DisabledReason = row.DisabledReason, UnresolvedOwner = row.UnresolvedOwner }).ToArray();
                var targets = rules.Targets.Select(Target).ToArray();
                var others = preview.SecondForces.Select(Target).ToArray();
                bool complete = actors.Any(row => row.OfficerId == executor && row.Enabled == true) &&
                    targets.Any(row => row.Id == target && row.Enabled == true) &&
                    (kind != 10 || others.Any(row => row.Id == second && row.Enabled == true));
                bool budget = preview.GoldCost is int cost && rules.Gold >= cost &&
                    rules.Books - CommandBookAvailability.Reserved(state, rules.DistrictId) >= rules.BookCost &&
                    rules.MilitaryIntelligence is int stock && military >= 0 && military <= stock;
                PersonnelDevelopmentOfficer? advisor = null;
                if (preview.AdvisorOfficerId is int id)
                {
                    int physical = current.ResolveOfficerSource(id) ?? throw StrategyMissing("advice_Person_pointer");
                    advisor = project(id, physical, state.Officers.GetValueOrDefault(id)?.Name
                        ?? throw StrategyMissing("advice_Person_name"));
                }
                return new(actors, targets, others,
                    new(rules.Gold, preview.GoldCost is int value ? unchecked(rules.Gold - value) : null, preview.GoldCost,
                        rules.Books, unchecked(rules.Books - rules.BookCost), rules.BookCost,
                        rules.MilitaryIntelligence, rules.MilitaryIntelligence is int before ? unchecked(before - military) : null, military),
                    new StrategicCommandPreview(preview.ChancePercent, preview.TravelDays, preview.Effects,
                        preview.Guide, preview.UnresolvedOwner) { AdviceText = preview.AdviceText },
                    complete && budget, complete && budget && preview.CanContinue == true, preview.UnresolvedOwner)
                {
                    MinimumMilitaryIntelligence = preview.MinimumMilitaryIntelligence,
                    MaximumMilitaryIntelligence = preview.MaximumMilitaryIntelligence,
                    MilitaryIntelligenceStep = preview.MilitaryIntelligenceStep,
                    RequiredMilitaryIntelligence = preview.RequiredMilitaryIntelligence,
                    TargetDetails = preview.TargetDetails, AdvisorOfficer = advisor
                };
            }
        }
        catch (InvalidDataException error) { return new(forceId, kind, error.Message); }

        static StrategicCommandTargetChoice Target(StrategicCommandCandidate row) => new(row.Id, row.Name,
            row.ForceId, row.HomeId, row.HexId, row.Enabled, row.DisabledReason, row.UnresolvedOwner);
    }
}
