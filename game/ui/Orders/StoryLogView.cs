using Godot;
using San14.Data;

internal sealed record StoryLogVisual(string Name, string Text, LoadedSprite? Portrait);

/// <summary>The current script's retained message log. Scrolling rebinds five
/// source rows from captured text and portraits, without formatting or world queries.</summary>
internal partial class StoryLogView : Control
{
    private sealed record Row(Control Root, UiSourceText Name, UiSourceParagraph Body)
    { internal int Index = -1; internal UiSourcePortrait? Portrait; }
    private readonly List<Row> pool = [];
    private IReadOnlyList<StoryLogVisual> entries = [];
    private UiAssets? assets;
    private Shader? shader;
    private MapFont? font;
    private GameResourceRepository repository = null!;
    private UiSourceLayout rowSource = null!;
    private Control canvas = null!;
    private UiSourceScrollbar scrollbar = null!;
    private Func<bool>? admitted;
    private Action? closed;

    internal void Configure(GameResourceRepository repository, PreviewText text, IReadOnlyList<StoryLogVisual> entries,
        Func<bool> admitted, Action closed, Func<int> scrollSpeed)
    {
        this.repository = repository; this.entries = entries; this.admitted = admitted; this.closed = closed;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Stop; ZIndex = 30;
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        canvas = new() { Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop }; AddChild(canvas);
        var source = new UiSourceLayout(repository, 0x909CAB01);
        Vector2 mount = (new Vector2(1920, 1080) - source.Rectangle(0).Size) / 2;
        source.Art(canvas, 1, source.Rectangle(1, mount), assets, shader);
        var first = source.Rectangle(6, mount); var last = source.Rectangle(3, mount);
        int pitch = (int)(source.Rectangle(5).Position.Y - source.Rectangle(6).Position.Y);
        var rows = new Control { Name = "StoryLogRows", Position = first.Position,
            Size = new(first.Size.X, last.End.Y - first.Position.Y), ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        canvas.AddChild(rows); rowSource = new(repository, 0x0834A553);
        for (int slot = 0; slot < Math.Min(entries.Count, (int)rows.Size.Y / pitch + 2); slot++)
        {
            var root = new Control { Name = "StoryLogRow" + slot, Size = rowSource.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Pass };
            rows.AddChild(root); rowSource.Art(root, 1, rowSource.Rectangle(1), assets, shader);
            var name = rowSource.Text(root, 4, default, "", font);
            var body = new UiSourceParagraph { Position = rowSource.Rectangle(5).Position, Size = rowSource.Rectangle(5).Size };
            root.AddChild(body); body.Configure(font, MapFontProfile.FromNode(rowSource.Nodes[5], rowSource.Source.Source.ResourceUri));
            pool.Add(new(root, name, body));
            root.GuiInput += input => { if (Admitted()) scrollbar.HandleWheel(input); };
        }
        var box = source.Rectangle(7, mount);
        scrollbar = new() { Name = "StoryLogScrollbar", Position = box.Position, Size = box.Size }; canvas.AddChild(scrollbar);
        scrollbar.Configure(repository, assets, shader, Math.Max(entries.Count * pitch - (int)rows.Size.Y, 0), scrollSpeed(),
            _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        rows.GuiInput += input => { if (Admitted()) scrollbar.HandleWheel(input); };
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        var back = new UiSourceRoundButton { Name = "StoryLogReturn", Position = footer.Rectangle(1, new(0, 904)).Position,
            InputAdmission = Admitted };
        canvas.AddChild(back); back.ConfigureScenarioReturn(repository, assets, shader, text.SourceLocale, RequestClose);
        Resized += Fit; Fit(); scrollbar.SetValue(scrollbar.Range); PositionRows();
        SetMeta("source_layout", source.Source.Source.ResourceUri); SetMeta("message_count", entries.Count);

        void PositionRows()
        {
            int firstIndex = scrollbar.Value / pitch;
            for (int slot = 0; slot < pool.Count; slot++)
            {
                var row = pool[slot]; int index = firstIndex + slot;
                if (index >= entries.Count) { row.Root.Hide(); continue; }
                row.Root.Show(); row.Root.Position = new(0, index * pitch - scrollbar.Value);
                if (row.Index == index) continue;
                row.Index = index; var entry = entries[index]; row.Name.Text = entry.Name; row.Body.Apply(entry.Text);
                if (row.Portrait is { } previous) { row.Root.RemoveChild(previous); previous.QueueFree(); row.Portrait = null; }
                if (entry.Portrait is { } portrait)
                {
                    var face = new UiSourcePortrait { Position = rowSource.Rectangle(2).Position, Size = rowSource.Rectangle(2).Size };
                    row.Root.AddChild(face); face.ConfigureFace2(repository, assets, shader, portrait); row.Portrait = face;
                }
            }
        }
    }

    private bool Admitted() => IsVisibleInTree() && (admitted?.Invoke() ?? false);
    internal void RequestClose() { if (Admitted()) closed?.Invoke(); }
    public override void _Input(InputEvent input)
    {
        if (Admitted() && (input is InputEventKey { Keycode: Key.Escape, Pressed: true, Echo: false } or
            InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false }))
        { GetViewport().SetInputAsHandled(); RequestClose(); }
    }
    private void Fit()
    { float factor = Math.Min(Size.X / 1920, Size.Y / 1080); canvas.Scale = Vector2.One * factor; canvas.Position = (Size - new Vector2(1920, 1080) * factor) / 2; }
    internal void Abandon() { admitted = null; closed = null; Hide(); }
    public override void _ExitTree() { admitted = null; closed = null; font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
}
