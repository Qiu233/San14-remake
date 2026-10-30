using Godot;
using San14.Data;
using San14.Data.Formats;
using System.Globalization;

/// <summary>743E80's auto-map flag enters the city-only map picker. Its list
/// button exposes the retained publication without accepting a selection.</summary>
internal partial class PersonnelHireCitySelectionPage : Control
{
    private CommandSelectionView list = null!;
    private AdministrationPageChrome map = null!;
    private Func<bool>? current;
    private Action<IReadOnlyList<int>?>? closed;
    private Action<bool>? modeChanged;
    private bool mapMode;
    internal bool IsMapMode => mapMode && Admitted();

    internal void Configure(GameResourceRepository repository, PreviewText text, CityInformationPageSet source,
        Func<bool> current, Action<IReadOnlyList<int>?> closed, Action<bool> modeChanged, Func<int> scrollSpeed)
    {
        this.current = current; this.closed = closed; this.modeChanged = modeChanged;
        Name = "PersonnelHireCitySelection"; ZIndex = 5;
        MouseFilter = MouseFilterEnum.Stop; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var descriptor = source.Groups[0];
        var columns = descriptor.Columns.Where(column => column.Visible).ToArray();
        var positions = descriptor.Columns.Select((column, index) => (column.Id, index))
            .ToDictionary(value => value.Id, value => value.index);
        var guide = new AdministrationMessages(repository, text.SourceLocale).Format(0x5CDE);
        var directions = new Dictionary<string, bool>();
        list = new(); AddChild(list);
        list.Configure(repository, text, 0x39A,
            columns.Select(column => new CommandSelectionColumn(Key(column.Id), column.Caption, column.Width,
                column.CellFormat == 5, (byte)(((column.CellFormat - 3) << 4) | 1),
                (byte)(column.CellFormat - 2), column.InitialAscending)).ToArray(),
            Rows(source.ReadPage(0)), [], 1, () => Admitted() && !mapMode,
            Finish, scrollSpeed, guide, retainOnAccept: true, requireSelection: true,
            singleSelection: true, activateRows: true, returnOnly: true, retainOnReturn: true, sourceSort: (key, ascending) =>
            {
                var result = source.Sort(0, key == "name" ? 0 : int.Parse(key, CultureInfo.InvariantCulture), ascending);
                if (!result.Available) { list.ApplyNotice(text.Get("information_partial")); return null; }
                return Rows(result.Value!);
            }, nextSortDirections: directions);
        list.Name = "PersonnelHireCityList"; list.ApplyTitle(source.Title);
        var owner = list.SourceChrome;
        map = new(); AddChild(map);
        map.Configure(repository, text, 0x39A, () => Finish(null), () => { }, returnOnly: true,
            resources: new(owner.Assets, owner.Font, owner.SpriteShader));
        map.ApplyDefaultHint(guide);
        map.ApplyReturnHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2733));
        owner.ApplyReturnHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2733));
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        var button = new UiSourceBottomButton { Name = "PersonnelHireCityMap", Position = footer.Rectangle(14, new(0, 904)).Position,
            InputAdmission = () => Admitted() && !mapMode };
        owner.Canvas.AddChild(button);
        button.Configure(repository, owner.Assets, owner.SpriteShader, owner.Font,
            repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1].Records[0x5D].Text
                ?? throw new InvalidDataException("Missing city map caption."), () => SetMapMode(true));
        var listButton = new UiSourceBottomButton { Name = "PersonnelHireCityListMode", Position = footer.Rectangle(14, new(0, 904)).Position,
            InputAdmission = () => Admitted() && mapMode };
        map.Canvas.AddChild(listButton);
        listButton.Configure(repository, owner.Assets, owner.SpriteShader, owner.Font,
            repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1].Records[0x61].Text
                ?? throw new InvalidDataException("Missing city list caption."), ReturnToList);
        mapMode = true; map.Show(); list.Hide(); MouseFilter = MouseFilterEnum.Ignore;
        SetMeta("source_map_input_kind", 5);
        SetMeta("source_default_mode", "map");

        IReadOnlyList<CommandSelectionRow> Rows(CityInformationListPage page) => page.Rows.Select(row =>
            new CommandSelectionRow(row.CityId, columns.Select(column =>
            {
                var cell = row.Cells[positions[column.Id]];
                return new CommandSelectionCell(cell.UnresolvedOwner is null ? cell.Text ?? "" : text.Get("detail_unknown"),
                    SortUnavailable: !column.CanSort || cell.SortUnresolvedOwner is not null, SourcePreset: cell.PresetIndex);
            }).ToArray(), true)).ToArray();
    }

    private bool Admitted() => closed is not null && (current?.Invoke() ?? false);
    private static string Key(int id) => id == 0 ? "name" : id.ToString(CultureInfo.InvariantCulture);
    private void SetMapMode(bool value)
    {
        if (!Admitted() || mapMode == value) return;
        mapMode = value; list.Visible = !value; map.Visible = value;
        MouseFilter = value ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
        modeChanged?.Invoke(!value);
    }
    private void ReturnToList() => SetMapMode(false);
    internal void PickMapCity(int id) { if (IsMapMode) Finish([id]); }
    internal void RequestCancel() { if (mapMode) Finish(null); else list.RequestCancel(); }
    private void Finish(IReadOnlyList<int>? ids)
    { if (Admitted()) { Hide(); closed?.Invoke(ids); } }
    internal void Abandon()
    { current = null; closed = null; modeChanged = null; map?.Abandon(); list?.Abandon(); Hide(); }
    public override void _ExitTree() => Abandon();
}
