using Godot;
using San14.Data;
using System.Globalization;

/// <summary>The top-bar merchant's single City selector. The same source
/// publication supplies display values and stable source-column sorting.</summary>
internal partial class GlobalMerchantCitySelectionView : Control
{
    private CommandSelectionView? list;
    internal void Configure(GameResourceRepository repository, PreviewText text,
        CityInformationPageSet source, Func<bool> admitted, Action<int?> closed, Func<int> scrollSpeed,
        AdministrationPageChrome.Resources? resources = null)
    {
        Name = "GlobalMerchantCitySelection"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var group = source.Groups[0];
        var columns = group.Columns.Where(column => column.Visible).ToArray();
        var slots = group.Columns.Select((column, slot) => (column.Id, slot)).ToDictionary(value => value.Id, value => value.slot);
        list = new(); AddChild(list);
        list.Configure(repository, text, 0x2B0,
            columns.Select(column => new CommandSelectionColumn(Key(column.Id), column.Caption, column.Width,
                column.CellFormat == 5, (byte)(((column.CellFormat - 3) << 4) | 1),
                (byte)(column.CellFormat - 2), column.InitialAscending)).ToArray(),
            Rows(source.ReadPage(0)), [], 1, admitted,
            result => closed(result is { Count: > 0 } ? result[0] : null), scrollSpeed,
            retainOnAccept: true, requireSelection: true, singleSelection: true, activateRows: true,
            returnOnly: true, retainOnReturn: true,
            sourceSort: (key, ascending) =>
            {
                var sorted = source.Sort(0, int.Parse(key, CultureInfo.InvariantCulture), ascending);
                if (sorted.Available) return Rows(sorted.Value!);
                list.ApplyNotice(text.Get("information_partial")); return null;
            }, groupCaptions: [group.Caption], resources: resources);
        list.ConfigureCommonBackdrop(repository);
        list.ApplyTitle(source.Title);

        IReadOnlyList<CommandSelectionRow> Rows(CityInformationListPage page) => page.Rows.Select(row =>
            new CommandSelectionRow(row.CityId, columns.Select(column =>
            {
                var cell = row.Cells[slots[column.Id]];
                return new CommandSelectionCell(cell.UnresolvedOwner is null ? cell.Text ?? "" : text.Get("detail_unknown"),
                    SortUnavailable: !column.CanSort || cell.SortUnresolvedOwner is not null,
                    SourcePreset: cell.PresetIndex, FirstGlyphColour: cell.MarkerColour?.CpuPacked);
            }).ToArray(), true)).ToArray();
    }
    private static string Key(int id) => id.ToString(CultureInfo.InvariantCulture);
    internal void RequestCancel() => list?.RequestCancel();
    internal void Abandon() { list?.Abandon(); Hide(); }
    public override void _ExitTree() => Abandon();
}
