using Godot;
using San14.Data;

internal sealed record MapInformationRecordField(string Caption, string Value);

/// <summary>Readable values for information types whose dedicated detail
/// composition is still pending. Only values from the selected publication
/// are shown; no missing value is replaced by an invented default.</summary>
internal partial class MapInformationRecordView : DetailsView
{
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!, rows = null!;
    private UiSourceScrollbar scrollbar = null!;
    private readonly List<(Control Row, float Top, float Height)> entries = [];
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text, string title,
        IReadOnlyList<MapInformationRecordField> fields, Action close, Func<int> scrollSpeed)
    {
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var header = new UiSourceHeader(); canvas.AddChild(header);
        ConfigureSourcePage("InformationRecord", header, close);
        header.Configure(repository, assets, shader, font, title);
        var source = new UiSourceLayout(repository, 0xA3B4A82E);
        source.Blur(canvas, 2, source.Rectangle(2, new(191, 147)));
        source.Art(canvas, 3, source.Rectangle(3, new(191, 147)), assets, shader);
        rows = new() { Position = new(230, 177), Size = new(1432, 652), ClipContents = true,
            MouseFilter = MouseFilterEnum.Pass };
        canvas.AddChild(rows);
        var help = new UiSourceLayout(repository, 0xAB6ADAA7);
        var helpNode = help.Nodes.Single(node => node.Type == 2);
        var profile = MapFontProfile.FromNode(helpNode, help.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[13]);
        var paragraphProfile = profile with { RawAlign = 0, RawLineArrange = 1,
            WidthFitMode = 3, RawCharacterMode = 2 };
        float top = 0;
        foreach (var field in fields)
        {
            int Measure(string value) => (int)font.Measure(value, paragraphProfile).Width;
            string wrapped = DeploymentNarrationPlainParagraph.WrapKnownColourParagraph(field.Value, Measure, 1080, 64);
            float height = Math.Max(48, (wrapped.Count(code => code == '\n') + 1) * 38 + 12);
            var row = new Control { Size = new(1432, height), MouseFilter = MouseFilterEnum.Pass };
            rows.AddChild(row); entries.Add((row, top, height));
            var caption = new UiSourceText { Position = new(0, 8), Size = new(300, 32) };
            row.AddChild(caption); caption.Configure(font, profile); caption.Text = field.Caption;
            var value = new UiSourceParagraph { Position = new(324, 4), Size = new(1080, height - 8) };
            row.AddChild(value); value.Configure(font, paragraphProfile); value.Apply(wrapped);
            top += height;
        }
        scrollbar = new() { Position = new(1687, 177), Size = new(16, 652) };
        canvas.AddChild(scrollbar);
        scrollbar.Configure(repository, assets, shader, Math.Max(0, (int)Math.Ceiling(top - 652)),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        rows.GuiInput += input => { if (!abandoned) scrollbar.HandleWheel(input); };
        foreach (var entry in entries)
            entry.Row.GuiInput += input => { if (!abandoned) scrollbar.HandleWheel(input); };
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        var back = new UiSourceRoundButton { Position = footer.Rectangle(1, new(0, 904)).Position,
            InputAdmission = () => !abandoned && IsVisibleInTree() };
        canvas.AddChild(back);
        back.Configure(repository, assets, shader, text.SourceLocale, "000_cmn_txt_decision_modoru", RequestClose, UiAudioCue.Cancel);
        SetMeta("dedicated_source_detail_composition", false);
        Resized += Fit; Fit(); PositionRows();
    }

    private void PositionRows()
    {
        foreach (var entry in entries)
        {
            float top = entry.Top - scrollbar.Value;
            entry.Row.Position = new(0, top);
            entry.Row.Visible = top > -entry.Height && top < rows.Size.Y;
        }
    }
    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale;
        canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree()
    { abandoned = true; font?.Dispose(); assets?.Dispose(); shader?.Dispose(); base._ExitTree(); }
}
