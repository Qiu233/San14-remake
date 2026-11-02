using Godot;
using San14.Data;

/// <summary>The report sequence's CCmnBg picture28 and medium blur. Its
/// independent application-resource lease outlives both content dialogs.</summary>
internal partial class ReportSequenceBackgroundView : Control
{
    private Control canvas = null!;
    private bool configured;

    internal void Configure(GameResourceRepository repository, AdministrationPageChrome.Resources resources)
    {
        if (configured) throw new InvalidOperationException("Report background is already configured.");
        configured = true;
        Name = "ReportSequenceBackground";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var assets = resources.Assets; var shader = resources.SpriteShader;
        {
            canvas = new Control { Name = "ReportBackgroundCanvas", Size = new(1920, 1080),
                MouseFilter = MouseFilterEnum.Ignore };
            AddChild(canvas);
            var source = assets.SourceLayout(0xF6352F00);
            source.Art(canvas, 1, source.Rectangle(1), assets, shader,
                commonTexturePack: "texture_stl_event_bg_028", commonSpriteOverride: "stl_event_bg_028");
            var blur = new UiSourceCommonBlur(); canvas.AddChild(blur);
            blur.Configure(repository, assets.SourceLayout(0xAEF1609E));
            Resized += Fit; Fit();
        }
    }

    internal void Retire() { Hide(); }

    private void Fit()
    {
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale;
        canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }

}
