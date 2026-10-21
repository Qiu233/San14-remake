using Godot;
using San14.Data;

/// <summary>A common source tab, resized by its caller and sharing the view's font.</summary>
internal partial class UiSourceTabButton : UiSourceButton
{
    internal override UiAudioCue PressedCue => UiAudioCue.Tab;
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string caption, uint layoutFile = 0x9AFB27B7)
    {
        var source = new UiSourceLayout(repository, layoutFile);
        var presets = MapTextPresets.Create();
        var states = new[] { 3, 5, 8, 11, 13 }
            .Select(index => source.Art(this, index, new Rect2(Vector2.Zero, Size), assets, shader)).ToArray();
        Rect2 captionRectangle = source.Rectangle(16);
        captionRectangle.Size += Size - new Vector2(source.Nodes[0].Value44, source.Nodes[0].Value46);
        var profile = MapFontProfile.FromNode(source.Nodes[16], source.Source.Source.ResourceUri);
        var label = source.Text(this, 16, default, caption, font, rectangle: captionRectangle);
        ConfigureStates(states, state =>
        {
            label.Configure(font, profile.WithPreset(presets[8 + Math.Min(state, 3)]));
            label.QueueRedraw();
        });
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }
}
