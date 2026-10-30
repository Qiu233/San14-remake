using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

internal partial class DiplomacyOrdersView
{
    private UiSourceCommandButton? captiveButton,requestedReset;
    private UiSourceText? ransom,goldBefore,goldAfter,goldCost,requestedDescription;
    private readonly List<UiSourceRadioButton> requestedRadios=[];
    private readonly List<(Control Root,UiSourceText Name,Control Portrait,Rect2 Rectangle,AreaAdministrationTraitMarker Trait)> captiveSlots=[];
    private IReadOnlyList<LoadedSprite?> displayedCaptivePortraits=[];
    private UiSourceLayout? additionalSource;
    private GameResourceRepository? additionalRepository;
    private DiplomaticMissionKind? additionalKind;
    private void ConfigureAdditional(GameResourceRepository repository,PreviewText text,DiplomaticMissionKind kind,
        Func<bool> admitted,Action returned,Action accepted,Action chooseForce,Action chooseExecutor,
        Action<int> chooseGift,Action resetGift,Action? chooseCaptives,Action<int>? chooseRequested,Action? resetRequested)
    {
        this.text=text;this.admitted=admitted;this.returned=returned;this.accepted=accepted;
        additionalRepository=repository;additionalKind=kind;
        bool captives=kind==DiplomaticMissionKind.ReturnCaptives,exchange=kind==DiplomaticMissionKind.Exchange;
        var (file,title,mount)=kind switch
        {
            DiplomaticMissionKind.ReturnCaptives=>(0x91C29FEDu,0x028B,new Vector2(460,206)),
            DiplomaticMissionKind.DemandSurrender=>(0xE76B9790u,0x028A,new Vector2(434,251)),
            DiplomaticMissionKind.Exchange=>(0x97ECC119u,0x070A,new Vector2(460,137)),
            _=>throw new ArgumentOutOfRangeException(nameof(kind))
        };
        Name="Diplomacy"+kind;ZIndex=3;MouseFilter=MouseFilterEnum.Stop;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop=new UiSourceCommonBlur();AddChild(backdrop);backdrop.Configure(repository);
        chrome=new();AddChild(chrome);chrome.Configure(repository,text,title,RequestBack,
            ()=>{if(ReadAdmission()&&page?.CanSubmit==true)this.accepted?.Invoke();});
        var source=new UiSourceLayout(repository,file);additionalSource=source;
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        string Message(int id)=>messages.Records[id].Text??throw new InvalidDataException($"Missing diplomacy text {id:X4}.");
        int card=captives?58:25,face=captives?153:exchange?176:111;
        var replaced=new HashSet<int>{6,card,face};
        if(captives)foreach(int node in new[]{25,34,35,37,44,45,47,48,49,51})replaced.Add(node);
        if(exchange)foreach(int node in new[]{102,106,110,114,118,132,136,140,144,148})replaced.Add(node);
        foreach(var node in source.Nodes)
        {
            bool skip=false;
            for(int? cursor=node.Index;cursor is int at;cursor=source.Nodes[at].ParentIndex)
                if(replaced.Contains(at)){skip=true;break;}
            if(skip)continue;
            if(node.Type is 1 or 3)source.Art(chrome.Canvas,node.Index,source.Rectangle(node.Index,mount),chrome.Assets,chrome.SpriteShader);
            else if(node.Type==6)source.Blur(chrome.Canvas,node.Index,source.Rectangle(node.Index,mount));
        }
        force=Button(6,Message(0x632),chooseForce);Label(5,0x632);Label(14,0x413);relation=Value(18);
        executor=new(){Name="DiplomacyExecutor",Position=source.Rectangle(card,mount).Position};chrome.Canvas.AddChild(executor);
        executor.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,text,
            ()=>{if(ReadAdmission())chooseExecutor();},chrome.Canvas);
        Label(captives?56:23,0x541);
        int day=captives?147:exchange?161:96,book=captives?138:exchange?168:103;
        Label(day-2,0x26C);duration=Value(day);Value(day+1).Text=Message(0x469);
        Label(book-2,0x292);books=Value(book);remaining=Value(book+2);cost=Value(book+3);
        advice=new(){Name="DiplomacyAdvice"};chrome.Canvas.AddChild(advice);
        advice.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,source,face,mount);
        if(captives)
        {
            Label(23,0x633);Label(31,0x20C);Label(127,0x20C);
            captiveButton=Button(25,Message(0x58E),()=>chooseCaptives?.Invoke());captiveButton.Name="ChooseDiplomacyCaptives";
            ransom=Value(32);goldBefore=Value(129);goldAfter=Value(131);goldCost=Value(132);
            int[] faces=[34,44,48],frames=[35,45,49],markers=[37,47,51],names=[36,46,50];
            for(int i=0;i<3;i++)
            {
                var root=new Control{Name="DiplomacyCaptiveSlot"+i,Size=new(1920,1080),MouseFilter=MouseFilterEnum.Ignore};
                chrome.Canvas.AddChild(root);source.Art(root,frames[i],source.Rectangle(frames[i],mount),chrome.Assets,chrome.SpriteShader);
                var name=source.Text(root,names[i],mount,"",chrome.Font);
                var markerBox=source.Rectangle(markers[i],mount);
                var trait=new AreaAdministrationTraitMarker{Position=markerBox.Position};root.AddChild(trait);
                trait.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,size:markerBox.Size);trait.Hide();
                var portrait=new Control{Name="CaptivePortrait",MouseFilter=MouseFilterEnum.Ignore};root.AddChild(portrait);
                captiveSlots.Add((root,name,portrait,source.Rectangle(faces[i],mount),trait));
            }
        }
        if(exchange)
        {
            Label(99,0x710);Label(129,0x711);gift=Value(101);requestedDescription=Value(131);
            Radios([102,106,110,114],gifts,chooseGift);
            Radios([132,136,140,144],requestedRadios,index=>chooseRequested?.Invoke(index));
            amount=Button(118,Message(0x29F),resetGift);
            requestedReset=Button(148,Message(0x29F),()=>resetRequested?.Invoke());requestedReset.Name="ResetDiplomacyRequestedOffer";
        }
        SetMeta("source_layout",source.Source.Source.ResourceUri);SetBusy(false);

        UiSourceText Value(int index)=>source.Text(chrome.Canvas,index,mount,"",chrome.Font);
        void Label(int index,int message)=>source.Text(chrome.Canvas,index,mount,Message(message),chrome.Font);
        UiSourceCommandButton Button(int index,string caption,Action action)
        {
            var box=source.Rectangle(index,mount);
            var button=new UiSourceCommandButton{Position=box.Position,Size=box.Size,InputAdmission=ReadAdmission};
            chrome.Canvas.AddChild(button);button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,caption,
                ()=>{if(ReadAdmission())action();});return button;
        }
        void Radios(int[] nodes,List<UiSourceRadioButton> list,Action<int> action)
        {
            for(int i=0;i<4;i++)
            {
                int index=i;var box=source.Rectangle(nodes[i],mount);
                var button=new UiSourceRadioButton{Position=box.Position,Size=box.Size,InputAdmission=ReadAdmission};
                chrome.Canvas.AddChild(button);button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,
                    i==0?UiSourceRadioSegment.Left:i==3?UiSourceRadioSegment.Right:UiSourceRadioSegment.Centre,
                    Message(new[]{0x20C,0x20D,0x246,0x202}[i]),()=>{if(ReadAdmission())action(index);});list.Add(button);
            }
        }
    }
    private void ApplyAdditional(DiplomacyOrdersPage value)
    {
        if(additionalKind is null)return;
        if(captiveButton is not null)captiveButton.Disabled=busy||!value.CanChooseCaptives;
        if(ransom is not null)ransom.Text=Number(value.CaptiveRansomGold);
        if(goldBefore is not null)goldBefore.Text=Number(value.AvailableGold);
        if(goldAfter is not null)goldAfter.Text=Number(value.AvailableGold-value.CaptiveRansomGold);
        if(goldCost is not null)goldCost.Text=value.CaptiveRansomGold is int paid?$"(-{Number(paid)})":text.Get("detail_unknown");
        for(int i=0;i<captiveSlots.Count;i++)
        {
            var slot=captiveSlots[i];slot.Root.Visible=i<value.CaptivePeople.Count;
            var person=i<value.CaptivePeople.Count?value.CaptivePeople[i]:null;
            slot.Name.Text=person?.Name??"";
            slot.Trait.Visible=person?.Trait is not null||person?.TraitUnresolvedInput is not null;
            if(person?.Trait is{} trait)slot.Trait.Apply(trait.PhysicalTraitId,trait.Name,trait.Style);
            else if(person?.TraitUnresolvedInput is not null)slot.Trait.ApplyUnknown(text.Get("detail_unknown"));
            slot.Root.SetMeta("source_unknown",person?.UnresolvedInput??person?.TraitUnresolvedInput??value.CaptivePeopleUnresolvedInput??"");
            var next=person?.Portrait;
            var old=i<displayedCaptivePortraits.Count?displayedCaptivePortraits[i]:null;
            if(ReferenceEquals(next,old))continue;
            foreach(var child in slot.Portrait.GetChildren()){slot.Portrait.RemoveChild(child);child.QueueFree();}
            if(next is not null)
            {
                var face=new UiSourcePortrait{Position=slot.Rectangle.Position,Size=slot.Rectangle.Size};slot.Portrait.AddChild(face);
                face.ConfigureFace2(additionalRepository!,chrome.Assets,chrome.SpriteShader,next);
            }
        }
        displayedCaptivePortraits=value.CaptivePeople.Select(person=>person.Portrait).ToArray();
        if(requestedDescription is not null)requestedDescription.Text=value.RequestedDescription;
        for(int i=0;i<requestedRadios.Count;i++)
        {requestedRadios[i].Selected=i==value.RequestedKind;requestedRadios[i].Disabled=busy||!value.CanChooseRequested[i];}
        if(requestedReset is not null)requestedReset.Disabled=busy;
    }
}
