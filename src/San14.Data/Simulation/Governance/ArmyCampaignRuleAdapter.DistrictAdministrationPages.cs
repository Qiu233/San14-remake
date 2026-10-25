using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public DistrictAdministrationDraft OpenDistrictAdministrationDraft(GameState state, int forceId, bool reorganize)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var rules = CaptureDistrictAdministrationRules(state, current, forceId);
            var project = PersonnelDevelopmentOfficerProjector(state, current);
            var confirmations = new Dictionary<int, AreaAdministrationQuery<string>>();
            var facts = new List<(BaseReference Base, int CityId, ushort Link)>();
            Dictionary<ushort, int>? personCounts = null;
            string? factsMissing = null;
            try
            {
                var force = current.ReadForceTableReference(forceId) ?? throw DistrictPageMissing("summary_Force");
                foreach (var entry in current.ReadRegisteredBases() ?? throw DistrictPageMissing("summary_Foothold_registry"))
                {
                    var home = entry ?? throw DistrictPageMissing("summary_Foothold_pointer");
                    if (!DistrictPageNeed(current.ReadBaseNativeValidity(home), "summary_Foothold_validity")) continue;
                    int raw = DistrictPageNeed(current.ReadBaseForceSlot(home), "summary_Foothold_Force");
                    var owner = current.ReadForceTableReference(raw is >= 0 and <= 51 ? raw : 0)
                        ?? throw DistrictPageMissing("summary_Foothold_Force_pointer");
                    if (owner != force) continue;
                    var city = CurrentGateMapQueries.FindCity(home, current);
                    facts.Add((home, DistrictPageNeed(current.ReadBaseObjectLink(city), "summary_parent_City4E"),
                        DistrictPageNeed(current.ReadBaseObjectLink(home), "summary_Foothold4E")));
                }
            }
            catch (InvalidDataException error) { factsMissing = error.Message; }
            var catalog = _repository.LoadObjectDefinitions(_scenario.FixedDataSource.SourcePath).Catalog.Definitions;
            var facilities = Enumerable.Range(3, 15).Select(id =>
            {
                var reference = current.ReadObjectDefinitionReference(id) ?? throw DistrictPageMissing("facility_definition_lookup");
                int physical = reference.PhysicalSlot ?? throw DistrictPageMissing("facility_definition_pointer");
                if ((uint)physical >= (uint)catalog.Count) throw DistrictPageMissing("facility_definition_catalog");
                return new DistrictAdministrationOption(id, catalog[physical].Name);
            }).ToArray();
            return new(reorganize, rules,
                (district, cities, settings) => QueryDistrictAdministrationSelectionCurrent(state, current, forceId, district, cities, settings),
                (district, cities, settings, leader, initialize) => ReadDistrictAdministrationDefaultsCurrent(state, current,
                    forceId, district, cities, settings, leader, initialize),
                (settings, policy, cities) => ReadDistrictAdministrationPolicyChoiceCurrent(state, current, forceId, settings, policy, cities),
                Summary, Leader, Confirmation, district => district is int id ? ReadDistrictAdministrationIntelligenceCurrent(current, id) : 0)
            {
                Policies = Enumerable.Range(1, 5).Select(id => new DistrictAdministrationOption(id, ReadHistoryDataText(0, 0x175 + id))).ToArray(),
                Priorities = Enumerable.Range(1, 4).Select(id => new DistrictAdministrationOption(id, ReadHistoryDataText(0, 0x17B + id))).ToArray(),
                ResourceUsageChoices = Enumerable.Range(0, 3).Select(id => new DistrictAdministrationOption(id, ReadHistoryDataText(0, 0x180 + id))).ToArray(),
                Facilities = facilities,
                Missions = new[] { (7, 0x2A6), (5, 0x2A7), (6, 0x2A8), (3, 0x2A9), (4, 0x6F0), (1, 0xC5), (0, 0xC6) }
                    .Select(row => new DistrictAdministrationOption(row.Item1, ReadHistoryDataText(1, row.Item2))).ToArray()
            };

            PersonnelDevelopmentOfficer Leader(int id)
            {
                int physical = current.ResolveOfficerSource(id) ?? throw DistrictPageMissing("leader_projection_Person");
                string name = state.Officers.GetValueOrDefault(id)?.Name ?? throw DistrictPageMissing("leader_projection_owner");
                return project(id, physical, name);
            }
            AreaAdministrationQuery<string> Confirmation(int district)
            {
                if (confirmations.TryGetValue(district, out var cached)) return cached;
                return confirmations[district] = QueryDistrictDissolveConfirmationCurrent(state, current, district);
            }
            DistrictAdministrationSummary Summary(IReadOnlyList<int> selected, int? leader, DistrictSettings? settings, int number)
            {
                string name = rules.Districts.FirstOrDefault(row => row.Number == number)?.Name
                    ?? (number is >= 1 and <= 8 ? ReadHistoryDataText(1, 0x38C + number) : "");
                string cityCaption = string.Join("、", selected.Select(id => rules.Cities.FirstOrDefault(row => row.Id == id)?.Name ?? ""));
                string leaderCaption = leader is int person ? state.Officers.GetValueOrDefault(person)?.Name ?? "" : "";
                string targetCaption = settings?.Target is { } target ? target.Kind switch
                {
                    DistrictTargetKind.Force => state.Forces.GetValueOrDefault(target.Id)?.Name ?? "",
                    DistrictTargetKind.Foothold => state.Cities.GetValueOrDefault(target.Id)?.Name ?? state.Gates.GetValueOrDefault(target.Id)?.Name ?? "",
                    _ => rules.Districts.FirstOrDefault(row => row.Id == target.Id)?.Name ?? ""
                } : "";
                int? people = null, troops = null; string? missing = factsMissing;
                try
                {
                    if (missing is not null) throw new InvalidDataException(missing);
                    personCounts ??= CountPersons();
                    int personTotal = 0, troopTotal = 0;
                    foreach (var fact in facts.Where(row => selected.Contains(row.CityId)))
                    {
                        personTotal = unchecked(personTotal + personCounts.GetValueOrDefault(fact.Link));
                        troopTotal = unchecked(troopTotal + DistrictPageNeed(current.ReadBaseResource(fact.Base, BaseResourceKind.Troops), "summary_Base_troops"));
                    }
                    people = personTotal; troops = troopTotal;
                }
                catch (InvalidDataException error) { missing = error.Message; }
                return new(name, cityCaption, leaderCaption, targetCaption, selected.Count, people, troops, missing);
            }
            Dictionary<ushort, int> CountPersons()
            {
                var counts = new Dictionary<ushort, int>();
                foreach (var entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw DistrictPageMissing("summary_Person_registry"))
                {
                    int physical = entry ?? throw DistrictPageMissing("summary_Person_pointer");
                    if (!DistrictPageNeed(current.ReadPersonNativeValidity(physical), "summary_Person_validity") ||
                        DistrictPageNeed(current.ReadPersonStatus(physical), "summary_Person_status") is < 1 or > 4) continue;
                    ushort location = DistrictPageNeed(current.ReadPersonFirstLocation(physical), "summary_Person_E2");
                    counts[location] = unchecked(counts.GetValueOrDefault(location) + 1);
                }
                return counts;
            }
        }
        catch (InvalidDataException error) { return new(forceId, reorganize, error.Message); }
    }
}
