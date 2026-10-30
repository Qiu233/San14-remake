using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The common seven-category CEditFaceDialog. A cell release returns
/// one actual appearance; only the parent owns its draft and durable library.</summary>
internal partial class OfficerPortraitSelectionView : Control
{
    private GameResourceRepository repository=null!;
    private OfficerEditorPortraitCatalog catalog=null!;
    private PreviewText text=null!;
    private UiAssets assets=null!;
    private MapFont font=null!;
    private Shader shader=null!;
    private Control canvas=null!,viewport=null!,scrollHost=null!;
    private UiSourceScrollbar scroll=null!;
    private UiSourceRoundButton back=null!;
    private UiSourceText hint=null!;
    private Func<bool>? admitted;
    private Action<int?>? closed;
    private Func<int> scrollSpeed=null!;
    private readonly List<UiSourceTabButton> tabs=[];
    private readonly List<OfficerPortraitSelectionCell> cells=[];
    private readonly Dictionary<int,int> scrollPositions=[];
    private readonly Dictionary<int,LoadedSprite> images=[];
    private HistoricalOfficerPortraitCategory category=null!;
    private int initialAppearance;
    private int selectedChoiceIndex=-1;
    private bool abandoned;
    internal int CurrentCategoryId=>category.Id;
    internal int ScrollOffset=>scroll.Value;
    internal IReadOnlyList<OfficerPortraitSelectionCell> Cells=>cells;

    internal void Configure(GameResourceRepository repository,PreviewText text,OfficerEditorPortraitCatalog catalog,
        int currentAppearance,Func<bool> admitted,Action<int?> closed,Func<int> scrollSpeed)
    {
        this.repository=repository;this.text=text;this.catalog=catalog;this.admitted=admitted;
        this.closed=closed;this.scrollSpeed=scrollSpeed;initialAppearance=currentAppearance;
        Name="OfficerPortraitSelection";ZIndex=8;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets=new(repository);font=new(repository,text.SourceLocale);shader=UiSourceSprite.CreateShader();
        canvas=new(){Name="PortraitSourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Stop};AddChild(canvas);
        var source=new UiSourceLayout(repository,0x7F88B1E3);
        int[] tabNodes=[4,6,8,10,12,65,14];
        var replaced=new HashSet<int>(tabNodes.Concat(new[]{16,19,22,29,31,33,35,37,39,41,43,45,47,49,51,53,55,57}));
        foreach(var node in source.Nodes)
        {
            bool skip=false;
            for(int? cursor=node.Index;cursor is int at;cursor=source.Nodes[at].ParentIndex)
                if(replaced.Contains(at)){skip=true;break;}
            if(skip)continue;
            if(node.Type is 1 or 3)source.Art(canvas,node.Index,source.Rectangle(node.Index),assets,shader);
            else if(node.Type==6)source.Blur(canvas,node.Index,source.Rectangle(node.Index));
        }
        for(int i=0;i<7;i++)
        {
            int id=i;var box=source.Rectangle(tabNodes[i]);
            var tab=new UiSourceTabButton{Name="PortraitCategory"+i,Position=box.Position,Size=box.Size,InputAdmission=CanUse};
            canvas.AddChild(tab);tabs.Add(tab);
            tab.Configure(repository,assets,shader,font,catalog.Categories.Single(row=>row.Id==i).Caption,
                i==0?0x172EDC24u:i==6?0xD45873AAu:0x9AFB27B7u);
            tab.Pressed+=()=>{if(CanUse())SelectCategory(id);};
            tab.Visible=i!=5||catalog.BonusCategoryVisible;
        }
        var first=source.Rectangle(22);var last=source.Rectangle(55);
        viewport=new(){Name="PortraitGridViewport",Position=first.Position,Size=last.End-first.Position,
            ClipContents=true,MouseFilter=MouseFilterEnum.Pass};canvas.AddChild(viewport);
        viewport.GuiInput+=Wheel;
        scrollHost=new(){Name="PortraitScrollbarHost",Position=source.Rectangle(57).Position,Size=source.Rectangle(57).Size,
            MouseFilter=MouseFilterEnum.Pass};canvas.AddChild(scrollHost);
        for(int i=0;i<20;i++)
        {
            var cell=new OfficerPortraitSelectionCell{Name="PortraitCell"+i,InputAdmission=CanUse};
            viewport.AddChild(cell);cells.Add(cell);cell.Configure(repository,assets,shader,font);
            cell.GuiInput+=Wheel;
            cell.Pressed+=()=>{if(CanUse()&&cell.AppearanceId is int value)Finish(value);};
        }
        var footer=new UiSourceLayout(repository,0xD8913AB2);
        back=new(){Name="ReturnPortraitSelection",Position=footer.Rectangle(1,new(0,904)).Position,
            InputAdmission=CanUse,ActionMode=BaseButton.ActionModeEnum.Release};canvas.AddChild(back);
        back.ConfigureScenarioReturn(repository,assets,shader,text.SourceLocale,RequestCancel);
        var hintSource=new UiSourceLayout(repository,0xAB6ADAA7);var hintMount=footer.Rectangle(19,new(0,904)).Position;
        foreach(var node in hintSource.Nodes.Where(node=>node.Type is 1 or 3))
            hintSource.Art(canvas,node.Index,hintSource.Rectangle(node.Index,hintMount),assets,shader);
        hint=hintSource.Text(canvas,hintSource.Nodes.Single(node=>node.Type==2).Index,hintMount,"",font);
        category=catalog.Categories.FirstOrDefault(row=>row.Choices.Any(choice=>choice.Id==currentAppearance))??catalog.Categories[0];
        SelectCategory(category.Id,remember:false);
        SetMeta("source_layout",source.Source.Source.ResourceUri);Resized+=Fit;Fit();
    }

    private bool CanUse()=>!abandoned&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    private void SelectCategory(int id,bool remember=true)
    {
        if(remember&&scroll is not null&&category.Id==id)return;
        if(remember&&scroll is not null)scrollPositions[category.Id]=scroll.Value;
        category=catalog.Categories.Single(row=>row.Id==id);
        foreach(var tab in tabs)tab.Selected=tab.Name=="PortraitCategory"+id;
        foreach(var child in scrollHost.GetChildren()){scrollHost.RemoveChild(child);child.QueueFree();}
        scroll=new(){Name="PortraitScrollbar",Size=scrollHost.Size};scrollHost.AddChild(scroll);
        int range=Math.Max(0,((category.Choices.Count+4)/5)*210-(int)viewport.Size.Y);
        scroll.Configure(repository,assets,shader,range,scrollSpeed(),_=>PublishCells(),currentScrollSpeed:scrollSpeed);
        int position=scrollPositions.GetValueOrDefault(id);selectedChoiceIndex=-1;
        for(int i=0;i<category.Choices.Count;i++)if(category.Choices[i].Id==initialAppearance){selectedChoiceIndex=i;break;}
        if(selectedChoiceIndex>=0)
        {
            int top=selectedChoiceIndex/5*210;
            if(top<position)position=top;
            else if(top+210>position+viewport.Size.Y)position=top+210-(int)viewport.Size.Y;
        }
        scroll.SetValue(position);
        hint.Text=category.UnresolvedOwners.Count==0?"":text.Get("detail_unknown");
        SetMeta("category_id",id);SetMeta("category_unknown",string.Join(";",category.UnresolvedOwners));
        PublishCells();
    }
    private void PublishCells()
    {
        int firstRow=scroll.Value/210,offset=scroll.Value%210;
        for(int i=0;i<cells.Count;i++)
        {
            int index=(firstRow+i/5)*5+i%5;var cell=cells[i];
            cell.Position=new(i%5*186,i/5*210-offset);
            if(index>=category.Choices.Count){cell.Apply(null,null,false,null);continue;}
            var choice=category.Choices[index];string? missing=null;LoadedSprite? image=null;
            if(!images.TryGetValue(choice.Id,out image))
            {
                var query=catalog.ResolveFace(choice.Id);missing=query.UnresolvedInput;
                if(query.Available&&query.Value is{} names)
                {
                    image=new LocalizedUiSpriteSource(repository).LoadTexturePackSprite(names.ResourceName,names.SpriteName);
                    images.Add(choice.Id,image);
                }
            }
            cell.Apply(choice,image,index==selectedChoiceIndex,missing);
        }
    }
    private void Wheel(InputEvent input){if(CanUse())scroll.HandleWheel(input);}
    internal void RequestCancel(){if(CanUse())Finish(null);}
    private void Finish(int? value)
    {
        if(value is int appearance)catalog.ValidateChoice(appearance);
        var callback=closed;Abandon();callback?.Invoke(value);
    }
    internal void Abandon()
    {
        abandoned=true;admitted=null;closed=null;back.Disabled=true;
        foreach(var tab in tabs)tab.Disabled=true;foreach(var cell in cells)cell.Disabled=true;Hide();
    }
    private void Fit(){float scale=Math.Min(Size.X/1920,Size.Y/1080);canvas.Scale=Vector2.One*scale;canvas.Position=(Size-canvas.Size*scale)/2;}
    public override void _ExitTree(){abandoned=true;closed=null;assets?.Dispose();font?.Dispose();shader?.Dispose();}
}

internal partial class OfficerPortraitSelectionCell : UiSourceButton
{
    private GameResourceRepository repository=null!;
    private UiAssets assets=null!;
    private Shader shader=null!;
    private Control portrait=null!;
    private UiSourceText label=null!;
    private LoadedSprite? currentImage;
    private UiSourceLayout source=null!;
    internal int? AppearanceId {get;private set;}
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font)
    {
        this.repository=repository;this.assets=assets;this.shader=shader;source=new(repository,0xE0E066F8);
        Size=source.Rectangle(0).Size;
        source.Art(this,1,source.Rectangle(1),assets,shader);
        portrait=new(){Name="Portrait",MouseFilter=MouseFilterEnum.Ignore};AddChild(portrait);
        source.Art(this,6,source.Rectangle(6),assets,shader);
        label=source.Text(this,7,default,"",font);
        var states=new Control[5];
        for(int i=0;i<states.Length;i++)
        {
            var state=new Control{MouseFilter=MouseFilterEnum.Ignore};AddChild(state);states[i]=state;
            foreach(int art in i==1?new[]{9,10}:i==2?new[]{12,13}:Array.Empty<int>())
                source.Art(state,art,source.Rectangle(art),assets,shader);
        }
        ConfigureStates(states);ActionMode=ActionModeEnum.Release;
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
    internal void Apply(RegisteredOfficerEditorChoice? choice,LoadedSprite? image,bool selected,string? missing)
    {
        AppearanceId=choice?.Id;Visible=choice is not null;Disabled=choice is null||image is null;Selected=selected;
        label.Text=choice?.Name??"";SetMeta("appearance_id",choice?.Id??-1);SetMeta("source_unknown",missing??"");
        if(ReferenceEquals(currentImage,image))return;
        currentImage=image;
        foreach(var child in portrait.GetChildren()){portrait.RemoveChild(child);child.QueueFree();}
        if(image is null)return;
        var node=source.Nodes[3];var box=source.Rectangle(3);
        var face=new UiSourcePortrait{Name="Face04",Position=box.Position,Size=new(node.Value44,node.Value46),
            Scale=new(node.HeaderValues[7],node.HeaderValues[8])};portrait.AddChild(face);
        face.Configure(repository,assets,shader,image);
    }
}
