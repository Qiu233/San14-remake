namespace San14.Core;

/// <summary>Proof of a source construction performed for this game's own Army.
/// It does not identify a record in an original running process.</summary>
public sealed record NativeArmyOrigin(string Revision, string Language, string RuleVersion)
{
    public int? NativeSlotIndex { get; init; }
}
/// <summary>Independent current native fields; an omitted byte remains unknown.
/// Effective formation still requires the reached terrain branch.</summary>
public sealed record NativeArmyConfiguration(byte? Formation1B, byte? Formation1C,
    byte? Action37, byte? Category40)
{
    public ushort[]? TraitSlots64 { get; init; }
}

public sealed partial record UnitState
{
    public NativeArmyOrigin? NativeArmyOrigin { get; init; }
    public NativeArmyConfiguration? NativeArmyConfiguration { get; init; }
    /// <summary>Independent current native Army byte1A. Missing source writes
    /// remain unknown; the provisional Morale value never fills this field.</summary>
    public byte? NativeMorale1A { get; init; }
    /// <summary>Current native Army saved region Hex152. Its source writer is
    /// independent of the unit's movement Hex and Core region identity.</summary>
    public ushort? NativeSavedRegionHex152 { get; init; }
}

public sealed record PlayableArmyCreationResolution(NativeArmyTacticRuntime Runtime,
    int WoundedTroops, NativeArmyOrigin Origin, ulong RandomState)
{
    public NativeArmyOverrideTargetRuntime? NativeOverrideTarget { get; init; }
    public NativeArmyConfiguration? Configuration { get; init; }
    public byte? NativeMorale1A { get; init; }
    public ushort? NativeSavedRegionHex152 { get; init; }
    public ushort? NativeCurrentHex2A { get; init; }
    public byte? NativeDirection36 { get; init; }
    public byte? NativeMapUiByte13C { get; init; }
    public byte? NativeCommandControl140 { get; init; }
    public byte? NativeRouteHold56 { get; init; }
    public ushort? NativeRouteGoal1F8 { get; init; }
    public ushort? NativeHomeSequence28 { get; init; }
    /// <summary>Explicit commander location write from the source creator.
    /// Core applies the receipt without deriving a native location encoding.</summary>
    public ushort? CommanderSecondLocation11C { get; init; }
    public NativeArmyReturnResources? NativeReturnResources { get; init; }
    /// <summary>Explicit saved arena writes produced alongside this receiver.
    /// Core retains the source pool/header owner between queued allocations.</summary>
    public NativeArmyLifecycleState? NativeLifecycle { get; init; }
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
    internal static GameState PublishOwners(GameState state, PlayableArmyCreationResolution result) => state with
    {
        NativeArmyLifecycle = result.NativeLifecycle ?? state.NativeArmyLifecycle,
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
            state.NativeArmyLifecycle is not null && result.NativeLifecycle is null ||
            state.ArmyMapOccupancy is not null && result.ArmyMapOccupancy is null)
            throw new InvalidDataException("Source-created Army initialization is incomplete.");
        NativeSubdayStateValidation.ValidateRuntime(state, result.Runtime);
        UnitState changed = army with
        {
            WoundedTroops = result.WoundedTroops, NativeArmyOrigin = result.Origin with { },
            NativeMorale1A = result.NativeMorale1A,
            NativeSavedRegionHex152 = result.NativeSavedRegionHex152,
            NativeCurrentHex2A = result.NativeCurrentHex2A,
            NativeDirection36 = result.NativeDirection36,
            NativeMapUiByte13C = result.NativeMapUiByte13C,
            NativeCommandControl140 = result.NativeCommandControl140,
            NativeRouteHold56 = result.NativeRouteHold56,
            NativeRouteGoal1F8 = result.NativeRouteGoal1F8,
            NativeHomeSequence28 = result.NativeHomeSequence28,
            NativeOverrideTarget = result.NativeOverrideTarget,
            NativeReturnResources = result.NativeReturnResources is null ? null : result.NativeReturnResources with { },
            NativeArmyConfiguration = result.Configuration is null ? null : result.Configuration with
                { TraitSlots64 = result.Configuration.TraitSlots64?.ToArray() },
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
        if (army.NativeTransport is { } transport &&
            (army.NativeArmyOrigin is null || army.Mission?.Kind != UnitMissionKind.Transport ||
             transport.Waypoints2C is not { Length: 5 } || transport.Choice41 > 2))
            throw new InvalidDataException("Native transport fields require their actual source-created transport owner.");
        if ((army.NativeMorale1A is not null || army.NativeSavedRegionHex152 is not null || army.NativeCurrentHex2A is not null || army.NativeReturnResources is not null) && (state.NativeClock is null ||
            army.NativeArmyOrigin is null || army.NativeArmyConfiguration is null))
            throw new InvalidDataException("Current native Army attributes require their explicit Army construction and clock.");
        if (army.NativeSavedRegionHex152 is > 48400)
            throw new InvalidDataException("Current native Army saved region Hex is outside the source map.");
        if (army.NativeCurrentHex2A is > 48399)
            throw new InvalidDataException("Current native Army position Hex is outside the source map.");
        if (army.NativeArmyOrigin is not { } origin) return;
        if (string.IsNullOrWhiteSpace(origin.Revision) || origin.Language is not ("SC" or "TC") ||
            string.IsNullOrWhiteSpace(origin.RuleVersion) ||
            origin.NativeSlotIndex is null or < 1 or > 500 ||
            state.NativeClock is null || army.NativeTacticRuntime is null || army.NativeArmyConfiguration is null ||
            army.WoundedTroops is null || army.FormationId is null ||
            army.TacticIds is null || army.SourceLocationId is null || army.Troops > ushort.MaxValue ||
            army.Mission?.Kind == UnitMissionKind.Transport &&
                (army.NativeTacticRuntime.Status10 != 2 || army.NativeTransport is not { Waypoints2C.Length: 5, Choice41: <= 2 } ||
                 army.NativeHomeSequence28 is null || army.FormationId != 19 ||
                 army.NativeReturnResources is not { } cargo || cargo.Gold5C != army.Mission.CargoGold ||
                 cargo.Supplies60 != army.Supplies) ||
            army.NativeArmyConfiguration.TraitSlots64 is { Length: not 36 } ||
            army.NativeArmyConfiguration.Formation1B is byte landFormation && landFormation != army.FormationId)
            throw new InvalidDataException("Source-created Army provenance or current construction facts are incomplete.");
    }
}
