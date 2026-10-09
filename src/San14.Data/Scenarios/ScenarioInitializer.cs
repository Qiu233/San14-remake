using San14.Core;

namespace San14.Data;

public sealed record InitializedScenario(GameState State,
    SourceProvenance ScenarioSource, SourceProvenance FixedDataSource, SourceProvenance MapSource);

/// <summary>
/// Converts verified original records to format-independent simulation entities.
/// The resulting state stays in initialization until the remaining rules and fields
/// are resolved; it cannot accidentally run the prototype turn rules.
/// </summary>
public static class ScenarioInitializer
{
    /// <summary>Compatibility entry; choices other than difficulty remain unknown.</summary>
    public static InitializedScenario Create(LoadedScenario loaded, LoadedMap map,
        ulong randomSeed, string difficultyId = "intermediate") =>
        CreateCore(loaded, map, randomSeed, difficultyId, null);

    /// <summary>Records explicit player choices without treating static source settings as defaults.</summary>
    public static InitializedScenario Create(LoadedScenario loaded, LoadedMap map,
        ulong randomSeed, NewGameOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return CreateCore(loaded, map, randomSeed, options.DifficultyId, options);
    }

    private static InitializedScenario CreateCore(LoadedScenario loaded, LoadedMap map,
        ulong randomSeed, string difficultyId, NewGameOptions? options)
    {
        ScenarioCityPreview source = loaded.Snapshot;
        if (source.RankParameters is null)
            throw new InvalidDataException("Scenario initialization requires source rank parameters.");
        if (source.DoctrineParameters is null)
            throw new InvalidDataException("Scenario initialization requires source doctrine definitions.");
        var doctrines = source.DoctrineParameters.Doctrines.Where(doctrine => doctrine.Slot is >= 1 and <= 6).ToArray();
        if (doctrines.Length != 6 || doctrines.Select(doctrine => doctrine.Slot).Distinct().Count() != 6)
            throw new InvalidDataException("Scenario initialization requires the six verified doctrine definitions.");
        if (source.PolicyParameters is null)
            throw new InvalidDataException("Scenario initialization requires source policy definitions.");
        // Original policy validity uses a nonzero table identity, not nonempty or unique text.
        var policies = source.PolicyParameters.Policies.Where(policy => policy.Slot is >= 1 and <= 120).ToArray();
        if (policies.Length != 120 || policies.Select(policy => policy.Slot).Distinct().Count() != 120 ||
            policies.Any(policy => policy.RawCostAddition is null))
            throw new InvalidDataException("Scenario initialization requires the 120 policy definitions and their verified cost additions.");
        var effects = source.PolicyParameters.Effects.Where(effect => effect.Slot is >= 1 and <= 100).ToArray();
        if (effects.Length != 100 || effects.Select(effect => effect.Slot).Distinct().Count() != 100 ||
            effects.Any(effect => effect.RawValues is not { Count: 10 }) ||
            policies.Any(policy => policy.RawEffectSlots is not { Count: 8 } || policy.RawEffectSlots.Any(slot => slot > 100)))
            throw new InvalidDataException("Scenario initialization requires the verified policy effect definitions and references.");
        if (source.CombatParameters is null || source.Characters.Any(person =>
            person.BaseFormationSlots is null || person.BaseTacticSlots is null))
            throw new InvalidDataException("Scenario initialization requires source combat definitions and references.");
        if (source.UnresolvedCharacters.Count != 0)
            throw new InvalidDataException("Scenario initialization requires all character identities to resolve.");
        if (loaded.Metadata.StartYear is not int year || loaded.Metadata.StartMonth is not int month ||
            loaded.Metadata.StartDay is not int day)
            throw new InvalidDataException("Scenario initialization requires a complete start date.");
        ItemProjection itemProjection = ItemProjection.Create(source, loaded.Metadata.FormatMarker);
        ScenarioTerritory territory = ScenarioTerritory.Build(map.Map, source);
        static int? OptionalId(int id) => id == 0 ? null : id;
        // Original office 73 is a known "none" definition. Code 0 and the
        // reserved tail do not establish an appointment; keep their raw bytes
        // in the source snapshot and leave the independent state unresolved.
        static int? OfficeId(byte? code) => code is >= 1 and <= 237 ? code.Value : null;
        // The original serializer reads the doctrine and its following field as
        // separate bytes. The retained two-byte diagnostic window is not a type ID.
        // Missing typed data stays unknown, including older cached snapshots.
        static int? DoctrineId(byte? code) => code is >= 1 and <= 6 ? code.Value : null;
        Dictionary<int, int?> personalDoctrines = source.Characters.Where(person => person.CharacterId != 0)
            .ToDictionary(person => person.CharacterId, person => DoctrineId(person.RawDoctrineId));
        static OfficerPresence? Presence(byte code) => code switch
        {
            >= 1 and <= 5 => OfficerPresence.Present,
            7 => OfficerPresence.NotAppeared,
            8 => OfficerPresence.Undiscovered,
            9 => OfficerPresence.Dead,
            _ => null
        };

        var issues = new List<string>
        {
            "rules.command_budget", "rules.period_settlement", "rules.difficulty_modifiers",
            "officers.ability_modifiers",
            "areas.administration", "initial_units", "events.initial_state",
            "rules.deployment_eligibility", "rules.deployment_resources", "forces.governance"
        };
        if (source.UnresolvedPlacements.Count != 0) issues.Add("officers.template_placements");
        if (source.Characters.Any(person => person.CharacterId != 0 && Presence(person.StatusCode) is null))
            issues.Add("officers.unresolved_presence");
        if (source.Characters.Any(person => person.CharacterId != 0 && person.RawSexCode is not (0 or 1)))
            issues.Add("officers.unresolved_sex");
        if (source.Characters.Any(person => person.CharacterId != 0 && person.RawPolicySlot is not (>= 1 and <= 120)))
            issues.Add("officers.unresolved_policies");
        if (source.Characters.Any(person => person.CharacterId != 0 && person.RawPolicyBaseCost is null))
            issues.Add("officers.unresolved_policy_costs");
        if (source.Characters.Any(person => person.CharacterId != 0 && OfficeId(person.RawOfficeId) is null))
            issues.Add("officers.unresolved_offices");
        if (personalDoctrines.Values.Any(id => id is null)) issues.Add("officers.unresolved_doctrines");
        if (source.Cities.Any(city => city.Slot != 0 && city.HexId is null)) issues.Add("cities.map_position");
        if (source.Cities.Any(city => city.Slot != 0 &&
            CitySourceNormalization.SizeFromCode(city.SizeCode) is null))
            issues.Add("cities.unverified_size_codes");
        Dictionary<int, int?> cityDurability = source.Cities.Where(city => city.Slot != 0)
            .ToDictionary(city => city.Slot, city => CitySourceNormalization.CurrentDurability(
                city, loaded.Metadata.FormatMarker, source.ObjectSourceRecords));
        if (cityDurability.Values.Any(value => value is null)) issues.Add("cities.object_durability");
        Dictionary<int, int?> gateDurability = source.Gates.Where(gate => gate.Slot != 0)
            .ToDictionary(gate => gate.LocationSlot, gate => GateSourceNormalization.CurrentDurability(
                gate, loaded.Metadata.FormatMarker, source.ObjectSourceRecords));
        if (gateDurability.Values.Any(value => value is null)) issues.Add("gates.object_durability");
        issues.AddRange(itemProjection.Issues);

        var state = new GameState
        {
            ScenarioKey = GameResourceId.File(loaded.Metadata.SourcePath).ToString(),
            MapSha256 = map.Source.Sha256,
            RulesetId = "san14:original-pending",
            DifficultyId = difficultyId,
            NewGameOptions = options,
            InitializationIssues = issues.ToArray(),
            ContentHashes = new Dictionary<string, string>
            {
                [loaded.ScenarioSource.ResourceUri] = loaded.ScenarioSource.Sha256,
                [loaded.FixedDataSource.ResourceUri] = loaded.FixedDataSource.Sha256,
                [map.Source.ResourceUri] = map.Source.Sha256
            },
            MapWidth = map.Map.Width,
            MapHeight = map.Map.Height,
            Date = new PeriodDate(year, month, day),
            Phase = TurnPhase.Initializing,
            RandomState = randomSeed,
            ItemDefinitions = itemProjection.Definitions,
            Items = itemProjection.Items,
            Forces = source.Forces.Where(force => force.Slot != 0).ToDictionary(force => force.Slot,
                force => new ForceState(force.Slot, force.RulerName ?? "", null, null)
                {
                    RulerId = OptionalId(force.RulerSlot),
                    RankId = force.RankId,
                    SourceIndexedBytes13 = force.RawIndexedBytes13?.ToArray(),
                    SourceIndexedBytesEA = force.RawIndexedBytesEA?.ToArray(),
                    // Initial scenario derivation only; no force-record field or runtime succession rule is asserted.
                    DoctrineId = personalDoctrines.GetValueOrDefault(force.RulerSlot),
                    IsActive = source.Cities.Any(city => city.Slot != 0 && city.ForceSlot == force.Slot) ||
                        source.Gates.Any(gate => gate.Slot != 0 && gate.ForceSlot == force.Slot)
                }),
            ForceRanks = source.RankParameters.Ranks.ToDictionary(rank => rank.Id,
                rank => new ForceRankDefinition(rank.Id, rank.Name, rank.CommandLimit,
                    new OfficerAbilities(rank.AbilityBonus.Leadership, rank.AbilityBonus.Strength,
                        rank.AbilityBonus.Intelligence, rank.AbilityBonus.Politics, rank.AbilityBonus.Charm))),
            OfficerOffices = source.RankParameters.Offices.ToDictionary(office => office.Id,
                office => new OfficerOfficeDefinition(office.Id, office.Name, office.CommandLimit,
                    new OfficerAbilities(office.AbilityBonus.Leadership, office.AbilityBonus.Strength,
                        office.AbilityBonus.Intelligence, office.AbilityBonus.Politics, office.AbilityBonus.Charm))),
            Doctrines = doctrines.ToDictionary(doctrine => doctrine.Slot,
                doctrine => new DoctrineDefinition(doctrine.Slot, doctrine.Name, doctrine.Description)),
            Policies = policies.ToDictionary(policy => policy.Slot,
                policy => new PolicyDefinition(policy.Slot, policy.Name, policy.Description, policy.RawCostAddition!.Value)
                {
                    DepartmentCode = policy.RawTailBytes.Length > 0 ? policy.RawTailBytes[0] : null,
                    EffectIds = policy.RawEffectSlots.Where(slot => slot != 0).Select(slot => (int)slot).ToArray()
                }),
            PolicyEffects = effects.ToDictionary(effect => effect.Slot,
                effect => new PolicyEffectDefinition(effect.Slot, effect.Name, effect.Description,
                    effect.RawValues.Select(value => (int)value).ToArray())),
            Districts = source.Districts.Where(district => district.Number > 0 && district.ForceSlot != 0)
                .ToDictionary(district => district.Slot, district => new DistrictState(district.Slot,
                    district.ForceSlot, district.Number, OptionalId(district.LeaderSlot))),
            Formations = source.CombatParameters.Formations.ToDictionary(formation => formation.Slot,
                formation =>
                {
                    if (formation.RawParameterBytes.Length <= 8)
                        throw new InvalidDataException($"Formation {formation.Slot} has no base coefficient bytes.");
                    return new FormationDefinition(formation.Slot, formation.Name, formation.Description)
                    {
                        SourceBase = new FormationBaseCoefficients(formation.RawParameterBytes[1],
                            formation.RawParameterBytes[5], formation.RawParameterBytes[6],
                            formation.RawParameterBytes[7], formation.RawParameterBytes[8])
                    };
                }),
            Tactics = source.CombatParameters.Tactics.ToDictionary(tactic => tactic.Slot,
                tactic => new TacticDefinition(tactic.Slot, tactic.Name) { FormationIds = tactic.FormationSlots.ToArray() }),
            Cities = source.Cities.Where(city => city.Slot != 0).ToDictionary(city => city.Slot,
                city => new CityState(city.Slot, city.Name, OptionalId(city.ForceSlot), city.HexId,
                    checked((int)city.Gold), checked((int)city.Supplies), checked((int)city.Troops),
                    source.Areas.Where(area => area.CitySlot == city.Slot).Select(area => area.Slot).ToArray())
                {
                    DistrictId = OptionalId(city.DistrictSlot),
                    GovernorId = OptionalId(city.OfficerSlot),
                    Morale = city.Morale,
                    Durability = cityDurability[city.Slot],
                    Size = CitySourceNormalization.SizeFromCode(city.SizeCode)
                }),
            Gates = source.Gates.Where(gate => gate.Slot != 0).ToDictionary(gate => gate.LocationSlot,
                gate => new GateState(gate.LocationSlot, gate.Name, OptionalId(gate.ForceSlot),
                    OptionalId(gate.DistrictSlot), OptionalId(gate.OfficerSlot), gate.HexId, checked((int)gate.Troops))
                { Durability = gateDurability[gate.LocationSlot] }),
            Officers = source.Characters.Where(person => person.CharacterId != 0)
                .ToDictionary(person => person.CharacterId, person => new OfficerState(person.CharacterId,
                    person.Name, OptionalId(person.ForceSlot), OptionalId(person.CitySlot), false)
                {
                    DistrictId = OptionalId(person.DistrictSlot),
                    Sex = person.RawSexCode switch { 0 => OfficerSex.Male, 1 => OfficerSex.Female, _ => null },
                    Presence = Presence(person.StatusCode),
                    OfficeId = OfficeId(person.RawOfficeId),
                    Loyalty = Formats.S14PersonnelSourceReader.ReadCharacterLoyaltyPreview(person,
                        loaded.Metadata.FormatMarker) is byte storedLoyalty && storedLoyalty <= 100
                        ? storedLoyalty : null,
                    DoctrineId = personalDoctrines[person.CharacterId],
                    PolicyId = person.RawPolicySlot is >= 1 and <= 120 ? person.RawPolicySlot.Value : null,
                    PolicyBaseCost = OfficerSourceNormalization.PolicyBaseCost(
                        person.RawPolicyBaseCost, loaded.Metadata.FormatMarker),
                    Role = person.StatusCode switch
                    {
                        1 => OfficerRole.Ruler, 2 => OfficerRole.DistrictLeader,
                        3 => OfficerRole.Governor, 4 => OfficerRole.Officer,
                        5 => OfficerRole.Free, _ => OfficerRole.Unresolved
                    },
                    BaseAbilities = OfficerSourceNormalization.Abilities(
                        person, loaded.Metadata.FormatMarker),
                    BaseFormationIds = person.BaseFormationSlots!.ToArray(),
                    BaseTacticIds = person.BaseTacticSlots!.ToArray()
                }),
            Areas = source.Areas.Where(area => area.CitySlot != 0)
                .ToDictionary(area => area.Slot, area => new AreaState(area.Slot, area.CitySlot)
                {
                    Name = area.Name,
                    HexId = area.HexId,
                    ForceId = OptionalId(area.ForceSlot),
                    Security = loaded.Metadata.FormatMarker == 0x66
                        ? AreaSecuritySource.ReadPreview(area, loaded.Metadata.FormatMarker) : null,
                    EconomySource = loaded.Metadata.FormatMarker == 0x66
                        ? CeAreaEconomySource.Project(area, loaded) : null,
                    AdjacentAreaIds = area.AdjacentAreaSlots.ToArray()
                }),
            Territory = territory.Cells.ToDictionary(cell => cell.HexId,
                cell => new TerritoryState(cell.HexId, OptionalId(cell.AreaId), OptionalId(cell.ForceId)))
        };
        state = ArmyIdentityAllocation.Initialize(state);
        StateValidator.Validate(state);
        return new InitializedScenario(state, loaded.ScenarioSource, loaded.FixedDataSource, map.Source);
    }
}
