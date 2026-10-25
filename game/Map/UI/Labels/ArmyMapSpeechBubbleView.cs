using Godot;
using San14.Data;

/// <summary>The source UnitAction balloon groups, shared by Army speech actions.</summary>
internal partial class ArmyMapSpeechBubbleView : Control
{
    private readonly Control[] _groups = new Control[2];
    private readonly UiSourceParagraph[] _bodies = new UiSourceParagraph[2];
    internal int LayoutStyle { get; private set; }

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font, int style = 0)
    {
        if (style is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(style));
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new(288, 140);
        var source = new UiSourceLayout(repository, 0x47FB32BE);
        //1BC3C0 selects balloon_a/text_a or balloon_b/text_b; the unused
        //groups, including balloon_c, never become visible with this owner.
        for (int group = 0; group < 2; group++)
        {
            int art = group == 0 ? 5 : 7, text = group == 0 ? 6 : 8;
            var host = new Control { Name = "Balloon_" + group, MouseFilter = MouseFilterEnum.Ignore, Size = Size };
            AddChild(host); _groups[group] = host;
            source.Art(host, art, source.Rectangle(art), assets, shader);
            var rectangle = source.Rectangle(text);
            var profile = MapFontProfile.FromNode(source.Nodes[text], source.Source.Source.ResourceUri)
                .WithPreset(MapTextPresets.Create()[1]) with
                { RawAlign = 0, RawLineArrange = 1, WidthFitMode = 3, RawCharacterMode = 2 };
            var body = new UiSourceParagraph { Name = "ArmySpeechText", Position = rectangle.Position, Size = rectangle.Size };
            host.AddChild(body); body.Configure(font, profile); _bodies[group] = body;
        }
        ApplyStyle(style);
    }

    internal void ApplyStyle(int style)
    {
        if (style is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(style));
        LayoutStyle = style;
        for (int index = 0; index < _groups.Length; index++) _groups[index].Visible = index == style;
    }

    internal void ApplyText(string text) => _bodies[LayoutStyle].Apply(text);
}
