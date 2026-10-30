using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The ordinary campaign Item page, retaining its actual caller's
/// selection while replacing the complete current-world body.</summary>
public partial class ItemDetailsView : DetailsView
{
    private GameResourceRepository repository=null!;
    private PreviewText text=null!;
    private UiAssets assets=null!;
    private MapFont font=null!;
    private Shader shader=null!;
    private Control canvas=null!;
    private Control? currentBody;
    private UiSourceDetailSelector selector=null!;
    private UiSourceCommandButton? poemButton;
    private ItemInformationPage currentPage=null!;
    internal int CurrentItemId=>currentPage.ItemId;
    internal InformationDetailSelectionReturn ReadSelectionReturn()=>selector.ReadReturn(CurrentItemId);

    internal void Configure(GameResourceRepository repository,PreviewText text,ItemInformationPage page,
        InformationDetailSelectionContext selection,Action close,Action<int> select,Action openPoem)
    {
        this.repository=repository;this.text=text;
        assets=new(repository);font=new(repository,text.SourceLocale);shader=UiSourceSprite.CreateShader();
        ConfigureCommonBackdrop(repository);
        var header=new UiSourceHeader {Name="ItemDetailsTitle"};
        ConfigureSourcePage("ItemDetails",header,close);
        canvas=new() {Name="ItemSourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Stop};AddChild(canvas);
        var shell=new UiSourceLayout(repository,0xA3B4A82E);
        shell.Blur(canvas,2,shell.Rectangle(2,new(191,147)));
        shell.Art(canvas,3,shell.Rectangle(3,new(191,147)),assets,shader);
        canvas.AddChild(header);header.Configure(repository,assets,shader,font,Message(ItemInformationSources.TitleId));
        string basic=Message(ItemInformationSources.BasicTabCaptionId);
        var tab=new UiSourceTabButton {Name="BasicTab",Position=shell.Rectangle(4,new(191,147)).Position,
            Size=new(basic.Length*28+36,64),Selected=true};
        canvas.AddChild(tab);tab.Configure(repository,assets,shader,font,basic);
        selector=new();canvas.AddChild(selector);
        selector.Configure(repository,assets,shader,font,text,selection,InformationListKind.Items,page.ItemId,Admitted,select);
        Apply(page);
        var footer=new UiSourceLayout(repository,0xD8913AB2);
        var back=new UiSourceRoundButton {Name="CloseItemDetails",Position=footer.Rectangle(1,new(0,904)).Position,InputAdmission=Admitted};
        canvas.AddChild(back);back.ConfigureScenarioReturn(repository,assets,shader,text.SourceLocale,RequestClose);
        string caption=Message(ItemInformationSources.PoemCaptionId);
        poemButton=new() {Name="ItemPoem",Position=footer.Rectangle(21,new(0,904)).Position+new Vector2(45,-103),
            Size=new(Math.Max(96,caption.Length*28+20),48),InputAdmission=Admitted,Visible=page.CanAppreciatePoem==true};
        canvas.AddChild(poemButton);poemButton.Configure(repository,assets,shader,font,caption,
            ()=>{if(Admitted()&&currentPage.CanAppreciatePoem==true)openPoem();});
        Resized+=Fit;Fit();
    }
    internal void Apply(ItemInformationPage page)
    {
        currentPage=page;
        int previousIndex=currentBody?.GetIndex()??-1;
        if(currentBody is { } old){canvas.RemoveChild(old);old.QueueFree();}
        var source=new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,page.LayoutName));
        var body=new Control {Name="CurrentItemDetails",Position=new(191,197),Size=source.Rectangle(0).Size,MouseFilter=MouseFilterEnum.Ignore};
        canvas.AddChild(body);currentBody=body;
        if(previousIndex>=0)canvas.MoveChild(body,previousIndex);
        foreach(var node in source.Nodes)
        {
            if(Replaced(node.Index))continue;
            if(node.Type is 1 or 3)source.Art(body,node.Index,source.Rectangle(node.Index),assets,shader);
            else if(node.Type==6)source.Blur(body,node.Index,source.Rectangle(node.Index));
        }
        var presets=MapTextPresets.Create();
        var values=new Dictionary<int,string>(page.Text);
        foreach(var (node,_) in page.UnresolvedFields)
            if((uint)node<(uint)source.Nodes.Count&&source.Nodes[node].Type==2)values.TryAdd(node,text.Get("detail_unknown"));
        foreach(var (node,value) in values)
        {
            if(node==47)
            {
                var box=source.Rectangle(node);
                var paragraph=new UiSourceParagraph {Name="ItemDescription",Position=box.Position,Size=box.Size};body.AddChild(paragraph);
                paragraph.Configure(font,MapFontProfile.FromNode(source.Nodes[node],source.Source.Source.ResourceUri)
                    .WithPreset(presets[0]) with {RawAlign=0,RawLineArrange=1});paragraph.Apply(value);
                continue;
            }
            var label=source.Text(body,node,default,value,font,presets[ItemInformationSources.Captions.Any(c=>c.Node==node)?13:0]);
            label.Name="ItemField"+node;
            if(page.UnresolvedFields.TryGetValue(node,out var missing))label.SetMeta("unresolved_owner",missing);
        }
        Image(source,body,page.ImageIndex);
        if(page.ForceFlag is { } flag)
        {
            var control=new UiSourceForceFlag {Name="ItemForceFlag",Position=source.Rectangle(7).Position};body.AddChild(control);
            control.Configure(repository,assets,shader,font,flag);
        }
        if(page.HolderPortrait is {Kind:OfficerPortraitRouteKind.StockNamed,ResourceName:{ } pack,SpriteName:{ } sprite})
        {
            var box=source.Rectangle(50);
            var portrait=new UiSourcePortrait {Name="ItemHolderPortrait",Position=box.Position,Size=box.Size};body.AddChild(portrait);
            portrait.ConfigureFace2(repository,assets,shader,new LocalizedUiSpriteSource(repository).LoadTexturePackSprite(pack,sprite));
        }
        if(poemButton is not null)poemButton.Visible=page.CanAppreciatePoem==true;
        selector.Select(page.ItemId);
        SetMeta("item_id",page.ItemId);SetMeta("source_layout",source.Source.Source.ResourceUri);

        bool Replaced(int index)
        {
            for(int? cursor=index;cursor is int current;cursor=source.Nodes[current].ParentIndex)
                if(current is 2 or 7 or 50)return true;
            return false;
        }
    }
    private void Image(UiSourceLayout source,Control parent,int? index)
    {
        if(index is null or 0)return;
        if(index is >=201)
        {
            var external=index<=401?ItemEditorExternalImages.ReadImage(index.Value):null;
            if(external?.Texture is { } rgba)
            {
                using var bitmap=Godot.Image.CreateFromData(rgba.Width,rgba.Height,false,Godot.Image.Format.Rgba8,rgba.Pixels);
                parent.AddChild(new TextureRect {Name="ItemImage",Position=source.Rectangle(2).Position,Size=source.Rectangle(2).Size,
                    Texture=ImageTexture.CreateFromImage(bitmap),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore});
                return;
            }
            var empty=CollectionImageRoute.ForItem(999);
            var placeholder=source.Art(parent,2,source.Rectangle(2),assets,shader,
                commonTexturePack:empty.TexturePack,commonSpriteOverride:empty.SpriteName);
            placeholder.Name="ItemImage";placeholder.SetMeta("unresolved_owner",external?.Error??"information.Item.external_image_slot");
            return;
        }
        var route=CollectionImageRoute.ForItem(index.Value);
        var image=source.Art(parent,2,source.Rectangle(2),assets,shader,
            commonTexturePack:route.TexturePack,commonSpriteOverride:route.SpriteName);image.Name="ItemImage";
    }
    private bool Admitted()=>IsVisibleInTree()&&CanProcess()&&!IsQueuedForDeletion();
    private string Message(int id)=>repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1].Records[id].Text
        ??throw new InvalidDataException($"Missing Item detail Data1/{id:X}.");
    private void Fit(){float scale=Math.Min(Size.X/1920,Size.Y/1080);canvas.Scale=Vector2.One*scale;canvas.Position=(Size-new Vector2(1920,1080)*scale)/2;}
    public override void _ExitTree(){font?.Dispose();shader?.Dispose();assets?.Dispose();base._ExitTree();}
}
