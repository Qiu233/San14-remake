using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The startup page's unique_otomo image and its independent mode13
/// alpha mask. Resource routing belongs to the fixed page publication's host.</summary>
internal partial class NewGameRulerPortrait : Control
{
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, LoadedSprite portrait)
        => ConfigureAuthored(repository, assets, shader, portrait, new UiSourceLayout(repository, 0xB468AE65), 3, false);

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,LoadedUiSpriteSource portrait)
        =>ConfigureAuthored(repository,assets,shader,portrait,assets.SourceLayout(0xB468AE65),3,false);

    internal void ConfigureAuthored(GameResourceRepository repository,UiAssets assets,Shader shader,
        LoadedUiSpriteSource portrait,UiSourceLayout source,int nodeIndex,bool localCoordinates=true)
    {
        MouseFilter=MouseFilterEnum.Ignore;
        var group=LcskSpriteNodeReader.Read(source.Source.Layout,source.Nodes[nodeIndex]).Groups.Single();
        if(group.ComponentCount!=2||group.Parts.Count!=2)throw new InvalidDataException("Startup ruler portrait requires its two original components.");
        var color=group.Parts[0];var maskPart=group.Parts[1];
        if(color.RawBytes[6]!=0||color.RawBytes[7]!=0||color.RawBytes[8]!=0||maskPart.RawBytes[6]!=13||maskPart.RawBytes[7]!=0||maskPart.RawBytes[8]!=0)
            throw new InvalidDataException("Startup ruler portrait component modes differ from the original source.");
        var rectangle=source.Rectangle(nodeIndex);
        Position=localCoordinates?Vector2.Zero:rectangle.Position-source.Rectangle(2).Position;
        Size=localCoordinates?new(source.Nodes[nodeIndex].Value44,source.Nodes[nodeIndex].Value46):rectangle.Size;
        var texture=assets.PortraitPart(portrait,color.Uv);
        Texture2D mask=MapPortraitQuad.UsesSrgbView(repository,assets,maskPart.SpriteName)
            ?assets.SrgbLayoutSpritePart(source.Source,maskPart.SpriteName,maskPart.Uv):assets.SpritePart(maskPart.SpriteName,maskPart.Uv);
        var image=new UiSourceSprite{Name="UniqueRuler",Size=Size};AddChild(image);
        image.Configure(shader,texture,UiSourceSprite.Blend.UniqueRulerPortrait);
        image.SetEncodedPortraitSampling(portrait.Image);image.SetClipping(texture,mask,new(Vector2.Zero,Size));
        SetMeta("source_layout",source.Source.Source.ResourceUri);SetMeta("source_node_index",nodeIndex);
        SetMeta("portrait_sprite",portrait.Definition.Name);SetMeta("mask_sprite",maskPart.SpriteName);
    }

    internal void ConfigureAuthored(GameResourceRepository repository, UiAssets assets, Shader shader,
        LoadedSprite portrait, UiSourceLayout source, int nodeIndex, bool localCoordinates = true)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var node = LcskSpriteNodeReader.Read(source.Source.Layout, source.Nodes[nodeIndex]);
        var group = node.Groups.Single();
        if (group.ComponentCount != 2 || group.Parts.Count != 2)
            throw new InvalidDataException("Startup ruler portrait requires its two declared components.");
        var imagePart = group.Parts[0]; var maskPart = group.Parts[1];
        if (imagePart.RawBytes[6] != 0 || imagePart.RawBytes[7] != 0 || imagePart.RawBytes[8] != 0 ||
            maskPart.RawBytes[6] != 13 || maskPart.RawBytes[7] != 0 || maskPart.RawBytes[8] != 0)
            throw new InvalidDataException("Startup ruler portrait component modes differ from the source pane.");
        Rect2 rectangle = source.Rectangle(nodeIndex);
        Position = localCoordinates ? Vector2.Zero : rectangle.Position - source.Rectangle(2).Position;
        Size = localCoordinates ? new(source.Nodes[nodeIndex].Value44, source.Nodes[nodeIndex].Value46) : rectangle.Size;
        Texture2D texture = MapPortraitQuad.UsesSrgbView(repository, assets, portrait.Definition.Name, portrait)
            ? assets.SrgbSpritePart(portrait.Definition.Name, imagePart.Uv, portrait)
            : assets.SpritePart(portrait.Definition.Name, imagePart.Uv, portrait);
        Texture2D mask = MapPortraitQuad.UsesSrgbView(repository, assets, maskPart.SpriteName)
            ? assets.SrgbLayoutSpritePart(source.Source, maskPart.SpriteName, maskPart.Uv)
            : assets.SpritePart(maskPart.SpriteName, maskPart.Uv);
        var image = new UiSourceSprite { Name = "UniqueRuler", Size = Size };
        AddChild(image);
        image.Configure(shader, texture, UiSourceSprite.Blend.UniqueRulerPortrait);
        image.SetClipping(texture, mask, new(Vector2.Zero, Size));
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("source_node_index", nodeIndex);
        SetMeta("portrait_sprite", portrait.Definition.Name);
        SetMeta("mask_sprite", maskPart.SpriteName);
    }
}
