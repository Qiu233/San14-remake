using Godot;
using San14.Data;

/// <summary>The small original force flag, borrowing the detail view's resources.</summary>
internal partial class UiSourceForceFlag : Control
{
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        DetailForceAppearance appearance, bool embedded = false, bool neighbor = false)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new(40, 52);
        if (neighbor) Scale = new(34f / 40, 44f / 52);
        var source = new UiSourceLayout(repository, embedded ? 0x3858CC07u : 0xA564F68Du);
        int spriteNode = embedded ? 5 : 1, textNode = embedded ? 6 : 2;
        Vector2 mount = embedded ? -source.Rectangle(4).Position : Vector2.Zero;
        var fill = appearance.Fill;
        source.Art(this, spriteNode, source.Rectangle(spriteNode, mount), assets, shader, new(fill, fill, fill, fill));
        Rect2 textRectangle = source.Rectangle(textNode, mount);
        if (neighbor && appearance.IsValid) textRectangle.Size = new(22, 22);
        var text = source.Text(this, textNode, default, appearance.Glyph, font, rectangle: textRectangle);
        text.Name = "FlagLetter";
        uint colour = appearance.Letter.CpuPacked;
        var profile = MapFontProfile.FromNode(source.Nodes[textNode], source.Source.Source.ResourceUri);
        text.Configure(font, profile with { ColourLT = colour, ColourRT = colour, ColourLB = colour, ColourRB = colour });
        SetMeta("fill_colour", fill.CpuPacked);
    }
}
