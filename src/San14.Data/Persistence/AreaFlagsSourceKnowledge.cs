using San14.Core;

namespace San14.Data;

/// <summary>The complete mutable source byte, including report and special-mode
/// bits. Identity is the physical Area table receiver, including default0.</summary>
public sealed record AreaFlagsSourceResidue
{
    public int AreaId { get; }
    public byte Flags38 { get; }
    public AreaFlagsSourceResidue(int areaId, byte flags38)
    {
        if ((uint)areaId >= 501) throw new InvalidDataException("Area flags require a physical receiver0..500.");
        AreaId = areaId; Flags38 = flags38;
    }
}

internal static class AreaFlagsSourceAdapter
{
    // Only ScenarioInitializer's new allocation calls this. Restore and captures
    // must not recover missing knowledge from the installed scenario.
    internal static GameState InstallFresh(GameState state, LoadedScenario scenario)
    {
        var knowledge = CampaignSourceKnowledge.From(state.ResourceState);
        if (state.Phase != TurnPhase.Initializing || state.NativeClock is not null ||
            state.NativeArmySimulation is not null || knowledge.AreaFlags.Count != 0 ||
            scenario.Metadata.FormatMarker != 0x66 || scenario.Snapshot.Areas.Count != 501)
            throw new InvalidDataException("Fresh Area flags require an unused initializing CE scenario owner.");
        var rows = new List<AreaFlagsSourceResidue>(501);
        for (int physical = 0; physical < 501; physical++)
        {
            var source = scenario.Snapshot.Areas[physical];
            if (source.Slot != physical || source.RawFlags38 is not byte flags)
                throw new InvalidDataException("Fresh Area flags require every actual scenario byte.");
            rows.Add(new(physical, flags));
        }
        return state with { ResourceState = knowledge.ReplaceAreaFlags(rows) };
    }

    internal static void BindCurrent(GameState state, SourceWorldContext current)
    {
        var knowledge = CampaignSourceKnowledge.From(state.ResourceState);
        for (int physical = 0; physical < 501; physical++)
            current.SetAreaFlags38(physical, knowledge.FindAreaFlags(physical)?.Flags38);
        // A read-only capture cannot silently perform a campaign store into
        // only its mirror. The error is reached at the actual write point.
        current.AreaFlags38WritePreparation = (_, _) =>
            throw new InvalidDataException("Current Area flags write has no reached campaign publisher.");
    }

    /// <summary>Bind one operation's latest world and publisher to the same
    /// current. The publisher assigns the prepared state; observers run after
    /// the closed current-store/publication pair, not inside that assignment.</summary>
    internal static void BindMutation(SourceWorldContext current, Func<GameState> latest,
        Action<GameState> publish)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(latest);
        ArgumentNullException.ThrowIfNull(publish);
        BindCurrent(latest(), current);
        current.AreaFlags38WritePreparation = (physical, flags) =>
        {
            GameState before = latest();
            var knowledge = CampaignSourceKnowledge.From(before.ResourceState);
            GameState reached = before with { ResourceState = knowledge.WithAreaFlags(new(physical, flags)) };
            return () => publish(reached);
        };
    }
}
