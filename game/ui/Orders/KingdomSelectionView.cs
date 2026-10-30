using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The shared original kingdom-name list. The caller supplies eligible
/// display rows and owns the actual selection or cancellation continuation.</summary>
internal partial class KingdomSelectionView : Control
{
    private CommandSelectionView selector = null!;
    private Action<int?>? closed;
    private bool abandoned;

    internal int? SelectedKingdomId => selector is not null && selector.SelectedIds is { Count: 1 } selected
        ? selected[0] : null;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<KingdomNameSelectionRow> candidates, Func<bool> admitted,
        Action<int?> closed, Func<int> scrollSpeed)
    {
        this.closed = closed;
        Name = "KingdomSelection"; ZIndex = 20; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Caption(int id) => messages.Records[id].Text
            ?? throw new InvalidDataException("Kingdom selector caption is missing.");
        selector = new(); AddChild(selector);
        selector.ConfigureCommonBackdrop(repository);
        selector.Configure(repository, text, 0x5AC,
            [new("name", Caption(0x3C8), 76), new("description", Caption(0x400), 860)],
            candidates.Select(choice => new CommandSelectionRow(choice.Id,
                [CommandSelectionCell.PersonName(choice.Name, choice.NameSortKey), new(choice.Description, SortUnavailable: true)], true)).ToArray(),
            [], 1, () => !abandoned && IsVisibleInTree() && admitted(),
            result => Finish(result is { Count: 1 } ? result[0] : null), scrollSpeed,
            requireSelection: true, singleSelection: true);
        SetMeta("source_layout_name", "00_03_00_list_re");
        SetMeta("source_data_type", "CKingdomData");
        SetMeta("source_name_sort", "CKingdomData.secondary_text18.ordinal_utf16");
        SetMeta("source_description_sortable", false); //column sort type2, no getter
        SetMeta("source_selection_rows", candidates.Count);
        SetMeta("original_columns_recovered", true); SetMeta("selection_maximum", 1);
    }

    internal void RequestBack() => selector?.RequestCancel();
    internal void RequestCancel() => RequestBack();
    private void Finish(int? selected)
    {
        if (abandoned || closed is null) return;
        var callback = closed; closed = null; Hide(); callback(selected);
    }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; closed = null; selector?.Abandon(); Hide();
    }
    public override void _ExitTree() => Abandon();
}
