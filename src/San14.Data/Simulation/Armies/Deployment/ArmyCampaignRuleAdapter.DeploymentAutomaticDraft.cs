using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Ordinary new Make has no external formation counts or rows.
    /// The five initial permission queries share their accumulating counts.</summary>
    public DeploymentAutomaticOptions QueryDeploymentAutomaticOptions(GameState state, int locationId)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            var operation = new AutomaticDraftOperation(this, state, locationId);
            var counts = new int[21];
            var result = new List<DeploymentAutomaticOption>();
            foreach (var style in Enum.GetValues<DeploymentAutomaticStyle>())
                result.Add(new(style, operation.Create(style, counts).Count != 0));
            return new(result.AsReadOnly(), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    public DeploymentDraftQuery QueryDeploymentAutomaticDraft(GameState state, int locationId,
        DeploymentAutomaticStyle style)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            if (!Enum.IsDefined(style)) throw AutomaticMissing("style");
            var rows = new AutomaticDraftOperation(this, state, locationId).Create(style, new int[21]);
            // All packets already exist, so the ordinary shared draft producer
            // retains every field and only applies Make's final row ordering.
            // It does not create defaults or fill sentinel troop counts here.
            return QueryDeploymentDraft(state, locationId, rows.Select(row => row.Request.OfficerId).ToArray(), rows);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    private sealed class AutomaticDraftOperation
    {
        private readonly ArmyCampaignRuleAdapter adapter;
        private readonly GameState state;
        private readonly int locationId;
        private readonly SourceWorldContext current;
        private readonly BaseReference sourceBase;
        private readonly TacticRemakeCurrentAbilities abilities;
        private readonly Dictionary<int, OfficerAbilities> effective = [];
        private readonly Dictionary<int, uint> masks = [];
        private readonly Dictionary<int, int> capacities = [];
        internal AutomaticDraftOperation(ArmyCampaignRuleAdapter adapter, GameState state, int locationId,
            SourceWorldContext? operationCurrent = null)
        {
            this.adapter = adapter; this.state = state; this.locationId = locationId;
            sourceBase = locationId switch
            {
                >= 1 and <= 51 when state.Cities.ContainsKey(locationId) => new(BaseKind.City, locationId),
                >= 52 and <= 61 when state.Gates.ContainsKey(locationId) => new(BaseKind.Gate, locationId - 51),
                _ => throw AutomaticMissing("source_base")
            };
            if (state.NativeArmySimulation?.Parameters is not { } p || p.Language != adapter.Seed.Language ||
                p.RuleVersion != SimulationRuleIdentity.ForLocale(adapter.Seed.Language)) throw AutomaticMissing("rule_profile");
            current = operationCurrent ?? adapter.CaptureDeploymentPersonSelectionCurrent(state);
            adapter._moraleWaveInputs.BindCurrent(state, current);
            adapter._baseCapacityInputs.BindCurrent(state, current);
            DeploymentTacticCurrentInputs.BindCurrent(state, current);
            abilities = new(current, adapter._scenario, adapter.Seed.Language, p.RuleVersion);
        }

        internal List<DeploymentDraftRow> Create(DeploymentAutomaticStyle style, int[] newCounts, int externalRowCount = 0)
        {
            var candidates = Candidates();
            if (style is DeploymentAutomaticStyle.FieldAttack or DeploymentAutomaticStyle.SiegeTroops)
                candidates = candidates.Where(person => Effective(person).Leadership > 20 ||
                    Effective(person).Strength > 10 || Effective(person).Intelligence > 10).ToList();
            bool enemy = HasAdjacentEnemy();
            int number = candidates.Count;
            if (enemy) number = Math.Min(number - 1, unchecked(number * 90) / 100);
            int district = Need(current.ReadBaseDistrictSlot(sourceBase), "source_district");
            district = district < 52 && district >= 0 ? district : 0;
            int books = state.Districts.GetValueOrDefault(district)?.OrdersRemaining is int b && b is >= 0 and <= 255 ? b : throw AutomaticMissing("command_books");
            number = Math.Max(0, Math.Min(number, Math.Min(unchecked(books - externalRowCount), 9)));
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Army) ?? throw AutomaticMissing("army_registry");
            int armyCount = Need(current.ReadTemporaryHexListCount(new(descriptor.Identity)), "army_count");
            number = Math.Min(number, unchecked(500 - armyCount - externalRowCount));
            if (number < 1) return [];
            int stock = Need(current.ReadBaseResource(sourceBase, BaseResourceKind.Troops), "base_troops");
            int[] scores = candidates.Select(person => Score(person, style, stock, number)).ToArray();
            int[] order = DeploymentAutomaticCandidateOrder.Sort(candidates, scores);
            int sumCapacity = 0;
            foreach (int person in candidates) sumCapacity = unchecked(sumCapacity + Capacity(person));
            int available = Math.Min(Math.Min(sumCapacity, unchecked(stock * (enemy ? 60 : 85)) / 100),
                DeploymentAutomaticFoodLimit.Read(state, current, sourceBase));
            int[] existing = ExistingFormationCounts(district);
            int gold = Gold();
            var rows = new List<DeploymentDraftRow>();
            int attempts = 0;
            foreach (int candidateIndex in order)
            {
                if (available < 1000 || rows.Count >= number || ++attempts > 100) break;
                int person = candidates[candidateIndex];
                if (!Need(current.ReadPersonNativeValidity(person), "candidate_validity")) continue;
                var weights = DeploymentAutomaticPreferences.FormationWeights[(int)style];
                int selected = DeploymentAutomaticFormationChoice.Select(weights, newCounts, existing, gold,
                    formation => Allows(person, formation),
                    formation => Need(TacticRemakeCurrentFormations.ReadRawWord(state, formation, 0x9A), "formation_cost"));
                if (selected == 0) continue;
                int id = Identity(person);
                int[] tactics = new int[3];
                if (selected != 7)
                {
                    int[] choices = DeploymentTacticSelectionSource.ReadCandidates(current, id, person, selected,
                        () => Effective(person), style);
                    Array.Copy(choices, tactics, Math.Min(3, choices.Length));
                }
                int troops = Math.Max(1000, Math.Min(available, Capacity(person))) / 100 * 100;
                var defaults = state.DefaultDeploymentBehavior ?? throw AutomaticMissing("default_behavior");
                var behavior = ReadCurrentDeploymentPolicyTraits(current, person).ApplyDefaults(defaults);
                int naval = DeploymentNavalFormationSource.Resolve(state, current, id, person);
                int costGold = Need(TacticRemakeCurrentFormations.ReadRawWord(state, selected, 0x9A), "selected_formation_cost");
                gold = unchecked(gold - costGold);
                rows.Add(new(new(id, selected, troops, Array.AsReadOnly(tactics), []) { Behavior = behavior }, naval));
                newCounts[selected] = unchecked(newCounts[selected] + 1);
                available = unchecked(available - troops);
            }
            return rows;
        }

        internal List<int> Candidates()
        {
            ushort link = Need(current.ReadBaseObjectLink(sourceBase), "base_link");
            var result = new List<int>();
            if (link is < 1 or > 61) return result;
            foreach (int? value in current.ReadRegisteredPersonPhysicalSlots() ?? throw AutomaticMissing("person_registry"))
            {
                int person = value ?? throw AutomaticMissing("person_pointer");
                if (Need(current.ReadPersonFirstLocation(person), "first_location") != link) continue;
                if (Need(current.ReadPersonStatus(person), "service_status") is not (1 or 2 or 3 or 4)) continue;
                if ((Need(current.ReadPersonResetWord1FF(person), "rumor_counter") & 255) != 0) continue;
                if (Need(current.ReadPersonSecondLocation(person), "second_location") != link) continue;
                if ((Need(current.ReadArmyReturnPersonFlags196(person), "person_flags") & 1) != 0) continue;
                if ((current.ReadPersonLinkedEntity(person) ?? throw AutomaticMissing("person_association")).Identity.HasValue) continue;
                _ = Identity(person); result.Add(person);
            }
            return result;
        }
        //2680A0's usable troop bound, also consumed by the ordinary general
        //target entry before its shared260D70 composition call.
        internal int ReadUsableTroops(IReadOnlyList<int> candidates, int adjacentEnemyReserve)
        {
            int capacity = 0;
            foreach (int person in candidates) capacity = unchecked(capacity + Capacity(person));
            int stock = Need(current.ReadBaseResource(sourceBase, BaseResourceKind.Troops), "base_troops");
            return Math.Min(capacity, unchecked(stock - (HasAdjacentEnemy() ? adjacentEnemyReserve : 0)));
        }
        private bool HasAdjacentEnemy()
        {
            int force = Need(CurrentBaseAffiliationSource.ReadForce(state, current, sourceBase), "base_force");
            var links = sourceBase.Kind == BaseKind.City ? adapter._scenario.Snapshot.Cities[sourceBase.PhysicalSlot!.Value].LinkedLocationSlots
                : adapter._scenario.Snapshot.Gates[sourceBase.PhysicalSlot!.Value].LinkedLocationSlots;
            ushort self = Need(current.ReadBaseObjectLink(sourceBase), "base_link");
            foreach (byte link in links.Take(10))
            {
                if (link is < 1 or > 61) continue;
                var neighbor = BaseLookup.Find(link, current);
                if (!Need(current.ReadBaseNativeValidity(neighbor), "adjacent_base_validity")) continue;
                if (link == self) continue;
                int otherForce = Need(CurrentBaseAffiliationSource.ReadForce(state, current, neighbor), "adjacent_base_force");
                if (Need(ArmyLogisticsPreparation.ReadNativeRelationGate(state, force, otherForce), "adjacent_relation")) return true;
            }
            return false;
        }
        private int[] ExistingFormationCounts(int district)
        {
            int[] counts = new int[21];
            if (district is < 1 or > 51) return counts;
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw AutomaticMissing("army_registry"))
            {
                int physical = entry is { Kind: RegistryKind.Army, PhysicalSlot: int p } ? p : throw AutomaticMissing("army_pointer");
                var army = new ArmyUnitReference(physical);
                ushort commander = Need(current.ReadArmyUnitCommanderId(army), "army_commander");
                var person = commander <= 6000 ? current.ReadPersonLookupReference(commander) : current.ReadTransferPersonTableReference(0);
                int slot = person?.PhysicalSlot ?? throw AutomaticMissing("army_commander_pointer");
                int armyDistrict = Need(current.ReadPersonNativeValidity(slot), "army_commander_validity")
                    ? Need(current.ReadPersonDistrictSlot(slot), "army_district") : 0;
                if (armyDistrict > 51) armyDistrict = 0;
                if (armyDistrict != district || Need(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "army_excluded")) continue;
                int land = Need(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, NotificationArmy(state, physical)), "army_formation");
                int formation = land < 21 ? land : 0;
                //22DE60 is physical identity; the table0 receiver counts too.
                counts[formation] = unchecked(counts[formation] + 1);
            }
            return counts;
        }
        private int Gold()
        {
            BaseReference city = sourceBase.Kind == BaseKind.City ? sourceBase : CurrentGateMapQueries.FindCity(sourceBase, current);
            return Need(current.ReadBaseResource(city, BaseResourceKind.Gold), "base_gold");
        }
        private int Identity(int person)
        {
            int id = Need(current.ReadPersonIdentity(person), "person_identity");
            if (adapter._definitions.ResolveOfficerSource(id) != person || !state.Officers.ContainsKey(id)) throw AutomaticMissing("person_domain_identity");
            return id;
        }
        private OfficerAbilities Effective(int person) => effective.TryGetValue(person, out var value) ? value :
            effective[person] = abilities.Resolve(state, Identity(person)) ?? throw AutomaticMissing("effective_abilities");
        private int Capacity(int person)
        {
            if (capacities.TryGetValue(person, out int value)) return value;
            var result = OfficerCommandCapacity.Resolve(state, current, person);
            return capacities[person] = result.Capacity ?? throw new InvalidDataException(result.UnresolvedInput);
        }
        private bool Allows(int person, int formation)
        {
            if (!masks.TryGetValue(person, out uint mask)) masks[person] = mask = Need(current.ReadPersonFormationMask164(person), "formation_capabilities");
            return DeploymentFormationEligibility.Allows(mask, formation, Identity(person));
        }
        private int Score(int person, DeploymentAutomaticStyle style, int stock, int number)
        {
            var a = Effective(person);
            int count = 0;
            for (; count < 10; count++)
            {
                var tactic = DeploymentTacticSelectionSource.ReadCurrentSlot(current, Identity(person), person, count);
                if (!Need(current.ReadSkillTacticValidity(tactic), "score_tactic_validity")) break;
            }
            return DeploymentAutomaticCandidateScore.Read(a, count, stock, number, style,
                formation => Allows(person, formation), () => Capacity(person));
        }
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw AutomaticMissing(field);
    }
    private static InvalidDataException AutomaticMissing(string input) => new("deployment.automatic." + input);
}
