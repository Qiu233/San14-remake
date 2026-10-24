using San14.Data;

/// <summary>One editor owns its effective 28-song draft. Cancelling leaves
/// current signed preferences and cue mapping untouched.</summary>
internal sealed class MusicEditDraft
{
    private readonly MapMusicPreferences owner;
    private readonly int[] selections;
    internal MusicEditCatalog Catalog { get; }

    internal MusicEditDraft(MapMusicPreferences owner, MusicEditCatalog catalog)
    {
        this.owner = owner;
        Catalog = catalog;
        selections = Enumerable.Range(0, AudioMapMusicSelector.SourceSlotCount)
            .Select(slot => owner.EffectiveSelection(slot, catalog)).ToArray();
    }

    internal int Selection(int sourceSlot) => selections[Slot(sourceSlot)];
    internal bool HasChanges => Enumerable.Range(0, selections.Length)
        .Any(slot => selections[slot] != owner.EffectiveSelection(slot, Catalog));
    internal bool CanInitializeDefaults => Enumerable.Range(0, selections.Length)
        .Any(slot => selections[slot] != Catalog.DefaultIndex(slot));

    internal void Select(int sourceSlot, int musicIndex)
    {
        int slot = Slot(sourceSlot);
        if (!Catalog.CanSelect(musicIndex)) throw new ArgumentOutOfRangeException(nameof(musicIndex));
        selections[slot] = musicIndex;
    }

    internal void InitializeDefaults()
    {
        for (int slot = 0; slot < selections.Length; slot++) selections[slot] = Catalog.DefaultIndex(slot);
    }

    // Prepare the complete preference file. The application atomically saves
    // it before installing its new live owner; failure leaves this draft usable.
    internal MapMusicPreferenceFile Commit() => owner.CaptureWithSelections(selections);
    private static int Slot(int slot) => (uint)slot < AudioMapMusicSelector.SourceSlotCount ? slot
        : throw new ArgumentOutOfRangeException(nameof(slot));
}
