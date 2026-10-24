using Godot;
using San14.Data;

/// <summary>The report's original advice cell and ordinary three-layer portrait.
/// The presenter resolves the person/portrait branch and prepares each text page.</summary>
internal partial class ActionReportStrategistView : Control
{
    private GameResourceRepository repository = null!;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private Control portraitHost = null!;
    private UiSourceStandingPortrait? portrait;
    private IReadOnlyList<LoadedSprite>? currentLayers;
    private UiSourceParagraph body = null!;
    private Rect2 advanceRectangle;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        IReadOnlyList<LoadedSprite>? standingLayers)
    {
        this.repository = repository; this.assets = assets; this.shader = shader;
        MouseFilter = MouseFilterEnum.Ignore;
        var source = new UiSourceLayout(repository, 0x47835420);
        Size = source.Rectangle(0).Size;
        var box = source.Rectangle(1);
        portraitHost = new Control
        {
            Name = "StrategistPortrait", Position = box.Position, Size = box.Size,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(portraitHost);
        ApplyPortrait(standingLayers);
        source.Art(this, 3, source.Rectangle(3), assets, shader);
        advanceRectangle = source.Rectangle(3);
        var cell = source.Rectangle(4);
        body = new UiSourceParagraph { Name = "StrategistBody", Position = cell.Position, Size = cell.Size };
        AddChild(body);
        body.Configure(font, MapFontProfile.FromNode(source.Nodes[4], source.Source.Source.ResourceUri));
        body.Apply("");
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    // Null means a known-invalid person, whose whole standing control is hidden.
    // Animated/masked branches must be resolved by their own presenter/renderer;
    // they must not be supplied here as an ordinary static portrait fallback.
    internal void ApplyPortrait(IReadOnlyList<LoadedSprite>? standingLayers)
    {
        if (standingLayers is null) { portraitHost.Hide(); return; }
        if (!ReferenceEquals(currentLayers, standingLayers))
        {
            if (portrait is not null) { portrait.Hide(); portrait.QueueFree(); }
            portrait = new UiSourceStandingPortrait { Name = "StandingPortrait" };
            portraitHost.AddChild(portrait);
            portrait.Configure(repository, assets, shader, standingLayers);
            currentLayers = standingLayers;
        }
        portraitHost.Show();
    }

    internal void ApplyPage(string expanded) => body.Apply(expanded);

    internal bool ContainsAdvancePoint(Vector2 point) =>
        point.X >= advanceRectangle.Position.X && point.Y >= advanceRectangle.Position.Y &&
        point.X <= advanceRectangle.End.X && point.Y <= advanceRectangle.End.Y;

    public override void _ExitTree() => currentLayers = null;
}
