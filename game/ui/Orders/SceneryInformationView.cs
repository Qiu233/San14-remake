using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The ordinary CInfoDetailSceneryDlg. It retains its source list and
/// real current-world detail rather than substituting Collection static values.</summary>
internal partial class SceneryInformationView : Control
{
    private GameResourceRepository repository=null!;
    private PreviewText text=null!;
    private LoadedMessages messages=null!;
    private MapInformationListView list=null!;
    private Control? body;
    private Func<bool>? admitted;
    private Action? close;
    private bool abandoned;
    internal bool MainVisible=>!abandoned&&IsVisibleInTree()&&list is not null&&list.IsVisibleInTree();
    internal bool ControlsReady=>MainVisible&&list.CanProcess()&&body is not null&&body.IsVisibleInTree();
    internal int? SelectedSceneryId {get;private set;}
    internal IReadOnlyDictionary<int, (Node2D Root, Vector2 ContentMount)> MotionGroups => list.MotionGroups;
    internal void RegisterHeader(UiSourceHeaderStack owner) => list.RegisterHeader(owner);

    internal void Configure(GameResourceRepository repository,PreviewText text,SceneryInformationPage page,
        Func<bool> admitted,Action close,AdministrationPageChrome.Resources? resources=null,
        IReadOnlyList<MapInformationListRow>? selectionRows=null,Action<int>? select=null,Func<int>? scrollSpeed=null)
    {
        this.repository=repository;this.text=text;this.admitted=admitted;this.close=close;
        Name="SceneryInformation340";ZIndex=4;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        messages=resources?.Messages??repository.LoadMessages(text.SourceLocale,MessageKind.Data);
        var data=messages.Messages.Sections[1].Records;
        string Msg(int id)=>data[id].Text??throw new InvalidDataException($"Missing Scenery information Data1/{id:X}.");
        list=new(){Name="SceneryInformationList"};AddChild(list);
        list.Configure(repository,text,Msg(SceneryInformationSources.TitleId),
            [new("name",Msg(0xAD),188,false)],
            selectionRows??[new(page.SceneryId,new[]{page.Name},true)],ReadAdmission,RequestBack,select??(_=>{}),scrollSpeed??(()=>0),
            tabCaptions:new[]{Msg(0x3CD)},completeColumnProfile:true,normalColumnLayout:true,narrowTable:true,resources:resources);
        SetMeta("source_registry",0x68);
        Apply(page);
    }

    internal void Apply(SceneryInformationPage page)
    {
        if(abandoned)return;
        SelectedSceneryId=page.SceneryId;
        list.SelectSourceIndex(page.SceneryId);
        if(body is { } previous){previous.Hide();previous.QueueFree();}
        body=new(){Name="CurrentSceneryInformation",Position=new(191,197),Size=new(1540,688),MouseFilter=MouseFilterEnum.Ignore};
        list.SourceBodyCanvas.AddChild(body);
        var source=list.SourceAssets.SourceLayout(SceneryInformationSources.LayoutName);
        var presets=MapTextPresets.Create();
        foreach(var node in source.Nodes)
        {
            if(Hidden(node.Index))continue;
            if(node.Type is 1 or 3)source.Art(body,node.Index,source.Rectangle(node.Index),list.SourceAssets,list.SourceShader);
            else if(node.Type==6)source.Blur(body,node.Index,source.Rectangle(node.Index));
        }
        var data=messages.Messages.Sections[1].Records;
        foreach(var (node,message) in SceneryInformationSources.Captions)
        {
            if(Hidden(node))continue;
            source.Text(body,node,default,data[message].Text??throw new InvalidDataException($"Missing Scenery caption {message:X}."),
                list.SourceFont,presets[13]);
        }
        source.Text(body,32,default,data[0x60C].Text??throw new InvalidDataException("Missing Scenery progress separator."),list.SourceFont,presets[0]);
        foreach(int node in new[]{12,13,16,20,23,30,31,33,40,45,50})
        {
            if(Hidden(node))continue;
            string value=page.Text.GetValueOrDefault(node)??text.Get("detail_unknown");
            var label=source.Text(body,node,default,value,list.SourceFont,presets[0]);
            if(node==12&&page.ForceNameColor is { } color)
            {
                uint packed=color.CpuPacked;
                label.Configure(list.SourceFont,MapFontProfile.FromNode(source.Nodes[node],source.Source.Source.ResourceUri)
                    .WithPreset(presets[0]) with {ColourLT=packed,ColourRT=packed,ColourLB=packed,ColourRB=packed});
            }
        }
        if(page.ImageIndex is int image)
            source.Art(body,5,source.Rectangle(5),list.SourceAssets,list.SourceShader,
                commonSpriteOverride:SceneryInvestmentSources.ImageSprite(image),commonTexturePack:SceneryInvestmentSources.ImagePack(image));
        if(page.ForceColorIndex is int forceColor)
        {
            var fill=MapSpritePalettes.ForceFill(forceColor);
            source.Art(body,11,source.Rectangle(11),list.SourceAssets,list.SourceShader,new(fill,fill,fill,fill));
        }
        if(page.EffectKind is int kind&&SceneryInvestmentSources.EffectSprite(kind) is string icon)
            source.Art(body,38,source.Rectangle(38),list.SourceAssets,list.SourceShader,commonSpriteOverride:icon);
        var descriptionBox=source.Rectangle(56);
        var description=new UiSourceParagraph {Name="SceneryDescription",Position=descriptionBox.Position,Size=descriptionBox.Size};
        body.AddChild(description);
        description.Configure(list.SourceFont,MapFontProfile.FromNode(source.Nodes[56],source.Source.Source.ResourceUri)
            .WithPreset(presets[0]) with {RawAlign=0,RawLineArrange=1,WidthFitMode=0});
        description.Apply(page.Text.GetValueOrDefault(56)??text.Get("detail_unknown"));
        var mini=new UiSourceMiniMap {Position=source.Rectangle(51).Position};body.AddChild(mini);
        mini.Configure(repository,list.SourceAssets,list.SourceShader,page.MiniMap);
        ProgressGauge(source.Rectangle(34));
        SetMeta("source_layout",source.Source.Source.ResourceUri);SetMeta("source_unknown",page.UnresolvedOwner??"");

        bool Hidden(int index)
        {
            for(int? cursor=index;cursor is int current;cursor=source.Nodes[current].ParentIndex)
            {
                if(current is 5 or 11 or 34 or 38 or 51)return true;
                if(current==41&&page.UseAlternateStatePanel!=false)return true;
                if(current==46&&page.UseAlternateStatePanel!=true)return true;
            }
            return false;
        }
        void ProgressGauge(Rect2 box)
        {
            var bar=list.SourceAssets.SourceLayout(0xAFC125D8);
            var gauge=new Control {Name="SceneryDevelopmentProgress",Position=box.Position,Size=bar.Rectangle(0).Size,
                Scale=box.Size/bar.Rectangle(0).Size,MouseFilter=MouseFilterEnum.Ignore};body.AddChild(gauge);
            bar.Art(gauge,1,bar.Rectangle(1),list.SourceAssets,list.SourceShader);
            if(page.ProgressCurrent is not int current||page.ProgressMaximum is not int maximum||maximum<=0)return;
            int width=(int)((long)bar.Nodes[1].Value44*Math.Clamp(current,0,maximum)/maximum);
            if(width<=0)return;
            var fill=new UiSourceLayout(bar.Source,new Dictionary<int,UiSourceNodeGeometry>{[2]=new(Size:new(width,bar.Nodes[2].Value46))});
            fill.Art(gauge,2,fill.Rectangle(2),list.SourceAssets,list.SourceShader,MapSpritePalettes.Create()[1]);
        }
    }
    private bool ReadAdmission()=>!abandoned&&IsVisibleInTree()&&(admitted?.Invoke()??false);
    internal void RequestBack(){if(ReadAdmission())close?.Invoke();}
    internal void Abandon(){if(abandoned)return;abandoned=true;admitted=null;close=null;list?.Abandon();Hide();}
    public override void _ExitTree()=>Abandon();
}
