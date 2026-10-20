namespace San14.Core;

/// <summary>Proof of a source construction performed for this game's own Army.
/// It does not identify a record in an original running process.</summary>
public sealed record NativeArmyOrigin(string Revision, string Language, string RuleVersion)
{
    public int? NativeSlotIndex { get; init; }
}
public sealed partial record UnitState
{
    public NativeArmyOrigin? NativeArmyOrigin { get; init; }
    public byte? NativeCommandChoice41 { get; init; }
    public ArmyFormationState? Formations { get; init; }
    public ArmyOrdinaryFieldAttackPolicy? FieldAttackPolicy { get; init; }
    /// <summary>Current native Army saved region Hex152. Its source writer is
    /// independent of the unit's movement Hex and Core region identity.</summary>
    public ushort? NativeSavedRegionHex152 { get; init; }
}

public sealed record PlayableArmyCreationResolution(NativeArmyTacticRuntime Runtime,
    int WoundedTroops, NativeArmyOrigin Origin, ulong RandomState)
{
    public ArmyMarchState? March { get; init; }
    public byte? NativeCommandChoice41 { get; init; }
    public ArmyOverrideTargetState? OverrideTarget { get; init; }
    public ArmyFormationState? Formations { get; init; }
    public ArmyOrdinaryFieldAttackPolicy? FieldAttackPolicy { get; init; }
    public ArmyTraitRoster? TraitRoster { get; init; }
    public int? CurrentMorale { get; init; }
    public ushort? NativeSavedRegionHex152 { get; init; }
    public int? CurrentHexId { get; init; }
    public byte? NativeDirection36 { get; init; }
    public byte? NativeMapUiByte13C { get; init; }
    public byte? NativeCommandControl140 { get; init; }
    public byte? NativeRouteHold56 { get; init; }
    public ushort? NativeRouteGoal1F8 { get; init; }
    public ushort? NativeHomeSequence28 { get; init; }
    /// <summary>Explicit commander location write from the source creator.
    /// Core applies the receipt without deriving a native location encoding.</summary>
    public OfficerPosition? CommanderPosition { get; init; }
    public ICampaignResourceState? ResourceState { get; init; }
    public ArmyStores? CarriedStores { get; init; }
    /// <summary>Explicit saved arena writes produced alongside this receiver.
    /// Core retains the source pool/header owner between queued allocations.</summary>
    public ArmyLifecycleState? Lifecycle { get; init; }
    public ArmyMapOccupancyState? ArmyMapOccupancy { get; init; }
}

/// <summary>Explicit initialization after this game's deployment allocation.
/// Null rejects the period atomically; the default skeleton does not install it.</summary>
public interface IPlayableArmyCreationRules
{
    PlayableArmyCreationResolution? Create(GameState state, PreparedDeployment deployment, UnitState army);
}

/// <summary>Optional source preparation check. This inspects the complete
/// pending allocation queue without publishing construction, debits or RNG.
/// Execution still checks its actual later inputs atomically.</summary>
public interface IPlayableArmyCreationPreparationRules
{
    DeploymentEligibility CheckPreparedDeployments(GameState preparedState);
}

public static class PlayableArmyCreation
{
    /// <summary>Publishes one real registered Army and its source owners in the
    /// caller's transaction. Both immediate normal commands and legacy queued
    /// execution use this single receipt application.</summary>
    public static GameState PublishCreated(GameState state, UnitState army, PlayableArmyCreationResolution receipt)
    {
        if (state.Units.ContainsKey(army.Id)) throw new InvalidDataException("Created Army identity is already active.");
        var created = Apply(state, army, receipt);
        var commander = state.Officers[army.OfficerId];
        if (receipt.CommanderPosition is { } position)
            commander = commander with { LocationState = commander.LocationState is { } location
                ? location with { Position = position } : new(position, null) };
        var result = PublishOwners(state, receipt) with
        {
            Units = new(state.Units) { [created.Id] = created },
            Officers = new(state.Officers) { [commander.Id] = commander with { Deployed = true } },
            RandomState = receipt.RandomState
        };
        return ArmyIdentityAllocation.Commit(result, created.Id);
    }
    internal static GameState PublishOwners(GameState state, PlayableArmyCreationResolution result) => state with
    {
        ResourceState = result.ResourceState ?? state.ResourceState,
        ArmyLifecycle = result.Lifecycle ?? state.ArmyLifecycle,
        ArmyMapOccupancy = result.ArmyMapOccupancy ?? state.ArmyMapOccupancy
    };

    /// <summary>Starts an owned native clock at the current strategy boundary.
    /// An existing clock is preserved and never reset during loading or deployment.</summary>
    public static GameState InitializeClock(GameState state, string revision)
    {
        StateValidator.Validate(state);
        if (string.IsNullOrWhiteSpace(revision))
            throw new InvalidDataException("Army construction clock revision is missing.");
        if (state.NativeClock is not null) return state;
        if (state.Phase != TurnPhase.Strategy)
            throw new InvalidDataException("Army construction clock requires a strategy boundary.");
        GameState next = GameStateSnapshot.Detach(state) with
        {
            NativeClock = new(state.Date, 0, 0, revision), NativeSubdayReports = []
        };
        StateValidator.Validate(next);
        return next;
    }

    internal static UnitState Apply(GameState state, UnitState army, PlayableArmyCreationResolution result)
    {
        if (state.NativeClock is null || result.WoundedTroops is < 0 or > ushort.MaxValue ||
            result.Runtime is null || result.Origin is null ||
            state.ArmyLifecycle is not null && result.Lifecycle is null ||
            state.ArmyMapOccupancy is not null && result.ArmyMapOccupancy is null)
            throw new InvalidDataException("Source-created Army initialization is incomplete.");
        if (result.CurrentHexId is int confirmedHex && confirmedHex != army.HexId)
            throw new InvalidDataException("Army creation position differs from its deployment position.");
        NativeSubdayStateValidation.ValidateRuntime(state, result.Runtime);
        UnitState changed = army with
        {
            WoundedTroops = result.WoundedTroops, NativeArmyOrigin = result.Origin with { },
            MoraleState = army.MoraleState with { CurrentValue = result.CurrentMorale },
            NativeSavedRegionHex152 = result.NativeSavedRegionHex152,
            PositionKnowledge = result.CurrentHexId.HasValue ? ArmyPositionKnowledge.Confirmed : ArmyPositionKnowledge.Unknown,
            NativeDirection36 = result.NativeDirection36,
            NativeMapUiByte13C = result.NativeMapUiByte13C,
            NativeCommandControl140 = result.NativeCommandControl140,
            NativeCommandChoice41 = result.NativeCommandChoice41,
            NativeRouteHold56 = result.NativeRouteHold56,
            NativeRouteGoal1F8 = result.NativeRouteGoal1F8,
            NativeHomeSequence28 = result.NativeHomeSequence28,
            OverrideTarget = result.OverrideTarget,
            March = result.March,
            CarriedStores = result.CarriedStores,
            Formations = result.Formations, FieldAttackPolicy = result.FieldAttackPolicy,
            TraitRoster = result.TraitRoster,
            NativeTacticRuntime = result.Runtime with
            {
                StatModifiers4A = result.Runtime.StatModifiers4A.ToArray(),
                StatDurations50 = result.Runtime.StatDurations50.ToArray()
            }
        };
        Validate(state, changed);
        return changed;
    }

    internal static void Validate(GameState state, UnitState army)
    {
        if (army.NativeCommandChoice41 is > 2) throw new InvalidDataException("Army command choice exceeds its source clamp.");
        if (army.TransportRoute is not null && army.Mission?.Kind != UnitMissionKind.Transport)
            throw new InvalidDataException("Transport waypoints require a transport mission.");
        if ((army.NativeSavedRegionHex152 is not null || army.CarriedStores is not null) && (state.NativeClock is null ||
            army.NativeArmyOrigin is null || army.FieldAttackPolicy is null))
            throw new InvalidDataException("Current native Army attributes require their explicit Army construction and clock.");
        if (army.NativeSavedRegionHex152 is > 48400)
            throw new InvalidDataException("Current native Army saved region Hex is outside the source map.");
        if (army.NativeArmyOrigin is not { } origin) return;
        if (string.IsNullOrWhiteSpace(origin.Revision) || origin.Language is not ("SC" or "TC") ||
            string.IsNullOrWhiteSpace(origin.RuleVersion) ||
            origin.NativeSlotIndex is null or < 1 or > 500 ||
            state.NativeClock is null || army.NativeTacticRuntime is null || army.FieldAttackPolicy is null ||
            army.WoundedTroops is null || army.FormationId is null ||
            army.TacticIds is null || army.SourceLocationId is null || army.Troops > ushort.MaxValue ||
            army.Mission?.Kind == UnitMissionKind.Transport &&
                (army.NativeTacticRuntime.Status10 != 2 || army.TransportRoute is null ||
                 army.NativeHomeSequence28 is null || army.FormationId != 19 ||
                 army.CarriedStores is not { } cargo || cargo.Gold != army.Mission.CargoGold ||
                 cargo.Food != army.Supplies))
            throw new InvalidDataException("Source-created Army provenance or current construction facts are incomplete.");
    }
}
