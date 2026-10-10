namespace San14.Core;

public enum TurnPhase { Strategy, Reporting, Initializing }
public enum OfficerRole { Officer, Ruler, DistrictLeader, Governor, Free, Unresolved }
public enum OfficerSex { Male, Female }
public enum OfficerPresence { Present, NotAppeared, Undiscovered, Dead }
public enum CitySize { Small, Medium, Large, Giant }
// Persisted values are independent of the original byte codes. Keep existing saves stable.
// None is a known absence of focus; a nullable focus still means unresolved information.
public enum AreaDevelopmentFocus { Commerce = 0, Agriculture = 1, Barracks = 2, None = 3 }

/// <summary>Explicit player choices, independent of source formats and initialization effects.</summary>
public sealed record NewGameOptions(string DifficultyId, bool InitialCreditEnabled,
    bool CapturedOfficersEnabled);

public readonly record struct PeriodDate(int Year, int Month, int Day)
{
    public PeriodDate Next()
    {
        if (Month is < 1 or > 12 || Day is not (1 or 11 or 21))
            throw new InvalidDataException("The current period date is invalid.");
        if (Day != 21) return this with { Day = Day + 10 };
        return Month == 12 ? new PeriodDate(Year + 1, 1, 1) : new PeriodDate(Year, Month + 1, 1);
    }
}

// Force-level order fields are retained only for legacy saves and PrototypeOrderRules.
// Original command books belong to districts; do not infer a district budget from these fields.
public sealed partial record ForceState(int Id, string Name, int? OrdersRemaining, int? OrderLimit)
{
    public int? RulerId { get; init; }
    public int? RankId { get; init; }
    // Current force type is stored independently of the ruler's personal doctrine.
    // A change of ruler has no implicit inheritance or reset rule.
    public int? DoctrineId { get; init; }
    public bool IsActive { get; init; } = true;
    public GovernanceState? Governance { get; init; }
    // Opaque CE source evidence indexed by original force slot. These are not
    // mutable diplomatic relations or treaty state. Null means unavailable.
    public byte[]? SourceIndexedBytes13 { get; init; }
    public byte[]? SourceIndexedBytesEA { get; init; }
}
/// <summary>Confirmed rank parameters; other sources of officer modifiers are separate.</summary>
public sealed record ForceRankDefinition(int Id, string Name, int CommandLimit, OfficerAbilities AbilityBonus);
/// <summary>Office parameters independent of ruler rank, appointment rules and final ability modifiers.</summary>
public sealed record OfficerOfficeDefinition(int Id, string Name, int CommandLimit, OfficerAbilities AbilityBonus);
/// <summary>Type identity and text only; levels and governance effects have separate rules.</summary>
public sealed record DoctrineDefinition(int Id, string Name, string Description);
/// <summary>Policy identity and source cost addition; assignment and effective level use separate rules.</summary>
public sealed record PolicyDefinition(int Id, string Name, string Description, int CostAddition)
{
    // Source policy-group byte. Null keeps older saves and unsupported sources unresolved.
    public int? DepartmentCode { get; init; }
    // Null is unparsed/legacy data; an empty array is a known policy with no effects.
    // Repeated references are meaningful in the original aggregation and remain ordered.
    public int[]? EffectIds { get; init; }
}
/// <summary>Values for positive aggregate levels; a zero aggregate has no effect.</summary>
public sealed record PolicyEffectDefinition(int Id, string Name, string Description, int[] LevelValues);
/// <summary>Independent item identity and ability parameters. Null bonuses remain unresolved.</summary>
public sealed partial record ItemDefinition(int Id, string Name, OfficerAbilities? AbilityBonus);
/// <summary>A known holder record. Null officer means no character holder; location is a separate rule.</summary>
public sealed record ItemHolder(int? OfficerId);
public sealed record ItemState(int Id, int DefinitionId)
{
    // Null holder is unknown; ItemHolder(null) explicitly records no character holder.
    public ItemHolder? Holder { get; init; }
    // World inventory membership, independent of ownership or command eligibility.
    public bool? IsPresent { get; init; }
}
public sealed record DistrictState(int Id, int ForceId, int Number, int? LeaderId)
{
    public int? OrdersRemaining { get; init; }
    // Optional cap, separate from stock/recovery. Original arithmetic is verified,
    // but final leader abilities must be resolved before assigning this value.
    public int? OrderLimit { get; init; }
}
public sealed partial record CityState(int Id, string Name, int? ForceId, int? HexId,
    int Gold, int Supplies, int Troops, int[] AreaIds)
{
    public int? DistrictId { get; init; }
    public int? GovernorId { get; init; }
    public int? Morale { get; init; }
    public int? Durability { get; init; }
    public CitySize? Size { get; init; }
    // Null is unresolved original/legacy duty state; the playable ruleset starts with known vacancies.
    public CityDutyState? Duties { get; init; }
}
public sealed record CityDutyState(int? RecruiterId, int? TrainerId, int PreparationPeriods);
public sealed partial record GateState(int Id, string Name, int? ForceId, int? DistrictId,
    int? GovernorId, int? HexId, int Troops)
{
    //Current wall stock is independent of the source Base capacity. Older
    //snapshots and unparsed source profiles leave it unknown.
    public int? Durability { get; init; }
}
public sealed record OfficerAbilities(int Leadership, int Strength, int Intelligence, int Politics, int Charm);
/// <summary>Candidate base coefficients from the source formation definition.
/// These are not final unit abilities or period movement distances.</summary>
public sealed record FormationBaseCoefficients(int Mobility, int ArmyAttack,
    int GarrisonAttack, int DurabilityAttack, int Defense);
public sealed record FormationDefinition(int Id, string Name, string Description)
{
    // Null preserves old saves and synthetic definitions without a fixed-data source.
    public FormationBaseCoefficients? SourceBase { get; init; }
}
public sealed partial record TacticDefinition(int Id, string Name)
{
    // Definition references are separate from an officer's final permission after runtime effects.
    public int[]? FormationIds { get; init; }
}
public sealed partial record OfficerState(int Id, string Name, int? ForceId, int? LocationId, bool Deployed)
{
    public int? DistrictId { get; init; }
    public OfficerRole Role { get; init; } = OfficerRole.Officer;
    // Null is unresolved, including snapshots saved before this field existed.
    public OfficerSex? Sex { get; init; }
    // Independent of office/role. Null retains unresolved source or legacy state.
    // Present does not establish health, task availability or command eligibility.
    public OfficerPresence? Presence { get; init; }
    public int? DoctrineId { get; init; }
    // Nullable values preserve unresolved original fields and older snapshots independently.
    public int? PolicyId { get; init; }
    public int? PolicyBaseCost { get; init; }
    public OfficerAbilities? BaseAbilities { get; init; }
    // Original scenario capabilities before runtime effects; null means unavailable in an older snapshot.
    public int[]? BaseFormationIds { get; init; }
    public int[]? BaseTacticIds { get; init; }
}
/// <summary>A present record makes the appointment known; null focus retains legacy uncertainty.</summary>
public sealed record AreaAdministrationState(int? OverseerId, AreaDevelopmentFocus? DevelopmentFocus);
public sealed partial record AreaState(int Id, int CityId)
{
    // Null is unresolved administration, not a verified vacant appointment.
    public AreaAdministrationState? Administration { get; init; }
    public string Name { get; init; } = "";
    public int? HexId { get; init; }
    public int? ForceId { get; init; }
    // Directed source adjacency; not an assertion of passability or an auto-generated symmetric graph.
    public int[]? AdjacentAreaIds { get; init; }
}
public sealed record TerritoryState(int Id, int? AreaId, int? ForceId);
/// <summary>Map construction identity; KindId belongs to the active ruleset's catalog.</summary>
public sealed record StructureState(int Id, string KindId, int HexId, int? ForceId,
    int Durability, int MaxDurability = 0, int? SourceCityId = null,
    bool Completed = false);
public enum UnitMissionKind { Combat, Transport, OccupyArea, Retreat, BuildStructure,
    RepairStructure, DemolishStructure }
public enum TransportArrivalDisposition { EnterCity, ReturnToSource }
/// <summary>Typed objective for provisional playable units; null keeps legacy/source uncertainty.</summary>
public sealed record UnitMission(UnitMissionKind Kind, int? DestinationCityId = null,
    int CargoGold = 0, TransportArrivalDisposition Arrival = TransportArrivalDisposition.EnterCity,
    bool Returning = false, int? TargetAreaId = null, string? StructureKindId = null,
    int? TargetStructureId = null);
/// <summary>In the playable ruleset Supplies is transport cargo only; combat food stays in the source city.</summary>
public sealed partial record UnitState(int Id, int ForceId, int OfficerId, int HexId,
    int DestinationHexId, int Troops, int Supplies, ArmyMoraleState MoraleState)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public int Morale => MoraleState.ProvisionalValue;
    // Null retains unknown configuration in legacy saves and non-original prototypes.
    /// <summary>Current supporting Base. A source ordinary Army follows its
    /// commander's actual Home changes; construction provenance is separate.
    /// A transport keeps its own departure/source semantics.</summary>
    public int? SourceLocationId { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public int? FormationId => Formations?.SelectedLandFormationId;
    public int[]? TacticIds { get; init; }
    public DeploymentBehavior? Behavior { get; init; }
    public UnitMission? Mission { get; init; }
}
/// <summary>Typed period events persist across report, save and reload. Text is
/// rendered by the client locale, never frozen in the simulation snapshot.</summary>
public sealed record PeriodReportEntry(string Kind, int ForceId, int? CityId = null,
    int? OfficerId = null, int Amount = 0, int? FromHexId = null, int? ToHexId = null)
{
    // A base can be a gate. CityId remains a strict city reference for existing
    // report consumers; this field preserves the independent city/gate identity.
    public int? LocationId { get; init; }
    // Optional calculation provenance. Missing rules remain explicit in reports
    // instead of being silently credited as a known zero amount.
    public string? RuleId { get; init; }
    public string[]? MissingRuleKeys { get; init; }
}
/// <summary>Pending city reassignment in the provisional playable ruleset.</summary>
public enum OfficerMoveMode { Dispatch, Summon }
public sealed record PreparedOfficerMove(int OfficerId, int ForceId, int SourceCityId,
    int DestinationCityId, int DistrictId, OfficerMoveMode Mode);

/// <summary>The engine state is independent from any original or local save file layout.</summary>
public sealed partial record GameState
{
    public string ScenarioKey { get; init; } = "";
    public string MapSha256 { get; init; } = "";
    public string RulesetId { get; init; } = "";
    public string DifficultyId { get; init; } = "intermediate";
    // Null preserves unknown choices in old saves and difficulty-only callers.
    // Recording these inputs does not establish that their effects have been applied.
    public NewGameOptions? NewGameOptions { get; init; }
    public string[] InitializationIssues { get; init; } = [];
    public Dictionary<string, string> ContentHashes { get; init; } = [];
    public int MapWidth { get; init; }
    public int MapHeight { get; init; }
    public PeriodDate Date { get; init; }
    public TurnPhase Phase { get; init; } = TurnPhase.Strategy;
    public int PeriodNumber { get; init; }
    public ulong RandomState { get; init; } = 1;
    public Dictionary<int, ForceState> Forces { get; init; } = [];
    public Dictionary<int, ForceRankDefinition> ForceRanks { get; init; } = [];
    public Dictionary<int, OfficerOfficeDefinition> OfficerOffices { get; init; } = [];
    public Dictionary<int, DoctrineDefinition> Doctrines { get; init; } = [];
    public Dictionary<int, PolicyDefinition> Policies { get; init; } = [];
    public Dictionary<int, PolicyEffectDefinition> PolicyEffects { get; init; } = [];
    public Dictionary<int, ItemDefinition> ItemDefinitions { get; init; } = [];
    // Null means the inventory is unknown, including legacy saves. Empty means known empty.
    public Dictionary<int, ItemState>? Items { get; init; }
    public Dictionary<int, FormationDefinition> Formations { get; init; } = [];
    public Dictionary<int, TacticDefinition> Tactics { get; init; } = [];
    public Dictionary<int, DistrictState> Districts { get; init; } = [];
    public Dictionary<int, CityState> Cities { get; init; } = [];
    public Dictionary<int, GateState> Gates { get; init; } = [];
    public Dictionary<int, OfficerState> Officers { get; init; } = [];
    public Dictionary<int, AreaState> Areas { get; init; } = [];
    public AreaMapStatisticsState? RegionStatistics { get; init; }
    public CombatSettlementQueue? PendingCombatSettlements { get; init; }
    public PersonnelReturnTasks? PersonnelReturns { get; init; }
    public Dictionary<int, UnitState> Units { get; init; } = [];
    // Null preserves unknown original/legacy map constructions. Empty is known empty.
    public Dictionary<int, StructureState>? Structures { get; init; }
    public PeriodReportEntry[] PeriodReports { get; init; } = [];
    //Remake command events waiting for the next formal report. This queue does
    //not infer whether an old PeriodReports entry has already been presented.
    public PeriodReportEntry[] PendingCommandReports { get; init; } = [];
    public Dictionary<int, PreparedDeployment> PreparedDeployments { get; init; } = [];
    // Null is unresolved original/legacy planning state; playable worlds start with known emptiness.
    public Dictionary<int, PreparedOfficerMove>? PreparedOfficerMoves { get; init; }
    // Talent task timing is independent from officer relocation and deployment plans.
    public Dictionary<int, PreparedTalentOrder>? PreparedTalentOrders { get; init; }
    public int NextDeploymentId { get; init; } = 1;
    public Dictionary<int, TerritoryState> Territory { get; init; } = [];
}

public static class StateValidator
{
    public static void Validate(GameState state)
    {
        if (state is null) throw new InvalidDataException("Game state is missing.");
        if (state.ContentHashes is null || state.InitializationIssues is null ||
            state.Forces is null || state.ForceRanks is null || state.OfficerOffices is null ||
            state.Formations is null || state.Tactics is null || state.Districts is null ||
            state.Cities is null || state.Gates is null || state.Officers is null ||
            state.Areas is null || state.Units is null || state.PreparedDeployments is null ||
            state.Territory is null)
            throw new InvalidDataException("A required game-state collection is missing.");
        if (state.Forces.Values.Any(row => row is null) || state.ForceRanks.Values.Any(row => row is null) ||
            state.OfficerOffices.Values.Any(row => row is null) || state.Formations.Values.Any(row => row is null) ||
            state.Tactics.Values.Any(row => row is null) || state.Districts.Values.Any(row => row is null) ||
            state.Cities.Values.Any(row => row is null || row.AreaIds is null) ||
            state.Gates.Values.Any(row => row is null) || state.Officers.Values.Any(row => row is null) ||
            state.Areas.Values.Any(row => row is null) || state.Units.Values.Any(row => row is null) ||
            state.PreparedDeployments.Values.Any(row => row is null) || state.Territory.Values.Any(row => row is null) ||
            state.PreparedOfficerMoves?.Values.Any(row => row is null) == true ||
            state.PreparedTalentOrders?.Values.Any(row => row is null) == true)
            throw new InvalidDataException("A required game-state record is missing or incomplete.");
        if (string.IsNullOrWhiteSpace(state.ScenarioKey)) throw new InvalidDataException("Scenario key is missing.");
        if (string.IsNullOrWhiteSpace(state.RulesetId)) throw new InvalidDataException("Ruleset ID is missing.");
        if (state.MapSha256 is null || state.MapSha256.Length != 64 || state.MapSha256.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("Map SHA-256 is invalid.");
        foreach ((string key, string hash) in state.ContentHashes)
            if (string.IsNullOrWhiteSpace(key) || hash is null || hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c)))
                throw new InvalidDataException("Content revision reference is invalid.");
        if (state.MapWidth < 1 || state.MapHeight < 1) throw new InvalidDataException("Map dimensions are invalid.");
        int mapSize = checked(state.MapWidth * state.MapHeight);
        if (state.Forces.Count == 0) throw new InvalidDataException("No forces exist.");
        if (string.IsNullOrWhiteSpace(state.DifficultyId)) throw new InvalidDataException("Difficulty is missing.");
        if (state.NewGameOptions is { } options &&
            (string.IsNullOrWhiteSpace(options.DifficultyId) ||
             !string.Equals(options.DifficultyId, state.DifficultyId, StringComparison.Ordinal)))
            throw new InvalidDataException("New-game options conflict with the state's difficulty.");
        if (!Enum.IsDefined(state.Phase)) throw new InvalidDataException("Turn phase is invalid.");
        if (state.PeriodNumber < 0) throw new InvalidDataException("Period number is invalid.");
        if (state.Phase != TurnPhase.Initializing && state.InitializationIssues.Length != 0 &&
            !PlayableSkeleton.IsPlayableRuleset(state.RulesetId))
            throw new InvalidDataException("Initialization is incomplete.");
        _ = state.Date.Next();

        bool KnownForce(int? id) => id is null || state.Forces.ContainsKey(id.Value);
        bool KnownOfficer(int? id) => id is null || state.Officers.ContainsKey(id.Value);
        if (state.RegionStatistics is { } statistics)
        {
            if (statistics.Entries is null || statistics.UpdateOrder is null)
                throw new InvalidDataException("Regional map statistics are incomplete.");
            foreach ((int id, AreaMapStatisticsEntry area) in statistics.Entries)
                if (area is null || id != area.AreaId || id < 0 || area.CenterHexId < 0 || area.CenterHexId >= mapSize ||
                    area.OwnedCellCount < 0 || area.TerrainCellCount < 0 || !KnownForce(area.ForceId) ||
                    (area.CityId is int cityId && !state.Cities.ContainsKey(cityId)))
                    throw new InvalidDataException($"Regional map statistics for area {id} are invalid.");
            if (statistics.UpdateOrder.Any(id => !statistics.Entries.ContainsKey(id)))
                throw new InvalidDataException("The regional update order references a missing area.");
        }
        bool ValidHex(int? hex) => hex is null ? state.Phase == TurnPhase.Initializing ||
            PlayableSkeleton.IsPlayableRuleset(state.RulesetId) : hex >= 0 && hex < mapSize;
        if (state.Structures is { } structures)
            foreach ((int id, StructureState structure) in structures)
                if (structure is null || id != structure.Id || id < 0 ||
                    string.IsNullOrWhiteSpace(structure.KindId) ||
                    structure.HexId < 0 || structure.HexId >= mapSize ||
                    !KnownForce(structure.ForceId) || structure.Durability < 0 ||
                    structure.MaxDurability < structure.Durability ||
                    (structure.Completed && structure.MaxDurability == 0) ||
                    (structure.SourceCityId is int structureCity && !state.Cities.ContainsKey(structureCity)))
                    throw new InvalidDataException($"Map structure {id} is invalid.");
        if (state.PeriodReports is null || state.PendingCommandReports is null)
            throw new InvalidDataException("Period or pending command reports are missing.");
        if ((state.Phase == TurnPhase.Reporting && state.PendingCommandReports.Length != 0) ||
            state.PendingCommandReports.Any(entry => entry?.Kind == "period"))
            throw new InvalidDataException("Pending command reports cannot retain a published period or its summary marker.");
        foreach (PeriodReportEntry entry in state.PeriodReports.Concat(state.PendingCommandReports))
            if (entry is null || string.IsNullOrWhiteSpace(entry.Kind) || entry.Amount < 0 ||
                (entry.RuleId is not null && string.IsNullOrWhiteSpace(entry.RuleId)) ||
                (entry.MissingRuleKeys is not null && entry.MissingRuleKeys.Any(string.IsNullOrWhiteSpace)) ||
                (entry.Kind == "period" ? entry.ForceId != 0 : !state.Forces.ContainsKey(entry.ForceId)) ||
                (entry.CityId is int reportCity && !state.Cities.ContainsKey(reportCity)) ||
                (entry.LocationId is int reportLocation && !state.Cities.ContainsKey(reportLocation) &&
                    !state.Gates.ContainsKey(reportLocation)) ||
                (entry.OfficerId is int reportOfficer && !state.Officers.ContainsKey(reportOfficer)) ||
                (entry.FromHexId is int from && (from < 0 || from >= mapSize)) ||
                (entry.ToHexId is int to && (to < 0 || to >= mapSize)))
                throw new InvalidDataException("Period report contains an invalid reference.");
        bool KnownDistrict(int? id, int? forceId) => id is null ||
            (state.Districts.TryGetValue(id.Value, out DistrictState? district) && district.ForceId == forceId);

        if (state.Doctrines is null)
            throw new InvalidDataException("Doctrine definitions are missing.");
        foreach ((int id, DoctrineDefinition doctrine) in state.Doctrines)
            if (doctrine is null || id != doctrine.Id || id < 0 || doctrine.Name is null || doctrine.Description is null)
                throw new InvalidDataException($"Doctrine definition {id} is invalid.");
        if (state.Policies is null)
            throw new InvalidDataException("Policy definitions are missing.");
        if (state.PolicyEffects is null)
            throw new InvalidDataException("Policy effect definitions are missing.");
        foreach ((int id, PolicyEffectDefinition effect) in state.PolicyEffects)
            if (effect is null || id != effect.Id || id < 0 || effect.Name is null || effect.Description is null ||
                effect.LevelValues is not { Length: > 0 })
                throw new InvalidDataException($"Policy effect definition {id} is invalid.");
        foreach ((int id, PolicyDefinition policy) in state.Policies)
            if (policy is null || id != policy.Id || id < 0 || policy.Name is null ||
                policy.Description is null || policy.CostAddition < 0 ||
                policy.DepartmentCode is < 0 or > 4 ||
                (policy.EffectIds is not null && policy.EffectIds.Any(effectId => !state.PolicyEffects.ContainsKey(effectId))))
                throw new InvalidDataException($"Policy definition {id} is invalid.");
        if (state.ItemDefinitions is null)
            throw new InvalidDataException("Item definitions are missing.");
        foreach ((int id, ItemDefinition definition) in state.ItemDefinitions)
            if (definition is null || id != definition.Id || id < 0 || definition.Name is null)
                throw new InvalidDataException($"Item definition {id} is invalid.");
        if (state.Items is not null)
            foreach ((int id, ItemState item) in state.Items)
                if (item is null || id != item.Id || id < 0 || !state.ItemDefinitions.ContainsKey(item.DefinitionId) ||
                    (item.Holder?.OfficerId is int holder && (holder < 0 || !KnownOfficer(holder))))
                    throw new InvalidDataException($"Item {id} has an invalid definition or holder.");
        foreach ((int id, ForceState force) in state.Forces)
            if (id != force.Id || !KnownOfficer(force.RulerId) ||
                (force.SourceIndexedBytes13 is { Length: not 52 }) ||
                (force.SourceIndexedBytesEA is { Length: not 52 }) ||
                (force.SourceIndexedBytes13 is null) != (force.SourceIndexedBytesEA is null) ||
                (force.RankId is int rankId && !state.ForceRanks.ContainsKey(rankId)) ||
                (force.DoctrineId is int doctrineId && !state.Doctrines.ContainsKey(doctrineId)) ||
                force.CombatObjective?.TargetLocationId < 0 || force.CombatObjective?.Progress < 0 ||
                force.OrdersRemaining < 0 || force.OrderLimit < force.OrdersRemaining ||
                force.OrdersRemaining.HasValue != force.OrderLimit.HasValue)
                throw new InvalidDataException($"Force {id} has invalid orders or ID.");
        foreach ((int id, ForceRankDefinition rank) in state.ForceRanks)
            if (id != rank.Id || id < 0 || rank.CommandLimit < 0 || rank.AbilityBonus is null)
                throw new InvalidDataException($"Force rank {id} has invalid parameters or ID.");
        if (state.OfficerOffices is null)
            throw new InvalidDataException("Officer office definitions are missing.");
        foreach ((int id, OfficerOfficeDefinition office) in state.OfficerOffices)
            if (office is null || id != office.Id || id < 0 || office.Name is null || office.CommandLimit < 0 || office.AbilityBonus is null)
                throw new InvalidDataException($"Officer office {id} has invalid parameters or ID.");
        foreach ((int id, FormationDefinition formation) in state.Formations)
            if (id != formation.Id || id < 0 || formation.Name is null || formation.Description is null ||
                (formation.SourceBase is { } sourceBase &&
                 (sourceBase.Mobility < 0 || sourceBase.ArmyAttack < 0 ||
                  sourceBase.GarrisonAttack < 0 || sourceBase.DurabilityAttack < 0 ||
                  sourceBase.Defense < 0)))
                throw new InvalidDataException($"Formation definition {id} is invalid.");
        foreach ((int id, TacticDefinition tactic) in state.Tactics)
            if (id != tactic.Id || id < 0 || tactic.Name is null ||
                (tactic.FormationIds is { } formationIds && (formationIds.Distinct().Count() != formationIds.Length ||
                    formationIds.Any(formation => !state.Formations.ContainsKey(formation)))))
                throw new InvalidDataException($"Tactic definition {id} is invalid.");
        foreach ((int id, DistrictState district) in state.Districts)
            if (id != district.Id || !state.Forces.ContainsKey(district.ForceId) ||
                district.Number < 1 || !KnownOfficer(district.LeaderId) || district.OrdersRemaining < 0 ||
                district.OrderLimit < 0 || district.OrderLimit < district.OrdersRemaining)
                throw new InvalidDataException($"District {id} is invalid.");
        foreach ((int id, CityState city) in state.Cities)
        {
            if (id != city.Id || !KnownForce(city.ForceId) || !ValidHex(city.HexId) ||
                !KnownDistrict(city.DistrictId, city.ForceId) || !KnownOfficer(city.GovernorId) ||
                state.Gates.ContainsKey(id) || city.Gold < 0 || city.Supplies < 0 || city.Troops < 0 ||
                city.Morale < 0 || city.Durability < 0 ||
                (city.Duties is { } duties &&
                    (!KnownOfficer(duties.RecruiterId) || !KnownOfficer(duties.TrainerId) ||
                        duties.PreparationPeriods is < 0 or > 3)))
                throw new InvalidDataException($"City {id} is invalid.");
            foreach (int areaId in city.AreaIds)
                if (!state.Areas.TryGetValue(areaId, out AreaState? area) || area.CityId != id)
                    throw new InvalidDataException($"City {id} has an invalid area reference {areaId}.");
        }
        foreach ((int id, GateState gate) in state.Gates)
            if (id != gate.Id || !KnownForce(gate.ForceId) || !ValidHex(gate.HexId) ||
                !KnownDistrict(gate.DistrictId, gate.ForceId) || !KnownOfficer(gate.GovernorId) ||
                gate.Troops < 0 || gate.Durability < 0)
                throw new InvalidDataException($"Gate {id} is invalid.");
        foreach ((int id, OfficerState officer) in state.Officers)
            if (id != officer.Id || officer.Appointment is null || !KnownForce(officer.ForceId) ||
                !KnownDistrict(officer.DistrictId, officer.ForceId) ||
                (officer.LocationId is int location && !state.Cities.ContainsKey(location) && !state.Gates.ContainsKey(location)) ||
                ((officer.Presence is null or OfficerPresence.Present) &&
                    officer.Role != OfficerRole.Unresolved && officer.LocationId is null) ||
                !Enum.IsDefined(officer.Role) ||
                (officer.Sex is OfficerSex sex && !Enum.IsDefined(sex)) ||
                (officer.Presence is OfficerPresence presence && !Enum.IsDefined(presence)) ||
                (officer.OfficeId is int officeId && !state.OfficerOffices.ContainsKey(officeId)) ||
                (officer.DoctrineId is int doctrineId && !state.Doctrines.ContainsKey(doctrineId)) ||
                (officer.PolicyId is int policyId && !state.Policies.ContainsKey(policyId)) ||
                officer.PolicyBaseCost < 0 ||
                (officer.BaseFormationIds is { } formations &&
                    (formations.Distinct().Count() != formations.Length || formations.Any(f => !state.Formations.ContainsKey(f)))) ||
                (officer.BaseTacticIds is { } tactics &&
                    (tactics.Distinct().Count() != tactics.Length || tactics.Any(t => !state.Tactics.ContainsKey(t)))))
                throw new InvalidDataException($"Officer {id} is invalid.");
        foreach ((int id, AreaState area) in state.Areas)
            if (id != area.Id || !state.Cities.ContainsKey(area.CityId) ||
                !KnownForce(area.ForceId) || (area.HexId is int hex && (hex < 0 || hex >= mapSize)) ||
                (area.Administration?.OverseerId is int officerId && !state.Officers.ContainsKey(officerId)) ||
                (area.Administration?.DevelopmentFocus is AreaDevelopmentFocus focus && !Enum.IsDefined(focus)) ||
                (area.AdjacentAreaIds is { } neighbors && (neighbors.Distinct().Count() != neighbors.Length ||
                    neighbors.Any(neighbor => neighbor == area.Id || !state.Areas.ContainsKey(neighbor)))))
                throw new InvalidDataException($"Area {id} is invalid.");
        var deployedOfficers = new HashSet<int>();
        foreach ((int id, UnitState unit) in state.Units)
            if (id != unit.Id || !state.Forces.ContainsKey(unit.ForceId) ||
                !state.Officers.TryGetValue(unit.OfficerId, out OfficerState? officer) ||
                officer.ForceId != unit.ForceId || !officer.Deployed || !deployedOfficers.Add(unit.OfficerId) ||
                unit.HexId < 0 || unit.HexId >= state.MapWidth * state.MapHeight ||
                unit.DestinationHexId < 0 || unit.DestinationHexId >= state.MapWidth * state.MapHeight ||
                unit.Troops < 1 || unit.Supplies < 0 ||
                (PlayableSkeleton.IsPlayableRuleset(state.RulesetId) &&
                 unit.Mission?.Kind != UnitMissionKind.Transport && unit.Supplies != 0) ||
                (unit.SourceLocationId is int sourceId && !state.Cities.ContainsKey(sourceId) &&
                    !state.Gates.ContainsKey(sourceId)) ||
                (unit.FormationId is int formationId && !state.Formations.ContainsKey(formationId)) ||
                (unit.TacticIds is { } tacticIds &&
                    (tacticIds.Distinct().Count() != tacticIds.Length ||
                        tacticIds.Any(tacticId => !state.Tactics.ContainsKey(tacticId)))) ||
                (unit.Behavior is { } behavior && !Enum.IsDefined(behavior.AfterAction)) ||
                (unit.Mission is { } mission &&
                    (!Enum.IsDefined(mission.Kind) || !Enum.IsDefined(mission.Arrival) ||
                     mission.CargoGold < 0 ||
                     (mission.Kind == UnitMissionKind.Combat &&
                         (mission.DestinationCityId is not null || mission.CargoGold != 0 ||
                          mission.Arrival != TransportArrivalDisposition.EnterCity || mission.Returning ||
                          mission.TargetAreaId is not null || mission.StructureKindId is not null ||
                          mission.TargetStructureId is not null)) ||
                     (mission.Kind == UnitMissionKind.Transport &&
                         (mission.TargetAreaId is not null || mission.StructureKindId is not null ||
                          mission.TargetStructureId is not null ||
                          mission.DestinationCityId is not int destinationCityId ||
                          !state.Cities.TryGetValue(destinationCityId, out CityState? destinationCity) ||
                          destinationCity.HexId != unit.DestinationHexId ||
                          (mission.Returning &&
                           (mission.Arrival != TransportArrivalDisposition.ReturnToSource ||
                            mission.CargoGold != 0 || unit.Supplies != 0)))) ||
                     (mission.Kind == UnitMissionKind.OccupyArea &&
                         (mission.TargetAreaId is not int targetAreaId ||
                          !state.Areas.ContainsKey(targetAreaId) ||
                          mission.DestinationCityId is not null || mission.CargoGold != 0 ||
                          mission.Arrival != TransportArrivalDisposition.EnterCity || mission.Returning ||
                          mission.StructureKindId is not null || mission.TargetStructureId is not null)) ||
                     (mission.Kind == UnitMissionKind.Retreat &&
                         (mission.DestinationCityId is not null || mission.TargetAreaId is not null ||
                          mission.CargoGold != 0 ||
                          mission.Arrival != TransportArrivalDisposition.EnterCity || mission.Returning ||
                          mission.StructureKindId is not null || mission.TargetStructureId is not null)) ||
                     (mission.Kind == UnitMissionKind.BuildStructure &&
                         (string.IsNullOrWhiteSpace(mission.StructureKindId) ||
                          (PlayableSkeleton.IsPlayableRuleset(state.RulesetId) &&
                           !PlayableConstructionRules.IsSupportedKind(mission.StructureKindId)) ||
                          unit.SourceLocationId is not int buildCity || !state.Cities.ContainsKey(buildCity) ||
                          mission.DestinationCityId is not null || mission.TargetAreaId is not null ||
                          mission.CargoGold != 0 || mission.Returning ||
                          mission.Arrival != TransportArrivalDisposition.EnterCity ||
                          mission.TargetStructureId is not null)) ||
                     (mission.Kind is UnitMissionKind.RepairStructure or UnitMissionKind.DemolishStructure &&
                         (mission.TargetStructureId is not >= 0 ||
                          mission.StructureKindId is not null ||
                          mission.DestinationCityId is not null || mission.TargetAreaId is not null ||
                          mission.CargoGold != 0 || mission.Returning ||
                          mission.Arrival != TransportArrivalDisposition.EnterCity)))))
                throw new InvalidDataException($"Unit {id} is invalid.");
        if (state.NextDeploymentId < 1) throw new InvalidDataException("Next deployment ID is invalid.");
        if (state.Phase == TurnPhase.Reporting && state.PreparedDeployments.Count != 0)
            throw new InvalidDataException("Report state still contains unexecuted deployment preparations.");
        var preparedOfficers = new HashSet<int>();
        foreach ((int id, PreparedDeployment plan) in state.PreparedDeployments)
        {
            int? locationForce = null;
            int? locationDistrict = null;
            if (state.Cities.TryGetValue(plan.SourceLocationId, out CityState? city))
                (locationForce, locationDistrict) = (city.ForceId, city.DistrictId);
            else if (state.Gates.TryGetValue(plan.SourceLocationId, out GateState? gate))
                (locationForce, locationDistrict) = (gate.ForceId, gate.DistrictId);
            if (id != plan.Id || id < 1 || id >= state.NextDeploymentId || plan.BatchId < 1 || plan.BatchId > id ||
                !state.Forces.ContainsKey(plan.ForceId) ||
                locationForce != plan.ForceId || locationDistrict != plan.DistrictId ||
                !state.Districts.TryGetValue(plan.DistrictId, out DistrictState? district) || district.ForceId != plan.ForceId ||
                !state.Officers.TryGetValue(plan.OfficerId, out OfficerState? officer) || officer.ForceId != plan.ForceId ||
                officer.LocationId != plan.SourceLocationId || officer.Deployed || !preparedOfficers.Add(plan.OfficerId) ||
                plan.Troops < 1 || plan.FormationId < 0 || plan.TacticIds is null ||
                plan.TacticIds.Any(t => t < 0) ||
                plan.Actions is null || plan.Actions.Length == 0 ||
                plan.Actions.Any(a => !DeploymentStateValidation.IsActionTargetValid(state, a,
                    allowMissingUnitTargets: PlayableSkeleton.IsPlayableRuleset(state.RulesetId))) ||
                (PlayableSkeleton.IsPlayableRuleset(state.RulesetId) &&
                 plan.Actions.Any(action => action.ActionKey == "build"
                     ? action.Target.Kind != DeploymentTargetKind.Hex ||
                       !PlayableConstructionRules.IsSupportedKind(action.StructureKindId)
                     : action.StructureKindId is not null)) ||
                plan.WaypointHexIds is null ||
                plan.WaypointHexIds.Any(hex => hex < 0 || hex >= mapSize) || plan.Behavior is null ||
                !Enum.IsDefined(plan.Behavior.AfterAction))
                throw new InvalidDataException($"Prepared deployment {id} is invalid.");
        }
        foreach (var batch in state.PreparedDeployments.Values.GroupBy(p => p.BatchId))
            if (batch.Select(p => (p.ForceId, p.DistrictId, p.SourceLocationId)).Distinct().Count() != 1)
                throw new InvalidDataException($"Prepared deployment batch {batch.Key} is inconsistent.");
        foreach (int location in state.PreparedDeployments.Values.Select(p => p.SourceLocationId).Distinct())
        {
            int troops = state.Cities.TryGetValue(location, out CityState? city) ? city.Troops : state.Gates[location].Troops;
            if (DeploymentAvailability.ReservedTroops(state, location) > troops)
                throw new InvalidDataException($"Prepared troops overcommit source {location}.");
        }
        if (state.Phase == TurnPhase.Reporting && state.PreparedOfficerMoves is { Count: > 0 })
            throw new InvalidDataException("Report state still contains pending officer moves.");
        if (state.PreparedOfficerMoves is not null)
            foreach ((int id, PreparedOfficerMove move) in state.PreparedOfficerMoves)
                if (move is null || id != move.OfficerId ||
                    !state.Officers.TryGetValue(id, out OfficerState? officer) ||
                    officer.ForceId != move.ForceId || officer.LocationId != move.SourceCityId ||
                    officer.Presence != OfficerPresence.Present || officer.Deployed ||
                    !state.Cities.TryGetValue(move.SourceCityId, out CityState? source) ||
                    !state.Cities.TryGetValue(move.DestinationCityId, out CityState? destination) ||
                    move.SourceCityId == move.DestinationCityId ||
                    source.ForceId != move.ForceId || destination.ForceId != move.ForceId ||
                    !Enum.IsDefined(move.Mode) ||
                    (move.Mode == OfficerMoveMode.Dispatch ? source.DistrictId :
                        destination.DistrictId) != move.DistrictId ||
                    state.PreparedDeployments.Values.Any(plan => plan.OfficerId == id))
                    throw new InvalidDataException($"Prepared officer move {id} is invalid.");
        if (state.Phase == TurnPhase.Reporting && state.PreparedTalentOrders is { Count: > 0 })
            throw new InvalidDataException("Report state still contains pending talent tasks.");
        if (state.PreparedTalentOrders is not null)
            foreach ((int id, PreparedTalentOrder plan) in state.PreparedTalentOrders)
            {
                if (plan is null || id != plan.ExecutorOfficerId ||
                    !Enum.IsDefined(plan.Kind) ||
                    !state.Cities.TryGetValue(plan.CityId, out CityState? city) ||
                    city.ForceId != plan.ForceId || city.DistrictId != plan.DistrictId ||
                    !state.Districts.TryGetValue(plan.DistrictId, out DistrictState? district) ||
                    district.ForceId != plan.ForceId ||
                    !state.Officers.TryGetValue(id, out OfficerState? executor) ||
                    executor.ForceId != plan.ForceId || executor.LocationId != plan.CityId ||
                    executor.Presence != OfficerPresence.Present || executor.Deployed ||
                    state.PreparedOfficerMoves?.ContainsKey(id) == true ||
                    state.PreparedDeployments.Values.Any(deployment => deployment.OfficerId == id) ||
                    state.Cities.Values.Any(active => active.Duties?.RecruiterId == id ||
                        active.Duties?.TrainerId == id) ||
                    state.Areas.Values.Any(area => area.Administration?.OverseerId == id) ||
                    (plan.Kind == TalentOrderKind.Search && plan.TargetOfficerId is not null) ||
                    (plan.Kind == TalentOrderKind.Recruit &&
                        (plan.TargetOfficerId is not int targetId ||
                         !state.Officers.TryGetValue(targetId, out OfficerState? target) ||
                         target.Role != OfficerRole.Free || target.Presence != OfficerPresence.Present ||
                         target.ForceId is not null || target.LocationId != plan.CityId)))
                    throw new InvalidDataException($"Prepared talent task {id} is invalid.");
            }
        if (state.PreparedTalentOrders is not null &&
            state.PreparedTalentOrders.Values.Where(plan => plan.TargetOfficerId is not null)
                .GroupBy(plan => plan.TargetOfficerId).Any(group => group.Count() != 1))
            throw new InvalidDataException("One free officer has duplicate recruitment tasks.");
        GovernanceValidation.Validate(state);
        PlayableDiplomacy.Validate(state);
        PlayableMilitaryStateValidation.Validate(state);
        PlayableTacticStateValidation.Validate(state);
        PlayableForceDefeat.Validate(state);
        PlayableEventStateValidation.Validate(state);
        PlayablePersonnelStateValidation.Validate(state);
        EconomyReportStateValidation.Validate(state);
        NativeSubdayStateValidation.Validate(state);
        NativeNormalArmyCombatCalendarStateValidation.Validate(state);
        NativeArmyReturnCurrentStateValidation.Validate(state);
        ArmyLifecycleSaveValidation.Validate(state);
        ArmyMapOccupancySaveValidation.Validate(state);
        NativeNormalArmyAftermathStateValidation.Validate(state);
        CombatSettlementSaveValidation.Validate(state.PendingCombatSettlements);
        PersonnelReturnSaveValidation.Validate(state.PersonnelReturns);
        NativeForceUnregisterCurrentStateValidation.Validate(state);
        NativePlayableTerminalStateValidation.Validate(state);
        NativeBaseCaptureCurrentStateValidation.Validate(state);
        foreach (DistrictState district in state.Districts.Values)
            if (district.OrdersRemaining is int remaining && CommandBookAvailability.Reserved(state, district.Id) > remaining)
                throw new InvalidDataException($"Prepared orders overcommit district {district.Id}.");
        if (state.Territory.Count != 0 && state.Territory.Count != mapSize)
            throw new InvalidDataException("Territory is missing map cells.");
        foreach ((int id, TerritoryState cell) in state.Territory)
            if (id != cell.Id || id < 0 || id >= mapSize || !KnownForce(cell.ForceId) ||
                (cell.AreaId is int areaId && !state.Areas.ContainsKey(areaId)))
                throw new InvalidDataException($"Territory cell {id} is invalid.");
    }
}
