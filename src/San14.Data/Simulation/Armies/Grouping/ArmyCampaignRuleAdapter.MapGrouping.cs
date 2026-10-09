using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private readonly ArmyMapGroupingOwner _mapArmyGrouping = new();
    private ArmyMapGroupingProfile? _mapArmyGroupingProfile;
    private GameState? _mapArmyGroupingCapturedWorld;
    private ArmyMapGroupingFrame? _mapArmyGroupingFrame;

    /// <summary>Only after the actual fresh5012209A0/2269E0 constructor,
    ///never merely because an old self-format save was continued.</summary>
    public void InitializeMapArmyGroupsKnownReset(IEnumerable<int> physicalSlots)
    {
        _mapArmyGrouping.InitializeKnownReset(physicalSlots);
        _mapArmyGroupingCapturedWorld = null; _mapArmyGroupingFrame = null;
    }
    /// <summary>A committed2269E0 receiver reset, not a UI visibility/reset.</summary>
    public void ResetMapArmyGroupReceiver(int physicalSlot)
    {
        _mapArmyGrouping.ResetReceiver(physicalSlot);
        _mapArmyGroupingCapturedWorld = null; _mapArmyGroupingFrame = null;
    }
    /// <summary>The explicit2FCDA0 game-stage writer. Camera Capture does not
    ///call it; the currently unimplemented full state-machine cadence stays separate.</summary>
    public ArmyMapGroupingRecomputeResult RecomputeMapArmyGroups(GameState state)
    {
        if (MapArmyGroupingProfile() is not { } profile) return new(false, [], "source.Army_grouping_locale_tables");
        if (CaptureMapArmyGroupingCurrent(state) is not { } current) return new(false, [], "current.Army_grouping_context");
        var queries = MapArmyGroupingQueries(state, current);
        var result = _mapArmyGrouping.Recompute(profile, queries);
        if (result.Applied)
        {
            _mapArmyGroupingFrame = _mapArmyGrouping.Capture(profile, queries);
            _mapArmyGroupingCapturedWorld = state;
        }
        return result;
    }
    /// <summary>Pre-submit immutable publication. Repeated camera requests on
    ///the identical World reuse this frame; no live owner/context escapes it.</summary>
    public ArmyMapGroupingFrame? CaptureMapArmyGroups(GameState state)
    {
        if (ReferenceEquals(state, _mapArmyGroupingCapturedWorld)) return _mapArmyGroupingFrame;
        return CaptureMapArmyGroupingCurrent(state) is { } current ? CaptureMapArmyGroups(state, current) : null;
    }
    /// <summary>Reuse an already captured current query view when the caller
    ///owns it for this same World; the resulting frame still contains values only.</summary>
    public ArmyMapGroupingFrame? CaptureMapArmyGroups(GameState state, SourceWorldContext current)
    {
        if (ReferenceEquals(state, _mapArmyGroupingCapturedWorld)) return _mapArmyGroupingFrame;
        if (MapArmyGroupingProfile() is not { } profile) return null;
        _mapArmyGroupingFrame = _mapArmyGrouping.Capture(profile, MapArmyGroupingQueries(state, current));
        _mapArmyGroupingCapturedWorld = state;
        return _mapArmyGroupingFrame;
    }
    private SourceWorldContext? CaptureMapArmyGroupingCurrent(GameState state)
    {
        //The existing definition boundary supplies current tables. Grouping
        //does not repeat whole-owner validation or import writable list storage.
        if (state.Officers.Values.Any(person => person.NativeAbilityRuntime is null)) return null;
        var current = _definitions.Capture(state);
        if (current is null) return null;
        current.OverlayOwnedArmyReturnCurrentFields(state);
        _personDestructionInputs.BindCurrent(state, current);
        OwnedArmyDestructionCurrent.BindOwnedCurrentContext(state, current);
        ForceUnregisterInputs.BindCurrent(state, current);
        BaseCaptureCurrentInputs.BindCurrent(state, current);
        return current;
    }
    private ArmyMapGroupingProfile? MapArmyGroupingProfile() => Seed.Language is "SC" or "TC"
        ? _mapArmyGroupingProfile ??= ArmyMapGroupingProfile.Create() : null;
    private ArmyMapGroupingCurrentQueries MapArmyGroupingQueries(GameState state, SourceWorldContext current)
        => new(state, current, _scenario, _governorInputs, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
}
