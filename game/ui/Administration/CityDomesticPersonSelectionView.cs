using Godot;
using San14.Data;

internal sealed record CityDomesticPersonEntry(int Occurrence, CityDomesticCandidate Candidate,
    IReadOnlyList<DeploymentPersonCell> Cells, bool? AlteredHealth);

internal sealed class CityDomesticPersonPageSortState
{
    internal readonly Dictionary<int, bool> NextAscending = [];
    internal int? ActiveColumn;
    internal bool? ActiveAscending;
}

/// <summary>One role retains one occurrence order and scroll across its six
/// pages. Ordinary directions belong to pages; check direction is shared.</summary>
internal sealed class CityDomesticPersonListState
{
    internal IReadOnlyList<CityDomesticCandidate>? Candidates;
    internal readonly List<int> Order = [];
    internal readonly CityDomesticPersonPageSortState[] Pages =
        Enumerable.Range(0, CityDomesticPersonColumns.Captions.Count).Select(_ => new CityDomesticPersonPageSortState()).ToArray();
    internal int Scroll, ActivePage;
    internal bool NextCheckAscending = true;
}

/// <summary>Immediate single-person selection. Every page uses the same
/// publication occurrences; only the host stages a selected physical receiver.</summary>
internal partial class CityDomesticPersonSelectionView : Control
{
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!, body = null!;
    private UiSourceScrollbar scroll = null!;
    private CityDomesticPersonListState state = null!;
    private CityDomesticDuty duty;
    private GameResourceRepository repository = null!;
    private readonly List<(CityDomesticPersonEntry Entry, UiSourcePersonRow Row)> rows = [];
    private readonly Dictionary<int, UiSourceColumnButton> headers = [];
    private readonly List<UiSourceTabButton> tabs = [];
    private Func<int, IReadOnlyList<int>, int, bool, AreaAdministrationQuery<IReadOnlyList<int>>>? order;
    private Func<int, IReadOnlyList<int>, AreaAdministrationQuery<IReadOnlyList<CityDomesticPersonEntry>>>? queryPage;
    private Action<CityDomesticCandidate?>? complete;
    private Action<string> unavailable = null!;
    private IReadOnlyList<CityDomesticPersonEntry> basicEntries = null!;
    private UiSourceRoundButton accept = null!;
    private int? selectedOccurrence;
    private int activePage;
    private string checkGlyph = "";
    private string locale = "";

    internal void Configure(GameResourceRepository repository, PreviewText text, CityDomesticDuty duty,
        IReadOnlyList<DeploymentPersonColumn> columns, IReadOnlyList<CityDomesticPersonEntry> entries,
        CityDomesticPersonListState state, int? preselectedOccurrence, int scrollSpeed,
        Func<int, IReadOnlyList<int>, int, bool, AreaAdministrationQuery<IReadOnlyList<int>>> order,
        Action<CityDomesticCandidate?> complete, Action<string> unavailable,
        Func<int, IReadOnlyList<int>, AreaAdministrationQuery<IReadOnlyList<CityDomesticPersonEntry>>>? queryPage = null,
        Func<int>? currentScrollSpeed=null)
    {
        if (!columns.SequenceEqual(CityDomesticPersonColumns.For(duty, state.ActivePage)))
            throw new InvalidDataException("City active-page columns do not match the role preset.");
        this.repository = repository; this.duty = duty; this.state = state; this.order = order; locale = text.SourceLocale;
        this.complete = complete; this.unavailable = unavailable; this.queryPage = queryPage; basicEntries = entries;
        var original = entries.OrderBy(e => e.Occurrence).ToArray();
        ValidateOrder(original.Select(e => e.Occurrence).ToArray(), original.Length);
        if (state.Candidates is null)
        {
            state.Candidates = original.Select(e => e.Candidate).ToArray(); state.Order.AddRange(original.Select(e => e.Occurrence));
            for (int page = 0; page < state.Pages.Length; page++)
                foreach (var column in CityDomesticPersonColumns.For(duty, page))
                    state.Pages[page].NextAscending.Add(column.Id, column.InitialAscending);
        }
        else if (!state.Candidates.SequenceEqual(original.Select(e => e.Candidate)))
            throw new InvalidDataException("City person publication changed its retained physical candidates.");
        ValidateOrder(state.Order, original.Length);
        if (preselectedOccurrence is int chosen && !state.Order.Contains(chosen))
            throw new InvalidDataException("City preselection is outside its publication.");
        selectedOccurrence = preselectedOccurrence is int preselected ? FirstPhysical(state.Candidates[preselected].PhysicalSlot) : null;
        int initialPage = state.ActivePage, initialScroll = state.Scroll;
        Name = "CityPersonSelection"; ZIndex = 4; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        var messages = repository.LoadMessages(text.SourceLocale, San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing City person caption.");
        canvas = new Control { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop }; AddChild(canvas);
        canvas.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
        canvas.AddChild(new MapDialogBackdrop { Size = canvas.Size });
        var title = new UiSourceLayout(repository, 0xDC8AC8AA);
        title.Art(canvas, 1, title.Rectangle(1), assets, shader);
        title.Text(canvas, 2, default, Message(0x032E), font, MapTextPresets.Create()[56]);
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Art(canvas, 3, shell.Rectangle(3, new(191, 147)), assets, shader);
        float tabX = 230;
        for (int i = 0; i < CityDomesticPersonColumns.Captions.Count; i++)
        {
            int page = i; string caption = Message(CityDomesticPersonColumns.Captions[i]); float width = caption.Length * 28 + 36;
            var tab = new UiSourceTabButton { Name = "CityPersonTab" + i, Position = new(tabX, 147), Size = new(width, 64),
                Selected = i == 0, Disabled = i != 0 && queryPage is null };
            canvas.AddChild(tab); tabs.Add(tab);
            tab.Configure(repository, assets, shader, font, caption, i == 0 ? 0x172EDC24u : i == CityDomesticPersonColumns.Captions.Count - 1 ? 0xD45873AAu : 0x9AFB27B7u);
            tab.Pressed += () => SwitchPage(page); tabX += width - 6;
        }
        scroll = new UiSourceScrollbar { Name = "PersonScrollbar", Position = new(1687, 265), Size = new(16, 597) }; canvas.AddChild(scroll);
        scroll.Configure(repository, assets, shader, Math.Max(0, entries.Count * 48 - 596), scrollSpeed,
            value => { state.Scroll = value; PositionRows(); },currentScrollSpeed:currentScrollSpeed);
        accept = new UiSourceRoundButton { Name = "AcceptCityPersonSelection", Position = new(827, 955) }; canvas.AddChild(accept);
        accept.Configure(repository, assets, shader, text.SourceLocale, "000_cmn_txt_decision_kettei", () =>
        {
            if (selectedOccurrence is int occurrence) Activate(rows.Single(r => r.Entry.Occurrence == occurrence).Entry);
        });
        var cancel = new UiSourceRoundButton { Name = "CancelCityPersonSelection", Position = new(965, 955) }; canvas.AddChild(cancel);
        cancel.Configure(repository, assets, shader, text.SourceLocale, "000_cmn_txt_decision_modoru", RequestCancel);
        checkGlyph = Message(0x0628);
        PublishPage(initialPage, entries);
        scroll.SetValue(initialScroll); PositionRows(); Resized += Fit; Fit();
    }

    private int? FirstPhysical(int physical) => state.Order.Where(o => state.Candidates![o].PhysicalSlot == physical).Select(o => (int?)o).FirstOrDefault();

    private void SwitchPage(int page)
    {
        if (complete is null || page == activePage || page != 0 && queryPage is null) return;
        var callback = complete;
        var query = queryPage?.Invoke(page, state.Order.ToArray());
        if (complete != callback) return;
        if (query is { Available: false }) { unavailable(query.UnresolvedInput!); return; }
        var entries = queryPage is null ? basicEntries : query?.Value ?? throw new InvalidDataException("City page query returned no publication.");
        PublishPage(page, entries);
    }

    private void PublishPage(int page, IReadOnlyList<CityDomesticPersonEntry> entries)
    {
        var columns = CityDomesticPersonColumns.For(duty, page);
        var original = entries.OrderBy(e => e.Occurrence).ToArray();
        ValidateOrder(original.Select(e => e.Occurrence).ToArray(), state.Candidates!.Count);
        if (!original.Select(e => e.Candidate).SequenceEqual(state.Candidates) || original.Any(e => e.Cells.Count != columns.Count))
            throw new InvalidDataException("City page rows do not match their role publication and visible columns.");
        var next = new Control { Name = "CityPersonPage" + page, Size = canvas.Size, Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        var nextRows = new List<(CityDomesticPersonEntry Entry, UiSourcePersonRow Row)>();
        var nextHeaders = new Dictionary<int, UiSourceColumnButton>();
        bool published = false; int x = 213;
        try
        {
            canvas.AddChild(next);
            var clip = new Control { Name = "PersonListViewport", Position = new(213, 264), Size = new(1464, 596),
                ClipContents = true, MouseFilter = MouseFilterEnum.Pass }; next.AddChild(clip); clip.GuiInput += scroll.HandleWheel;
            Header(-1, checkGlyph, 48);
            var messages = repository.LoadMessages(locale, San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
            foreach (var column in columns)
                Header(column.Id, messages.Records[column.CaptionMessage].Text ?? throw new InvalidDataException("Missing City column caption."), column.Width);
            foreach (int occurrence in state.Order)
            {
                var entry = original[occurrence]; var row = new UiSourcePersonRow { Name = "CityPersonRow" + occurrence }; clip.AddChild(row);
                row.Configure(repository, assets, shader, font, checkGlyph, columns, entry.Cells); row.ApplyHealthStyle(entry.AlteredHealth);
                nextRows.Add((entry, row)); row.GuiInput += scroll.HandleWheel;
                row.Pressed += () => { if (body == next && activePage == page) Activate(entry); };
            }
            if (body is not null) { body.Hide(); body.QueueFree(); }
            body = next; activePage = page; state.ActivePage = page;
            rows.Clear(); rows.AddRange(nextRows); headers.Clear(); foreach (var pair in nextHeaders) headers.Add(pair.Key, pair.Value);
            for (int i = 0; i < tabs.Count; i++) tabs[i].Selected = i == page;
            ApplySelection(); ApplyArrows(); PositionRows(); next.Show(); published = true;
            if (entries.Any(e => e.AlteredHealth is null)) unavailable("city-domestic.person_row_style");
        }
        finally { if (!published) next.QueueFree(); }

        void Header(int id, string caption, int width)
        {
            var header = new UiSourceColumnButton { Name = "CityPersonHeader" + id, Position = new(x, 214), Size = new(width, 48) };
            next.AddChild(header); header.Configure(repository, assets, shader, font, caption, personList: true);
            nextHeaders.Add(id, header); header.Pressed += () => { if (body == next && activePage == page) Sort(id); }; x += width;
        }
    }

    private void Activate(CityDomesticPersonEntry entry)
    {
        if (complete is null) return;
        selectedOccurrence = entry.Occurrence; ApplySelection(); var callback = complete; complete = null; callback(entry.Candidate);
    }
    private void Sort(int column)
    {
        if (complete is null) return;
        var page = state.Pages[activePage]; bool ascending = column == -1 ? state.NextCheckAscending : page.NextAscending[column];
        var callback = complete;
        var query = column == -1 ? new AreaAdministrationQuery<IReadOnlyList<int>>(
            state.Order.OrderBy(o => ascending ? o == selectedOccurrence : o != selectedOccurrence).ToArray(), null)
            : order!(activePage, state.Order.ToArray(), column, ascending);
        if (complete != callback) return;
        if (!query.Available) { unavailable(query.UnresolvedInput!); return; }
        var sorted = query.Value!; ValidateOrder(sorted, state.Candidates!.Count);
        int? physical = selectedOccurrence is int old ? state.Candidates[old].PhysicalSlot : null;
        var byOccurrence = rows.ToDictionary(r => r.Entry.Occurrence); var reordered = sorted.Select(o => byOccurrence[o]).ToArray();
        rows.Clear(); rows.AddRange(reordered); state.Order.Clear(); state.Order.AddRange(sorted);
        selectedOccurrence = physical is int receiver ? FirstPhysical(receiver) : null;
        if (column == -1) state.NextCheckAscending = !ascending; else page.NextAscending[column] = !ascending;
        foreach (var other in state.Pages)
        {
            other.ActiveColumn = column == -1 || other == page ? column : null;
            other.ActiveAscending = other.ActiveColumn is null ? null : ascending;
        }
        ApplySelection(); ApplyArrows(); PositionRows();
    }
    private static void ValidateOrder(IReadOnlyList<int> order, int count)
    {
        if (order.Count != count || !order.Order().SequenceEqual(Enumerable.Range(0, count)))
            throw new InvalidDataException("City person order must preserve all publication occurrences.");
    }
    private void ApplySelection()
    {
        accept.Disabled = complete is null || selectedOccurrence is null;
        foreach (var pair in rows) { pair.Row.Selected = pair.Entry.Occurrence == selectedOccurrence; pair.Row.QueueRedraw(); }
    }
    private void ApplyArrows()
    {
        var page = state.Pages[activePage];
        foreach (var (id, header) in headers) header.ApplySortDirection(page.ActiveColumn == id ? page.ActiveAscending : null);
    }
    private void PositionRows() { for (int i = 0; i < rows.Count; i++) rows[i].Row.Position = new(0, i * 48 - scroll.Value); }
    internal void RequestCancel() { var callback = complete; complete = null; callback?.Invoke(null); }
    internal void Abandon() { complete = null; order = null; queryPage = null; if (accept is not null) accept.Disabled = true; Hide(); SetProcessInput(false); }
    public override void _Input(InputEvent input)
    {
        if (IsVisibleInTree() && !IsQueuedForDeletion() && input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        { GetViewport().SetInputAsHandled(); RequestCancel(); }
    }
    private void Fit() { float factor = Math.Min(Size.X / 1920, Size.Y / 1080); canvas.Scale = Vector2.One * factor; canvas.Position = (Size - new Vector2(1920, 1080) * factor) / 2; }
    public override void _ExitTree() { complete = null; order = null; queryPage = null; font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
}
