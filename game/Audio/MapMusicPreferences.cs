using System.Text.Json.Serialization;
using San14.Data;

internal sealed record MapMusicPreferenceFile(
    [property: JsonRequired] sbyte[] SourceSelections,
    [property: JsonRequired] Dictionary<int, AudioMusicCueMapping> CueMapping);

internal sealed record MapMusicEntrySelection(int SourceMonth, AudioPreparedMusicSelection Selection,
    AudioPreparedMusicCommit Commit);

/// <summary>One application-owned current music selection and cue mapping.
/// Fresh constructor defaults are installed once, independently of campaigns.
/// Missing/unsupported current map entries keep the selector's real zero result.</summary>
internal sealed class MapMusicPreferences
{
    private readonly sbyte[] selections;
    private readonly Dictionary<int, AudioMusicCueMapping> mapping;

    internal MapMusicPreferences()
    {
        selections = Enumerable.Repeat((sbyte)-1, AudioMapMusicSelector.SourceSlotCount).ToArray();
        mapping = Enumerable.Range(0, AudioMapMusicSelector.MusicIndexCount)
            .ToDictionary(index => index, AudioMapMusicSelector.StockMappingForIndex);
    }

    internal MapMusicPreferences(MapMusicPreferenceFile file)
    {
        if (file.SourceSelections is null || file.SourceSelections.Length != AudioMapMusicSelector.SourceSlotCount ||
            file.CueMapping is null || file.CueMapping.Count > AudioMapMusicSelector.MusicIndexCount ||
            file.CueMapping.Any(pair => pair.Key is < 0 or >= AudioMapMusicSelector.MusicIndexCount || pair.Value is null))
            throw new InvalidDataException("Current music preferences have an invalid selection or mapping layout.");
        selections = (sbyte[])file.SourceSelections.Clone();
        mapping = new(file.CueMapping);
    }

    internal MapMusicPreferenceFile Capture() => new((sbyte[])selections.Clone(), new(mapping));

    internal MapMusicEntrySelection SelectEntry(string locale, int currentThirdMonth)
    {
        var selector = AudioMapMusicSelector.Create(locale);
        int slot = AudioMapMusicSelector.SelectMonthSourceSlot(currentThirdMonth);
        sbyte setting = selections[slot];
        int configured = setting == -1 ? selector.DefaultMusicIndices[slot] : setting;
        // These are the exact reached query1 and indexed-query contracts for
        // the supported CE TC/SC profile, not an arbitrary entitlement query.
        var selected = selector.PrepareFromSlot(slot, setting, new(1, 1),
            configured is >= 28 and <= 115 ? new(1, 1) : null);
        mapping.TryGetValue(selected.EffectiveMusicIndex, out var current);
        return new(currentThirdMonth, selected, selector.PrepareCommit(selected, current));
    }
}
