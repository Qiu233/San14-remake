using System.Diagnostics.CodeAnalysis;

namespace San14.Core;

/// <summary>Copies the mutable parts of a world when ownership crosses a session or rule boundary.
/// Immutable record leaves remain shared; dictionaries and arrays never do.</summary>
public static class GameStateCopy
{
    private static PersonnelCommandTasks? CopyPersonnelCommands(PersonnelCommandTasks? value) => value is null ? null : value with
    {
        AvailableDispatchSlots = value.AvailableDispatchSlots.ToArray(),
        PendingRetirements = value.PendingRetirements.ToArray(),
        Disappearances = value.Disappearances?.ToDictionary(pair => pair.Key, pair => pair.Value with { MemberIds = pair.Value.MemberIds.ToArray() }),
        Diplomacies = value.Diplomacies?.ToDictionary(pair => pair.Key, pair => pair.Value with { MemberIds = pair.Value.MemberIds.ToArray() }),
        Strategies = value.Strategies?.ToDictionary(pair => pair.Key, pair => pair.Value with { MemberIds = pair.Value.MemberIds.ToArray() }),
        Independences = value.Independences?.ToDictionary(pair => pair.Key, pair => pair.Value with { MemberIds = pair.Value.MemberIds.ToArray() }),
        Talents = value.Talents?.ToDictionary(pair => pair.Key, pair => pair.Value with { MemberIds = pair.Value.MemberIds.ToArray() }),
        Returns = value.Returns.ToDictionary(pair => pair.Key, pair => pair.Value with
        {
            MemberIds = pair.Value.MemberIds.ToArray(),
            RouteDestinations = pair.Value.RouteDestinations.ToArray()
        })
    };

    [return: NotNullIfNotNull(nameof(value))]
    public static GameState? Copy(GameState? value) => value is null ? null : value with
    {
        Diplomacy = Copy(value.Diplomacy),
        NativePeriodContinuation = value.NativePeriodContinuation?.Copy(),
        NativeNormalTurnContinuation = value.NativeNormalTurnContinuation is not { } normalTurn ? null : normalTurn with
        { Grouping = normalTurn.Grouping with { Roots = normalTurn.Grouping.Roots.ToArray() } },
        CampaignExpeditions = value.CampaignExpeditions?.Copy(),
        // Both the reserve dictionary and its six-point vectors are immutable.
        ForceCreditReserves = value.ForceCreditReserves,
        // The template catalogue is owned read-only. Hiring detaches only the
        // selected template's mutable lists when it creates a world officer.
        PersonnelHire = value.PersonnelHire,
        CampaignCrtRandom = value.CampaignCrtRandom,
        ArmyMapSpeech = value.ArmyMapSpeech,
        CampaignTurnStop = value.CampaignTurnStop,
        ActiveDisasterScene = value.ActiveDisasterScene,
        ForeignCountryRelations = value.ForeignCountryRelations,
        NormalCombatRefresh = value.NormalCombatRefresh?.Copy(),
        CampaignEffectRequests = value.CampaignEffectRequests?.Copy(),
        ManualTacticIntervention = value.ManualTacticIntervention?.Copy(),
        CampaignDuelService = value.CampaignDuelService?.Copy(),
        MonthlyIncomeReports = CopyArray(value.MonthlyIncomeReports, item => Copy(item)!),
        InitializationIssues = CopyArray(value.InitializationIssues),
        ContentHashes = CopyDictionary(value.ContentHashes),
        Forces = CopyDictionary(value.Forces, item => Copy(item)!),
        ForceRanks = CopyDictionary(value.ForceRanks),
        OfficerOffices = CopyDictionary(value.OfficerOffices),
        Doctrines = CopyDictionary(value.Doctrines),
        Policies = CopyDictionary(value.Policies, item => Copy(item)!),
        PolicyEffects = CopyDictionary(value.PolicyEffects, item => Copy(item)!),
        ItemDefinitions = CopyDictionary(value.ItemDefinitions),
        Items = CopyDictionary(value.Items),
        LandmarkInvestments = value.LandmarkInvestments is null ? null : new(value.LandmarkInvestments),
        CampaignEvents = value.CampaignEvents is not { } events ? null : events with
        { Occurred = (bool[])events.Occurred.Clone(), Acknowledged = (bool[])events.Acknowledged.Clone(),
            OccurrenceDates = (CampaignEventOccurrenceDate?[])events.OccurrenceDates.Clone() },
        CampaignEventEligibility = value.CampaignEventEligibility is not { } eligibility ? null :
            eligibility with { Blocked = (bool[])eligibility.Blocked.Clone() },
        Formations = CopyDictionary(value.Formations),
        Tactics = CopyDictionary(value.Tactics, item => Copy(item)!),
        Districts = CopyDictionary(value.Districts),
        Cities = CopyDictionary(value.Cities, item => Copy(item)!),
        Gates = CopyDictionary(value.Gates),
        Officers = CopyDictionary(value.Officers, item => Copy(item)!),
        Areas = CopyDictionary(value.Areas, item => Copy(item)!),
        Units = CopyDictionary(value.Units, item => Copy(item)!),
        Structures = CopyDictionary(value.Structures),
        PeriodReports = CopyArray(value.PeriodReports, item => Copy(item)!),
        PendingCommandReports = CopyArray(value.PendingCommandReports, item => Copy(item)!),
        PendingArmyFireAnnouncements = value.PendingArmyFireAnnouncements.ToArray(),
        PendingArmyFireCasts = value.PendingArmyFireCasts.ToArray(),
        PendingForceRelationAnnouncements = value.PendingForceRelationAnnouncements.ToArray(),
        PreparedDeployments = CopyDictionary(value.PreparedDeployments, item => Copy(item)!),
        PreparedOfficerMoves = CopyDictionary(value.PreparedOfficerMoves),
        PreparedTalentOrders = CopyDictionary(value.PreparedTalentOrders),
        Territory = CopyDictionary(value.Territory),
        ArmyLifecycle = Copy(value.ArmyLifecycle),
        ArmyMapOccupancy = value.ArmyMapOccupancy?.Copy(),
        MapObjects = value.MapObjects?.Copy(),
        NativeArmySimulation = Copy(value.NativeArmySimulation),
        NativeBaseCaptureCurrent = Copy(value.NativeBaseCaptureCurrent),
        NativeForceUnregisterCurrent = Copy(value.NativeForceUnregisterCurrent),
        NativeNormalArmyCombatCalendar = Copy(value.NativeNormalArmyCombatCalendar),
        PersonnelCommands = CopyPersonnelCommands(value.PersonnelCommands),
        TalentGemDiscoveries = value.TalentGemDiscoveries is null ? null : new(
            value.TalentGemDiscoveries.Gems.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray())),
        GemForceInventory = value.GemForceInventory is null ? null : new(
            value.GemForceInventory.Forces.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray())),
        PendingCombatSettlements = value.PendingCombatSettlements is null ? null : new([.. value.PendingCombatSettlements.Requests]),
        NativeSubdayReports = CopyArray(value.NativeSubdayReports),
        EventRuntime = Copy(value.EventRuntime),
        EventReports = CopyArray(value.EventReports),
        ForceDefeats = CopyDictionary(value.ForceDefeats, item => Copy(item)!),
        PersonnelReports = CopyArray(value.PersonnelReports),
        TacticReports = CopyArray(value.TacticReports),
        RegionStatistics = CopyRegionStatistics(value.RegionStatistics),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static AreaState? Copy(AreaState? value) => value is null ? null : value with
    {
        AdjacentAreaIds = CopyArray(value.AdjacentAreaIds),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static CityState? Copy(CityState? value) => value is null ? null : value with
    {
        AreaIds = CopyArray(value.AreaIds),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static DiplomacyState? Copy(DiplomacyState? value) => value is null ? null : value with
    {
        Relationships = CopyDictionary(value.Relationships),
        Treaties = CopyDictionary(value.Treaties),
        Missions = CopyDictionary(value.Missions),
        AttackRequests = CopyDictionary(value.AttackRequests),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static ForceState? Copy(ForceState? value) => value is null ? null : value with
    {
        Governance = Copy(value.Governance),
        SourceIndexedBytes13 = CopyArray(value.SourceIndexedBytes13),
        SourceIndexedBytesEA = CopyArray(value.SourceIndexedBytesEA),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static GovernanceConfiguration? Copy(GovernanceConfiguration? value) => value is null ? null : value with
    {
        Assignments = CopyArray(value.Assignments),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static GovernanceState? Copy(GovernanceState? value) => value is null ? null : value with
    {
        OpenSlots = CopyArray(value.OpenSlots),
        Configured = Copy(value.Configured),
        Active = Copy(value.Active),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static MonthlyIncomeAreaReceipt? Copy(MonthlyIncomeAreaReceipt? value) => value is null ? null : value with
    {
        Inputs = CopyArray(value.Inputs),
        Steps = CopyArray(value.Steps),
        MissingInputs = CopyArray(value.MissingInputs),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static MonthlyIncomeQuote? Copy(MonthlyIncomeQuote? value) => value is null ? null : value with
    {
        MissingInputs = CopyArray(value.MissingInputs),
        Areas = CopyArray(value.Areas, item => Copy(item)!),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static MonthlyIncomeReportEntry? Copy(MonthlyIncomeReportEntry? value) => value is null ? null : value with
    {
        Quote = Copy(value.Quote),
    };


    [return: NotNullIfNotNull(nameof(value))]
    internal static NativeArmyForceState? Copy(NativeArmyForceState? value) => value is null ? null : value with
    {
        RelationsEA = CopyArray(value.RelationsEA),
        NormalAftermath = Copy(value.NormalAftermath),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static ArmyLifecycleState? Copy(ArmyLifecycleState? value) => value is null ? null : value with
    {
        Receivers = value.Receivers.Copy(),
        ArmyCautions = value.ArmyCautions?.Copy(),
        Indexes = value.Indexes.Copy(),
        ArmyGroups = value.ArmyGroups?.Copy(),
        Targets = value.Targets,
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static NativeArmyParameterState? Copy(NativeArmyParameterState? value) => value is null ? null : value with
    {
        Int32Values = CopyDictionary(value.Int32Values),
        Float32Values = CopyDictionary(value.Float32Values),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static NativeArmySimulationState? Copy(NativeArmySimulationState? value) => value is null ? null : value with
    {
        Parameters = Copy(value.Parameters),
        Forces = CopyDictionary(value.Forces, item => Copy(item)!),
        World = Copy(value.World),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static NativeArmyTacticRuntime? Copy(NativeArmyTacticRuntime? value) => value is null ? null : value with
    {
        StatModifiers4A = CopyArray(value.StatModifiers4A),
        StatDurations50 = CopyArray(value.StatDurations50),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static NativeArmyWorldState? Copy(NativeArmyWorldState? value) => value is null ? null : value with
    {
        PendingArmyIds = CopyArray(value.PendingArmyIds),
        ObjectPairs = CopyArray(value.ObjectPairs),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static NativeBaseCaptureCurrentState? Copy(NativeBaseCaptureCurrentState? value) => value is null ? null : value with
    {
        HexWords16 = CopyArray(value.HexWords16),
        Cities = CopyDictionary(value.Cities),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static NativeForceUnregisterCurrentState? Copy(NativeForceUnregisterCurrentState? value) => value is null ? null : value with
    {
        DistrictWindows10 = CopyDictionary(value.DistrictWindows10, item => CopyArray(item)!),
        ForceRankings = CopyDictionary(value.ForceRankings),
        ForceResetImages = CopyDictionary(value.ForceResetImages, item => CopyArray(item)!),
        PlaceholderRelations13 = CopyArray(value.PlaceholderRelations13),
        PlaceholderRelationsEA = CopyArray(value.PlaceholderRelationsEA),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static NativeNormalArmyCombatCalendarState? Copy(NativeNormalArmyCombatCalendarState? value) => value is null ? null : value with
    {
        LastAdvanceDiagnostics = CopyArray(value.LastAdvanceDiagnostics),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static NativeNormalArmyForceAftermath? Copy(NativeNormalArmyForceAftermath? value) => value is null ? null : value with
    {
        Relations13 = CopyArray(value.Relations13),
    };


    [return: NotNullIfNotNull(nameof(value))]
    internal static OfficerState? Copy(OfficerState? value) => value is null ? null : value with
    {
        BaseFormationIds = CopyArray(value.BaseFormationIds),
        BaseTacticIds = CopyArray(value.BaseTacticIds),
        CurrentTacticSlots = CopyArray(value.CurrentTacticSlots),
        CurrentTraitIds = CopyArray(value.CurrentTraitIds),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static PeriodReportEntry? Copy(PeriodReportEntry? value) => value is null ? null : value with
    {
        MissingRuleKeys = CopyArray(value.MissingRuleKeys),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static PlayableEventRuntimeState? Copy(PlayableEventRuntimeState? value) => value is null ? null : value with
    {
        SourceRevisions = CopyDictionary(value.SourceRevisions),
        Executions = CopyDictionary(value.Executions),
        Flags = CopyDictionary(value.Flags),
        Counters = CopyDictionary(value.Counters),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static PlayableForceDefeatRecord? Copy(PlayableForceDefeatRecord? value) => value is null ? null : value with
    {
        WithdrawnUnits = CopyArray(value.WithdrawnUnits, item => Copy(item)!),
        UnresolvedOfficerIds = CopyArray(value.UnresolvedOfficerIds),
        MissingRuleKeys = CopyArray(value.MissingRuleKeys),
        ResolvedOfficerIds = CopyArray(value.ResolvedOfficerIds),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static PlayableMilitaryPlan? Copy(PlayableMilitaryPlan? value) => value is null ? null : value with
    {
        WaypointHexIds = CopyArray(value.WaypointHexIds),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static PolicyDefinition? Copy(PolicyDefinition? value) => value is null ? null : value with
    {
        EffectIds = CopyArray(value.EffectIds),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static PolicyEffectDefinition? Copy(PolicyEffectDefinition? value) => value is null ? null : value with
    {
        LevelValues = CopyArray(value.LevelValues),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static PreparedDeployment? Copy(PreparedDeployment? value) => value is null ? null : value with
    {
        TacticIds = CopyArray(value.TacticIds),
        Actions = CopyArray(value.Actions),
        WaypointHexIds = CopyArray(value.WaypointHexIds),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static TacticDefinition? Copy(TacticDefinition? value) => value is null ? null : value with
    {
        FormationIds = CopyArray(value.FormationIds),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static UnitState? Copy(UnitState? value) => value is null ? null : value with
    {
        TacticIds = CopyArray(value.TacticIds),
        NativeTacticRuntime = Copy(value.NativeTacticRuntime),
        Formations = value.Formations, FieldAttackPolicy = value.FieldAttackPolicy,
        MoraleState = value.MoraleState,
        TraitRoster = value.TraitRoster,
        TacticRuntime = Copy(value.TacticRuntime),
        TransportRoute = value.TransportRoute,
        MilitaryPlan = Copy(value.MilitaryPlan),
    };

    [return: NotNullIfNotNull(nameof(value))]
    internal static UnitTacticRuntime? Copy(UnitTacticRuntime? value) => value is null ? null : value with
    {
        ReadyAtPeriod = CopyDictionary(value.ReadyAtPeriod),
        Statuses = CopyArray(value.Statuses),
    };

    [return: NotNullIfNotNull(nameof(value))]
    private static AreaMapStatisticsState? CopyRegionStatistics(AreaMapStatisticsState? value) =>
        value is null ? null : value with
        {
            Entries = new(value.Entries),
            UpdateOrder = value.UpdateOrder.ToArray()
        };

    [return: NotNullIfNotNull(nameof(values))]
    private static T[]? CopyArray<T>(T[]? values) => values?.ToArray();

    [return: NotNullIfNotNull(nameof(values))]
    private static T[]? CopyArray<T>(T[]? values, Func<T, T> copy) =>
        values?.Select(copy).ToArray();

    [return: NotNullIfNotNull(nameof(values))]
    private static Dictionary<TKey, TValue>? CopyDictionary<TKey, TValue>(
        Dictionary<TKey, TValue>? values) where TKey : notnull =>
        values is null ? null : new(values, values.Comparer);

    [return: NotNullIfNotNull(nameof(values))]
    private static Dictionary<TKey, TValue>? CopyDictionary<TKey, TValue>(
        Dictionary<TKey, TValue>? values, Func<TValue, TValue> copy) where TKey : notnull =>
        values?.ToDictionary(pair => pair.Key, pair => copy(pair.Value), values.Comparer);
}
