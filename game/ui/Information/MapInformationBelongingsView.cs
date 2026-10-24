using Godot;
using San14.Data;

internal sealed record MapInformationListSection(string Caption,
    IReadOnlyList<MapInformationListColumn> Columns, IReadOnlyList<MapInformationListRow> Rows,
    Action<int> Inspect, Func<string, bool, IReadOnlyList<MapInformationListRow>> Sort, string? Notice);

/// <summary>The original two retained belongings lists. Each tab keeps its own
/// sort, selected row and scroll position while an item detail is open.</summary>
internal partial class MapInformationBelongingsView : DetailsView
{
    private readonly Dictionary<int, MapInformationListView> lists = [];
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!;
    private Func<bool>? admitted;
    private bool abandoned;
    private int selected;

    internal void Configure(GameResourceRepository repository, PreviewText text, string title,
        IReadOnlyList<MapInformationListSection> sections, Func<bool> admitted,
        Action close, Func<int> scrollSpeed)
    {
        this.admitted = admitted;
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        canvas = new() { Name = "SourceHeaderCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(canvas);
        var header = new UiSourceHeader(); canvas.AddChild(header);
        header.Configure(repository, assets, shader, font, title);
        ConfigureSourcePage("InformationBelongings", header, close);
        var captions = sections.Select(section => section.Caption).ToArray();
        Select(0);
        Resized += Fit; Fit();

        void Select(int index)
        {
            if (abandoned || (uint)index >= sections.Count) return;
            foreach (var existing in lists.Values) { existing.Hide(); existing.ProcessMode = ProcessModeEnum.Disabled; }
            selected = index;
            if (!lists.TryGetValue(index, out var view))
            {
                var section = sections[index];
                view = new(); lists.Add(index, view); AddChild(view); MoveChild(view, 0);
                view.Configure(repository, text, title, section.Columns, section.Rows,
                    () => ReadAdmission() && selected == index, RequestClose, section.Inspect,
                    scrollSpeed, section.Sort, section.Notice, captions, index, Select, includeHeader: false);
            }
            view.ProcessMode = ProcessModeEnum.Inherit; view.Show();
        }
    }

    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree()
    {
        abandoned = true; admitted = null;
        foreach (var list in lists.Values) list.Abandon();
        lists.Clear(); base._ExitTree(); font?.Dispose(); assets?.Dispose(); shader?.Dispose();
    }
}
