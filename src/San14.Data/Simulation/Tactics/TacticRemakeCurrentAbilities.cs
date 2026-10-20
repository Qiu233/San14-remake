using San14.Core;

namespace San14.Data;

/// <summary>Resolves effective officer abilities from current health, merit,
/// appointments, items, doctrine and independent upper limits. Resource identity
/// is admitted by the source directory and campaign content ledger.</summary>
public sealed class TacticRemakeCurrentAbilities : IEffectiveOfficerAbilities
{
    private readonly SourceWorldContext _current;
    private readonly LoadedScenario _source;
    private readonly string _language, _ruleVersion;

    public TacticRemakeCurrentAbilities(SourceWorldContext current,
        LoadedScenario source, string language, string ruleVersion)
    {
        _current = current; _source = source; _language = language; _ruleVersion = ruleVersion;
        if (language is not ("SC" or "TC") || source.Metadata.FormatMarker != 0x66 ||
            source.Snapshot.Characters.Count != 1851)
            throw new InvalidDataException("Owned Person abilities require the current paired CE source profile.");
    }

    /// <summary>One explicit new owned allocation: constructor reset, actual
    /// static Person row and the proved cap/base-normalization operators. This
    /// is not a claim about later original first-player initialization.</summary>
    public GameState CreateFreshOwnedPeople(GameState state)
    {
        if (!Bound(state) || state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.Officers.Values.Any(person => person.AbilityState is not null || person.Health is not null ||
                person.ServiceStatus is not null || person.Behavior is not null || person.MeritState is not null))
            throw new InvalidDataException("Fresh Person ability fields cannot replace a saved or populated owner.");
        var officers = new Dictionary<int, OfficerState>(state.Officers);
        foreach (OfficerState officer in state.Officers.Values)
        {
            TransferPersonReference? pointer = _current.ReadPersonLookupReference(officer.Id);
            if (pointer?.PhysicalSlot is not int physical || physical is < 1 or > 1850 ||
                _current.ReadPersonIdentity(physical) != officer.Id)
                throw new InvalidDataException("Owned Person lookup does not match its source identity.");
            CharacterPreviewRecord raw = _source.Snapshot.Characters[physical];
            if (raw.CharacterId != officer.Id || raw.RawOfficeId is null || raw.RawDoctrineId is null || raw.CreditReference is null)
                throw new InvalidDataException("Owned Person source auxiliaries are missing.");
            _current.InitializePersonAttributeCaps(physical);
            _current.NormalizePersonAttributeValues(physical);
            var normalized = new OfficerAbilities(Read(0x124), Read(0x125), Read(0x126), Read(0x127), Read(0x128));
            if (officer.BaseAbilities != normalized)
                throw new InvalidDataException("Fresh current base abilities differ from the actual source normalization.");
            officers[officer.Id] = officer with { AbilityState = OfficerAbilitySourceAdapter.Decode(physical,
                (byte)Read(0x224), (byte)Read(0x225), raw.RawDoctrineId), Health = OfficerHealth.Healthy,
                Behavior = OfficerBehaviorSourceAdapter.Decode(raw.CreditReference.RawFlagsWord, raw.CreditReference.RawTailCode),
                Appointment = officer.Appointment.Observe(raw.RawOfficeId), ServiceStatus = OfficerServiceStatusSourceAdapter.Decode(raw.StatusCode),
                MeritState = new(null, null, raw.CreditReference.RawStageCode, null, null, null) };
            int Read(int offset) => _current.ReadPersonPostLoadByte(physical, offset)
                ?? throw new InvalidDataException("Owned Person attribute operator left an unknown field.");
        }
        GameState result = state with { Officers = officers };
        StateValidator.Validate(result); return result;
    }

    public OfficerAbilities? Resolve(GameState state, int officerId) =>
        ResolveCore(state, officerId, null, null);

    /// <summary>The same current ability algorithm for one consumed dimension.
    /// Other caps and doctrine panels are not queried. An optional current
    /// panel reader supplies an already owned mutable doctrine value.</summary>
    public int? ResolveDimension(GameState state, int officerId, int dimension,
        Func<int, int, int?>? panelReader = null)
    {
        if (dimension is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(dimension));
        var result = ResolveCore(state, officerId, dimension, panelReader);
        return result is null ? null : Values(result)[dimension];
    }

    private OfficerAbilities? ResolveCore(GameState state, int officerId, int? dimension,
        Func<int, int, int?>? panelReader)
    {
        if (!Bound(state) || !state.Officers.TryGetValue(officerId, out OfficerState? officer) ||
            officer.AbilityState is not { } abilities ||
            _current.ResolveOfficerSource(officerId) is not int physical ||
            officer.BaseAbilities is not { } basic || OfficerServiceStatusSourceAdapter.Encode(officer.ServiceStatus) is not byte status ||
            officer.Health is not { } healthState || OfficerMeritRules.IsStageOne(officer.MeritState) is not bool stageOne ||
            (dimension != 4 && abilities.OrdinaryLimit is null) ||
            (dimension is null or 4 && abilities.CharmLimit is null))
            return null;
        //CPersonData V18: physical slot0 is invalid; the flags4000 branch uses
        //the current appendix tail, while ordinary identities use status11E.
        bool? valid = NativeValidity.PersonWithBehavior(physical, checked((ushort)officerId), officer.Behavior, status);
        if (valid is null) return null;
        if (!valid.Value) return new(0, 0, 0, 0, 0); //Army22E610's proved invalid-person result.
        if (!RoleMatches(officer.Role, status) || Values(basic).Any(value => value is < 0 or > 255)) return null;
        int? health = OfficerHealthRules.AbilityModifier(healthState) switch
        {
            HealthAbilityModifier.Healthy => Parameter(state, SimulationParameterKeys.Tactics.HealthyAbilityPercent),
            HealthAbilityModifier.InjuryLevel1 => Parameter(state, SimulationParameterKeys.Tactics.InjuryLevel1AbilityPercent),
            HealthAbilityModifier.InjuryLevel2 => Parameter(state, SimulationParameterKeys.Tactics.InjuryLevel2AbilityPercent),
            HealthAbilityModifier.InjuryLevel3 => Parameter(state, SimulationParameterKeys.Tactics.InjuryLevel3AbilityPercent),
            _ => 0
        };
        int? secondary = Parameter(state, SimulationParameterKeys.Tactics.AbilitySecondaryBaseline);
        //For native rulers and every stage except1, both settings outcomes
        //have the same baseline. Stage1 may additionally require the complete
        //special-person/affiliation gate; unresolved branches stay nullable.
        if (status != 1 && OfficerBehaviorRules.UsesOrdinaryPersonRules(officer.Behavior) == true && stageOne)
        {
            if (state.NewGameOptions is null) return null;
            if (state.NewGameOptions.CapturedOfficersEnabled)
            {
                if (IsBandPerson(officerId)) return null;
                secondary = Parameter(state, SimulationParameterKeys.Tactics.AbilitySecondaryAdjustment);
            }
        }
        OfficerAbilities? appointment = Appointment(state, officer, status);
        OfficerAbilities? doctrine = Doctrine(state, officer, dimension, panelReader);
        if (health is null || secondary is null || appointment is null || doctrine is null ||
            !ItemAbilityBonuses.TryCalculate(state, officerId, out OfficerAbilities? items, 10)) return null;
        int? forcedCharm = dimension is null or 4 && state.Items!.TryGetValue(123, out ItemState? seal) &&
            seal.IsPresent == true && seal.Holder?.OfficerId == officerId ? 100 : null;
        int Calc(int kind, int cap, int? forced = null) => EffectiveOfficerAbilityResolver.Calculate(Values(basic)[kind],
            health.Value, secondary.Value, Values(doctrine)[kind], Values(appointment)[kind], Values(items!)[kind], cap, forced);
        int One(int kind) => dimension is int selected && selected != kind ? 0
            : Calc(kind, (kind == 4 ? abilities.CharmLimit : abilities.OrdinaryLimit)!.Value,
                kind == 4 ? forcedCharm : null);
        return new(One(0), One(1), One(2), One(3), One(4));
    }

    private static OfficerAbilities? Appointment(GameState state, OfficerState officer, byte status)
    {
        if (status == 1)
        {
            if (officer.ForceId is not int forceId || !state.Forces.TryGetValue(forceId, out ForceState? force) ||
                force.RulerId != officer.Id || officer.DistrictId is not int districtId ||
                !state.Districts.TryGetValue(districtId, out DistrictState? district) || district.ForceId != forceId ||
                force.RankId is not int rankId || rankId is < 0 or > 12 ||
                !state.ForceRanks.TryGetValue(rankId, out ForceRankDefinition? rank)) return null;
            return rankId == 0 ? Zero : rank.AbilityBonus;
        }
        int? officeId = OfficerAppointmentRules.ReadAbilityOfficeId(officer.Appointment);
        if (officeId is not int id || id is < 0 or > 255 ||
            !state.OfficerOffices.TryGetValue(id, out OfficerOfficeDefinition? office)) return null;
        //Current CPostData validity requires a nonzero physical slot and raw
        //command limit. Its successful fresh five-i32 getter returns charm0.
        return id == 0 || office.CommandLimit == 0 ? Zero : office.AbilityBonus with { Charm = 0 };
    }

    private static OfficerAbilities? Doctrine(GameState state, OfficerState officer, int? dimension,
        Func<int, int, int?>? panelReader)
    {
        int? doctrine = OfficerDoctrineRules.ReadDoctrineId(officer.DoctrineId, officer.AbilityState);
        if (doctrine == 0) return Zero;
        if (doctrine is not >= 1 or > 6 || officer.ForceId is not int forceId ||
            state.NativeArmySimulation!.Forces.GetValueOrDefault(forceId) is null ||
            state.Forces.GetValueOrDefault(forceId)?.PolicyEffects is not { HasActivationKnowledge: true } effects) return null;
        var values = new int[5];
        for (int kind = 0; kind < 5; kind++)
        {
            if (dimension is int selected && selected != kind) continue;
            int effect = kind + doctrine.Value * 5 - 3;
            if (effects.IsEnabled(effect) is not bool enabled) return null;
            if (!enabled) continue;
            // A fixed catalog alone is not current knowledge. The caller can
            // supply the existing saved panel owner for its consumed dimension.
            if (panelReader is null || panelReader(forceId, effect) is not int value) return null;
            values[kind] = value;
        }
        return new(values[0], values[1], values[2], values[3], values[4]);
    }

    private bool Bound(GameState state) => state.NativeArmySimulation?.Parameters is { } parameters &&
        parameters.Language == _language && parameters.RuleVersion == _ruleVersion &&
        state.ContentHashes.GetValueOrDefault(_source.ScenarioSource.ResourceUri) == _source.ScenarioSource.Sha256 &&
        state.ContentHashes.GetValueOrDefault(_source.FixedDataSource.ResourceUri) == _source.FixedDataSource.Sha256;
    private int? Parameter(GameState state, string key) => state.NativeArmySimulation!.Parameters.Int32Values
        .TryGetValue(key, out int value) ? value : null;
    private static readonly OfficerAbilities Zero = new(0, 0, 0, 0, 0);
    private static int[] Values(OfficerAbilities value) => [value.Leadership, value.Strength, value.Intelligence, value.Politics, value.Charm];
    private static bool RoleMatches(OfficerRole role, byte status) => (role, status) switch
        { (OfficerRole.Ruler, 1) or (OfficerRole.DistrictLeader, 2) or (OfficerRole.Governor, 3) or
          (OfficerRole.Officer, 4) or (OfficerRole.Free, 5) => true, _ => false };
    private static bool IsBandPerson(int id) => unchecked((uint)(id - 4111)) <= 9 || unchecked((uint)(id - 4131)) <= 9 ||
        unchecked((uint)(id - 4151)) <= 9 || unchecked((uint)(id - 4171)) <= 9 || unchecked((uint)(id - 4191)) <= 9;
}
