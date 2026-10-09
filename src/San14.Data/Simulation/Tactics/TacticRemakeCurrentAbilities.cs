using San14.Core;

namespace San14.Data;

/// <summary>Current SC2300B0/TC22FE40 full abilities over this game's explicitly
/// constructed Person auxiliaries, current rank/office/items and Force policy
/// bitmaps. Native counters are not inferred from displayed base abilities.</summary>
public sealed class TacticRemakeCurrentAbilities : IEffectiveOfficerAbilities
{
    public const string ProfileId = "san14:ce-owned-person-ability-20261002";
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
            state.Officers.Values.Any(person => person.NativeAbilityRuntime is not null || person.Health is not null ||
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
            officers[officer.Id] = officer with { NativeAbilityRuntime = new(ProfileId, _language, _ruleVersion,
                _source.ScenarioSource.ResourceUri, _source.ScenarioSource.Sha256, physical,
                raw.RawDoctrineId,
                (byte)Read(0x224), (byte)Read(0x225)), Health = OfficerHealth.Healthy,
                Behavior = OfficerBehaviorSourceAdapter.Decode(raw.CreditReference.RawFlagsWord, raw.CreditReference.RawTailCode),
                Appointment = officer.Appointment.Observe(raw.RawOfficeId), ServiceStatus = OfficerServiceStatusSourceAdapter.Decode(raw.StatusCode),
                MeritState = new(null, null, raw.CreditReference.RawStageCode, null, null, null) };
            int Read(int offset) => _current.ReadPersonPostLoadByte(physical, offset)
                ?? throw new InvalidDataException("Owned Person attribute operator left an unknown field.");
        }
        GameState result = state with { Officers = officers };
        StateValidator.Validate(result); return result;
    }

    public OfficerAbilities? Resolve(GameState state, int officerId)
    {
        if (!Bound(state) || !state.Officers.TryGetValue(officerId, out OfficerState? officer) ||
            officer.NativeAbilityRuntime is not { } person || person.Revision != ProfileId ||
            person.Language != _language || person.RuleVersion != _ruleVersion ||
            person.SourceScenarioUri != _source.ScenarioSource.ResourceUri ||
            person.SourceScenarioSha256 != _source.ScenarioSource.Sha256 ||
            _current.ReadPersonLookupReference(officerId)?.PhysicalSlot != person.PhysicalSlot ||
            _current.ReadPersonIdentity(person.PhysicalSlot) != officerId ||
            officer.BaseAbilities is not { } basic || OfficerServiceStatusSourceAdapter.Encode(officer.ServiceStatus) is not byte status ||
            officer.Health is not { } healthState || OfficerMeritRules.IsStageOne(officer.MeritState) is not bool stageOne ||
            person.EncodedOrdinaryCap224 is not byte ordinaryEncoded || person.EncodedCharmCap225 is not byte charmEncoded)
            return null;
        //CPersonData V18: physical slot0 is invalid; the flags4000 branch uses
        //the current appendix tail, while ordinary identities use status11E.
        bool? valid = NativeValidity.PersonWithBehavior(person.PhysicalSlot, checked((ushort)officerId), officer.Behavior, status);
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
        OfficerAbilities? doctrine = Doctrine(state, officer, person);
        if (health is null || secondary is null || appointment is null || doctrine is null ||
            !ItemAbilityBonuses.TryCalculate(state, officerId, out OfficerAbilities? items, 10)) return null;
        int ordinaryCap = DecodeCap(ordinaryEncoded, person.PhysicalSlot), charmCap = DecodeCap(charmEncoded, person.PhysicalSlot);
        int? forcedCharm = state.Items!.TryGetValue(123, out ItemState? seal) &&
            seal.IsPresent == true && seal.Holder?.OfficerId == officerId ? 100 : null;
        int Calc(int kind, int cap, int? forced = null) => EffectiveOfficerAbilityResolver.Calculate(Values(basic)[kind],
            health.Value, secondary.Value, Values(doctrine)[kind], Values(appointment)[kind], Values(items!)[kind], cap, forced);
        return new(Calc(0, ordinaryCap), Calc(1, ordinaryCap), Calc(2, ordinaryCap), Calc(3, ordinaryCap), Calc(4, charmCap, forcedCharm));
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

    private static OfficerAbilities? Doctrine(GameState state, OfficerState officer, NativePersonAbilityRuntime person)
    {
        int? doctrine = officer.DoctrineId ?? (person.Doctrine184 == 0 ? 0 : null);
        if (doctrine == 0) return Zero;
        if (doctrine is not >= 1 or > 6 || officer.ForceId is not int forceId ||
            state.NativeArmySimulation!.Forces.GetValueOrDefault(forceId) is null ||
            state.Forces.GetValueOrDefault(forceId)?.PolicyEffects is not { HasActivationKnowledge: true } effects) return null;
        var values = new int[5];
        for (int kind = 0; kind < 5; kind++)
        {
            int effect = kind + doctrine.Value * 5 - 3;
            if (effects.IsEnabled(effect) is not bool enabled) return null;
            if (!enabled) continue;
            //An active current definition7C requires its own definition writer;
            //a fixed catalog entry alone is not a current mutable value.
            return null;
        }
        return new(values[0], values[1], values[2], values[3], values[4]);
    }

    private bool Bound(GameState state) => state.NativeArmySimulation?.Parameters is { } parameters &&
        parameters.Language == _language && parameters.RuleVersion == _ruleVersion &&
        state.ContentHashes.GetValueOrDefault(_source.ScenarioSource.ResourceUri) == _source.ScenarioSource.Sha256 &&
        state.ContentHashes.GetValueOrDefault(_source.FixedDataSource.ResourceUri) == _source.FixedDataSource.Sha256;
    private int? Parameter(GameState state, string key) => state.NativeArmySimulation!.Parameters.Int32Values
        .TryGetValue(key, out int value) ? value : null;
    public static int DecodeCap(byte encoded, int physicalSlot) =>
        (byte)((byte)((encoded << 3) | (encoded >> 5)) ^ (byte)physicalSlot);
    private static readonly OfficerAbilities Zero = new(0, 0, 0, 0, 0);
    private static int[] Values(OfficerAbilities value) => [value.Leadership, value.Strength, value.Intelligence, value.Politics, value.Charm];
    private static bool RoleMatches(OfficerRole role, byte status) => (role, status) switch
        { (OfficerRole.Ruler, 1) or (OfficerRole.DistrictLeader, 2) or (OfficerRole.Governor, 3) or
          (OfficerRole.Officer, 4) or (OfficerRole.Free, 5) => true, _ => false };
    private static bool IsBandPerson(int id) => unchecked((uint)(id - 4111)) <= 9 || unchecked((uint)(id - 4131)) <= 9 ||
        unchecked((uint)(id - 4151)) <= 9 || unchecked((uint)(id - 4171)) <= 9 || unchecked((uint)(id - 4191)) <= 9;
}
