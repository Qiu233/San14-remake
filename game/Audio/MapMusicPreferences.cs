using System.Text.Json.Serialization;
using San14.Data;

internal sealed record MapMusicPreferenceFile(
    [property: JsonRequired] sbyte[] SourceSelections,
    [property: JsonRequired] Dictionary<int, AudioMusicCueMapping> CueMapping);

internal sealed record MapMusicEntrySelection(int? SourceMonth, AudioPreparedMusicSelection? Selection,
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

    internal MusicEditDraft BeginEdit(MusicEditCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new(this, catalog);
    }

    internal int EffectiveSelection(int sourceSlot, MusicEditCatalog catalog)
    {
        if ((uint)sourceSlot >= (uint)selections.Length) throw new ArgumentOutOfRangeException(nameof(sourceSlot));
        return selections[sourceSlot] == -1 ? catalog.DefaultIndex(sourceSlot) : selections[sourceSlot];
    }

    /// <summary>An explicit candidate selected in the editor. It has a source
    /// slot, but no campaign month and no preference or mapping mutation.</summary>
    internal MapMusicEntrySelection Preview(string locale, int sourceSlot, int musicIndex)
    {
        if (musicIndex is < 0 or >= AudioMapMusicSelector.MusicIndexCount)
            throw new ArgumentOutOfRangeException(nameof(musicIndex));
        var selector = AudioMapMusicSelector.Create(locale);
        // The ordinary candidate's native eligibility call returns 1/out1.
        var selected = selector.PrepareFromSlot(sourceSlot, checked((sbyte)musicIndex), new(1, 1),
            musicIndex is >= 28 and <= 115 ? new(1, 1) : null);
        mapping.TryGetValue(musicIndex, out var current);
        return new(null, selected, selector.PrepareCommit(selected, current));
    }

    internal MapMusicPreferenceFile CaptureWithSelections(IReadOnlyList<int> current)
    {
        if (current.Count != selections.Length) throw new ArgumentException("Music editing requires all 28 source slots.", nameof(current));
        // Validate the one preference-format boundary before replacing any byte.
        for (int slot = 0; slot < selections.Length; slot++)
            if (current[slot] is < sbyte.MinValue or > sbyte.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(current));
        var draft = new sbyte[selections.Length];
        for (int slot = 0; slot < selections.Length; slot++) draft[slot] = (sbyte)current[slot];
        return new(draft, new(mapping));
    }

    internal MapMusicEntrySelection SelectEntry(string locale, int currentThirdMonth)
        => SelectSourceSlot(locale, AudioMapMusicSelector.SelectMonthSourceSlot(currentThirdMonth), currentThirdMonth);

    // Collection uses the selected music index directly, not an editor setting
    // or one of the 28 source slots. Reading the mapping does not mutate it.
    internal MapMusicEntrySelection SelectMusicIndex(string locale, int musicIndex)
    {
        if (musicIndex is < 0 or >= AudioMapMusicSelector.MusicIndexCount)
            throw new ArgumentOutOfRangeException(nameof(musicIndex));
        var selector = AudioMapMusicSelector.Create(locale);
        mapping.TryGetValue(musicIndex, out var current);
        return new(null, null, selector.PrepareCommit(musicIndex, current));
    }

    internal MapMusicEntrySelection SelectSourceSlot(string locale, int slot, int? currentThirdMonth = null)
    {
        if ((uint)slot >= (uint)selections.Length) throw new ArgumentOutOfRangeException(nameof(slot));
        var selector = AudioMapMusicSelector.Create(locale);
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
