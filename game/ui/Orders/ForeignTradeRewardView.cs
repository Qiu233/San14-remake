using Godot;
using San14.Data;

/// <summary>CCaravanItemDialog's real item/book or weapon body. The enclosing
/// report's original message wait owns confirmation; this pane cannot acknowledge it.</summary>
internal partial class ForeignTradeRewardView : Control
{
    private bool ownsResources=true;
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Control canvas=null!;
    internal void Configure(GameResourceRepository repository,PreviewText text,ForeignTradeRewardPanel value,
        AdministrationPageChrome.Resources? resources=null)
    {
        Name="ForeignTradeReward";MouseFilter=MouseFilterEnum.Ignore;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ownsResources=resources is null;
        assets=resources?.Assets??new(repository);font=resources?.Font??new(repository,text.SourceLocale);
        shader=resources?.SpriteShader??UiSourceSprite.CreateShader();
        canvas=new() {Name="SourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Ignore};AddChild(canvas);
        var source=assets.SourceLayout(value.LayoutId);Vector2 mount=new(-300,164);
        foreach(var node in source.Nodes.Where(node=>node.Type is 1 or 3))
        {
            if(node.Index==2)continue;
            source.Art(canvas,node.Index,source.Rectangle(node.Index,mount),assets,shader);
        }
        if(value.Image is { } image)
            source.Art(canvas,2,source.Rectangle(2,mount),assets,shader,
                commonTexturePack:image.TexturePack,commonSpriteOverride:image.SpriteName);
        if(value.ExternalImage is { } rgba)
        {
            using var bitmap=Image.CreateFromData(rgba.Width,rgba.Height,false,Image.Format.Rgba8,rgba.Pixels);
            var box=source.Rectangle(2,mount);
            canvas.AddChild(new TextureRect {Position=box.Position,Size=box.Size,
                Texture=ImageTexture.CreateFromImage(bitmap),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore});
        }
        if(value.ImageResourceError is { } error)GD.PushWarning(error);
        var presets=MapTextPresets.Create();
        foreach(var field in value.Fields)
        {
            if(value.WeaponDescription&&field.Key==7)
            {
                //826F10 explicitly selects literal character mode0 and no
                //width fitting. Render its actual wrapped literal glyph lines,
                //without changing the ordinary formatted-message guard.
                var box=source.Rectangle(7,mount);var profile=MapFontProfile.FromNode(source.Nodes[7],source.Source.Source.ResourceUri)
                    with{WidthFitMode=0,RawCharacterMode=0};
                string wrapped=DeploymentNarrationPlainParagraph.WrapKnownColourParagraph(field.Value,
                    line=>(int)font.Measure(line,profile).Width,(int)box.Size.X,64);
                var lines=DeploymentNarrationParagraphPlan.Runs(wrapped);
                float advance=font.Source.Record('\u3042',profile.Face).Height*profile.ScaleY+profile.VerticalSpace;
                for(int index=0;index<lines.Count;index++)
                {
                    var line=new UiSourceText {Name="WeaponDescription"+index,
                        Position=box.Position+new Vector2(0,index*advance),Size=new(box.Size.X,advance)};canvas.AddChild(line);
                    line.Configure(font,profile);
                    line.SetRuns(lines[index].Runs.Select(run=>new UiSourceTextRun(run.Text,run.Colour switch
                    {
                        NarrationBodyColour.TextPreset38Body=>presets[38].LT.CpuPacked,
                        NarrationBodyColour.TextPreset39Body=>presets[39].LT.CpuPacked,
                        NarrationBodyColour.TextPreset40Body=>presets[40].LT.CpuPacked,
                        _=>null
                    })).ToArray());
                }
            }
            else source.Text(canvas,field.Key,mount,field.Value,font,field.Key is 7 or 9?presets[13]:null);
        }
        SetMeta("source_layout",source.Source.Source.ResourceUri);SetMeta("source_item_dialog",true);
        Resized+=Fit;Fit();
    }
    private void Fit()
    {
        if(canvas is null)return;float scale=Math.Min(Size.X/1920f,Size.Y/1080f);
        canvas.Scale=new(scale,scale);canvas.Position=(Size-new Vector2(1920,1080)*scale)/2;
    }
    public override void _ExitTree()
    {
        if(ownsResources){assets?.Dispose();font?.Dispose();shader?.Dispose();}
        assets=null;font=null;shader=null;
    }
}
