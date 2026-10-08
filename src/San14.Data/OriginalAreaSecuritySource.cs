using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>
/// The inspected CE static serializer transfers scenario area's byte2 to the
/// same +54 security byte used by execution opcodes193/194. This is a source
/// load projection, not a first-player final-state or subsequent-event claim.
/// </summary>
public static class OriginalAreaSecuritySource
{
    public static int ReadPreview(AreaPreviewRecord area, uint formatMarker)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (formatMarker != 0x66)
            throw new InvalidDataException("Area security currently requires the inspected CE source profile.");
        if (area.Value2 > 100)
            throw new InvalidDataException($"CE source area {area.Slot} has invalid security.");
        return area.Value2;
    }

    public static IReadOnlyDictionary<int, int> Read(S14Document scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (scenario.FormatMarker != 0x66)
            throw new InvalidDataException("Area security currently requires the inspected CE source profile.");
        IReadOnlyList<S14ScenarioArea> areas = S14ScenarioTables.ReadScenarioAreas(scenario);
        var security = new Dictionary<int, int>();
        foreach (S14ScenarioArea area in areas)
        {
            if (area.Value2 > 100 || !security.TryAdd(area.Slot, area.Value2))
                throw new InvalidDataException($"CE source area {area.Slot} has invalid security or repeated identity.");
        }
        return security;
    }

    public static GameState ApplyStaticSource(GameState state, S14Document scenario)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Phase != TurnPhase.Initializing)
            throw new InvalidOperationException("Static source security can only be projected during initialization.");
        IReadOnlyDictionary<int, int> security = Read(scenario);
        var areas = new Dictionary<int, AreaState>();
        foreach ((int id, AreaState area) in state.Areas)
        {
            if (!security.TryGetValue(id, out int value))
                throw new InvalidDataException($"Current area {id} has no CE source security record.");
            areas.Add(id, area with { Security = value });
        }
        GameState result = state with { Areas = areas };
        PlayableEventStateValidation.Validate(result);
        return result;
    }
}
