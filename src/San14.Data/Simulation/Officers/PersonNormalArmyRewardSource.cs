using San14.Core;

namespace San14.Data;

public enum PersonNormalArmyRewardOutcome { Committed, NativeSkip, CurrentInputsUnresolved }
public sealed record PersonNormalArmyRewardRequests(int Kind, int Amount, int LeadershipGrowth, int PendingMerit);
public sealed record PersonNormalArmyRewardReceipt(int OfficerId, int? ArmyId,
    PersonNormalArmyRewardRequests Requests, PersonGrowthOutcome GrowthOutcome,
    PersonPendingMeritOutcome PendingOutcome, PersonGrowthReceipt? Growth,
    PersonPendingMeritReceipt? Pending, string Revision);
public sealed record PersonNormalArmyRewardResult(PersonNormalArmyRewardOutcome Outcome,
    GameState State, PersonNormalArmyRewardReceipt? Receipt, string? MissingPhase);

/// <summary>Actual SC2B9110/TC2B9290 Person receiver, current Army getter and
/// manager gate, followed by leadership growth then pending merit. The two
/// independently implemented native callbacks commit as one transaction.</summary>
public sealed class PersonNormalArmyRewardSource
{
    public const string ProfileId = "san14:ce-owned-normal-person-reward-20261003";
    public const string VersionKey = "ce-normal-person-reward-current:version";
    private const int KindZeroLeadershipPercent = 50;
    private const int KindZeroPendingMeritPercent = 4;
    private const int KindOneLeadershipPercent = 640;
    private const int KindOnePendingMeritPercent = 18;
    private const int KindTwoLeadershipGrowth = 750;
    private const int KindTwoPendingMerit = 100;
    private const int KindThreeLeadershipGrowth = 2000;
    private static readonly string[] ParameterKeys = [SimulationParameterKeys.PersonCombatRewards.KindZeroLeadershipPercent, SimulationParameterKeys.PersonCombatRewards.KindZeroPendingMeritPercent, SimulationParameterKeys.PersonCombatRewards.KindOneLeadershipPercent, SimulationParameterKeys.PersonCombatRewards.KindOnePendingMeritPercent, SimulationParameterKeys.PersonCombatRewards.KindTwoLeadershipGrowth, SimulationParameterKeys.PersonCombatRewards.KindTwoPendingMerit, SimulationParameterKeys.PersonCombatRewards.KindThreeLeadershipGrowth];
    private readonly LoadedScenario _source;
    private readonly string _language, _ruleVersion;
    private readonly int[] _initial;
    private PersonNormalArmyRewardSource(LoadedScenario source, string language, string ruleVersion, int[] initial)
        => (_source, _language, _ruleVersion, _initial) = (source, language, ruleVersion, initial);

    public static PersonNormalArmyRewardSource Load(GameResourceRepository repository, LoadedScenario source, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        string ruleVersion = SimulationRuleIdentity.ForLocale(language);
        if (source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale != language)
            throw new InvalidDataException("Normal Person reward source is not the selected CE scenario.");
        int[] values = [KindZeroLeadershipPercent, KindZeroPendingMeritPercent, KindOneLeadershipPercent,
            KindOnePendingMeritPercent, KindTwoLeadershipGrowth, KindTwoPendingMerit, KindThreeLeadershipGrowth];
        return new(source, language, ruleVersion, values);
    }

    public GameState InstallFresh(GameState input)
    {
        if (!Bound(input) || input.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || input.Units.Count != 0 ||
            input.NativeArmySimulation!.Parameters.Int32Values.ContainsKey(VersionKey) || ParameterKeys.Any(key =>
                input.NativeArmySimulation.Parameters.Int32Values.ContainsKey(key) ||
                input.NativeArmySimulation.Parameters.Float32Values.ContainsKey(key)))
            throw new InvalidDataException("Fresh normal reward cannot replace a saved current parameter bank.");
        var simulation = input.NativeArmySimulation!;
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        for (int i = 0; i < ParameterKeys.Length; i++) values.Add(ParameterKeys[i], _initial[i]);
        return input with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public PersonNormalArmyRewardResult TryApply(GameState input, SourceWorldContext definitions,
        TacticRemakeCurrentContext current, ArmyCampaignRuleAdapter package,
        int personId, int kind, int amount) => Apply(input, definitions, current, package, personId, kind, amount, true);

    internal PersonNormalArmyRewardResult TryApplyWithinArmyOperation(GameState input, SourceWorldContext definitions,
        TacticRemakeCurrentContext current, ArmyCampaignRuleAdapter package,
        int personId, int kind, int amount) => Apply(input, definitions, current, package, personId, kind, amount, false);

    private PersonNormalArmyRewardResult Apply(GameState input, SourceWorldContext definitions,
        TacticRemakeCurrentContext current, ArmyCampaignRuleAdapter package,
        int personId, int kind, int amount, bool detachCallbacks)
    {
        if (!Bound(input) || !current.IsBoundCurrentState(input) || !input.Officers.TryGetValue(personId, out var person) ||
            definitions.StaticSource?.ResourceUri != _source.ScenarioSource.ResourceUri ||
            definitions.StaticSource.Sha256 != _source.ScenarioSource.Sha256 ||
            definitions.ResolveOfficerSource(personId) is not int physical ||
            definitions.ReadPersonNativeValidity(physical) is not bool valid)
            return Unknown("receiver");
        if (!valid) return Skip();
        if (definitions.ReadPersonFirstLocation(physical) is not ushort first) return Unknown("first-location");
        if (first is >= 1 and <= 61)
        {
            if (definitions.ReadPersonForce(physical)?.PhysicalSlot is not int force) return Unknown("special-force");
            if (force is >= 46 and <= 50) return Skip();
            if (new[] { 4111, 4131, 4151, 4171, 4191 }.Any(start => unchecked((uint)(personId - start)) <= 9)) return Skip();
        }
        //22A490(Person,1) decodes the independent saved second-location
        //word, selects the native Army slot and compares Commander12. A Core
        //Deployed flag cannot supply the missing source writer.
        var receivers = OfficerLocationSourceAdapter.Receivers(input);
        if (OfficerLocationSourceAdapter.EncodePosition(person.LocationState?.Position, receivers,
                (input.ResourceState as OfficerLocationSourceResidues)?.Find(personId)) is not ushort second ||
            definitions.ReadPersonSecondLocation(physical) != second) return Unknown("second-location");
        if (person.LocationState?.Position is not { Kind: OfficerPositionKind.ArmyReceiver, EntityId: int receiverId })
            return person.LocationState?.Position?.Kind == OfficerPositionKind.Unresolved && second is >= 62 and <= 561
                ? Unknown("army-slot") : Skip();
        var receiver = input.NativeArmyLifecycle?.Slots.SingleOrDefault(slot => slot.ReceiverId.Value == receiverId);
        if (receiver?.ActiveUnitId is not int armyId || !input.Units.TryGetValue(armyId, out UnitState? army)) return Unknown("army-slot");
        if (army.OfficerId != personId) return Skip(); //Real getter falls back to invalid Army0.
        if (current.ArmyValid(input, army) is not bool armyValid) return Unknown("army-validity");
        if (!armyValid) return Skip();
        if (current.ArmyManagerExcluded(input, army.Id) is not bool excluded) return Unknown("army-manager");
        if (excluded) return Skip();
        if (kind is < 0 or > 3) return Skip();
        if (ReadRequests(input, kind, amount) is not { } requests) return Unknown("request-parameters");
        return ApplyQualifiedRequests(input, personId, army.Id, requests,
            package.ApplyPersonGrowthDuringArmyOperation,
            package.ApplyPendingMeritDuringArmyOperation,
            $"{ProfileId}:{input.NativeArmySimulation!.Parameters.Revision}", detachCallbacks);
        PersonNormalArmyRewardResult Unknown(string phase) => new(PersonNormalArmyRewardOutcome.CurrentInputsUnresolved, input, null, phase);
        PersonNormalArmyRewardResult Skip() => new(PersonNormalArmyRewardOutcome.NativeSkip, input, null, null);
    }

    /// <summary>Callback transaction for an already resolved native
    /// receiver and requests. It does not certify upstream Army eligibility.
    /// Each callback borrows the candidate and returns that candidate or a
    /// replacement derived from it; a successful result transfers that state
    /// to the caller. A callback must not retain the candidate for later writes.
    /// Rejection discards candidate changes. External callback side effects
    /// belong to the callback. Growth always precedes pending credit.</summary>
    public static PersonNormalArmyRewardResult TryApplyQualifiedRequests(GameState input, int personId, int? armyId,
        PersonNormalArmyRewardRequests requests,
        Func<GameState, int, int, int, int, PersonGrowthResult> growth,
        Func<GameState, int, int, PersonPendingMeritResult> pending, string revision) =>
        ApplyQualifiedRequests(input, personId, armyId, requests, growth, pending, revision, true);

    private static PersonNormalArmyRewardResult ApplyQualifiedRequests(GameState input, int personId, int? armyId,
        PersonNormalArmyRewardRequests requests,
        Func<GameState, int, int, int, int, PersonGrowthResult> growth,
        Func<GameState, int, int, PersonPendingMeritResult> pending, string revision, bool detachCallbacks)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(growth); ArgumentNullException.ThrowIfNull(pending);
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Qualified normal reward needs source revision.");
        //Public callbacks receive one independent candidate. The actual owned
        //callbacks resolve first and return officer replacements: they cannot
        //mutate this input, so their intermediate growth need not be published
        //when pending merit rejects later.
        GameState draft = detachCallbacks ? GameStateCopy.Copy(input) : input;
        var first = growth(draft, personId, 0, requests.LeadershipGrowth, 0);
        if (first.Outcome == PersonGrowthOutcome.CurrentInputsUnresolved) return Unknown("growth");
        var second = pending(first.State, personId, requests.PendingMerit);
        if (second.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved) return Unknown("pending-merit");
        var receipt = new PersonNormalArmyRewardReceipt(personId, armyId, requests, first.Outcome, second.Outcome,
            first.Receipt, second.Receipt, revision);
        bool wrote = first.Outcome == PersonGrowthOutcome.Committed || second.Outcome == PersonPendingMeritOutcome.Committed;
        return new(wrote ? PersonNormalArmyRewardOutcome.Committed : PersonNormalArmyRewardOutcome.NativeSkip,
            wrote ? second.State : input, receipt, null);
        PersonNormalArmyRewardResult Unknown(string phase) => new(PersonNormalArmyRewardOutcome.CurrentInputsUnresolved, input, null, phase);
    }

    public PersonNormalArmyRewardRequests? ReadRequests(GameState input, int kind, int amount)
    {
        if (!Bound(input) || input.NativeArmySimulation!.Parameters.Int32Values.GetValueOrDefault(VersionKey) != 1 || kind is < 0 or > 3) return null;
        string growthKey = kind switch {
            0 => SimulationParameterKeys.PersonCombatRewards.KindZeroLeadershipPercent,
            1 => SimulationParameterKeys.PersonCombatRewards.KindOneLeadershipPercent,
            2 => SimulationParameterKeys.PersonCombatRewards.KindTwoLeadershipGrowth,
            _ => SimulationParameterKeys.PersonCombatRewards.KindThreeLeadershipGrowth
        };
        string? meritKey = kind switch {
            0 => SimulationParameterKeys.PersonCombatRewards.KindZeroPendingMeritPercent,
            1 => SimulationParameterKeys.PersonCombatRewards.KindOnePendingMeritPercent,
            2 => SimulationParameterKeys.PersonCombatRewards.KindTwoPendingMerit,
            _ => null
        };
        if (Read(growthKey) is not int growth) return null;
        int merit = 0;
        if (meritKey is not null && Read(meritKey) is not int) return null;
        if (meritKey is not null) merit = Read(meritKey)!.Value;
        return CalculateRequests(kind, amount, growth, merit);
        int? Read(string key)
        {
            var p = input.NativeArmySimulation!.Parameters;
            return !p.Float32Values.ContainsKey(key) && p.Int32Values.TryGetValue(key, out int value) ? value : null;
        }
    }

    public static PersonNormalArmyRewardRequests CalculateRequests(int kind, int amount, int growth, int merit)
        => PersonRewardArithmetic.CalculateRequests(kind, amount, growth, merit);
    private bool Bound(GameState input) => input.NativeClock is not null && input.NativeArmySimulation?.Parameters is { } p &&
        p.Language == _language && p.RuleVersion == _ruleVersion &&
        input.ContentHashes.GetValueOrDefault(_source.ScenarioSource.ResourceUri) == _source.ScenarioSource.Sha256 &&
        input.ContentHashes.GetValueOrDefault(_source.FixedDataSource.ResourceUri) == _source.FixedDataSource.Sha256;
}
