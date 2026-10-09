using System.Text.Json;
using San14.Core;

namespace San14.Data;

public enum OriginalPersonNormalArmyRewardOutcome { Committed, NativeSkip, CurrentInputsUnresolved }
public sealed record OriginalPersonNormalArmyRewardRequests(int Kind, int Amount, int LeadershipGrowth, int PendingMerit);
public sealed record OriginalPersonNormalArmyRewardReceipt(int OfficerId, int? ArmyId,
    OriginalPersonNormalArmyRewardRequests Requests, OriginalPersonGrowthOutcome GrowthOutcome,
    OriginalPersonPendingMeritOutcome PendingOutcome, OriginalPersonGrowthReceipt? Growth,
    OriginalPersonPendingMeritReceipt? Pending, string Revision);
public sealed record OriginalPersonNormalArmyRewardResult(OriginalPersonNormalArmyRewardOutcome Outcome,
    GameState State, OriginalPersonNormalArmyRewardReceipt? Receipt, string? MissingPhase);

/// <summary>Actual SC2B9110/TC2B9290 Person receiver, current Army getter and
/// manager gate, followed by leadership growth then pending merit. The two
/// independently implemented native callbacks commit as one transaction.</summary>
public sealed class OriginalPersonNormalArmyRewardSource
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
    private OriginalPersonNormalArmyRewardSource(LoadedScenario source, string language, string ruleVersion, int[] initial)
        => (_source, _language, _ruleVersion, _initial) = (source, language, ruleVersion, initial);

    public static OriginalPersonNormalArmyRewardSource Load(OriginalGameRepository repository, LoadedScenario source, string language)
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

    public OriginalPersonNormalArmyRewardResult TryApply(GameState input, OriginalEventInitializationContext definitions,
        OriginalTacticRemakeCurrentContext current, OriginalOwnedArmyCampaignRules package,
        int personId, int kind, int amount) => Apply(input, definitions, current, package, personId, kind, amount, true);

    internal OriginalPersonNormalArmyRewardResult TryApplyWithinArmyOperation(GameState input, OriginalEventInitializationContext definitions,
        OriginalTacticRemakeCurrentContext current, OriginalOwnedArmyCampaignRules package,
        int personId, int kind, int amount) => Apply(input, definitions, current, package, personId, kind, amount, false);

    private OriginalPersonNormalArmyRewardResult Apply(GameState input, OriginalEventInitializationContext definitions,
        OriginalTacticRemakeCurrentContext current, OriginalOwnedArmyCampaignRules package,
        int personId, int kind, int amount, bool validateWorld)
    {
        if (!Bound(input) || !current.IsBoundCurrentState(input) || !input.Officers.TryGetValue(personId, out var person) ||
            person.NativeAbilityRuntime is not { } ability || ability.Language != _language || ability.RuleVersion != _ruleVersion ||
            ability.Revision != OriginalTacticRemakeCurrentAbilities.ProfileId ||
            ability.SourceScenarioUri != _source.ScenarioSource.ResourceUri || ability.SourceScenarioSha256 != _source.ScenarioSource.Sha256 ||
            definitions.StaticSource?.ResourceUri != ability.SourceScenarioUri || definitions.StaticSource.Sha256 != ability.SourceScenarioSha256 ||
            definitions.ReadPersonLookupReference(personId)?.PhysicalSlot != ability.PhysicalSlot ||
            definitions.ReadPersonIdentity(ability.PhysicalSlot) != personId ||
            definitions.ReadPersonNativeValidity(ability.PhysicalSlot) is not bool valid)
            return Unknown("receiver");
        if (!valid) return Skip();
        if (definitions.ReadPersonFirstLocation(ability.PhysicalSlot) is not ushort first) return Unknown("first-location");
        if (first is >= 1 and <= 61)
        {
            if (definitions.ReadPersonForce(ability.PhysicalSlot)?.PhysicalSlot is not int force) return Unknown("special-force");
            if (force is >= 46 and <= 50) return Skip();
            if (new[] { 4111, 4131, 4151, 4171, 4191 }.Any(start => unchecked((uint)(personId - start)) <= 9)) return Skip();
        }
        //22A490(Person,1) decodes the independent saved second-location
        //word, selects the native Army slot and compares Commander12. A Core
        //Deployed flag cannot supply the missing source writer.
        if (person.NativeLocationRuntime?.Second11C is not ushort second ||
            definitions.ReadPersonSecondLocation(ability.PhysicalSlot) != second) return Unknown("second-location");
        OriginalNativeLocation location = OriginalPersonPositionRepair.DecodeSecondLocation(second);
        if (location.Kind != 27) return Skip(); //Real table0 invalid Army when second is not Army kind.
        UnitState[] matches = input.Units.Values.Where(u => u.NativeArmyOrigin?.NativeSlotIndex == location.Index).ToArray();
        if (matches.Length != 1) return Unknown("army-slot");
        UnitState army = matches[0];
        if (army.OfficerId != personId) return Skip(); //Real getter falls back to invalid Army0.
        if (current.ArmyValid(input, army) is not bool armyValid) return Unknown("army-validity");
        if (!armyValid) return Skip();
        if (current.ArmyManagerExcluded(input, army.Id) is not bool excluded) return Unknown("army-manager");
        if (excluded) return Skip();
        if (kind is < 0 or > 3) return Skip();
        if (ReadRequests(input, kind, amount) is not { } requests) return Unknown("request-parameters");
        return ApplyQualifiedRequests(input, personId, army.Id, requests,
            validateWorld ? package.TryApplyNativePersonGrowth : package.ApplyPersonGrowthDuringArmyOperation,
            validateWorld ? package.TryApplyNativePendingMerit : package.ApplyPendingMeritDuringArmyOperation,
            $"{ProfileId}:{input.NativeArmySimulation!.Parameters.Revision}", validateWorld);
        OriginalPersonNormalArmyRewardResult Unknown(string phase) => new(OriginalPersonNormalArmyRewardOutcome.CurrentInputsUnresolved, input, null, phase);
        OriginalPersonNormalArmyRewardResult Skip() => new(OriginalPersonNormalArmyRewardOutcome.NativeSkip, input, null, null);
    }

    /// <summary>Internal callback transaction for an already resolved native
    /// receiver and requests. It does not certify upstream Army eligibility.
    /// Scratch state protects the caller even when an explicit callback rejects
    /// after modifying its draft. Growth always precedes pending credit.</summary>
    public static OriginalPersonNormalArmyRewardResult TryApplyQualifiedRequests(GameState input, int personId, int? armyId,
        OriginalPersonNormalArmyRewardRequests requests,
        Func<GameState, int, int, int, int, OriginalPersonGrowthResult> growth,
        Func<GameState, int, int, OriginalPersonPendingMeritResult> pending, string revision) =>
        ApplyQualifiedRequests(input, personId, armyId, requests, growth, pending, revision, true);

    private static OriginalPersonNormalArmyRewardResult ApplyQualifiedRequests(GameState input, int personId, int? armyId,
        OriginalPersonNormalArmyRewardRequests requests,
        Func<GameState, int, int, int, int, OriginalPersonGrowthResult> growth,
        Func<GameState, int, int, OriginalPersonPendingMeritResult> pending, string revision, bool validateWorld)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(growth); ArgumentNullException.ThrowIfNull(pending);
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Qualified normal reward needs source revision.");
        GameState draft = JsonSerializer.Deserialize<GameState>(JsonSerializer.SerializeToUtf8Bytes(input))
            ?? throw new InvalidDataException("Normal reward state could not be detached.");
        var first = growth(draft, personId, 0, requests.LeadershipGrowth, 0);
        if (first.Outcome == OriginalPersonGrowthOutcome.CurrentInputsUnresolved) return Unknown("growth");
        var second = pending(first.State, personId, requests.PendingMerit);
        if (second.Outcome == OriginalPersonPendingMeritOutcome.CurrentInputsUnresolved) return Unknown("pending-merit");
        if (validateWorld) StateValidator.Validate(second.State);
        var receipt = new OriginalPersonNormalArmyRewardReceipt(personId, armyId, requests, first.Outcome, second.Outcome,
            first.Receipt, second.Receipt, revision);
        bool wrote = first.Outcome == OriginalPersonGrowthOutcome.Committed || second.Outcome == OriginalPersonPendingMeritOutcome.Committed;
        return new(wrote ? OriginalPersonNormalArmyRewardOutcome.Committed : OriginalPersonNormalArmyRewardOutcome.NativeSkip,
            wrote ? second.State : input, receipt, null);
        OriginalPersonNormalArmyRewardResult Unknown(string phase) => new(OriginalPersonNormalArmyRewardOutcome.CurrentInputsUnresolved, input, null, phase);
    }

    public OriginalPersonNormalArmyRewardRequests? ReadRequests(GameState input, int kind, int amount)
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

    public static OriginalPersonNormalArmyRewardRequests CalculateRequests(int kind, int amount, int growth, int merit)
        => kind switch {
            0 or 1 => new(kind, amount, unchecked(growth * amount) / 100,
                unchecked(unchecked(merit * amount) * 10) / 100),
            2 => new(kind, amount, growth, unchecked(merit * 10)),
            3 => new(kind, amount, growth, 0),
            _ => throw new InvalidDataException("Normal reward kind is outside the source switch.") };
    private bool Bound(GameState input) => input.NativeClock is not null && input.NativeArmySimulation?.Parameters is { } p &&
        p.Language == _language && p.RuleVersion == _ruleVersion &&
        input.ContentHashes.GetValueOrDefault(_source.ScenarioSource.ResourceUri) == _source.ScenarioSource.Sha256 &&
        input.ContentHashes.GetValueOrDefault(_source.FixedDataSource.ResourceUri) == _source.FixedDataSource.Sha256;
}
