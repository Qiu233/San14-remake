using San14.Core;

namespace San14.Data;

public enum PersonPendingMeritOutcome { Committed, NativeSkip, CurrentInputsUnresolved }
public sealed record PersonPendingMeritReceipt(int OfficerId, int Requested, int Trait122,
    int TraitCorrection, int Panel50Correction, int DoctrineCorrection, int Stage1Correction,
    int BeforeScaling, int Earned, int BeforePendingMerit, int AfterPendingMerit, string Revision);
public sealed record PersonPendingMeritResult(PersonPendingMeritOutcome Outcome,
    GameState State, PersonPendingMeritReceipt? Receipt);

/// <summary>Complete SC169320/TC1695C0 pending-word writer. Current doctrine
/// flag8 reaches localized-name validity and deliberately stays unresolved.
/// The separate merit settlement/report callback is not executed here.</summary>
public sealed class PersonPendingMeritSource
{
    public const string ProfileId = "san14:ce-owned-pending-merit-20261003";
    public const string VersionKey = "ce-pending-merit-current:version";
    public const string Panel50Key = "ce-pending-merit-current:panel50:7C";
    private const int TraitPendingMeritScale = 1;
    private const int DoctrinePendingMeritBonusPercent = 10;
    private const int StageOnePendingMeritBonusPercent = 10;
    private const int CreditModeOnePendingMeritPercent = 150;
    private const int CreditModeZeroPendingMeritPercent = 100;
    private static readonly string[] ParameterKeys = [SimulationParameterKeys.PersonMerit.TraitPendingMeritScale, SimulationParameterKeys.PersonMerit.DoctrinePendingMeritBonusPercent, SimulationParameterKeys.PersonMerit.StageOnePendingMeritBonusPercent, SimulationParameterKeys.PersonMerit.CreditModeOnePendingMeritPercent, SimulationParameterKeys.PersonMerit.CreditModeZeroPendingMeritPercent];
    private readonly LoadedScenario _source;
    private readonly string _language, _ruleVersion;
    private readonly int[] _initial;
    private readonly short _panel50;

    private PersonPendingMeritSource(LoadedScenario source, string language, string ruleVersion,
        int[] initial, short panel50) => (_source, _language, _ruleVersion, _initial, _panel50) =
            (source, language, ruleVersion, initial, panel50);

    public static PersonPendingMeritSource Load(GameResourceRepository repository, LoadedScenario source, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        string ruleVersion = SimulationRuleIdentity.ForLocale(language);
        var panels = repository.LoadDoctrinePanels(source.FixedDataSource.SourcePath);
        if (source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale != language ||
            panels.Source.Sha256 != source.FixedDataSource.Sha256 || panels.Catalog.Panels.Count != 80)
            throw new InvalidDataException("Pending merit requires the actual complete CE panel source.");
        var panel = panels.Catalog.Panels.Single(row => row.Slot == 50);
        if (panel.RawBytes.Length != 33)
            throw new InvalidDataException("Pending merit panel50 record has a different schema.");
        return new(source, language, ruleVersion, [TraitPendingMeritScale, DoctrinePendingMeritBonusPercent,
            StageOnePendingMeritBonusPercent, CreditModeOnePendingMeritPercent, CreditModeZeroPendingMeritPercent],
            panel.RawValue);
    }

    /// <summary>One new owned source allocation. Current globals/panel value
    /// are saved independently and never reloaded during a later query.</summary>
    public GameState InstallFresh(GameState state)
    {
        if (!Bound(state) || state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.NativeArmySimulation!.Parameters.Int32Values.ContainsKey(VersionKey) ||
            state.NativeArmySimulation.Parameters.Int32Values.ContainsKey(Panel50Key) ||
            ParameterKeys.Any(key => state.NativeArmySimulation.Parameters.Int32Values.ContainsKey(key) ||
                state.NativeArmySimulation.Parameters.Float32Values.ContainsKey(key)))
            throw new InvalidDataException("Fresh pending merit cannot replace a saved current bank.");
        var simulation = state.NativeArmySimulation!;
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1, [Panel50Key] = _panel50 };
        for (int i = 0; i < ParameterKeys.Length; i++) values.Add(ParameterKeys[i], _initial[i]);
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public PersonPendingMeritResult TryApply(GameState input, SourceWorldContext current,
        int personId, int requested) => Apply(input, current, personId, requested);

    internal PersonPendingMeritResult TryApplyWithinArmyOperation(GameState input, SourceWorldContext current,
        int personId, int requested) => Apply(input, current, personId, requested);

    private PersonPendingMeritResult Apply(GameState input, SourceWorldContext current,
        int personId, int requested)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(current);
        if (!Bound(input) || !input.Officers.TryGetValue(personId, out var person) ||
            current.StaticSource?.ResourceUri != _source.ScenarioSource.ResourceUri ||
            current.StaticSource.Sha256 != _source.ScenarioSource.Sha256 ||
            current.ReadPersonLookupReference(personId)?.PhysicalSlot is not int physical ||
            current.ReadPersonIdentity(physical) != personId ||
            OfficerServiceStatusSourceAdapter.Encode(person.ServiceStatus) is not byte status ||
            person.Behavior?.UsesSpecialPersonRules is not bool ||
            current.ReadPersonStatus(physical) != status ||
            current.ReadPersonBehavior(physical) != person.Behavior)
            return Unknown();
        if (status is 1 or 5) return Skip();
        if (new[] { 4111, 4131, 4151, 4171, 4191 }.Any(start => unchecked((uint)(personId - start)) <= 9))
        {
            if (current.ReadPersonFirstLocation(physical) is not ushort location) return Unknown();
            if (location is >= 1 and <= 61)
            {
                var bandForce = current.ReadPersonForce(physical);
                if (bandForce?.PhysicalSlot is not int bandForceId) return Unknown();
                if (bandForceId is not (>= 46 and <= 50)) return Skip();
            }
        }
        if (OfficerBehaviorRules.UsesOrdinaryPersonRules(person.Behavior) == false || requested <= 0) return Skip();
        var force = current.ReadPersonForce(physical);
        if (force?.PhysicalSlot is not int forceId || person.ForceId != (forceId == 0 ? null : (int?)forceId)) return Unknown();
        if (person.MeritState is not { PendingMerit: int pending } meritState ||
            OfficerMeritRules.IsStageOne(meritState) is not bool stageOne ||
            current.ReadPersonNativeValidity(physical) is not bool valid ||
            current.ReadForceNativeValidity(force) is not bool forceValid ||
            BattleRemakeCurrentSettings.ReadSelectorE58(input) is not int selector ||
            ReadCurrent(input, SimulationParameterKeys.PersonMerit.TraitPendingMeritScale) is not int traitScale)
            return Unknown();
        int trait = 0, policy = 0, doctrine = 0;
        if (valid)
        {
            if (TacticRemakeCurrentTraits.CommanderEffectSum(current, physical, 122) is not int value) return Unknown();
            trait = value;
        }
        if (forceValid)
        {
            if (!input.NativeArmySimulation!.Forces.TryGetValue(forceId, out var savedForce) ||
                savedForce.Flags12 is not byte forceFlags || ReadPanel50(input, input.Forces.GetValueOrDefault(forceId)?.PolicyEffects) is not int panel) return Unknown();
            policy = panel;
            if ((forceFlags & 8) != 0) return Unknown(); //22C230 current doctrine/name validity owner absent.
        }
        int? stageScale = stageOne ? ReadCurrent(input, SimulationParameterKeys.PersonMerit.StageOnePendingMeritBonusPercent) : 0;
        int? scaling;
        if (selector == 0)
        {
            scaling = BattleRemakeCurrentSettings.ReadAdvancedParameter(input, 21);
            if (scaling is null) return Unknown();
        }
        else
        {
            if (BattleRemakeCurrentSettings.ReadInt32(input, 0x124) is not int mode) return Unknown();
            scaling = mode == 1 ? ReadCurrent(input, SimulationParameterKeys.PersonMerit.CreditModeOnePendingMeritPercent) :
                mode == 0 ? ReadCurrent(input, SimulationParameterKeys.PersonMerit.CreditModeZeroPendingMeritPercent) : null;
            if (mode is 0 or 1 && scaling is null) return Unknown();
        }
        if (stageScale is null) return Unknown();
        var receipt = Calculate(personId, requested, pending, trait, traitScale, policy, doctrine,
            stageOne, stageScale.Value, scaling, $"{ProfileId}:{input.NativeArmySimulation!.Parameters.Revision}:{input.NativeClock!.Revision}");
        //Only this officer's pending merit changes. The enclosing simulation
        //transaction owns the other aggregates; no full-world serialization is needed.
        GameState result = input with { Officers = new(input.Officers) { [personId] = person with {
            MeritState = meritState.WithPendingMerit(receipt.AfterPendingMerit) } } };
        return new(PersonPendingMeritOutcome.Committed, result, receipt);
        PersonPendingMeritResult Unknown() => new(PersonPendingMeritOutcome.CurrentInputsUnresolved, input, null);
        PersonPendingMeritResult Skip() => new(PersonPendingMeritOutcome.NativeSkip, input, null);
    }

    /// <summary>Qualified callback arithmetic: every integer multiply/add is
    /// native signed-i32 wrap. Trait122 rounds only nonzero fractions to ±1;
    /// exact zero remains zero. The pending store clamps after wrapped add.</summary>
    public static PersonPendingMeritReceipt Calculate(int personId, int requested, int pending,
        int trait122, int traitScale, int panel50, int doctrineScale, bool stage1, int stageScale,
        int? percent, string revision)
    {
        if (requested <= 0 || pending < 0 || string.IsNullOrWhiteSpace(revision))
            throw new InvalidDataException("Pending merit arithmetic needs a reached positive request and revision.");
        var value = PersonRewardArithmetic.CalculatePendingMerit(requested, pending, trait122, traitScale,
            panel50, doctrineScale, stage1, stageScale, percent);
        return new(personId, requested, trait122, value.TraitCorrection, value.PanelCorrection,
            value.DoctrineCorrection, value.StageCorrection, value.BeforeScaling, value.Earned,
            pending, value.PendingMerit, revision);
    }

    private int? ReadPanel50(GameState state, ForcePolicyEffects? effects)
    {
        if (effects?.IsEnabled(50) is not bool enabled) return null;
        if (!enabled) return 0;
        var values = state.NativeArmySimulation!.Parameters.Int32Values;
        return values.GetValueOrDefault(VersionKey) == 1 && values.TryGetValue(Panel50Key, out int value) &&
            value is >= short.MinValue and <= short.MaxValue ? value : null;
    }
    private int? ReadCurrent(GameState state, string key)
    {
        var p = state.NativeArmySimulation!.Parameters;
        return p.Int32Values.GetValueOrDefault(VersionKey) == 1 && !p.Float32Values.ContainsKey(key) &&
            p.Int32Values.TryGetValue(key, out int value) ? value : null;
    }
    private bool Bound(GameState state) => state.NativeClock is not null && state.NativeArmySimulation?.Parameters is { } p &&
        p.Language == _language && p.RuleVersion == _ruleVersion &&
        state.ContentHashes.GetValueOrDefault(_source.ScenarioSource.ResourceUri) == _source.ScenarioSource.Sha256 &&
        state.ContentHashes.GetValueOrDefault(_source.FixedDataSource.ResourceUri) == _source.FixedDataSource.Sha256;
}
