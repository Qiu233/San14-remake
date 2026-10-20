using Godot;
using San14.Data;

/// <summary>The common decide/return control, sharing the view's actual assets.</summary>
internal partial class UiSourceRoundButton : UiSourceButton
{
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        string locale, string caption, Action? pressed = null)
    {
        var layout = new UiSourceLayout(repository, 0xAD4A3EFA);
        Size = new(layout.Nodes[0].Value44, layout.Nodes[0].Value46);
        Control[] states = new[] { 3, 5, 8, 12, 14 }
            .Select(index => layout.Art(this, index, layout.Rectangle(index), assets, shader)).ToArray();
        var declared = new LocalizedUiSpriteSource(repository).LoadCommonCaption(locale, caption);
        Texture2D texture = assets.SrgbSprite(declared.Definition.Name, declared);
        Rect2 rectangle = layout.Rectangle(16);
        var label = new UiSourceSprite { Name = "Caption", Position = rectangle.Position, Size = rectangle.Size };
        AddChild(label);
        label.Configure(shader, texture, UiSourceSprite.Blend.Ordinary);
        var palettes = MapSpritePalettes.Create();
        ConfigureStates(states, state => label.SetSource(texture, palettes[25 + state]));
        if (pressed is not null) Pressed += pressed;
    }
}
