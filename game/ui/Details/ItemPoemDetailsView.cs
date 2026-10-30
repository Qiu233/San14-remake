using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The ordinary Item's static poem page. Its caller owns the current
/// Item and return stack; this page has no appreciation-media action.</summary>
public partial class ItemPoemDetailsView : DetailsView
{
    private UiAssets assets=null!;
    private MapFont font=null!;
    private Shader shader=null!;
    private Control canvas=null!;
    internal void Configure(GameResourceRepository repository,PreviewText text,CollectionPoemDetail page,
        Action close,Func<int> scrollSpeed)
    {
        assets=new(repository);font=new(repository,text.SourceLocale);shader=UiSourceSprite.CreateShader();
        ConfigureCommonBackdrop(repository);
        var header=new UiSourceHeader {Name="ItemPoemTitle"};
        ConfigureSourcePage("ItemPoemDetails",header,close);
        canvas=new() {Name="ItemPoemSourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Stop};AddChild(canvas);
        var shell=new UiSourceLayout(repository,0xA3B4A82E);
        shell.Blur(canvas,2,shell.Rectangle(2,new(191,147)));
        shell.Art(canvas,3,shell.Rectangle(3,new(191,147)),assets,shader);
        canvas.AddChild(header);header.Configure(repository,assets,shader,font,Message(0x841));
        string basic=Message(ItemInformationSources.BasicTabCaptionId);
        var tab=new UiSourceTabButton {Name="BasicTab",Position=shell.Rectangle(4,new(191,147)).Position,
            Size=new(basic.Length*28+36,64),Selected=true};
        canvas.AddChild(tab);tab.Configure(repository,assets,shader,font,basic);
        var selection=new UiSourceDetailSelector();canvas.AddChild(selection);
        selection.Configure(repository,assets,shader,font,text,
            new([new(page.ItemId,page.Name,null){SortUnresolvedOwner="information.Item.poem_header_comparator"}],scrollSpeed),
            InformationListKind.Items,page.ItemId,Admitted,_=>{},sourceColumnWidth:160,sourceCaptionId:0xAE);
        var source=new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,"02_07_06_info_collection_sibun_cn"));
        var body=new Control {Name="ItemPoemBody",Position=new(191,197),Size=source.Rectangle(0).Size,MouseFilter=MouseFilterEnum.Ignore};
        canvas.AddChild(body);
        foreach(var node in source.Nodes)
        {
            if(node.Index==2)continue;
            if(node.Type is 1 or 3)source.Art(body,node.Index,source.Rectangle(node.Index),assets,shader);
            else if(node.Type==6)source.Blur(body,node.Index,source.Rectangle(node.Index));
        }
        source.Art(body,2,source.Rectangle(2),assets,shader,commonTexturePack:page.Image.TexturePack,
            commonSpriteOverride:page.Image.SpriteName).Name="ItemPoemImage";
        var presets=MapTextPresets.Create();
        foreach(var (node,value) in new (int,string)[]{(9,page.Name),(11,page.Author),(12,page.Era),(16,Message(0x400))})
        {
            var label=source.Text(body,node,default,value,font,presets[node==16?13:0]);label.Name="ItemPoemField"+node;
        }
        var box=source.Rectangle(17);
        var paragraph=new UiSourceParagraph {Name="ItemPoemDescription",Position=box.Position,Size=box.Size};body.AddChild(paragraph);
        paragraph.Configure(font,MapFontProfile.FromNode(source.Nodes[17],source.Source.Source.ResourceUri)
            .WithPreset(presets[0]) with {RawAlign=0,RawLineArrange=1});paragraph.Apply(page.Description);
        var footer=new UiSourceLayout(repository,0xD8913AB2);
        var back=new UiSourceRoundButton {Name="CloseItemPoemDetails",Position=footer.Rectangle(1,new(0,904)).Position,InputAdmission=Admitted};
        canvas.AddChild(back);back.ConfigureScenarioReturn(repository,assets,shader,text.SourceLocale,RequestClose);
        SetMeta("item_id",page.ItemId);SetMeta("source_layout",source.Source.Source.ResourceUri);
        Resized+=Fit;Fit();
        string Message(int id)=>repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1].Records[id].Text
            ??throw new InvalidDataException($"Missing Item poem Data1/{id:X}.");
    }
    private bool Admitted()=>IsVisibleInTree()&&CanProcess()&&!IsQueuedForDeletion();
    private void Fit(){float scale=Math.Min(Size.X/1920,Size.Y/1080);canvas.Scale=Vector2.One*scale;canvas.Position=(Size-new Vector2(1920,1080)*scale)/2;}
    public override void _ExitTree(){font?.Dispose();shader?.Dispose();assets?.Dispose();base._ExitTree();}
}
