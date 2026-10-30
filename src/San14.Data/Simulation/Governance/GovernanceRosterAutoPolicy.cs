using San14.Core;

namespace San14.Data;

public sealed record GovernanceAutoPolicyPublication(GovernanceRoster? Roster, string? UnresolvedOwner)
{
    public bool Available => Roster is not null && UnresolvedOwner is null;
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Prepare the ordinary automatic-policy button's five ordered
    /// lists. The committed force and the page's draft have separate roles;
    /// neither is written, and this producer consumes no application randoms.</summary>
    public GovernanceAutoPolicyPublication PrepareGovernanceAutoPolicy(GameState state, int forceId,
        GovernanceRoster currentDraft, GameState? tutorialControl = null)
    {
        try
        {
            if (!state.Forces.TryGetValue(forceId, out var force) || forceId is < 1 or > 51)
                throw GovernanceMissing("auto.force");
            if (currentDraft.Groups.Count != 5 ||
                !currentDraft.Groups.Select(group => group.Group).Order().SequenceEqual(Enumerable.Range(0, 5)) ||
                currentDraft.Groups.Any(group => group.OfficerIds.Count > 7))
                throw GovernanceMissing("auto.draft_shape");
            var current = CaptureDeploymentPersonSelectionCurrent(state);
            _baseCapacityInputs.BindCurrent(state, current);
            var reference = current.ReadForceTableReference(forceId) ?? throw GovernanceMissing("auto.force_pointer");
            if (!RosterNeed(current.ReadForceNativeValidity(reference), "auto.force_validity"))
                throw GovernanceMissing("auto.force_invalid");
            return new(new AutomaticPolicyWorkspace(this, state, current, force, currentDraft, tutorialControl ?? state).Prepare(), null);
        }
        catch (InvalidDataException missing) { return new(null, missing.Message); }
    }

    /// <summary>The 261FF0 producer: retain the draft, choose its adviser,
    /// compare two allocations for each priority effect, then fill ordinary
    /// effects. All caches are limited to this single button operation.</summary>
    private sealed class AutomaticPolicyWorkspace
    {
        private static readonly int[] PriorityEffects = [14, 13, 24, 19];
        private readonly ArmyCampaignRuleAdapter owner;
        private readonly GameState state;
        private readonly SourceWorldContext current;
        private readonly ForceState force;
        private readonly GovernanceRoster draft;
        private readonly GameState tutorialControl;
        private readonly TacticRemakeCurrentAbilities abilities;
        private readonly Lazy<int> selectedLesson;
        private readonly Dictionary<int, AutomaticPolicyPerson> people = [];
        private readonly int[][] originalGroups;
        private readonly List<int>[] output = [[], [], [], [], []];
        private readonly int[] slots = new int[5];
        private readonly int[] levels = new int[101];
        private int budget, books;

        internal AutomaticPolicyWorkspace(ArmyCampaignRuleAdapter owner, GameState state,
            SourceWorldContext current, ForceState force, GovernanceRoster draft, GameState tutorialControl)
        {
            this.owner = owner; this.state = state; this.current = current; this.force = force; this.draft = draft;
            this.tutorialControl = tutorialControl;
            selectedLesson = new(() => RosterNeed(TacticRemakeCurrentBattleControl.ReadSelectedLessonIndex(tutorialControl),
                "auto.selected_lesson"));
            abilities = new(current, owner._scenario, owner.Seed.Language, SimulationRuleIdentity.ForLocale(owner.Seed.Language));
            originalGroups = Enumerable.Range(0, 5).Select(group => Enumerable.Range(0, 7)
                .Select(index => SavedWord(BattleRemakeCurrentForcePolicies.DepartmentPersonKey(force.Id, group, index))).ToArray()).ToArray();
        }

        internal GovernanceRoster Prepare()
        {
            bool useEffect14 = SharesFrontierProvince();
            int ruler = RosterNeed(current.ReadForceRecord(force.Id)?.RawRuler10, "auto.ruler10");
            int rulerDoctrine = Valid(ruler) ? Doctrine(ruler) : 0;
            int[]? doctrine = DoctrineRow(rulerDoctrine);
            for (int group = 0; group < 5; group++)
                slots[group] = doctrine is null ? 0 : unchecked(doctrine[91 + group] +
                    SavedByte(BattleRemakeCurrentForcePolicies.DepartmentTrailerKey(force.Id, group)));
            var primary = ForcePrimaryDistrictSource.Resolve(current, new(force.Id));
            int district = RosterNeed(primary.PhysicalSlot, "auto.primary_district");
            books = RosterNeed(current.ReadDistrictRawByte(district, 0x14), "auto.district_books14");
            budget = unchecked((owner.ReadGovernanceCostLimit(state, force.Id) ??
                throw GovernanceMissing("auto.cost_limit")) - Cost(ruler));

            var candidates = new List<AutomaticPolicyPerson>();
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw GovernanceMissing("auto.person_registry"))
            {
                int physical = RosterNeed(entry, "auto.person_registry_payload");
                var membership = current.ReadPersonForce(physical) ?? throw GovernanceMissing("auto.person_force");
                if (RosterNeed(membership.PhysicalSlot, "auto.person_force_receiver") != force.Id) continue;
                // ForcePersonIterator(filter0), then233780(Person,1).
                // The source excludes status1 even if a malformed old owner
                // gives that person a different logical ruler ID.
                int status = RosterNeed(current.ReadPersonStatus(physical), "auto.person_status");
                if (status is < 2 or > 4) continue;
                if (!owner.RosterPickerEligible(state, current, force, physical)) continue;
                int id = RosterNeed(current.ReadPersonIdentity(physical), "auto.person_identity");
                var person = Person(id);
                if (!person.HasPolicy) continue;
                person.Preference = rulerDoctrine is >= 2 and <= 6 && doctrine is not null
                    ? doctrine[96 + person.Group] : 0;
                candidates.Add(person);
            }
            if (Valid(ruler)) AddEffects(Person(ruler));
            foreach (var group in draft.Groups.OrderBy(group => group.Group))
                foreach (int id in group.OfficerIds)
                {
                    if (!Valid(id)) continue;
                    budget = unchecked(budget - Cost(id));
                    slots[group.Group]--;
                    if (!Retained(id, group.Group)) books--;
                    candidates.RemoveAll(person => person.Id == id);
                    output[group.Group].Add(id);
                    AddEffects(Person(id));
                }

            int oldAdvisor = SavedWord(BattleRemakeCurrentForcePolicies.ForceFieldKey(force.Id, 0x52));
            int advisor = draft.AdvisorId ?? 0;
            if (advisor == oldAdvisor)
            {
                int minimum = GovernanceParameter(state, GovernanceRosterParameters.AdvisorIntelligenceMinimum)
                    ?? throw GovernanceMissing("auto.advisor_minimum");
                var advisors = candidates.Where(person => person.Intelligence >= minimum).ToList();
                foreach (var person in advisors) if (person.Id == advisor) person.Priority = 100;
                advisors.Sort(CompareAdvisor);
                advisor = advisors.Count == 0 ? 0 : advisors[0].Id;
                foreach (var person in advisors) person.Priority = 0;
            }
            if (RosterNeed(TacticRemakeCurrentBattleControl.Read(tutorialControl, 75), "auto.tutorial_control75"))
            {
                //570850/570840 are the real tutorial controller and bitmap.
                //Manager5F0 is logical lookup147 (158 +147*8), rather than
                //a physical Person-table slot or a hardcoded result identity.
                int physical = RosterNeed(current.ReadPersonLookupReference(147)?.PhysicalSlot,
                    "auto.tutorial_advisor_pointer");
                advisor = RosterNeed(current.ReadPersonNativeValidity(physical), "auto.tutorial_advisor_validity")
                    ? RosterNeed(current.ReadPersonIdentity(physical), "auto.tutorial_advisor_identity") : 0;
            }
            if (Valid(advisor))
            {
                candidates.RemoveAll(person => person.Id == advisor);
                AddEffects(Person(advisor));
                if (advisor != oldAdvisor) books--;
            }
            else advisor = 0;

            var special = new List<AutomaticPolicyPerson>[] { [], [], [], [] };
            var ordinary = new List<AutomaticPolicyPerson>();
            foreach (var person in candidates)
            {
                int first = -1;
                foreach (int effect in person.RawEffects)
                {
                    int index = Array.IndexOf(PriorityEffects, effect);
                    if (index < 0) continue;
                    person.Priority = unchecked(person.Priority + 1000);
                    if (first < 0) { first = index; special[index].Add(person); }
                }
                if (first < 0) ordinary.Add(person);
            }
            for (int index = 0; index < PriorityEffects.Length; index++)
            {
                int effect = PriorityEffects[index];
                if (effect == 14 && !useEffect14) { ReturnOrdinary(special[index], ordinary, null); continue; }
                var byLevel = special[index].ToList(); byLevel.Sort(CompareLevel);
                var byCoverage = special[index].ToList(); byCoverage.Sort(CompareCoverage);
                int a = SnapLevel(effect, Simulate(byLevel, effect));
                int b = SnapLevel(effect, Simulate(byCoverage, effect));
                int target = Math.Max(a, b);
                var selected = a <= b ? byCoverage : byLevel;
                var used = new HashSet<AutomaticPolicyPerson>();
                int at = 0;
                for (; at < selected.Count; at++)
                {
                    var person = selected[at];
                    if (books < 1 || target < unchecked(person.Contribution + levels[effect]) ||
                        slots[person.Group] < 1 || budget < person.Cost) break;
                    Apply(person); used.Add(person);
                }
                int gap = unchecked(target - levels[effect]);
                if (gap > 0)
                {
                    var closing = selected.Skip(at).Where(person => gap <= person.Contribution).ToList();
                    closing.Sort(CompareClosing);
                    foreach (var person in closing)
                        if (books > 0 && slots[person.Group] > 0 && person.Cost <= budget)
                        { Apply(person); used.Add(person); break; }
                }
                ReturnOrdinary(selected, ordinary, used);
            }

            ordinary.Sort(CompareCoverage);
            var installed = new HashSet<AutomaticPolicyPerson>();
            foreach (var person in ordinary)
            {
                if (books < 1) break;
                if (person.Cost > budget || slots[person.Group] < 1) continue;
                bool useful = person.EffectCount < 2
                    ? SingleEffectFits(person)
                    : person.Effects.Any(effect => levels[effect] < 10);
                if (useful) { Apply(person); installed.Add(person); }
            }
            if (books > 0)
            {
                ordinary.RemoveAll(installed.Contains);
                ordinary.Sort(CompareCost);
                foreach (var person in ordinary)
                {
                    if (books < 1) break;
                    if (person.Cost <= budget && slots[person.Group] > 0 && person.Effects.Any(effect => levels[effect] < 10))
                        Apply(person);
                }
            }
            return new(advisor == 0 ? null : advisor,
                Enumerable.Range(0, 5).Select(group => new GovernanceDepartmentConfiguration(group, output[group].ToArray())).ToArray());
        }

        private int Simulate(List<AutomaticPolicyPerson> candidates, int effect)
        {
            int sum = levels[effect], cost = 0, remainingBooks = books;
            var counts = new int[5];
            foreach (var person in candidates)
            {
                int nextCost = unchecked(cost + person.Cost);
                // The comparison really is strict here. Actual installation
                // below admits equality; this is the original plan estimator.
                if (budget <= nextCost || sum > 9) break;
                if (counts[person.Group] >= slots[person.Group] || remainingBooks <= 0) continue;
                sum = unchecked(sum + person.Contribution); cost = nextCost; counts[person.Group]++;
                if (!Retained(person.Id, person.Group)) remainingBooks--;
            }
            return sum;
        }

        private void Apply(AutomaticPolicyPerson person)
        {
            AddEffects(person); budget = unchecked(budget - person.Cost);
            output[person.Group].Add(person.Id); slots[person.Group]--;
            if (!Retained(person.Id, person.Group)) books--;
        }
        private bool Retained(int id, int group) => originalGroups[group].Any(old => Valid(old) && Identity(old) == id);
        private bool SingleEffectFits(AutomaticPolicyPerson person)
        {
            int effect = person.RawEffects[0];
            if ((uint)effect >= (uint)levels.Length) throw GovernanceMissing("auto.single_effect_index");
            return unchecked(levels[effect] + person.Contribution) < 11;
        }
        private void AddEffects(AutomaticPolicyPerson person)
        { foreach (int effect in person.Effects) levels[effect] = unchecked(levels[effect] + person.Contribution); }
        private static void ReturnOrdinary(IEnumerable<AutomaticPolicyPerson> candidates,
            List<AutomaticPolicyPerson> ordinary, HashSet<AutomaticPolicyPerson>? used)
        {
            foreach (var person in candidates)
                if (used?.Contains(person) != true && person.RawEffects.Any(effect => effect is >= 1 and <= 100 && !PriorityEffects.Contains(effect)))
                { person.Priority = 0; ordinary.Add(person); }
        }

        private int SnapLevel(int effect, int sum)
        {
            // Required facility levels come from the installed fixed resource,
            // with its normal override/cache behavior, rather than guessed caps.
            int[] thresholds = effect switch
            {
                13 => [FacilityLevel(6), FacilityLevel(4), FacilityLevel(7), FacilityLevel(10)],
                14 => [10, 5],
                24 => [FacilityLevel(15), FacilityLevel(17), FacilityLevel(16)],
                19 => [FacilityLevel(12), FacilityLevel(13)],
                _ => throw GovernanceMissing("auto.priority_effect")
            };
            foreach (int threshold in thresholds) if (sum >= threshold) return threshold;
            return 0;
        }
        private int FacilityLevel(int slot)
        {
            var pointer = current.ReadObjectDefinitionReference(slot) ?? throw GovernanceMissing("auto.facility_pointer");
            return (current.ReadFacilityConstructionDefinition(pointer) ??
                throw GovernanceMissing("auto.facility_definition")).RequiredPolicyLevel;
        }

        private bool SharesFrontierProvince()
        {
            // Each geographical group independently needs both an owned city
            // and a valid foreign city's hostile relation. A foreign city seen
            // first is retained until the owned city is encountered.
            foreach (int[] provinces in new int[][] { [3, 4, 7, 9, 0], [5, 6, 12, 14, 15] })
            {
                bool own = false, foreign = false;
                foreach (var entry in current.ReadRegistryReferences(RegistryKind.City) ?? throw GovernanceMissing("auto.city_registry90"))
                {
                    if (entry is not { Kind: RegistryKind.City, PhysicalSlot: int id }) throw GovernanceMissing("auto.city_registry_payload");
                    var city = new BaseReference(BaseKind.City, id);
                    if (!RosterNeed(current.ReadBaseNativeValidity(city), "auto.city_validity")) continue;
                    int province = RosterNeed(owner._growthQueries.ReadCityState(state, id), "auto.city_provinceCE");
                    if (!provinces.Contains(province)) continue;
                    int raw = RosterNeed(current.ReadBaseForceSlot(city), "auto.city_force");
                    if (raw == force.Id) own = true;
                    else
                    {
                        var other = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw GovernanceMissing("auto.other_force_pointer");
                        if (RosterNeed(current.ReadForceNativeValidity(other), "auto.other_force_validity"))
                        {
                            int otherId = RosterNeed(other.PhysicalSlot, "auto.other_force_receiver");
                            foreign = DLevel(state, DRelations(state, force.Id)[otherId]) < 2;
                        }
                    }
                    if (own && foreign) return true;
                }
            }
            return false;
        }

        private AutomaticPolicyPerson Person(int id)
        {
            if (people.TryGetValue(id, out var saved)) return saved;
            int physical = Physical(id);
            var person = state.Officers.GetValueOrDefault(id) ?? throw GovernanceMissing("auto.person_owner");
            int raw = SavedByte(BattleRemakeCurrentForcePolicies.PersonFieldKey(id, 0x185));
            int policy = RosterNeed(current.ReadStaticTableReference(0x78D50, raw < 121 ? raw : 0)?.PhysicalSlot, "auto.policy_pointer");
            bool validPolicy = policy != 0;
            int group = validPolicy ? state.Policies.GetValueOrDefault(policy)?.DepartmentCode ??
                throw GovernanceMissing("auto.policy_definition") : 0;
            if (group is < 0 or > 4) throw GovernanceMissing("auto.policy_department");
            int[] effects = validPolicy ? Enumerable.Range(0, 8).Select(index =>
                SavedByte(BattleRemakeCurrentForcePolicies.PolicyEffectSlotKey(policy, index))).ToArray() : new int[8];
            var result = new AutomaticPolicyPerson(id, group, validPolicy, effects,
                () => Cost(id), () => Math.Min(unchecked(SavedByte(BattleRemakeCurrentForcePolicies.PersonFieldKey(id, 0x1BA)) +
                    CreditContribution(physical)), 10),
                () => abilities.ResolveDimension(state, id, 2) ?? throw GovernanceMissing("auto.advisor_intelligence"),
                () => RosterNeed(owner._governorInputs.ReadCurrentPhysicalWord166(state, physical), "auto.person_entry_year166"));
            people.Add(id, result); return result;
        }
        private int CreditContribution(int physical)
        {
            //261FF0's inline kind3 queries suppress credit whenever the
            //controller has a selected lesson. They do not test its active
            //header or substitute the gameplay world's independent control.
            if (selectedLesson.Value != -1) return 0;
            return OfficerCommandCapacity.ResolveCreditKindBonus(state, current, physical, 3).Bonus
                ?? throw GovernanceMissing("auto.credit_kind3");
        }
        private int Cost(int id)
        {
            if (!Valid(id)) return 1;
            int raw = SavedByte(BattleRemakeCurrentForcePolicies.PersonFieldKey(id, 0x185));
            if (raw is 0 or > 120) return 1;
            return PolicyCosts.TryCalculate(state, id, out var cost) ? cost.Total : throw GovernanceMissing("auto.policy_cost");
        }
        private int Doctrine(int id)
        {
            var person = state.Officers.GetValueOrDefault(id) ?? throw GovernanceMissing("auto.ruler_owner");
            return RosterNeed(OfficerDoctrineRules.ReadDoctrineId(person.DoctrineId, person.AbilityState), "auto.ruler_doctrine");
        }
        private int[]? DoctrineRow(int raw)
        {
            int physical = RosterNeed(current.ReadStaticTableReference(0x78CF8, raw < 7 ? raw : 0)?.PhysicalSlot, "auto.doctrine_pointer");
            if (physical == 0) return null;
            var row = owner._scenario.Snapshot.DoctrineParameters?.Doctrines.FirstOrDefault(row => row.Slot == physical)
                ?? throw GovernanceMissing("auto.doctrine_definition");
            if (row.RawBytes.Length != 102) throw GovernanceMissing("auto.doctrine_record");
            return row.RawBytes.Select(value => (int)value).ToArray();
        }
        private int Physical(int id) => (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
            ?.PhysicalSlot ?? throw GovernanceMissing("auto.person_pointer");
        private int Identity(int id) => RosterNeed(current.ReadPersonIdentity(Physical(id)), "auto.person_id");
        private bool Valid(int id) => RosterNeed(current.ReadPersonNativeValidity(Physical(id)), "auto.person_validity");
        private int SavedWord(string key) => state.NativeArmySimulation?.Parameters.Int32Values.TryGetValue(key, out int value) == true &&
            value is >= 0 and <= 65535 ? value : throw GovernanceMissing("auto.saved_word:" + key);
        private int SavedByte(string key)
        { int value = SavedWord(key); return value <= 255 ? value : throw GovernanceMissing("auto.saved_byte:" + key); }

        private static int CompareAdvisor(AutomaticPolicyPerson a, AutomaticPolicyPerson b)
        {
            int c = b.Intelligence.CompareTo(a.Intelligence); if (c != 0) return c;
            c = b.Priority.CompareTo(a.Priority); if (c != 0) return c;
            c = a.Seniority.CompareTo(b.Seniority); return c != 0 ? c : a.Id.CompareTo(b.Id);
        }
        private static int CompareLevel(AutomaticPolicyPerson a, AutomaticPolicyPerson b)
        {
            int c = b.Priority.CompareTo(a.Priority); if (c != 0) return c;
            c = b.Preference.CompareTo(a.Preference); if (c != 0) return c;
            c = b.Contribution.CompareTo(a.Contribution); if (c != 0) return c;
            c = b.EffectCount.CompareTo(a.EffectCount); return c != 0 ? c : CompareCost(a, b);
        }
        private static int CompareCoverage(AutomaticPolicyPerson a, AutomaticPolicyPerson b)
        {
            int c = b.Priority.CompareTo(a.Priority); if (c != 0) return c;
            c = b.Preference.CompareTo(a.Preference); if (c != 0) return c;
            c = unchecked(b.Contribution * b.EffectCount).CompareTo(unchecked(a.Contribution * a.EffectCount));
            return c != 0 ? c : CompareCost(a, b);
        }
        private static int CompareClosing(AutomaticPolicyPerson a, AutomaticPolicyPerson b)
        {
            int c = b.Priority.CompareTo(a.Priority); if (c != 0) return c;
            c = unchecked(b.Contribution * b.EffectCount).CompareTo(unchecked(a.Contribution * a.EffectCount)); if (c != 0) return c;
            c = b.EffectCount.CompareTo(a.EffectCount); if (c != 0) return c;
            if (a.EffectCount < 2 || b.EffectCount < 2)
            { c = a.Cost.CompareTo(b.Cost); if (c != 0) return c; }
            c = b.Contribution.CompareTo(a.Contribution); if (c != 0) return c;
            c = b.Preference.CompareTo(a.Preference); if (c != 0) return c;
            return a.EffectCount < 2 || b.EffectCount < 2 ? a.Id.CompareTo(b.Id) : CompareCost(a, b);
        }
        private static int CompareCost(AutomaticPolicyPerson a, AutomaticPolicyPerson b)
        { int c = Math.Max(a.Cost, 1).CompareTo(Math.Max(b.Cost, 1)); return c != 0 ? c : a.Id.CompareTo(b.Id); }
    }

    private sealed class AutomaticPolicyPerson(int id, int group, bool hasPolicy, int[] rawEffects,
        Func<int> cost, Func<int> contribution, Func<int> intelligence, Func<int> seniority)
    {
        private readonly Lazy<int> costValue = new(cost), contributionValue = new(contribution),
            intelligenceValue = new(intelligence), seniorityValue = new(seniority);
        internal int Id { get; } = id;
        internal int Group { get; } = group;
        internal bool HasPolicy { get; } = hasPolicy;
        internal int[] RawEffects { get; } = rawEffects;
        internal int[] Effects { get; } = rawEffects.TakeWhile(effect => effect is >= 1 and <= 100).ToArray();
        internal int EffectCount => Effects.Length;
        internal int Priority { get; set; }
        internal int Preference { get; set; }
        internal int Cost => costValue.Value;
        internal int Contribution => contributionValue.Value;
        internal int Intelligence => intelligenceValue.Value;
        internal int Seniority => seniorityValue.Value;
    }
}
