using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum MapCityCommand { Deploy, CancelDeployment, Administration, Merchant, Move, Investment, CityInformation, AreaInformation, CityDomestic, Transport, Summon }

/// <summary>The city command tree renders the current query without making
/// gameplay permission decisions. Leaf editors retain their own acceptance.</summary>
internal partial class MapCityCommandMenu : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control? tree;
    private Control hintCanvas = null!;
    private UiSourceSimpleTooltip hint = null!;
    private readonly List<Action> abandonButtons = [];
    private readonly Dictionary<CityCommandMenuCommand, Control> childPanes = [];
    private int generation;
    private int cityId;
    internal int? CurrentCityId => Visible ? cityId : null;
    internal bool CancellationOpen { get; set; }
    internal event Action<int, MapCityCommand>? Command;

    internal void Configure(GameResourceRepository repository, PreviewText text)
    {
        this.repository = repository; this.text = text;
        Name = "MapCityCommandMenu"; MouseFilter = MouseFilterEnum.Stop;
        assets = new(repository); font = new(repository, text.SourceLocale);
        shader = UiSourceSprite.CreateShader();
        hintCanvas = new() { Name = "HintCanvas", MouseFilter = MouseFilterEnum.Ignore,
            Size = new(1920, 1080), ZIndex = 3 };
        AddChild(hintCanvas);
        hint = new(); hintCanvas.AddChild(hint); hint.Configure(repository, assets, shader, font);
        Hide();
    }

    internal void Close()
    {
        CancellationOpen=false;
        generation++;
        foreach (var abandon in abandonButtons) abandon();
        abandonButtons.Clear(); childPanes.Clear();
        if (tree is { } previous)
        { previous.Hide(); previous.Name = "PreviousCityCommandTree"; previous.QueueFree(); tree = null; }
        hint.DisplayText(null); Hide();
    }

    internal bool Open(int city, Vector2 anchor, Vector2 mapSize,
        AreaAdministrationQuery<CityCommandMenuProjection> query)
    {
        Close(); cityId = city;
        if (query.Available && query.Value!.Branch == CityCommandMenuBranch.Absent) return false;
        int currentGeneration = generation;
        float scale = MathF.Min(mapSize.X / 1920, mapSize.Y / 1080);
        if (!(scale > 0)) return false;
        Scale = new(scale, scale);
        Position = new(Math.Clamp(anchor.X, 0, mapSize.X), Math.Clamp(anchor.Y, 0, mapSize.Y));
        Size = Vector2.Zero;
        SetMeta("city_id", city);
        SetMeta("source_unknown", query.UnresolvedInput ?? "");
        Show();
        if (!query.Available)
        {
            // An unresolved branch is a status balloon, not an invented set of rows.
            PlaceHintCanvas(); hint.DisplayText(text.Get("detail_unknown")); return true;
        }

        var projection = query.Value!;
        SetMeta("source_branch", projection.Branch.ToString());
        var roots = projection.Entries.Where(entry => entry.Parent is null).ToArray();
        var source = new UiSourceLayout(repository, 0x26D980DF);
        int first = 3, last = 17;
        float span = source.Rectangle(last).Position.Y + source.Rectangle(last).Size.Y - source.Rectangle(first).Position.Y;
        int height = (int)source.Rectangle(2).Size.Y - (int)span + roots.Length * ((int)span / 8);
        Size = new(source.Rectangle(2).Size.X, height);
        bool hasChildren = projection.Entries.Any(entry => entry.Parent is not null);
        float clampWidth = hasChildren ? source.Rectangle(0).Size.X : Size.X;
        Position = new(Math.Clamp(anchor.X, 0, Math.Max(0, mapSize.X - clampWidth * scale)),
            Math.Clamp(anchor.Y, 0, Math.Max(0, mapSize.Y - height * scale)));
        PlaceHintCanvas();
        tree = new() { Name = "CityCommandTree", Size = Size, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(tree); MoveChild(tree, 0);
        source.Art(tree, 2, new(source.Rectangle(2).Position, Size), assets, shader);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        var captions = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1].Records;
        for (int index = 0; index < roots.Length; index++)
        {
            var entry = roots[index];
            var mount = source.Rectangle(first + index * 2);
            var children = projection.Entries.Where(item => item.Parent == entry.Command).ToArray();
            if (children.Length != 0)
            {
                var childSource = new UiSourceLayout(repository, 0x6E4B5E76);
                var pane = new Control { Name = entry.Command + "Children", Visible = false,
                    Position = source.Rectangle(35).Position + new Vector2(0, mount.Position.Y - source.Rectangle(7).Position.Y),
                    Size = childSource.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
                tree.AddChild(pane); childPanes.Add(entry.Command, pane);
                childSource.Art(pane, 1, childSource.Rectangle(1), assets, shader);
                for (int child = 0; child < children.Length; child++)
                {
                    var item = children[child];
                    var button = new UiSourceCitySubcommandButton { Name = item.Command.ToString(),
                        Position = childSource.Rectangle(2 + child * 3).Position };
                    pane.AddChild(button);
                    button.Configure(repository, assets, shader, font, Caption(item), () => Release(item));
                    Prepare(button, item, EditorRoute(item.Command).HasValue);
                    abandonButtons.Add(button.Abandon);
                }
            }
            var rootButton = new UiSourceListCheckboxButton { Name = entry.Command.ToString(), Position = mount.Position };
            tree.AddChild(rootButton);
            rootButton.Configure(repository, assets, shader, font, mount.Size, Caption(entry), () => Release(entry));
            Prepare(rootButton, entry, children.Length != 0 || EditorRoute(entry.Command).HasValue);
            abandonButtons.Add(rootButton.Abandon);
        }
        return true;

        string Caption(CityCommandMenuEntry entry) => captions[entry.CaptionId].Text
            ?? throw new InvalidDataException("City command caption is unavailable.");
        void Prepare(UiSourceButton button, CityCommandMenuEntry entry, bool editorSupported)
        {
            button.InputAdmission=()=>generation==currentGeneration&&Visible&&!CancellationOpen;
            button.Disabled = !entry.SourceEnabled.Available || !entry.SourceEnabled.Value || !editorSupported;
            button.SetMeta("source_unknown", entry.SourceEnabled.UnresolvedInput ?? "");
            button.SetMeta("source_enabled", entry.SourceEnabled.Available && entry.SourceEnabled.Value);
            button.SetMeta("editor_supported", editorSupported);
            button.MouseEntered += () =>
            {
                if (generation == currentGeneration && Visible && !entry.SourceEnabled.Available)
                    hint.DisplayText(text.Get("detail_unknown"));
            };
            button.MouseExited += () => { if (generation == currentGeneration) hint.DisplayText(null); };
        }
        void Release(CityCommandMenuEntry entry)
        {
            if (generation != currentGeneration || !Visible) return;
            hint.DisplayText(null);
            if (childPanes.ContainsKey(entry.Command))
            {
                // Source release opens this branch; repeated release keeps it open.
                foreach (var pair in childPanes) pair.Value.Visible = pair.Key == entry.Command;
                return;
            }
            foreach (var pane in childPanes.Values) pane.Hide();
            if (EditorRoute(entry.Command) is { } command) Command?.Invoke(cityId, command);
        }
    }

    private void PlaceHintCanvas() => hintCanvas.Position = -Position / Scale;

    // Capability belongs to the UI. In particular, cancellation and transfer
    // are not implemented by opening an unrelated command-deck page.
    private static MapCityCommand? EditorRoute(CityCommandMenuCommand command) => command switch
    {
        CityCommandMenuCommand.Deploy => MapCityCommand.Deploy,
        CityCommandMenuCommand.CancelDeployment => MapCityCommand.CancelDeployment,
        CityCommandMenuCommand.Transport => MapCityCommand.Transport,
        CityCommandMenuCommand.AreaAdministration => MapCityCommand.Administration,
        CityCommandMenuCommand.CityDomestic => MapCityCommand.CityDomestic,
        CityCommandMenuCommand.Merchant => MapCityCommand.Merchant,
        CityCommandMenuCommand.Investment => MapCityCommand.Investment,
        CityCommandMenuCommand.Transfer => MapCityCommand.Move,
        CityCommandMenuCommand.Summon => MapCityCommand.Summon,
        CityCommandMenuCommand.CityInformation => MapCityCommand.CityInformation,
        CityCommandMenuCommand.AreaInformation => MapCityCommand.AreaInformation,
        _ => null
    };

    public override void _Process(double delta)
    {
        if (Visible && hint is { Visible: true })
            hint.UpdatePointer(hintCanvas.GetLocalMousePosition());
    }

    public override void _ExitTree()
    {
        foreach (var abandon in abandonButtons) abandon();
        font?.Dispose(); assets?.Dispose(); shader?.Dispose();
    }
}
