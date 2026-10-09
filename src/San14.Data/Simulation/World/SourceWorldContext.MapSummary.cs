namespace San14.Data;

public sealed partial class SourceWorldContext : IAreaMapStatisticsSummaryState
{
    // The native caller passes one fixed summary receiver. Keep its word
    // storage independent of Area68 and preserve unspecified entries.
    private readonly Dictionary<int, ushort?> _areaMapStatisticsSummaryWords = [];

    public void SetAreaMapStatisticsSummaryWord(int physicalSlot, ushort? value)
    {
        Check(physicalSlot, 501, "summary area physical slot");
        _areaMapStatisticsSummaryWords[physicalSlot] = value;
    }

    public ushort? ReadAreaMapStatisticsSummaryWord(int physicalSlot)
    {
        Check(physicalSlot, 501, "summary area physical slot");
        return _areaMapStatisticsSummaryWords.GetValueOrDefault(physicalSlot);
    }

    public void WriteAreaMapStatisticsSummaryWord(int physicalSlot, ushort value) =>
        SetAreaMapStatisticsSummaryWord(physicalSlot, value);

    public void UpdateAreaMapStatisticsSummary() => AreaMapStatisticsSummary.Apply(this);
}
