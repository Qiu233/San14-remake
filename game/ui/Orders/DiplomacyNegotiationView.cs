using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>The reached proposal and explicit player reply. Terms are read-only
/// here; the original condition editor has its own page and state.</summary>
internal partial class DiplomacyNegotiationView : Control
{
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Control canvas = null!;
    private UiSourceHeader? header;
    private UiSourceText notice = null!;
    private readonly List<UiSourceCommandButton> details = [];
    private UiSourceRoundButton accept = null!, decline = null!;
    private Func<bool>? admitted;
    private Action<bool>? submit;
    private bool abandoned, busy, canAccept;

    internal void Configure(GameResourceRepository repository, GameState candidate,
        DiplomacyNegotiationRequest request, int playerForceId, DiplomacyNegotiationPage page,
        PreviewText text, Func<bool> admitted, Action<bool> submit, Action showForce, Action showMap)
    {
        this.admitted = admitted; this.submit = submit; canAccept = page.CanAccept;
        Name = "DiplomacyNegotiation"; ZIndex = 90; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets = new(repository); font = new(repository,text.SourceLocale); shader = UiSourceSprite.CreateShader();
        canvas = new() { Name="SourceCanvas", Size=new(1920,1080), MouseFilter=MouseFilterEnum.Stop };
        AddChild(canvas);
        bool sender = request.ForceId == playerForceId;
        bool goodwill = request.Kind == DiplomaticMissionKind.Goodwill;
        bool alliance = request.Kind == DiplomaticMissionKind.Alliance;
        bool decide = sender && !goodwill;
        var (file,mount,title) = request.Kind switch
        {
            DiplomaticMissionKind.Goodwill => (0xDC8556AEu,new Vector2(630,427),0x05EB),
            DiplomaticMissionKind.Alliance when sender => (0xB704F5E2u,new Vector2(535,401),0x05A7),
            DiplomaticMissionKind.Alliance => (0x11806F84u,new Vector2(630,402),0x05A6),
            DiplomaticMissionKind.RequestAttack when sender => (0x761D93CCu,new Vector2(535,401),0x05C5),
            DiplomaticMissionKind.RequestAttack => (0x11806F84u,new Vector2(630,402),0x05C4),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
        var source = new UiSourceLayout(repository,file);
        int forceButton = !decide ? 13 : alliance ? 21 : 20;
        int mapButton = !decide ? 19 : alliance ? 27 : 26;
        var replaced = new HashSet<int> { forceButton,mapButton };
        if(decide){replaced.Add(8);replaced.Add(10);replaced.Add(11);}
        if(alliance&&!sender)replaced.Add(33);
        foreach(var node in source.Nodes)
        {
            bool hidden=false;
            for(int? at=node.Index;at is int index;at=source.Nodes[index].ParentIndex)
                if(replaced.Contains(index)){hidden=true;break;}
            if(hidden)continue;
            if(node.Type is 1 or 3)source.Art(canvas,node.Index,source.Rectangle(node.Index,mount),assets,shader);
            else if(node.Type==6)source.Blur(canvas,node.Index,source.Rectangle(node.Index,mount));
        }
        header = new() { Name="DiplomacyReplyTitle" }; canvas.AddChild(header);
        header.Configure(repository,assets,shader,font,Message(1,title));
        Label(5,decide?0x0632:0x0631);
        Value(decide?14:7,candidate.Forces[page.DisplayedForceId].Name);
        Label(!decide?8:alliance?16:15,0x0413);
        Value(!decide?12:alliance?20:19,Message(0,0x0141+page.RelationLevel));
        if(alliance)
        {
            Label(decide?37:30,0x0414);
            Value(decide?39:32,Number(0x045F,request.AllianceMonths));
        }
        else if(!goodwill)
        {
            Label(decide?36:30,decide?0x0474:0x0478);
            Label(decide?41:37,0x01FD);
            var city=request.TargetCityId is int id?candidate.Cities.GetValueOrDefault(id):null;
            Value(decide?38:32,city?.Name??text.Get("detail_unknown"));
            Value(decide?43:39,city?.ForceId is int force&&candidate.Forces.TryGetValue(force,out var faction)
                ?faction.Name:text.Get("detail_unknown"));
        }
        Label(goodwill?30:decide?(alliance?44:49):44,0x03CE);
        string condition=page.NamedCondition??(request.InitialOffer.Gold!=0?Number(0x05F8,request.InitialOffer.Gold):
            request.InitialOffer.Supplies!=0?Number(0x059D,request.InitialOffer.Supplies):"");
        Value(goodwill?32:decide?(alliance?46:51):46,condition);
        if(decide&&page.Ruler is { Available:true,Value:{} card })
        {
            var values=new AreaAdministrationOfficerContent(repository).Create(card);
            if(values.Portrait is {} portraitSource)
            {
                var box=source.Rectangle(8,mount);
                var portrait=new UiSourcePortrait { Name="DiplomacyRecipientRuler",Position=box.Position,Size=box.Size };
                canvas.AddChild(portrait);portrait.ConfigureFace2(repository,assets,shader,portraitSource);
            }
            source.Art(canvas,10,source.Rectangle(10,mount),assets,shader);
            if(values.Force is {} appearance)
            {
                var flag=new UiSourceForceFlag { Name="DiplomacyRecipientFlag",Position=source.Rectangle(11,mount).Position };
                canvas.AddChild(flag);flag.Configure(repository,assets,shader,font,appearance);
            }
        }
        else if(decide)source.Art(canvas,10,source.Rectangle(10,mount),assets,shader);
        Command(forceButton,0x05A2,showForce);Command(mapButton,0x05AD,showMap);
        var footer=new UiSourceLayout(repository,0xD8913AB2);
        decline=Reply(4,false);accept=Reply(5,true);
        var hint=new UiSourceLayout(repository,0xAB6ADAA7);
        var hintMount=footer.Rectangle(19,new(0,904)).Position;
        foreach(var node in hint.Nodes.Where(node=>node.Type is 1 or 3))
            hint.Art(canvas,node.Index,hint.Rectangle(node.Index,hintMount),assets,shader);
        notice=hint.Text(canvas,hint.Nodes.Single(node=>node.Type==2).Index,hintMount,"",font);
        SetMeta("source_layout",source.Source.Source.ResourceUri);SetMeta("proposal_task_id",request.TaskId);
        SetBusy(false);Resized+=Fit;Fit();

        string Message(int section,int id)=>repository.LoadMessages(text.SourceLocale,MessageKind.Data)
            .Messages.Sections[section].Records[id].Text??throw new InvalidDataException($"Missing diplomacy reply text {section}:{id:X4}.");
        string Number(int id,int value)=>Message(1,id).Replace("%d",value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        void Value(int index,string value)=>source.Text(canvas,index,mount,value,font);
        void Label(int index,int message)=>Value(index,Message(1,message));
        void Command(int index,int message,Action clicked)
        {
            var box=source.Rectangle(index,mount);
            var button=new UiSourceCommandButton { Position=box.Position,Size=box.Size,InputAdmission=ReadAdmission };
            canvas.AddChild(button);button.Configure(repository,assets,shader,font,Message(1,message),
                ()=>{if(ReadAdmission())clicked();});details.Add(button);
        }
        UiSourceRoundButton Reply(int slot,bool accepted)
        {
            var button=new UiSourceRoundButton { Position=footer.Rectangle(slot,new(0,904)).Position,
                ActionMode=BaseButton.ActionModeEnum.Release,InputAdmission=ReadAdmission };
            canvas.AddChild(button);
            button.ConfigureDiplomaticReply(repository,assets,shader,text.SourceLocale,accepted,
                ()=>{if(ReadAdmission()&&(!accepted||canAccept))this.submit?.Invoke(accepted);});
            return button;
        }
    }

    private bool ReadAdmission()=>!abandoned&&!busy&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    internal void RegisterHeader(UiSourceHeaderStack headers)=>header?.Publish(headers);
    internal void SetNotice(string message){if(!abandoned)notice.Text=message;}
    internal void SetBusy(bool value)
    {
        busy=value;
        if(accept is not null)accept.Disabled=abandoned||busy||!canAccept;
        if(decline is not null)decline.Disabled=abandoned||busy;
        foreach(var detail in details)detail.Disabled=abandoned||busy;
        SetTermsBusy();
    }
    internal void SuspendBody(){SetBusy(true);canvas.Hide();MouseFilter=MouseFilterEnum.Ignore;}
    internal void ResumeBody(){if(abandoned)return;canvas.Show();MouseFilter=MouseFilterEnum.Stop;SetBusy(false);}
    internal void Abandon(){abandoned=true;admitted=null;submit=null;SetBusy(true);Hide();}
    private void Fit()
    {
        float scale=Math.Min(Size.X/1920,Size.Y/1080);canvas.Scale=Vector2.One*scale;
        canvas.Position=(Size-new Vector2(1920,1080)*scale)/2;
    }
    public override void _ExitTree(){admitted=null;submit=null;abandoned=true;font?.Dispose();assets?.Dispose();shader?.Dispose();}
}
