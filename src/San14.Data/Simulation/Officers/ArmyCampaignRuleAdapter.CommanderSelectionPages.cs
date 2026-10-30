namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private string PrepareCommanderSelectionGuide() => new NarrationResourceMessages(
        _repository.LoadMessages(Seed.Language, San14.Data.Formats.MessageKind.Game).Messages)
        .ResolvePersonSelectorHint(ReadHistoryDataText(1, 0x211));

    private OfficerInformationPageSet PrepareCommanderSelectionPages(San14.Core.GameState state,
        int playerForceId, IReadOnlyList<int> candidates, IReadOnlyList<int> selected)
    {
        // The actual multi-selector model leaves its Body palette callback
        // owner disabled. This is not inferred from a Person's health/status.
        var reader = new OfficerInformationCurrent(this, state, playerForceId, rowPaletteEnabled: false);
        var pages = reader.Pages(candidates, ReadHistoryDataText(1, 0x32E),
            CommanderSelectionColumnsSource.Create(ReadHistoryDataText));
        pages.SortSelection(selected, selectedFirst: true);
        return pages;
    }
}
