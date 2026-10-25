using Godot;
using San14.Data;

/// <summary>The original EventStill layer, shared by reports and resource notices.</summary>
internal partial class EventIllustrationView : Control
{
    private UiAssets? assets;
    private Shader? shader;
    private Control canvas = null!;
    internal int EventKind { get; private set; }

    internal void Configure(GameResourceRepository repository, int eventKind)
    {
        if (eventKind is not (14 or 15 or 16)) throw new ArgumentOutOfRangeException(nameof(eventKind));
        string suffix = eventKind.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
        Configure(repository, new FinalOrderIllustrationRoute(eventKind,
            "texture_stl_event_" + suffix, "stl_event_" + suffix));
    }

    internal void Configure(GameResourceRepository repository, FinalOrderIllustrationRoute route)
    {
        EventKind = route.IllustrationId;
        Name = "EventIllustration"; ZIndex = 94; MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(canvas);
        assets = new(repository); shader = UiSourceSprite.CreateShader();
        var still = new UiSourceLayout(repository, route.LayoutId);
        // 9317A0 changes only the authored dummy_ev_event image. Keep its
        // actual 864x648 rectangle; it is not a full-screen fitted background.
        still.Art(canvas, route.ArtNode, still.Rectangle(route.ArtNode), assets, shader,
            commonTexturePack: route.TexturePack, commonSpriteOverride: route.SpriteName);
        SetMeta("source_layout", still.Source.Source.ResourceUri);
        Resized += Fit; Fit();
    }

    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale;
        canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }

    public override void _ExitTree() { assets?.Dispose(); shader?.Dispose(); }
}
