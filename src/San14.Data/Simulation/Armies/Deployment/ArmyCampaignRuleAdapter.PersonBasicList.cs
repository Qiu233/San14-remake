using San14.Core;

namespace San14.Data;

/// <summary>A known null is the source's empty value; an unresolved input is
/// unknown. Message text and locale formatting belong to the presenter.</summary>
public sealed record DeploymentPersonBasicValue<T>(T? Value, string? UnresolvedInput) where T : struct
{
    public bool Available => UnresolvedInput is null;
}

public readonly record struct DeploymentPersonServiceStatus(bool Hired, byte? StatusCode);
public enum DeploymentPersonGovernorPriority { Empty, Marked, NotApplicable }

public sealed record DeploymentPersonBasicRow(DeploymentPersonCandidate Candidate,
    OfficerCommandCapacityResult CommandCapacity, OfficerCommandCapacityResult SortCommandCapacity,
    DeploymentPersonBasicValue<bool> CommandEmphasized,
    OfficerAbilities? Abilities, string? AbilitiesUnresolvedInput,
    DeploymentPersonBasicValue<byte> HealthCode, DeploymentPersonBasicValue<bool> IsCommandMember,
    DeploymentPersonBasicValue<DeploymentPersonServiceStatus> ServiceStatus,
    DeploymentPersonBasicValue<byte> CreditRankStage,
    DeploymentPersonBasicValue<DeploymentPersonGovernorPriority> GovernorPriority,
    DeploymentPersonBasicValue<DeploymentPersonResponsibility> Responsibility);

public sealed record DeploymentPersonBasicListQuery(IReadOnlyList<DeploymentPersonBasicRow>? Rows,
    int? MaximumSelection, string? UnresolvedInput)
{
    public bool Available => Rows is not null && MaximumSelection.HasValue && UnresolvedInput is null;
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One current capture for ordered candidates and all basic cells.
    /// Cell failures remain independent; no saved world or source is changed.</summary>
    public DeploymentPersonBasicListQuery QueryDeploymentPersonBasicList(GameState state, int locationId, int outside)
        => QueryPersonBasicList(state, locationId, outside, transport: false);

    private DeploymentPersonBasicListQuery QueryPersonBasicList(GameState state, int locationId, int outside, bool transport)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            var current = CaptureDeploymentPersonSelectionCurrent(state);
            var selection = transport ? BuildTransportPersonSelection(state, current, locationId)
                : BuildDeploymentPersonSelection(state, current, locationId, outside);
            _moraleWaveInputs.BindCurrent(state, current);
            _baseCapacityInputs.BindCurrent(state, current);
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
                SimulationRuleIdentity.ForLocale(Seed.Language));
            var rows = new List<DeploymentPersonBasicRow>();
            foreach (var candidate in selection.Candidates!)
            {
                int physical = candidate.PhysicalSlot;
                OfficerAbilities? effective = abilities.Resolve(state, candidate.OfficerId);
                var capacity = OfficerCommandCapacity.Resolve(state, current, physical);
                var displayCapacity = Cell<int>(() =>
                {
                    var force = current.ReadPersonForce(physical) ?? throw BasicMissing("person_force");
                    return !BasicNeed(current.ReadForceNativeValidity(force), "force_validity") ? 0 :
                        capacity.Capacity ?? throw new InvalidDataException(capacity.UnresolvedInput);
                });
                rows.Add(new(candidate, new(displayCapacity.Value, displayCapacity.UnresolvedInput), capacity,
                    ReadEmphasis(physical), effective, effective is null ? BasicMissing("current_abilities").Message : null,
                    Cell<byte>(() =>
                    {
                        byte health = BasicNeed(OfficerHealthSourceAdapter.ReadByte(current, physical), "health");
                        return health < 4 ? health : null;
                    }), Cell<bool>(() => ReadCommandMembership(physical)),
                    Cell<DeploymentPersonServiceStatus>(() =>
                    {
                        if ((Flags(physical) & 0x4000) != 0) return new(true, null);
                        byte status = Status(physical);
                        return status < 10 ? new(false, status) : null;
                    }), Cell<byte>(() => ReadCreditRank(physical)),
                    Cell<DeploymentPersonGovernorPriority>(() => ReadGovernorPriority(physical)),
                    Cell<DeploymentPersonResponsibility>(() => DeploymentPersonResponsibilityDisplaySource.Read(state, current, physical))));
            }
            //The first list construction uses the source's strict, stable
            //insertion order. Later keys are read only for genuine ties.
            for (int index = 1; index < rows.Count; index++)
            {
                var row = rows[index]; int previous = index - 1;
                while (previous >= 0 && ComesBefore(row, rows[previous]))
                {
                    rows[previous + 1] = rows[previous]; previous--;
                }
                rows[previous + 1] = row;
            }
            return new(rows.AsReadOnly(), selection.MaximumSelection, null);

            bool ComesBefore(DeploymentPersonBasicRow left, DeploymentPersonBasicRow right)
            {
                if (transport) return CompareTransportPersons(current, left.Candidate, right.Candidate) < 0;
                int a = left.SortCommandCapacity.Capacity ?? throw BasicMissing("sort_command_capacity");
                int b = right.SortCommandCapacity.Capacity ?? throw BasicMissing("sort_command_capacity");
                if (a != b) return a > b;
                int first = left.Abilities?.Leadership ?? throw BasicMissing("sort_leadership");
                int second = right.Abilities?.Leadership ?? throw BasicMissing("sort_leadership");
                if (first != second) return first > second;
                string one = state.Officers[left.Candidate.OfficerId].CurrentNameSortKey ?? throw BasicMissing("sort_name_key");
                string two = state.Officers[right.Candidate.OfficerId].CurrentNameSortKey ?? throw BasicMissing("sort_name_key");
                return CompareNameSortKeys(one, two) < 0;
            }

            byte Status(int p) => BasicNeed(current.ReadPersonStatus(p), "service_status");
            ushort Flags(int p) => BasicNeed(current.ReadArmyReturnPersonFlags196(p), "person_flags");
            int Force(int p) => BasicNeed(current.ReadPersonForce(p)?.PhysicalSlot, "person_force");
            bool SpecialPerson(int p)
            {
                int id = BasicNeed(current.ReadPersonIdentity(p), "person_identity");
                if (!InSpecialBand(id)) return false;
                if (BasicNeed(current.ReadPersonFirstLocation(p), "first_location") is not (>= 1 and <= 61)) return false;
                return Force(p) is not (>= 46 and <= 50);
            }
            bool ReadCommandMembership(int p) =>
                ReadDeploymentCommandMembership(state, current, p, BasicMissing);
            byte ReadCreditRank(int p)
            {
                if (Status(p) == 1 || (Flags(p) & 0x4000) != 0 || SpecialPerson(p)) return 0;
                int id = BasicNeed(current.ReadPersonIdentity(p), "person_identity");
                byte stage = BasicNeed(OfficerMeritSourceAdapter.EncodeStage(state.Officers.GetValueOrDefault(id)?.MeritState), "credit_stage");
                if (stage is < 1 or > 6) return 0;
                if (state.CampaignEventWorld?.CurrentUserIndex != -1) throw BasicMissing("credit_current_user_gate");
                if (state.CampaignEventWorld.ScenarioMode == 3) throw BasicMissing("credit_person_selection_mode");
                return stage;
            }
            DeploymentPersonGovernorPriority ReadGovernorPriority(int p)
            {
                //Either current branch independently proves the same mark.
                byte? status = current.ReadPersonStatus(p), priority = _governorInputs.ReadCurrentPriority160(state, p);
                if (status is 1 or 2 || (priority & 1) != 0 && priority.HasValue)
                    return DeploymentPersonGovernorPriority.Marked;
                _ = BasicNeed(status, "service_status"); _ = BasicNeed(priority, "governor_priority");
                return Force(p) is >= 46 and <= 50 || SpecialPerson(p) || (Flags(p) & 0x4000) != 0
                    ? DeploymentPersonGovernorPriority.NotApplicable : DeploymentPersonGovernorPriority.Empty;
            }
            DeploymentPersonBasicValue<bool> ReadEmphasis(int p)
            {
                var credit = OfficerCommandCapacity.ResolveCreditBonus(state, current, p);
                var policy = Cell<bool>(() =>
                {
                    var reference = current.ReadPersonForce(p) ?? throw BasicMissing("person_force");
                    if (!BasicNeed(current.ReadForceNativeValidity(reference), "force_validity")) return false;
                    int force = BasicNeed(reference.PhysicalSlot, "force_pointer");
                    if (!BasicNeed(state.Forces.GetValueOrDefault(force)?.PolicyEffects?.IsEnabled(0x37), "reform_activation")) return false;
                    return BasicNeed(_baseCapacityInputs.ReadPanelModifier(state, force, 0x37), "active_reform_effect") > 0;
                });
                var trait = Cell<bool>(() => !BasicNeed(current.ReadPersonNativeValidity(p), "person_validity") ? false :
                    BasicNeed(TacticRemakeCurrentTraits.CommanderEffectSum(current, p, 0x4D), "current_personalities") > 0);
                if (credit.Bonus > 0 || policy.Value == true || trait.Value == true) return new(true, null);
                string? unresolved = credit.UnresolvedInput ?? policy.UnresolvedInput ?? trait.UnresolvedInput;
                return unresolved is null ? new(false, null) : new(null, unresolved);
            }
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    private static DeploymentPersonBasicValue<T> Cell<T>(Func<T?> read) where T : struct
    {
        try { return new(read(), null); }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }
    private static int CompareNameSortKeys(string left, string right) => PersonNameOrder.Compare(left, right);
    private static bool InSpecialBand(int id) => unchecked((uint)(id - 4111)) <= 9 ||
        unchecked((uint)(id - 4131)) <= 9 || unchecked((uint)(id - 4151)) <= 9 ||
        unchecked((uint)(id - 4171)) <= 9 || unchecked((uint)(id - 4191)) <= 9;
    private static T BasicNeed<T>(T? value, string owner) where T : struct => value ?? throw BasicMissing(owner);
    private static InvalidDataException BasicMissing(string owner) => new($"deployment.person-basic.{owner}");
}
