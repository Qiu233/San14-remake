using Godot;
using San14.Data;
using System.Globalization;

/// <summary>The hire, learning and mediation callers retain their actual
/// candidate order and eligibility; tabs share one local selection.</summary>
internal partial class PersonnelDevelopmentSelectorPage : Control
{
    private readonly Dictionary<int, CommandSelectionView> visited = [];
    private readonly Dictionary<int, Dictionary<string, bool>> directions = [];
    private IReadOnlyList<int> selected = [];
    private Func<bool>? current;
    private Action<IReadOnlyList<int>?>? closed;
    private int active;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        OfficerInformationPageSet source, IReadOnlyDictionary<int, (bool Enabled, string? Reason)> eligibility,
        IReadOnlyList<int> initial, int maximum, bool single, string guide,
        Func<bool> current, Action<IReadOnlyList<int>?> closed, Func<int> scrollSpeed,
        AdministrationPageChrome.Resources resources)
    {
        this.current = current; this.closed = closed; selected = initial.ToArray();
        Name = "PersonnelDevelopmentSelector"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bool selectionNext = true;
        Switch(0);

        void Switch(int index)
        {
            if (!Admitted()) return;
            if (visited.TryGetValue(active, out var previous)) selected = previous.SelectedIds;
            if (!visited.TryGetValue(index, out var next))
            {
                var descriptor = source.Groups[index];
                var columns = descriptor.Columns.Where(column => column.Visible).ToArray();
                var positions = descriptor.Columns.Select((column, slot) => (column.Id, slot))
                    .ToDictionary(value => value.Id, value => value.slot);
                if (!directions.TryGetValue(index, out var nextDirections)) directions.Add(index, nextDirections = []);
                nextDirections["selection"] = selectionNext;
                next = new CommandSelectionView(); AddChild(next);
                var page = next;
                try
                {
                    page.Configure(repository, text, 0x32E,
                        columns.Select(column => new CommandSelectionColumn(Key(column.Id), column.Caption, column.Width,
                            column.CellFormat == 5, (byte)(((column.CellFormat - 3) << 4) | 1),
                            (byte)(column.CellFormat - 2), column.InitialAscending)).ToArray(),
                        Rows(source.ReadPage(index)), selected, maximum, () => Admitted() && active == index,
                        Finish, scrollSpeed, guide, retainOnAccept: true, requireSelection: single,
                        singleSelection: single, activateRows: single, returnOnly: single, retainOnReturn: true,
                        sourceSort: (key, ascending) =>
                        {
                            OfficerInformationPage sorted;
                            if (key == "selection")
                            {
                                source.SortSelection(page.SelectedIds, !ascending);
                                selectionNext = !ascending;
                                foreach (var state in directions.Values) state["selection"] = selectionNext;
                                sorted = source.ReadPage(index);
                            }
                            else
                            {
                                var result = source.Sort(index, key == "name" ? 0 : int.Parse(key, CultureInfo.InvariantCulture), ascending);
                                if (!result.Available)
                                { page.ApplyNotice(text.Get("information_partial")); return null; }
                                sorted = result.Value!;
                            }
                            return Rows(sorted);
                        }, groupCaptions: source.Groups.Select(group => group.Caption).ToArray(),
                        selectedGroup: index, groupSelected: Switch, nextSortDirections: nextDirections,
                        selectionSortable: !single, resources: resources);
                    page.ApplyTitle(source.Title);
                    page.Name = "PersonnelDevelopmentPage" + index;
                    page.SortOrderChanged = ids =>
                    { foreach (var other in visited.Values) if (other != page) other.ApplyRowOrder(ids); };
                    visited.Add(index, page);
                }
                catch { page.Abandon(); RemoveChild(page); page.QueueFree(); throw; }

                IReadOnlyList<CommandSelectionRow> Rows(OfficerInformationPage value) => value.Rows.Select(row =>
                {
                    var allowed = eligibility.GetValueOrDefault(row.OfficerId);
                    return new CommandSelectionRow(row.OfficerId, columns.Select(column =>
                    {
                        var cell = row.Cells[positions[column.Id]];
                        return new CommandSelectionCell(cell.UnresolvedOwner is null ? cell.Text ?? "" : text.Get("detail_unknown"),
                            SortUnavailable: !column.CanSort || cell.SortUnresolvedOwner is not null, SourcePreset: cell.PresetIndex);
                    }).ToArray(), allowed.Enabled, allowed.Reason);
                }).ToArray();
            }
            else next.ReplaceSelection(selected);
            if (previous != next) previous?.Hide();
            active = index; next.Show();
        }

        void Finish(IReadOnlyList<int>? result)
        {
            if (!Admitted()) return;
            // The native output reader walks the current ordered list, not
            // the order in which the player checked its rows.
            var ids = result?.ToHashSet();
            Hide(); this.closed?.Invoke(ids is null ? null : source.CurrentOfficerIds.Where(ids.Contains).ToArray());
        }
    }

    private bool Admitted() => closed is not null && (current?.Invoke() ?? false);
    private static string Key(int id) => id == 0 ? "name" : id.ToString(CultureInfo.InvariantCulture);
    internal void RequestCancel() { if (visited.TryGetValue(active, out var page)) page.RequestCancel(); }
    internal void Abandon()
    {
        current = null; closed = null;
        // Borrowers release their controls before their resource owner.
        foreach (var page in visited.Values.Reverse()) page.Abandon();
        Hide();
    }
    public override void _ExitTree() => Abandon();
}
