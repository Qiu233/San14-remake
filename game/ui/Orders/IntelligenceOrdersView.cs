using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum IntelligenceUiMode { Home,Away,Inspect }
internal enum IntelligenceUiPage { Other=0,HomeOverview=42,HomeEditor=43,HomeOverviewReturned=44,
    HomePerson=45,AwayEditor=46,AwayForce=47,AwayPerson=48 }
internal sealed record IntelligenceEditorPage(IntelligenceUiMode Mode,string ForceName,
    AreaAdministrationOfficerValues Officer,int? GoldBefore,int? GoldAfter,int? MonthlyGoldCost,
    int? MilitaryBefore,int? MilitaryAfter,int? BooksBefore,int? BooksAfter,int? TravelDays,int Duration,
    bool CanChooseForce,bool CanChoosePerson,bool CanReset,bool CanConfirm,
    IReadOnlySet<int> EnabledDurations,string? UnresolvedOwner);

/// <summary>The actual home-assignment editor and separate reconnaissance
/// preparation page. Their fields edit a caller-owned draft only.</summary>
internal partial class IntelligenceOrdersView : Control
{
    private AdministrationPageChrome chrome=null!;
    private AreaAdministrationOfficerView officer=null!;
    private UiSourceCommandButton? force,reset;
    private UiSourceText? forceName,goldBefore,goldAfter,homeCost,militaryBefore,militaryAfter,
        militaryDelta,booksBefore,booksAfter,booksDelta,goldDelta,travel;
    private readonly Dictionary<int,UiSourceRadioButton> durations=[];
    private IntelligenceEditorPage? page;
    private Func<bool>? admitted;
    private Action? returned;
    private PreviewText text=null!;
    private string homeCostUnit="";
    private bool suspended,abandoned,returnAllowed=true,decisionAllowed=true;

    internal void Configure(GameResourceRepository repository,PreviewText text,IntelligenceUiMode mode,
        Func<bool> admitted,Action returned,Action accepted,Action chooseForce,Action choosePerson,
        Action resetPerson,Action<int> chooseDuration)
    {
        this.text=text;this.admitted=admitted;this.returned=returned;
        Name=mode==IntelligenceUiMode.Home?"SpyHomeEditor":mode==IntelligenceUiMode.Away?"SpyAwayEditor":"SpyInspectEditor";ZIndex=5;
        MouseFilter=MouseFilterEnum.Stop;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if(mode!=IntelligenceUiMode.Home)
        { var backdrop=new UiSourceCommonBlur();AddChild(backdrop);backdrop.Configure(repository); }
        chrome=new();AddChild(chrome);
        bool away=mode!=IntelligenceUiMode.Home,inspect=mode==IntelligenceUiMode.Inspect;
        chrome.Configure(repository,text,inspect?0xC6:away?0x9D:0x9C,RequestBack,
            ()=>{if(ReadAdmission()&&page?.CanConfirm==true)accepted();});
        var source=new UiSourceLayout(repository,inspect?0x3FC6E25Au:away?0xE9BDB9C7u:0xD310A707u);
        Vector2 mount=inspect?new(450,290):away?new(450,200):new(450,249);
        int card=away?18:14;
        int[] replaced=away?[6,18,56,60,64,inspect?103:104]:[14,47,69,70,72,73,81];
        foreach(var node in source.Nodes)
        {
            if(Inside(node.Index))continue;
            if(node.Type is 1 or 3)source.Art(chrome.Canvas,node.Index,source.Rectangle(node.Index,mount),chrome.Assets,chrome.SpriteShader);
        }
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        string Message(int id)=>messages.Records[id].Text??throw new InvalidDataException("Missing intelligence page caption.");
        var bindings=inspect?new[]{(5,0x478),(16,0x541),(81,0x835),(72,0x20C),(97,0x292),(90,0x26C),(55,0x839)}:
            away?new[]{(5,0x632),(16,0x541),(71,0x835),(82,0x20C),(98,0x292),(91,0x26C),(55,0x839)}:
                new[]{(5,0x632),(12,0x8A1),(58,0x836),(67,0x403)};
        foreach(var (node,message) in bindings)source.Text(chrome.Canvas,node,mount,Message(message),chrome.Font);
        if(away)force=Button(6,chooseForce);
        else { forceName=Value(7);reset=Button(47,resetPerson);reset.Caption=Message(0x29E); }
        officer=new() {Name="IntelligenceOfficer",Position=source.Rectangle(card,mount).Position};chrome.Canvas.AddChild(officer);
        officer.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,text,
            ()=>{if(ReadAdmission()&&page?.CanChoosePerson==true)choosePerson();},chrome.Canvas);
        militaryBefore=Value(inspect?83:away?73:60);militaryAfter=Value(inspect?85:away?75:62);militaryDelta=Value(inspect?86:away?76:63);
        if(away) { booksBefore=Value(inspect?99:100);booksAfter=Value(inspect?101:102);booksDelta=Value(inspect?102:103); }
        else { homeCost=Value(71);homeCostUnit=Message(0x47A); }
        if(away)
        {
            goldBefore=Value(inspect?74:84);goldAfter=Value(inspect?76:86);goldDelta=Value(inspect?77:87);travel=Value(inspect?92:93);
            int[] nodes=[56,60,64],values=inspect?[10,20,30]:[5,10,15],captions=inspect?[0xC9,0xCA,0xCB]:[0xAA,0xAB,0xAC];
            for(int i=0;i<nodes.Length;i++)
            {
                int duration=values[i];var box=source.Rectangle(nodes[i],mount);
                var button=new UiSourceRadioButton {Name="IntelligenceDuration"+duration,Position=box.Position,
                    Size=box.Size,InputAdmission=ReadAdmission};chrome.Canvas.AddChild(button);
                button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,
                    i==0?UiSourceRadioSegment.Left:i==2?UiSourceRadioSegment.Right:UiSourceRadioSegment.Centre,
                    Message(captions[i]),()=>{if(ReadAdmission()&&page?.EnabledDurations.Contains(duration)==true)chooseDuration(duration);});
                durations.Add(duration,button);
            }
        }
        chrome.ApplyReturnHint(new AdministrationMessages(repository,text.SourceLocale).Format(0x2733));
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        SetMeta("source_officer_context",inspect?21:away?19:18);
        RefreshAdmission();

        bool Inside(int index)
        {
            for(int? parent=index;parent is int current;parent=source.Nodes[current].ParentIndex)
                if(replaced.Contains(current))return true;
            return false;
        }
        UiSourceText Value(int node)=>source.Text(chrome.Canvas,node,mount,"",chrome.Font);
        UiSourceCommandButton Button(int node,Action action)
        {
            var box=source.Rectangle(node,mount);
            var button=new UiSourceCommandButton {Position=box.Position,Size=box.Size,InputAdmission=ReadAdmission};
            chrome.Canvas.AddChild(button);button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,"",
                ()=>{if(ReadAdmission())action();});return button;
        }
    }

    internal void Apply(IntelligenceEditorPage value)
    {
        if(abandoned)return;page=value;
        if(force is not null)force.Caption=value.ForceName;if(forceName is not null)forceName.Text=value.ForceName;
        officer.Apply(value.Officer,!suspended&&value.CanChoosePerson);
        Set(goldBefore,value.GoldBefore);Set(goldAfter,value.GoldAfter);
        Set(militaryBefore,value.MilitaryBefore);Set(militaryAfter,value.MilitaryAfter);
        Set(booksBefore,value.BooksBefore);Set(booksAfter,value.BooksAfter);Set(travel,value.TravelDays);
        if(goldDelta is not null)goldDelta.Text=Delta(value.GoldBefore,value.GoldAfter);
        militaryDelta!.Text=Delta(value.MilitaryBefore,value.MilitaryAfter);
        if(booksDelta is not null)booksDelta.Text=Delta(value.BooksBefore,value.BooksAfter);
        else homeCost!.Text=Number(value.MonthlyGoldCost)+"/"+homeCostUnit;
        foreach(var (duration,button) in durations)
        {button.Selected=value.Duration==duration;button.Disabled=suspended||!value.EnabledDurations.Contains(duration);}
        chrome.ApplyDefaultHint(value.UnresolvedOwner is null?"":text.Get("detail_unknown"));RefreshAdmission();
        void Set(UiSourceText? control,int? number) {if(control is not null)control.Text=Number(number);}
    }
    private string Number(int? value)=>value?.ToString(System.Globalization.CultureInfo.InvariantCulture)??text.Get("detail_unknown");
    private string Delta(int? before,int? after)=>before is int a&&after is int b?$"({(b-a>=0?"+":"")}{b-a})":text.Get("detail_unknown");
    private bool ReadAdmission()=>!abandoned&&!suspended&&IsVisibleInTree()&&(admitted?.Invoke()??false);
    internal void SetSuspended(bool value) {suspended=value;RefreshAdmission();}
    internal void SetReturnAllowed(bool value) {returnAllowed=value;RefreshAdmission();}
    internal void SetDecisionAllowed(bool value) {decisionAllowed=value;RefreshAdmission();}
    internal void RefreshAdmission()
    {
        if(abandoned||chrome is null)return;
        bool enabled=!suspended;
        chrome.ApplyReturn(enabled&&returnAllowed);chrome.ApplyDecision(enabled&&decisionAllowed&&page?.CanConfirm==true,true);
        if(force is not null)force.Disabled=!enabled||page?.CanChooseForce!=true;
        if(reset is not null)reset.Disabled=!enabled||page?.CanReset!=true;
    }
    internal void ApplyNotice(string value) {if(!abandoned)chrome.ApplyDefaultHint(value);}
    internal void RequestBack() {if(ReadAdmission()&&returnAllowed)returned?.Invoke();}
    internal void Abandon() {abandoned=true;admitted=null;returned=null;officer?.Abandon();chrome?.Abandon();Hide();}
}
