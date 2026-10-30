using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The two investment entry paths share the authored scenery pane.
/// Values and permissions belong to the retained command draft.</summary>
internal partial class SceneryInvestmentView : Control
{
    private GameResourceRepository repository=null!;
    private PreviewText text=null!;
    private AdministrationPageChrome chrome=null!;
    private UiSourceLayout source=null!;
    private UiSourceCommandButton choose=null!;
    private UiSourceRoundButton next=null!;
    private MapInformationListView? selector;
    private UiSourceRoundButton? selectorConfirm;
    private Control dynamicArt=null!;
    private readonly Dictionary<int,UiSourceText> fields=[];
    private readonly List<Control> arrows=[];
    private SceneryInvestmentPage? page;
    private Func<bool>? admitted;
    private Func<bool>? decisionAdmission;
    private Action? close;
    private bool abandoned,busy,canReturn=true;
    private int? selectorSelection;
    private string unknown="",effectFormat="";
    private static readonly Vector2 Mount=new(484,232);
    internal bool MainVisible=>!abandoned&&chrome is not null&&chrome.IsVisibleInTree();
    internal bool ButtonsVisible=>MainVisible&&next is not null&&next.IsVisibleInTree();
    internal bool SelectorVisible=>!abandoned&&selector is not null&&selector.IsVisibleInTree();
    internal bool SelectorControlsReady=>SelectorVisible&&selector!.CanProcess()&&selectorConfirm is not null&&selectorConfirm.IsVisibleInTree();
    internal int? SelectedSceneryId=>SelectorVisible?selectorSelection:page?.SceneryId;

    internal void Configure(GameResourceRepository repository,PreviewText text,SceneryInvestmentPage page,
        Func<bool> current,Action selectScenery,Action<bool> submit,Action close)
    {
        this.repository=repository;this.text=text;admitted=current;this.close=close;
        Name="SceneryInvestment348";ZIndex=4;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);unknown=text.Get("detail_unknown");
        chrome=new();AddChild(chrome);
        chrome.Configure(repository,text,SceneryInvestmentSources.TitleId,RequestBack,
            ()=>{if(ReadMainAdmission()&&DecisionAdmitted()&&this.page?.CanConfirm==true)submit(false);},threeButtonFooter:true);
        var footer=new UiSourceLayout(repository,0xD8913AB2);
        next=new(){Name="SceneryInvestmentContinue350",Position=footer.Rectangle(3,new(0,904)).Position,
            InputAdmission=ReadMainAdmission};chrome.Canvas.AddChild(next);
        next.ConfigureContinue(repository,chrome.Assets,chrome.SpriteShader,text.SourceLocale,
            ()=>{if(ReadMainAdmission()&&DecisionAdmitted()&&this.page?.CanContinue==true)submit(true);});
        source=new(MapMarkerLayout.LoadNamed(repository,SceneryInvestmentSources.LayoutName));
        foreach(var node in source.Nodes)
        {
            if(Hidden(node.Index))continue;
            if(node.Type is 1 or 3)
            {
                var art=source.Art(chrome.Canvas,node.Index,source.Rectangle(node.Index,Mount),chrome.Assets,chrome.SpriteShader);
                if(node.Index is 39 or 46 or 61 or 69)arrows.Add(art);
            }
            else if(node.Type==6)source.Blur(chrome.Canvas,node.Index,source.Rectangle(node.Index,Mount));
        }
        var data=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1].Records;
        var presets=MapTextPresets.Create();
        foreach(var (node,message) in SceneryInvestmentSources.Captions)
            source.Text(chrome.Canvas,node,Mount,Message(message),chrome.Font,presets[13]);
        foreach(int node in new[]{25,30,38,40,48,47,54,55,60,62,63,68,70,71})
            fields[node]=source.Text(chrome.Canvas,node,Mount,"",chrome.Font,presets[node is 40 or 47?15:0]);
        fields[55].Text=Message(0x469);effectFormat=Message(0x862);
        var box=source.Rectangle(10,Mount);
        choose=new(){Name="SelectInvestmentScenery",Position=box.Position,Size=box.Size,InputAdmission=ReadMainAdmission};
        chrome.Canvas.AddChild(choose);choose.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,"",
            ()=>{if(ReadMainAdmission()&&this.page?.CanSelectScenery==true)selectScenery();});
        dynamicArt=new(){Name="SceneryInvestmentDynamicArt",MouseFilter=MouseFilterEnum.Ignore};chrome.Canvas.AddChild(dynamicArt);
        var messages=new AdministrationMessages(repository,text.SourceLocale);
        chrome.ApplyReturnHint(messages.Format(0x2733));chrome.ApplyDefaultHint(messages.Format(0x5C87));
        SetMeta("source_layout",source.Source.Source.ResourceUri);SetMeta("source_registry",0x69);
        Apply(page);

        string Message(int id)=>data[id].Text??throw new InvalidDataException($"Scenery investment Data1/{id:X} text is unavailable.");
        bool Hidden(int index)
        {
            for(int? cursor=index;cursor is int i;cursor=source.Nodes[i].ParentIndex)
                if(i is 10 or 16 or 31 or 45 or 72)return true;
            return false;
        }
    }

    internal void Apply(SceneryInvestmentPage value)
    {
        if(abandoned)return;page=value;
        choose.Caption=value.SceneryName;choose.Disabled=busy||value.CanSelectScenery!=true;
        fields[25].Text=value.CityName;fields[30].Text=value.ProvinceName;
        Number(38,value.CurrentLevel);Number(40,value.NextLevel);Number(54,value.Duration);
        Number(60,value.CityGold);Number(62,value.AfterGold);Number(68,value.Books);Number(70,value.AfterBooks);
        Effect(48,value.CurrentEffect);Effect(47,value.NextEffect);Delta(63,value.GoldDelta);Delta(71,value.BookDelta);
        foreach(var arrow in arrows)arrow.Visible=value.SceneryId is not null;
        RefreshDecisions();
        foreach(Node old in dynamicArt.GetChildren()){dynamicArt.RemoveChild(old);old.QueueFree();}
        if(value.SceneryIllustrationIndex is int image)
            source.Art(dynamicArt,31,source.Rectangle(31,Mount),chrome.Assets,chrome.SpriteShader,
                commonSpriteOverride:SceneryInvestmentSources.ImageSprite(image),commonTexturePack:SceneryInvestmentSources.ImagePack(image));
        if(value.EffectKind is int kind&&SceneryInvestmentSources.EffectSprite(kind) is string icon)
            source.Art(dynamicArt,45,source.Rectangle(45,Mount),chrome.Assets,chrome.SpriteShader,commonSpriteOverride:icon);
        var mini=new UiSourceMiniMap {Position=source.Rectangle(72,Mount).Position};dynamicArt.AddChild(mini);
        mini.Configure(repository,chrome.Assets,chrome.SpriteShader,value.MiniMap);
        SetMeta("source_unknown",value.UnresolvedOwner??"");

        void Number(int node,int? number)=>fields[node].Text=number?.ToString(CultureInfo.InvariantCulture)??unknown;
        void Effect(int node,int? number)=>fields[node].Text=number is int n?
            effectFormat.Replace("%d",n.ToString(CultureInfo.InvariantCulture),StringComparison.Ordinal)
                .Replace("%%","%",StringComparison.Ordinal):unknown;
        void Delta(int node,int? delta)
        {
            if(delta is not int amount){fields[node].Text=unknown;return;}
            fields[node].SetKnownColourLine("(^03-"+amount.ToString(CultureInfo.InvariantCulture)+"^00)");
        }
    }

    internal void ShowScenerySelector(IReadOnlyList<SceneryInvestmentSiteRow> candidates,int? selected,Action<int> accept)
    {
        if(!ReadMainAdmission())return;
        selectorSelection=selected is int selectedId&&candidates.Any(row=>row.SceneryId==selectedId&&row.CanSelect==true)?selectedId:null;
        var data=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1].Records;
        string Msg(int captionId)=>data[captionId].Text??throw new InvalidDataException($"Missing scenery selector caption {captionId:X}.");
        var columns=SceneryInvestmentSources.Columns.Select(c=>new MapInformationListColumn(c.Id.ToString(CultureInfo.InvariantCulture),Msg(c.CaptionId),c.Width,false)).ToArray();
        var rows=candidates.Select(row=>new MapInformationListRow(row.SceneryId,
            row.Cells??new[]{row.Name,unknown,unknown,unknown,row.Level.ToString(CultureInfo.InvariantCulture),unknown,unknown},row.CanSelect==true)).ToArray();
        selector=new(){Name="ScenerySelector340",ZIndex=2};AddChild(selector);
        selector.Configure(repository,text,Msg(SceneryInvestmentSources.SelectionTitleId),columns,rows,ReadAdmission,
            RequestBack,id=>{selectorSelection=id;if(selectorConfirm is not null)selectorConfirm.Disabled=false;},()=>0,
            tabCaptions:new[]{Msg(0x3CD)},completeColumnProfile:true,normalColumnLayout:true);
        if(selectorSelection is int initial)selector.SelectSourceIndex(initial);
        var footer=new UiSourceLayout(repository,0xD8913AB2);
        selectorConfirm=new(){Name="AcceptScenerySelection",Position=footer.Rectangle(2,new(0,904)).Position,
            Disabled=selectorSelection is null,InputAdmission=ReadAdmission};selector.SourceCanvas.AddChild(selectorConfirm);
        selectorConfirm.Configure(repository,selector.SourceAssets,selector.SourceShader,text.SourceLocale,"000_cmn_txt_decision_kettei",()=>{
            if(!ReadAdmission()||selectorSelection is not int picked)return;
            CloseSelector();accept(picked);
        });
        selector.SetMeta("source_registry",0x68);
    }
    private void CloseSelector(){var old=selector;selector=null;selectorConfirm=null;selectorSelection=null;old?.Abandon();old?.QueueFree();}
    private bool ReadAdmission()=>!abandoned&&!busy&&IsVisibleInTree()&&(admitted?.Invoke()??false);
    private bool ReadMainAdmission()=>ReadAdmission()&&!SelectorVisible;
    private bool DecisionAdmitted()=>decisionAdmission?.Invoke()??true;
    private void RefreshDecisions()
    {
        if(abandoned||page is null||chrome is null)return;
        bool allowed=DecisionAdmitted();
        chrome.ApplyDecision(!busy&&allowed&&page.CanConfirm==true,true);
        chrome.ApplyReturn(!busy&&canReturn);
        next.Disabled=busy||!allowed||page.CanContinue!=true;
    }
    internal void ApplyInteractionAdmission(Func<bool>? decisionAllowed,bool canReturn)
    {decisionAdmission=decisionAllowed;this.canReturn=canReturn;RefreshDecisions();}
    internal void SetBusy(bool value){busy=value;if(page is { } current)Apply(current);}
    internal void RequestBack(){if(!ReadAdmission()||!canReturn)return;if(SelectorVisible)CloseSelector();else close?.Invoke();}
    internal void Abandon(){if(abandoned)return;abandoned=true;admitted=null;decisionAdmission=null;close=null;CloseSelector();choose?.Abandon();chrome?.Abandon();Hide();}
    public override void _ExitTree()=>Abandon();
}
