using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public DeploymentDraftQuery QueryDeploymentDraft(GameState state, int locationId,
        IReadOnlyList<int> selectedOfficerIds, IReadOnlyList<DeploymentDraftRow> previousRows,
        int? focusedOfficerId = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(selectedOfficerIds);
        ArgumentNullException.ThrowIfNull(previousRows);
        try
        {
            if (selectedOfficerIds.Count > 9 || selectedOfficerIds.Distinct().Count() != selectedOfficerIds.Count ||
                previousRows.Select(x => x.Request.OfficerId).Distinct().Count() != previousRows.Count)
                throw DraftMissing("selection_identity");
            if (state.NativeArmySimulation?.Parameters is not { } parameters ||
                parameters.Language != Seed.Language || parameters.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language))
                throw DraftMissing("rule_profile");
            var current = CaptureOfficerCommandCapacityCurrent(state);
            DeploymentTacticCurrentInputs.BindCurrent(state, current);
            var target = locationId switch
            {
                >= 1 and <= 51 when state.Cities.ContainsKey(locationId) => new BaseReference(BaseKind.City, locationId),
                >= 52 and <= 61 when state.Gates.ContainsKey(locationId) => new BaseReference(BaseKind.Gate, locationId - 51),
                _ => throw DraftMissing("source_base")
            };
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, parameters.RuleVersion);
            var old = previousRows.ToDictionary(x => x.Request.OfficerId);
            var rows = new List<DeploymentDraftRow>();
            var physicals = new Dictionary<int, int>();
            foreach (int id in selectedOfficerIds)
            {
                int physical = _definitions.ResolveOfficerSource(id) ?? throw DraftMissing("person_identity");
                if (!state.Officers.ContainsKey(id) || DraftKnown(current.ReadPersonIdentity(physical), "person_identity") != id)
                    throw DraftMissing("person_owner");
                physicals.Add(id, physical);
                if (old.TryGetValue(id, out var retained)) rows.Add(retained);
                else rows.Add(Create(id, physical));
            }
            // Cache only reached comparator fields. Different service priorities
            // decide before reading either person's office or abilities.
            if (rows.Count > 1)
            {
                var statuses = new Dictionary<int, byte>();
                var capacities = new Dictionary<int, int>();
                var effective = new Dictionary<int, OfficerAbilities>();
                for (int index = 1; index < rows.Count; index++)
                {
                    var row = rows[index]; int at = index;
                    while (at > 0 && Before(row.Request.OfficerId, rows[at - 1].Request.OfficerId))
                    { rows[at] = rows[at - 1]; at--; }
                    rows[at] = row;
                }
                bool Before(int left, int right)
                {
                    byte a = Status(left), b = Status(right);
                    if ((a == 1) != (b == 1)) return a == 1;
                    if ((a == 3) != (b == 3)) return a == 3;
                    int ac = Office(left), bc = Office(right);
                    if (ac != bc) return ac > bc;
                    var aa = Effective(left); var ba = Effective(right);
                    if (aa.Leadership != ba.Leadership) return aa.Leadership > ba.Leadership;
                    if (aa.Strength != ba.Strength) return aa.Strength > ba.Strength;
                    return left < right;
                }
                byte Status(int id) => statuses.TryGetValue(id, out byte value) ? value :
                    statuses[id] = DraftKnown(current.ReadPersonStatus(physicals[id]), "sort_status");
                int Office(int id)
                {
                    if (capacities.TryGetValue(id, out int value)) return value;
                    int office = DraftKnown(current.ReadMoraleWavePersonPostE7(physicals[id]), "sort_office");
                    return capacities[id] = DraftKnown(OwnedArmyMoraleWaveInputs.ReadPostCommandCapacity(state, office), "sort_office_capacity");
                }
                OfficerAbilities Effective(int id) => effective.TryGetValue(id, out var value) ? value :
                    effective[id] = abilities.Resolve(state, id) ?? throw DraftMissing("sort_effective_abilities");
            }
            int stock = DraftKnown(current.ReadBaseResource(target, BaseResourceKind.Troops), "base_troops");
            for (int index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                if (row.Request.Troops != -1) continue;
                int remaining = stock;
                for (int other = 0; other < rows.Count; other++)
                    if (other != index && rows[other].Request.Troops > 0)
                        remaining = unchecked(remaining - rows[other].Request.Troops);
                var capacity = OfficerCommandCapacity.Resolve(state, current, physicals[row.Request.OfficerId]);
                if (!capacity.Available) throw DraftMissing(capacity.UnresolvedInput ?? "command_capacity");
                int available = Math.Min(remaining, capacity.Capacity!.Value);
                rows[index] = row with { Request = row.Request with
                    { Troops = available >= DeploymentDraftDefaults.CeMinimumNewUnitTroops ? DeploymentDraftDefaults.CeMinimumNewUnitTroops : 0 } };
            }
            int? focused = focusedOfficerId is int previous && rows.Any(x => x.Request.OfficerId == previous)
                ? previous : rows.FirstOrDefault()?.Request.OfficerId;
            return new(rows.AsReadOnly(), focused, null);

            DeploymentDraftRow Create(int id, int physical)
            {
                if (!DraftKnown(current.ReadPersonNativeValidity(physical), "person_validity"))
                    throw DraftMissing("invalid_person_packet_preferences");
                var behavior = state.DefaultDeploymentBehavior ?? throw DraftMissing("default_behavior");
                behavior = ReadCurrentDeploymentPolicyTraits(current, physical).ApplyDefaults(behavior);
                uint mask = DraftKnown(current.ReadPersonFormationMask164(physical), "formation_capabilities");
                int land = 0;
                for (int formation = 0; formation <= 20; formation++)
                {
                    if (!DeploymentFormationEligibility.Allows(mask, formation, id)) continue;
                    if (DraftKnown(TacticRemakeCurrentFormations.ReadRawByte(state, formation, 0x7E), "formation_kind") != 1) continue;
                    land = formation; break;
                }
                int naval = Naval(id, physical);
                var effective = abilities.Resolve(state, id) ?? throw DraftMissing("effective_abilities");
                var tactics = SelectTactics(id, physical, land, effective);
                return new(new(id, land, -1, Array.AsReadOnly(tactics), []) { Behavior = behavior }, naval);
            }

            int Naval(int id, int physical) => DeploymentNavalFormationSource.Resolve(state, current, id, physical);

            int[] SelectTactics(int id, int physical, int land, OfficerAbilities effective)
                => DeploymentTacticSelectionSource.CreateDefaults(current, id, physical, land, () => effective).ToArray();
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    private static T DraftKnown<T>(T? value, string input) where T : struct => value ?? throw DraftMissing(input);
    private static InvalidDataException DraftMissing(string input) => new($"deployment.draft.{input}");
}
