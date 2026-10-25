using Godot;
using San14.Data;

/// <summary>The ordinary Army action31 balloon, using its actual map child.</summary>
internal partial class ArmyMapSpeechBubbleView : Control
{
    private UiSourceParagraph _body = null!;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new(288, 140);
        var source = new UiSourceLayout(repository, 0x47FB32BE);
        source.Art(this, 5, source.Rectangle(5), assets, shader);
        var rectangle = source.Rectangle(6);
        var profile = MapFontProfile.FromNode(source.Nodes[6], source.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[1]) with
            { RawAlign = 0, RawLineArrange = 1, WidthFitMode = 3, RawCharacterMode = 2 };
        _body = new UiSourceParagraph { Name = "ArmySpeechText", Position = rectangle.Position, Size = rectangle.Size };
        AddChild(_body); _body.Configure(font, profile);
    }

    internal void ApplyText(string text) => _body.Apply(text);
}
