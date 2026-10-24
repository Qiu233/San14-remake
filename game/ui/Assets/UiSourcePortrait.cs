using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The ordinary Face2/Face3/Face4/Face5 portrait quad. Its caller supplies the resolved
/// current portrait source; this control does not guess stock/DLC/edit routing.</summary>
internal partial class UiSourcePortrait : Control
{
    internal void ConfigureFace1(GameResourceRepository repository,UiAssets assets,Shader shader,LoadedSprite portrait)
        => ConfigureSource(repository,assets,shader,portrait,new UiSourceLayout(repository,0xA76F2721),1,3);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, LoadedSprite portrait)
        => ConfigureSource(repository,assets,shader,portrait,new UiSourceLayout(repository,0xACFB207E),1,3);

    internal void ConfigureFace2(GameResourceRepository repository,UiAssets assets,Shader shader,LoadedSprite portrait)
        => ConfigureSource(repository,assets,shader,portrait,new UiSourceLayout(repository,0xA9487A40),1,3);

    internal void ConfigureFace3(GameResourceRepository repository,UiAssets assets,Shader shader,LoadedSprite portrait)
        => ConfigureSource(repository,assets,shader,portrait,new UiSourceLayout(repository,0xB20B608C),3,2);

    internal void ConfigureFace5(GameResourceRepository repository,UiAssets assets,Shader shader,LoadedSprite portrait)
        => ConfigureSource(repository,assets,shader,portrait,new UiSourceLayout(repository,0xAED4739D),1,3);

    internal void ConfigureOfficerCard(GameResourceRepository repository,UiAssets assets,Shader shader,LoadedSprite portrait)
        => ConfigureSource(repository,assets,shader,portrait,new UiSourceLayout(repository,0xCB7DEBFC),9,3);

    internal void ConfigureRankup(GameResourceRepository repository,UiAssets assets,Shader shader,LoadedSprite portrait)
        => ConfigureSource(repository,assets,shader,portrait,new UiSourceLayout(repository,0xC5EA2F4C),2,2);

    internal void ConfigureMediation(GameResourceRepository repository,UiAssets assets,Shader shader,LoadedSprite portrait,int slot)
        => ConfigureSource(repository,assets,shader,portrait,new UiSourceLayout(repository,0x1DE0C63E),14+slot*11,2);

    // The scenario caller resolves bank10 directly. Its two-part face mask
    // differs from both ordinary Face3 and the three-layer chara_B standing art.
    internal void ConfigureScenarioPreview(GameResourceRepository repository, UiAssets assets, Shader shader, LoadedSprite portrait)
    {
        var source = new UiSourceLayout(repository, 0xB1FE7E6D);
        Size = source.Rectangle(0).Size;
        ConfigureSource(repository, assets, shader, portrait, source, 1, 2);
    }

    internal void ConfigureMapHeader(GameResourceRepository repository,UiAssets assets,Shader shader,LoadedSprite portrait)
    {
        var source = new UiSourceLayout(repository, 0xB639C019);
        var root = source.Nodes[0];
        // Common09 owns its .4 root scale. The caller's integer control size
        // is a hit rectangle, not another scale or recursive artwork resize.
        Size = new(root.Value44 * root.HeaderValues[7], root.Value46 * root.HeaderValues[8]);
        ConfigureSource(repository, assets, shader, portrait, source, 1, 3);
    }

    private void ConfigureSource(GameResourceRepository repository,UiAssets assets,Shader shader,LoadedSprite portrait,
        UiSourceLayout source,int nodeIndex,int componentCount)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var node = LcskSpriteNodeReader.Read(source.Source.Layout,source.Nodes[nodeIndex]);
        var group = node.Groups.Single();
        if (group.ComponentCount != componentCount || group.Parts.Count != componentCount)
            throw new InvalidDataException("Portrait requires its original component count.");
        var parts = group.Parts;
        for (int i=0;i<componentCount;i++)
        {
            var raw = parts[i].RawBytes;
            if (raw[6] != (i==0?0:3) || raw[7] != (i==1?1:0) || raw[8] != 0)
                throw new InvalidDataException("Portrait component modes differ from the ordinary portrait consumer.");
        }
        var image = new UiSourceSprite {Name="PortraitQuad",Size=Size};
        AddChild(image);
        Texture2D texture = MapPortraitQuad.UsesSrgbView(repository,assets,portrait.Definition.Name,portrait)
            ? assets.SrgbSpritePart(portrait.Definition.Name,parts[0].Uv,portrait)
            : assets.SpritePart(portrait.Definition.Name,parts[0].Uv,portrait);
        Texture2D firstMask = Mask(parts[1]);
        Texture2D? secondMask=componentCount==3?Mask(parts[2]):null;
        image.Configure(shader,texture,componentCount==3?UiSourceSprite.Blend.ThreeTexturePortrait:UiSourceSprite.Blend.TwoTexturePortrait);
        image.SetClipping(texture,firstMask,new(Vector2.Zero,Size),secondMask,new(Vector2.Zero,Size));
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        SetMeta("portrait_sprite",portrait.Definition.Name);
        SetMeta("source_component_count",componentCount);

        Texture2D Mask(LcskSpriteNodePart part) => MapPortraitQuad.UsesSrgbView(repository,assets,part.SpriteName)
            ? assets.SrgbLayoutSpritePart(source.Source,part.SpriteName,part.Uv)
            : assets.SpritePart(part.SpriteName,part.Uv);
    }
}
