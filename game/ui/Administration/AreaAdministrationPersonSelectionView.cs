using Godot;
using San14.Data;

internal sealed class AreaAdministrationPersonPageSortState
{
    internal readonly Dictionary<int, bool> NextAscending = [];
    internal int? ActiveColumn;
    internal bool? ActiveAscending;
}

/// <summary>One region retains one occurrence order and scroll across its seven
/// pages. Ordinary directions belong to pages; check direction is shared.</summary>
internal sealed class AreaAdministrationPersonListState
{
    internal IReadOnlyList<CityDomesticCandidate>? Candidates;
    internal readonly List<int> Order = [];
    internal readonly AreaAdministrationPersonPageSortState[] Pages =
        Enumerable.Range(0, AreaAdministrationPersonColumns.Captions.Count).Select(_ => new AreaAdministrationPersonPageSortState()).ToArray();
    internal int Scroll, ActivePage;
    internal bool NextCheckAscending = true;
}

/// <summary>Immediate single-person selection. Every page uses the same
/// publication occurrences; only the host stages a selected physical receiver.</summary>
internal partial class AreaAdministrationPersonSelectionView : Control
{
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private bool ownsResources;
    private Control canvas = null!, body = null!;
    private UiSourceScrollbar scroll = null!;
    private AreaAdministrationPersonListState state = null!;
    private GameResourceRepository repository = null!;
    private readonly List<(CityDomesticPersonEntry Entry, UiSourcePersonRow Row)> rows = [];
    private readonly Dictionary<int, UiSourceColumnButton> headers = [];
    private readonly List<UiSourceTabButton> tabs = [];
    private Func<int, IReadOnlyList<int>, int, bool, AreaAdministrationQuery<IReadOnlyList<int>>>? order;
    private Func<int, IReadOnlyList<int>, AreaAdministrationQuery<IReadOnlyList<CityDomesticPersonEntry>>>? queryPage;
    private Action<int?>? complete;
    private Action<string> unavailable = null!;
    private IReadOnlyList<CityDomesticPersonEntry> basicEntries = null!;
    private UiSourceRoundButton back = null!;
    private int? selectedOccurrence;
    private int activePage;
    private string checkGlyph = "";
    private string locale = "";
    private UiSourceText? notice;
    internal Func<int,bool>? CandidateAdmission { get; set; }

    internal void Configure(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<DeploymentPersonColumn> columns, IReadOnlyList<CityDomesticPersonEntry> entries,
        AreaAdministrationPersonListState state, int? preselectedOccurrence, int scrollSpeed,
        Func<int, IReadOnlyList<int>, int, bool, AreaAdministrationQuery<IReadOnlyList<int>>> order,
        Action<int?> complete, Action<string> unavailable,
        Func<int, IReadOnlyList<int>, AreaAdministrationQuery<IReadOnlyList<CityDomesticPersonEntry>>>? queryPage = null,
        Func<int>? currentScrollSpeed=null, AdministrationPageChrome.Resources? resources=null)
    {
        if (!columns.SequenceEqual(AreaAdministrationPersonColumns.For(state.ActivePage)))
            throw new InvalidDataException("Area active-page columns do not match the role preset.");
        this.repository = repository; this.state = state; this.order = order; locale = text.SourceLocale;
        this.complete = complete; this.unavailable = unavailable; this.queryPage = queryPage; basicEntries = entries;
        var original = entries.OrderBy(e => e.Occurrence).ToArray();
        ValidateOrder(original.Select(e => e.Occurrence).ToArray(), original.Length);
        if (state.Candidates is null)
        {
            state.Candidates = original.Select(e => e.Candidate).ToArray(); state.Order.AddRange(entries.Select(e => e.Occurrence));
            for (int page = 0; page < state.Pages.Length; page++)
                foreach (var column in AreaAdministrationPersonColumns.For(page))
                    state.Pages[page].NextAscending.Add(column.Id, column.InitialAscending);
        }
        else if (!state.Candidates.SequenceEqual(original.Select(e => e.Candidate)))
            throw new InvalidDataException("Area person publication changed its retained physical candidates.");
        ValidateOrder(state.Order, original.Length);
        if (preselectedOccurrence is int chosen && !state.Order.Contains(chosen))
            throw new InvalidDataException("Area preselection is outside its publication.");
        selectedOccurrence = preselectedOccurrence is int preselected ? FirstPhysical(state.Candidates[preselected].PhysicalSlot) : null;
        if(selectedOccurrence is int selected&&CandidateAdmission?.Invoke(state.Candidates[selected].RawIdentity)==false)
            selectedOccurrence=null;
        int initialPage = state.ActivePage, initialScroll = state.Scroll;
        Name = "AreaPersonSelection"; ZIndex = 4; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop = new UiSourceCommonBlur(); AddChild(backdrop); backdrop.Configure(repository,resources?.Assets.SourceLayout(0xAEF1609E));
        ownsResources = resources is null;
        assets = resources?.Assets ?? new(repository);
        font = resources?.Font ?? new(repository, text.SourceLocale);
        shader = resources?.SpriteShader ?? UiSourceSprite.CreateShader();
        var messages = repository.LoadMessages(text.SourceLocale, San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing Area person caption.");
        canvas = new Control { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop }; AddChild(canvas);
        var title = new UiSourceLayout(repository, 0xDC8AC8AA);
        title.Art(canvas, 1, title.Rectangle(1), assets, shader);
        title.Text(canvas, 2, default, Message(0x032E), font, MapTextPresets.Create()[56]);
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Blur(canvas, 2, shell.Rectangle(2, new(191, 147)));
        shell.Art(canvas, 3, shell.Rectangle(3, new(191, 147)), assets, shader);
        float tabX = 230;
        for (int i = 0; i < AreaAdministrationPersonColumns.Captions.Count; i++)
        {
            int page = i; string caption = Message(AreaAdministrationPersonColumns.Captions[i]); float width = caption.Length * 28 + 36;
            var tab = new UiSourceTabButton { Name = "AreaPersonTab" + i, Position = new(tabX, 147), Size = new(width, 64),
                Selected = i == 0, Disabled = i != 0 && queryPage is null };
            canvas.AddChild(tab); tabs.Add(tab);
            tab.Configure(repository, assets, shader, font, caption, i == 0 ? 0x172EDC24u : i == AreaAdministrationPersonColumns.Captions.Count - 1 ? 0xD45873AAu : 0x9AFB27B7u);
            tab.Pressed += () => SwitchPage(page); tabX += width - 6;
        }
        scroll = new UiSourceScrollbar { Name = "PersonScrollbar", Position = new(1687, 265), Size = new(16, 597) }; canvas.AddChild(scroll);
        scroll.Configure(repository, assets, shader, Math.Max(0, entries.Count * 48 - 596), scrollSpeed,
            value => { state.Scroll = value; PositionRows(); },currentScrollSpeed:currentScrollSpeed);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        back = new UiSourceRoundButton { Name = "CancelAreaPersonSelection", Position = footer.Rectangle(1, new(0, 904)).Position };
        canvas.AddChild(back);
        back.Configure(repository, assets, shader, text.SourceLocale, "000_cmn_txt_decision_modoru", RequestCancel, cue: UiAudioCue.Cancel);
        var hint = new UiSourceLayout(repository, 0xAB6ADAA7);
        Vector2 hintMount = footer.Rectangle(19, new(0,904)).Position;
        foreach (var node in hint.Nodes.Where(n=>n.Type is 1 or 3))
            hint.Art(canvas,node.Index,hint.Rectangle(node.Index,hintMount),assets,shader);
        notice=hint.Text(canvas,hint.Nodes.Single(n=>n.Type==2).Index,hintMount,"",font);
        checkGlyph = Message(0x0628);
        PublishPage(initialPage, entries);
        scroll.SetValue(initialScroll); PositionRows(); Resized += Fit; Fit();
    }

    internal int? SelectedOccurrence => selectedOccurrence;
    internal int? SelectedPersonId => selectedOccurrence is int i ? state.Candidates![i].RawIdentity : null;

    private int? FirstPhysical(int physical) => state.Order.Where(o => state.Candidates![o].PhysicalSlot == physical).Select(o => (int?)o).FirstOrDefault();

    private void SwitchPage(int page)
    {
        if (complete is null || page == activePage || page != 0 && queryPage is null) return;
        var callback = complete;
        var query = queryPage?.Invoke(page, state.Order.ToArray());
        if (complete != callback) return;
        if (query is { Available: false }) { unavailable(query.UnresolvedInput!); return; }
        var entries = queryPage is null ? basicEntries : query?.Value ?? throw new InvalidDataException("Area page query returned no publication.");
        PublishPage(page, entries);
    }

    private void PublishPage(int page, IReadOnlyList<CityDomesticPersonEntry> entries)
    {
        var columns = AreaAdministrationPersonColumns.For(page);
        var original = entries.OrderBy(e => e.Occurrence).ToArray();
        ValidateOrder(original.Select(e => e.Occurrence).ToArray(), state.Candidates!.Count);
        if (!original.Select(e => e.Candidate).SequenceEqual(state.Candidates) || original.Any(e => e.Cells.Count != columns.Count))
            throw new InvalidDataException("Area page rows do not match their role publication and visible columns.");
        var next = new Control { Name = "AreaPersonPage" + page, Size = canvas.Size, Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        var nextRows = new List<(CityDomesticPersonEntry Entry, UiSourcePersonRow Row)>();
        var nextHeaders = new Dictionary<int, UiSourceColumnButton>();
        bool published = false; int x = 0;
        int tableWidth=Math.Max(1464,48+columns.Sum(c=>c.Width));
        var table=new Control{CustomMinimumSize=new(tableWidth,646),MouseFilter=MouseFilterEnum.Pass};
        try
        {
            canvas.AddChild(next);
            var viewport=new ScrollContainer { Position=new(213,214),Size=new(1464,664),
                HorizontalScrollMode=ScrollContainer.ScrollMode.Auto,VerticalScrollMode=ScrollContainer.ScrollMode.Disabled };
            next.AddChild(viewport);viewport.AddChild(table);
            var clip = new Control { Name = "PersonListViewport", Position = new(0, 50), Size = new(tableWidth, 596),
                ClipContents = true, MouseFilter = MouseFilterEnum.Pass }; table.AddChild(clip); clip.GuiInput += scroll.HandleWheel;
            Header(-1, checkGlyph, 48);
            var messages = repository.LoadMessages(locale, San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
            foreach (var column in columns)
                Header(column.Id, messages.Records[column.CaptionMessage].Text ?? throw new InvalidDataException("Missing City column caption."), column.Width);
            foreach (int occurrence in state.Order)
            {
                var entry = original[occurrence]; var row = new UiSourcePersonRow { Name = "AreaPersonRow" + occurrence }; clip.AddChild(row);
                row.Configure(repository, assets, shader, font, checkGlyph, columns, entry.Cells,tableWidth); row.ApplyHealthStyle(entry.AlteredHealth);
                row.Disabled=CandidateAdmission?.Invoke(entry.Candidate.RawIdentity)==false;
                nextRows.Add((entry, row)); row.GuiInput += scroll.HandleWheel;
                row.Pressed += () => { if (body == next && activePage == page) Activate(entry); };
            }
            if (body is not null) { body.Hide(); body.QueueFree(); }
            body = next; activePage = page; state.ActivePage = page;
            rows.Clear(); rows.AddRange(nextRows); headers.Clear(); foreach (var pair in nextHeaders) headers.Add(pair.Key, pair.Value);
            for (int i = 0; i < tabs.Count; i++) tabs[i].Selected = i == page;
            ApplySelection(); ApplyArrows(); PositionRows(); next.Show(); published = true;
            if (entries.Any(e => e.AlteredHealth is null)) unavailable("area-draft.person_row_style");
        }
        finally { if (!published) next.QueueFree(); }

        void Header(int id, string caption, int width)
        {
            var header = new UiSourceColumnButton { Name = "AreaPersonHeader" + id, Position = new(x, 0), Size = new(width, 48) };
            table.AddChild(header); header.Configure(repository, assets, shader, font, caption, personList: true);
            nextHeaders.Add(id, header); header.Pressed += () => { if (body == next && activePage == page) Sort(id); }; x += width;
        }
    }

    private void Activate(CityDomesticPersonEntry entry)
    {
        if (complete is null||CandidateAdmission?.Invoke(entry.Candidate.RawIdentity)==false) return;
        selectedOccurrence = entry.Occurrence; ApplySelection(); var callback = complete; complete = null; callback(entry.Occurrence);
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
            throw new InvalidDataException("Area person order must preserve all publication occurrences.");
    }
    private void ApplySelection()
    {
        foreach (var pair in rows) { pair.Row.Selected = pair.Entry.Occurrence == selectedOccurrence; pair.Row.QueueRedraw(); }
    }
    private void ApplyArrows()
    {
        var page = state.Pages[activePage];
        foreach (var (id, header) in headers) header.ApplySortDirection(page.ActiveColumn == id ? page.ActiveAscending : null);
    }
    private void PositionRows() { for (int i = 0; i < rows.Count; i++) rows[i].Row.Position = new(0, i * 48 - scroll.Value); }
    internal void ApplyNotice(string value) { if(complete is not null && notice is not null) notice.Text=value; }
    internal void RequestCancel() { var callback = complete; complete = null; callback?.Invoke(null); }
    internal void Abandon() { complete = null; order = null; queryPage = null; CandidateAdmission=null; if (back is not null) back.Disabled = true; Hide(); SetProcessInput(false); }
    public override void _Input(InputEvent input)
    {
        if (IsVisibleInTree() && !IsQueuedForDeletion() && input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        { GetViewport().SetInputAsHandled(); RequestCancel(); }
    }
    private void Fit() { float factor = Math.Min(Size.X / 1920, Size.Y / 1080); canvas.Scale = Vector2.One * factor; canvas.Position = (Size - new Vector2(1920, 1080) * factor) / 2; }
    public override void _ExitTree()
    {
        complete = null; order = null; queryPage = null;
        if (ownsResources) { font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
    }
}
