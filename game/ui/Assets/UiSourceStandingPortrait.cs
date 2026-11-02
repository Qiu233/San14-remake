using System.Buffers.Binary;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The source chara_B standing pose. Its caller resolves current
/// portrait resources; this renderer neither chooses characters nor masks faces.</summary>
internal partial class UiSourceStandingPortrait : Control
{
    private readonly List<(int Node, UiSourceSprite View)> storyLayers = [];
    private LcskDocument? storyLayout;
    internal bool? StoryDark { get; private set; }

    internal void SetStoryDark(bool dark, bool immediate = false)
    {
        var current = motion ?? throw new InvalidOperationException("Standing portrait is not configured.");
        ApplyMotion(current.SetDark(dark, immediate));
        StoryDark = dark;
        RefreshProcessing();
    }

    private static void ConfigureMaterial(UiSourceSprite image, LcskNodeRecord node)
    {
        //Ordinary portrait resources use no black interpolation, Gamma1 and
        //Saturation1. Other authored material routes require their own owner.
        var bytes = node.RawBytes.AsSpan();
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes[116..]) != 0 ||
            BinaryPrimitives.ReadSingleLittleEndian(bytes[124..]) != 1 ||
            BinaryPrimitives.ReadSingleLittleEndian(bytes[128..]) != 1)
            throw new InvalidDataException("Standing portrait material requires its ordinary colour route.");
        image.SetMaterialColorScale(BinaryPrimitives.ReadUInt32LittleEndian(bytes[120..]));
    }

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,
        IReadOnlyList<LoadedSprite> layers)
        =>ConfigureSources(repository,assets,shader,layers.Count,
            (layers as LoadedStandingPortraitSprites)?.Kind??StandingPortraitKind.ThreePart,
            (layers as LoadedStandingPortraitSprites)?.CurrentAppearanceIndex,
            (index,uv)=>
            {
                var sprite=layers[index];
                return MapPortraitQuad.UsesSrgbView(repository,assets,sprite.Definition.Name,sprite)
                    ?assets.SrgbSpritePart(sprite.Definition.Name,uv,sprite):assets.SpritePart(sprite.Definition.Name,uv,sprite);
            },index=>layers[index].Definition.Name,(_,_)=>{});

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,LoadedStandingPortraitSources layers)
        =>ConfigureSources(repository,assets,shader,layers.Count,layers.Kind,layers.CurrentAppearanceIndex,
            (index,uv)=>assets.PortraitPart(layers[index],uv),index=>layers[index].Definition.Name,
            (image,index)=>image.SetEncodedPortraitSampling(layers[index].Image));

    private void ConfigureSources(GameResourceRepository repository,UiAssets assets,Shader shader,int layerCount,
        StandingPortraitKind kind,ushort? currentAppearanceIndex,Func<int,LcskUvQuad,Texture2D> textureAt,
        Func<int,string> spriteName,Action<UiSourceSprite,int> configureSampling)
    {
        if (storyLayout is not null) throw new InvalidOperationException("Standing portrait already has a resource owner.");
        SetProcess(false);
        if(kind==StandingPortraitKind.AnimatedFrames)
        { ConfigureAnimated(repository,assets,shader,layerCount,textureAt,spriteName,configureSampling);ConfigureMotion(currentAppearanceIndex);return; }
        if(layerCount!=3)throw new InvalidDataException("Standing portrait requires its three resolved source layers.");
        MouseFilter=MouseFilterEnum.Ignore;
        var layout=assets.Layout(0xF8115B8B);
        storyLayout = layout.Layout;
        var nodes=LcskNodeReader.Read(layout.Layout);
        var branch=nodes.Single(node=>node.Name=="chara_B");
        RequirePlanar(branch);
        Vector2 size=new(branch.Value44,branch.Value46);
        if(branch.Type!=0||branch.Value48!=0x1111||size!=new Vector2(636,900)||
            branch.HeaderValues[7]!=1||branch.HeaderValues[8]!=1||
            branch.HeaderValues[0]!=size.X/2||branch.HeaderValues[1]!=-size.Y/2)
            throw new InvalidDataException("Standing portrait requires the original centered chara_B branch.");
        Size=size;
        var children=nodes.Where(node=>node.ParentIndex==branch.Index).ToArray();
        string[] names=["face_0001_w_633x900_a_99_b","face_0001_w_633x900_a_99_a","face_0001_w_633x900_a_99_c"];
        if(children.Length!=3)throw new InvalidDataException("Standing portrait source layer count differs.");
        for(int index=0;index<children.Length;index++)
        {
            var node=children[index];RequirePlanar(node);
            if(node.Type!=1||node.Name!=names[index])
                throw new InvalidDataException("Standing portrait source order differs.");
            var sprite=LcskSpriteNodeReader.Read(layout.Layout,node);
            if(sprite.RawTintBytes.Any(value=>value!=255)||sprite.Groups.Count!=1||
                sprite.Groups[0].ComponentCount!=1||sprite.Groups[0].Parts.Count!=1||
                BinaryPrimitives.ReadUInt32LittleEndian(node.RawBytes.AsSpan(84,4))!=0x00058400||
                BinaryPrimitives.ReadSingleLittleEndian(node.RawBytes.AsSpan(88,4))!=1||
                BinaryPrimitives.ReadSingleLittleEndian(sprite.Groups[0].RawScaleBytes.AsSpan(0,4))!=1||
                BinaryPrimitives.ReadSingleLittleEndian(sprite.Groups[0].RawScaleBytes.AsSpan(4,4))!=1)
                throw new InvalidDataException("Standing portrait requires its ordinary one-texture source composition.");
            Vector2 layerSize=new Vector2(node.Value44,node.Value46)*new Vector2(node.HeaderValues[7],node.HeaderValues[8]);
            Vector2 attachment=(node.Value48>>8) switch
            {0x11=>Vector2.Zero,0x12=>new(0,size.Y/2),_=>throw new InvalidDataException("Unknown standing portrait parent anchor.")};
            Vector2 pivot=(node.Value48&255) switch
            {0x11=>layerSize/2,0x12=>new(layerSize.X/2,layerSize.Y),_=>throw new InvalidDataException("Unknown standing portrait own anchor.")};
            var image=new UiSourceSprite{Name="StandingLayer"+index,
                Position=size/2+attachment+new Vector2(node.HeaderValues[0],-node.HeaderValues[1])-pivot,Size=layerSize};
            AddChild(image);
            var uv=sprite.Groups[0].Parts[0].Uv;
            image.Configure(shader,textureAt(index,uv),UiSourceSprite.Blend.SourceAlphaDestinationAlpha);
            configureSampling(image,index);
            ConfigureMaterial(image, node);
            storyLayers.Add((node.Index, image));
            image.SetMeta("portrait_sprite",spriteName(index));
            image.SetMeta("source_node_index",node.Index);
        }
        ConfigureMotion(currentAppearanceIndex);
        SetMeta("source_layout",layout.Source.ResourceUri);
    }

    private static void RequirePlanar(LcskNodeRecord node)
    {
        var values=node.HeaderValues;
        if(values[2]!=0||values[3]!=0||values[4]!=0||values[5]!=0||values[6]!=1||values[7]<=0||values[8]<=0)
            throw new InvalidDataException("Standing portrait requires its original planar static transform.");
    }
}
