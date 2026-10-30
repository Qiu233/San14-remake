using Godot;
using San14.Data;

/// <summary>The report sequence's CCmnBg picture28 and medium blur. Its
/// resources outlive both content dialogs and never borrow their UiAssets.</summary>
internal partial class ReportSequenceBackgroundView : Control
{
    private UiAssets? assets;
    private Shader? shader;
    private Control canvas = null!;
    private bool configured, released;

    internal void Configure(GameResourceRepository repository)
    {
        if (configured) throw new InvalidOperationException("Report background is already configured.");
        configured = true;
        Name = "ReportSequenceBackground";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        try
        {
            assets = new(repository); shader = UiSourceSprite.CreateShader();
            canvas = new Control { Name = "ReportBackgroundCanvas", Size = new(1920, 1080),
                MouseFilter = MouseFilterEnum.Ignore };
            AddChild(canvas);
            var source = new UiSourceLayout(repository, 0xF6352F00);
            source.Art(canvas, 1, source.Rectangle(1), assets, shader,
                commonTexturePack: "texture_stl_event_bg_028", commonSpriteOverride: "stl_event_bg_028");
            var blur = new UiSourceCommonBlur(); canvas.AddChild(blur); blur.Configure(repository);
            Resized += Fit; Fit();
        }
        catch { ReleaseResources(); throw; }
    }

    internal void Retire() { Hide(); }

    private void Fit()
    {
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale;
        canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }

    private void ReleaseResources()
    {
        if (released) return;
        released = true;
        assets?.Dispose(); assets = null;
        shader?.Dispose(); shader = null;
    }

    public override void _ExitTree() => ReleaseResources();
}
