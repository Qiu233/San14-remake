using San14.Data.Formats;

namespace San14.Data;

public enum TerrainSeasonCalendarOrigin
{
    ExplicitSourceMonth,
    ScenarioWorldThirdDate
}

/// <summary>The original world+34 month is a distinct input from Core simulation dates.</summary>
public sealed record TerrainSeasonCalendarInput(int SourceMonth,
    TerrainSeasonCalendarOrigin Origin, SourceProvenance? Source,
    S14ScenarioSourceDate? SourceDate, string? SourceWorldSha256)
{
    public bool OrdinaryDateWriterResolved => false;

    public static TerrainSeasonCalendarInput FromExplicitSourceMonth(int sourceMonth)
    {
        MapSeasonMath.SelectMonth(sourceMonth);
        return new(sourceMonth, TerrainSeasonCalendarOrigin.ExplicitSourceMonth, null, null, null);
    }

    public static TerrainSeasonCalendarInput FromScenarioWorld(LoadedScenarioWorld source)
    {
        ArgumentNullException.ThrowIfNull(source);
        S14ScenarioSourceDate date = source.Document.RawThirdDate;
        MapSeasonMath.SelectMonth(date.RawMonth);
        return new(date.RawMonth, TerrainSeasonCalendarOrigin.ScenarioWorldThirdDate,
            source.Source, date, source.Document.WorldSha256);
    }
}

/// <summary>TC BCE470 scans the names at +40 and returns the corresponding +50 word.
/// These are context action object identifiers; their physical resource URI is not proved.</summary>
public sealed record TerrainSeasonActionLookup(uint RequestedNameHash,
    bool Matched, int SourceRowIndex, uint ActionObjectIdentifier);

public sealed record TerrainSeasonChangePlan(MapSeasonSelection Requested,
    int PreviouslyAppliedSeasonIndex, bool Forced, bool NativeContextAvailable,
    bool ApplyRequested, int AppliedSeasonIndex);

/// <summary>Recovered name-table lookup and TC C600 gate, without fabricated material,
/// texture, weather, or Core date producers. Application requires a separate source resolver.</summary>
public static class TerrainSeasonPreparation
{
    public const uint ActionTableContextLookupKey = 0x61AD16CF;
    public const uint EffectTableContextLookupKey = 0x97485E9B;

    public static TerrainSeasonActionLookup LookupActionObject(
        MapSeasonSelection selection, IReadOnlyList<uint> originalNameHashes,
        IReadOnlyList<uint> originalObjectIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(originalNameHashes);
        ArgumentNullException.ThrowIfNull(originalObjectIdentifiers);
        ValidateSelection(selection);
        if (originalNameHashes.Count != originalObjectIdentifiers.Count)
            throw new InvalidDataException("Original seasonal action name and object columns have different lengths.");
        for (int i = 0; i < originalNameHashes.Count; i++)
            if (originalNameHashes[i] == selection.ActionNameHash)
                return new(selection.ActionNameHash, true, i, originalObjectIdentifiers[i]);
        return new(selection.ActionNameHash, false, -1, 0);
    }

    /// <summary>Native cached index may be an initialization sentinel. No index is
    /// committed when the context is absent or an ordinary request is unchanged.</summary>
    public static TerrainSeasonChangePlan PlanChange(MapSeasonSelection requested,
        int previouslyAppliedSeasonIndex, bool forced, bool nativeContextAvailable)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ValidateSelection(requested);
        bool apply = (forced || previouslyAppliedSeasonIndex != requested.SeasonIndex) && nativeContextAvailable;
        return new(requested, previouslyAppliedSeasonIndex, forced, nativeContextAvailable, apply,
            apply ? requested.SeasonIndex : previouslyAppliedSeasonIndex);
    }

    internal static void ValidateSelection(MapSeasonSelection selection)
    {
        if (selection.SeasonIndex is < 0 or > 3 ||
            selection != MapSeasonMath.SelectMonth(selection.SeasonIndex * 3 + 1))
            throw new InvalidDataException("Seasonal action requires the complete original selection keys.");
    }
}
