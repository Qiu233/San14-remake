using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Ten source person pages share one selection. Each visited page
/// retains its own sort and scroll, and captures its cells only on first use.</summary>
internal partial class PersonnelGroupMembersView : Control
{
    private readonly Dictionary<int, CommandSelectionView> pages = [];
    private readonly List<UiSourceTabButton> tabs = [];
    private Control canvas = null!;
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Func<bool>? current;
    private Action<IReadOnlyList<int>?>? closed;
    private int activePage;
    private IReadOnlyList<int> selected = [];

    internal void Configure(GameResourceRepository repository, PreviewText text, IReadOnlyList<int> initial,
        Func<bool> current, Action<IReadOnlyList<int>?> closed, Func<int> scrollSpeed, string guide,
        Func<int, AreaAdministrationQuery<IReadOnlyList<CommandSelectionRow>>> query)
    {
        this.current = current; this.closed = closed; selected = initial.ToArray();
        Name = "PersonnelGroupMembers"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        canvas = new Control { Name = "GroupPersonTabs", Size = new(1920, 1080), ZIndex = 6,
            MouseFilter = MouseFilterEnum.Ignore }; AddChild(canvas);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing group person caption.");
        float x = 230;
        for (int index = 0; index < PersonnelGroupListColumns.Captions.Length; index++)
        {
            int page = index; string caption = Message(PersonnelGroupListColumns.Captions[index]);
            float width = caption.Length * 28 + 36;
            var tab = new UiSourceTabButton { Name = "GroupPersonTab" + index, Position = new(x, 147), Size = new(width, 64) };
            canvas.AddChild(tab); tabs.Add(tab);
            tab.Configure(repository, assets, shader, font, caption, index == 0 ? 0x172EDC24u
                : index == PersonnelGroupListColumns.Captions.Length - 1 ? 0xD45873AAu : 0x9AFB27B7u);
            tab.Pressed += () =>
            { if (IsVisibleInTree() && this.closed is not null && (this.current?.Invoke() ?? false)) SwitchPage(page); };
            x += width - 6;
        }
        SwitchPage(0);
        VisibilityChanged += () => { if (Visible && pages.TryGetValue(activePage, out var view)) view.Show(); };
        Resized += Fit; Fit();

        void SwitchPage(int page)
        {
            if (this.closed is null || !(this.current?.Invoke() ?? false)) return;
            if (pages.TryGetValue(activePage, out var previous)) selected = previous.SelectedIds;
            if (!pages.TryGetValue(page, out var next))
            {
                var result = query(page);
                if (!result.Available)
                {
                    previous?.ApplyNotice(text.Get("information_partial"));
                    if (result.UnresolvedInput is { } reason) GD.PushWarning(reason);
                    return;
                }
                var columns = PersonnelGroupListColumns.Pages[page].Select(id => PersonnelGroupListColumns.Person[id])
                    .Select(column => new CommandSelectionColumn(column.Id == 0 ? "name" : column.Id.ToString(),
                        Message(column.CaptionMessage), column.Width, column.Align == 0x21,
                        column.Align, column.LineArrange, column.InitialAscending)).ToArray();
                next = new CommandSelectionView(); AddChild(next);
                try
                {
                    next.Configure(repository, text, 0x73F, columns, result.Value!, selected, 50,
                        () => IsVisibleInTree() && this.closed is not null && (this.current?.Invoke() ?? false),
                        value => { Hide(); this.closed?.Invoke(value); }, scrollSpeed, guide,
                        retainOnAccept: true, retainOnReturn: true);
                    next.Name = "GroupPersonPage" + page;
                    pages.Add(page, next);
                }
                catch { next.Abandon(); RemoveChild(next); next.QueueFree(); throw; }
            }
            else next.ReplaceSelection(selected);
            if (previous != next) previous?.Hide();
            activePage = page; next.Show();
            foreach (var (tab, index) in tabs.Select((tab, index) => (tab, index))) tab.Selected = index == page;
            // Tabs sit above the currently visible list shell.
            MoveChild(canvas, GetChildCount() - 1);
        }
    }

    internal void RequestCancel() { if (pages.TryGetValue(activePage, out var view)) view.RequestCancel(); }
    internal void ApplyNotice(string text) { if (pages.TryGetValue(activePage, out var view)) view.ApplyNotice(text); }
    internal void Abandon()
    {
        current = null; closed = null;
        foreach (var view in pages.Values) view.Abandon();
        Hide();
    }
    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = new(scale, scale); canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree()
    { current = null; closed = null; font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
}
