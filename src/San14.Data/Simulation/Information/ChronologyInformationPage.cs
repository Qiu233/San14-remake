using System.Collections.Immutable;

namespace San14.Data;

public sealed record ChronologyInformationMapSnapshot(ImmutableArray<byte> AreaColors,
    ImmutableArray<ImmutableArray<ushort>> CityForceShortNames, ImmutableArray<ushort> HexIds);
public sealed record ChronologyInformationRow(bool ImportantStorage, int StorageSlot,
    ushort EventCode, byte Family, ushort CampaignDay, ushort Sequence, byte EntryFlags,
    string MainLabel, byte ColourIndex, string? YearText, string? MonthText, string? PeriodText,
    string? Narrative, string? HeaderNarrative, ChronologyInformationMapSnapshot Map,
    IReadOnlyDictionary<string, string> UnresolvedFields)
{
    public bool ShowForceMarker => Family != 2 && ColourIndex != 61;
    public bool ShowFamilyMarker => Family == 2;
}

/// <summary>Saved chronology playback selection. Map snapshots remain independent
/// of the live campaign. The UI owns playback timing and camera restoration.</summary>
public sealed class ChronologyInformationPage
{
    public string Title { get; }
    public string FirstCaption { get; }
    public string SecondCaption { get; }
    public ImmutableArray<ChronologyInformationRow> Rows { get; }
    public int SelectedIndex { get; private set; }
    public ChronologyInformationRow? Selected => SelectedIndex >= 0 ? Rows[SelectedIndex] : null;
    public bool CanPrevious => SelectedIndex > 0;
    public bool CanNext => SelectedIndex >= 0 && SelectedIndex + 1 < Rows.Length;
    public string? SortUnresolvedOwner { get; }
    public const int PlaybackStepMilliseconds = 1000;
    internal ChronologyInformationPage(string title, string firstCaption, string secondCaption,
        ImmutableArray<ChronologyInformationRow> rows, string? sortUnresolvedOwner)
    {
        Title = title; FirstCaption = firstCaption; SecondCaption = secondCaption;
        Rows = rows; SortUnresolvedOwner = sortUnresolvedOwner;
        SelectedIndex = rows.IsEmpty ? -1 : 0;
    }
    public bool Select(int index)
    {
        if ((uint)index >= (uint)Rows.Length) return false;
        SelectedIndex = index;
        return true;
    }
    public bool Step(bool forward) => Select(SelectedIndex + (forward ? 1 : -1));
}
