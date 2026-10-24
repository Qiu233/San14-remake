using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public PersonnelHireDraft OpenPersonnelHireDraft(GameState state, int forceId)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            int district = PersonnelAdministrationPageDistrict(current, forceId);
            var rules = ReadPersonnelHireRulesCurrent(state, current, district);
            var naming = ArmyMapUiSources.Create();
            var people = rules.Candidates.Select(row => ReadPersonnelHireTemplateCard(state, current,
                rules.Templates[row.TemplateId], naming)).ToArray();
            return new(rules, people);
        }
        catch (InvalidDataException error) { return new(forceId, error.Message); }
    }

    public PersonnelLearnDraft OpenPersonnelLearnDraft(GameState state, int forceId)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var rules = CaptureLearnCommandCurrentRules(state, current, forceId);
            var project = PersonnelDevelopmentOfficerProjector(state, current);
            var people = rules.People.Select(row => project(row.Id, row.Physical, row.Name)).ToArray();
            return new(rules, people,
                (officer, gem) => QueryLearnGemEligibility(state, rules, officer, gem),
                officer => ReadLearnReplacementRequiredCurrent(rules, officer), ReadHistoryDataText(1, 0x5be),
                tactic => ReadPersonnelLearnEffectsCurrent(state, current, tactic),
                tactic => LearnTacticName(state, tactic), Confirmation);

            AreaAdministrationQuery<string> Confirmation(int officer, int gem, int? replacement)
            {
                try
                {
                    int learned = rules.Books.First(row => row.GemId == gem).TacticId;
                    return AreaAdministrationQuery<string>.Known(FormatReportMessage(state, current,
                        replacement is null ? 0x598b : 0x598c,
                        new(Person0: officer, String0: LearnTacticName(state, learned),
                            String1: replacement is int old ? LearnTacticName(state, old) : null),
                        ReportMessageOrigin.PersonnelDevelopment, null).Text);
                }
                catch (InvalidDataException error) { return AreaAdministrationQuery<string>.Unknown(error.Message); }
            }
        }
        catch (InvalidDataException error) { return new(forceId, error.Message); }
    }

    public PersonnelMediationDraft OpenPersonnelMediationDraft(GameState state, int forceId)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            int district = PersonnelAdministrationPageDistrict(current, forceId);
            var cards = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario,
                Seed.Language, _baseCapacityInputs, _repository);
            var cache = new Dictionary<int, AreaAdministrationOfficerCard>();
            return new(forceId, ids => QueryPersonnelMediationCurrent(state, current, forceId, district, ids), Project, Confirmation);

            AreaAdministrationQuery<string> Confirmation(IReadOnlyList<int> ids)
            {
                try
                {
                    var ordered = ReadMediationOrderedOfficerIds(state, current, ids);
                    bool marriage = state.Officers[ordered[0]].Sex != state.Officers[ordered[1]].Sex;
                    int root = marriage ? 0x58ac : ordered.Length == 3 ? 0x58ae : 0x58ad;
                    return AreaAdministrationQuery<string>.Known(FormatReportMessage(state, current, root,
                        new(Person0: ordered[0], Person1: ordered[1],
                            Person2: ordered.Length == 3 ? ordered[2] : null,
                            Integers: [MediationParameter(state, MediationGoldKey)]),
                        ReportMessageOrigin.PersonnelDevelopment, null).Text);
                }
                catch (InvalidDataException error) { return AreaAdministrationQuery<string>.Unknown(error.Message); }
            }

            PersonnelDevelopmentOfficer Project(PersonnelMediationCandidate row)
            {
                if (!cache.TryGetValue(row.OfficerId, out var card))
                    cache[row.OfficerId] = card = cards.ReadCity(state.PeriodNumber, row.Physical, true, 0);
                return new(row.OfficerId, row.Name, card.Portrait, card, row.Enabled,
                    row.DisabledReason, row.UnresolvedOwner)
                { NameSortKey = state.Officers.GetValueOrDefault(row.OfficerId)?.CurrentNameSortKey };
            }
        }
        catch (InvalidDataException error) { return new(forceId, error.Message); }
    }

    private Func<int, int, string, PersonnelDevelopmentOfficer> PersonnelDevelopmentOfficerProjector(
        GameState state, SourceWorldContext current)
    {
        var cards = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario,
            Seed.Language, _baseCapacityInputs, _repository);
        var cache = new Dictionary<int, PersonnelDevelopmentOfficer>();
        return (id, physical, name) =>
        {
            if (cache.TryGetValue(id, out var row)) return row;
            var card = cards.ReadCity(state.PeriodNumber, physical, true, 0);
            return cache[id] = new(id, name, card.Portrait, card)
            { NameSortKey = state.Officers.GetValueOrDefault(id)?.CurrentNameSortKey };
        };
    }
}
