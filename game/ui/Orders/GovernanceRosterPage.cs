using Godot;
using San14.Data;
using San14.Client.Gameplay;
using San14.Data.Formats;

/// <summary>The authored government screen edits one detached ordered roster.
/// Its owner supplies source publications and remains the only command issuer.</summary>
internal partial class GovernanceRosterPage : Control
{
    private AdministrationPageChrome chrome = null!;
    private GameResourceRepository repository = null!;
    private LocalizedUiSpriteSource sprites = null!;
    private UiSourceLayout layout = null!;
    private PreviewText ui = null!;
    private GovernanceRosterPageModel source = null!;
    private readonly Dictionary<int,List<int>> members = [];
    private readonly HashSet<int> editedGroups = [];
    private readonly Dictionary<int,GovernanceRosterLineButton> lines = [];
    private readonly Dictionary<int,GovernanceRosterSlotButton[]> slots = [];
    private readonly Dictionary<int,Control> links = [];
    private readonly Dictionary<int,UiSourceText> texts = [];
    private readonly List<Control> bookArtwork = [];
    private readonly Dictionary<(string Pack,string Name),LoadedSprite> faces = [];
    private readonly Dictionary<(string Pack,string Name),LoadedSprite> rulerFaces = [];
    private readonly Dictionary<int,(LoadedSprite Face,UiSourcePortrait Control)> portraits = [];
    private Control? adviserActive;
    private int portraitLayer;
    internal LoadedSprite? RulerFace => Face(source.Ruler);
    private GovernanceRosterLineButton adviser = null!;
    private GovernanceRosterPolicyButton rulerPolicy = null!, adviserPolicy = null!;
    private UiSourceBottomButton automatic = null!, effects = null!, information = null!;
    private Action<GovernanceRosterSelection>? changed;
    private Action? automaticRequested, effectsRequested;
    private Action<int?>? selectionRequested;
    private Action<int>? expansionRequested;
    private MapInformationListView? effectDialog;
    private Control? selector;
    private GovernancePolicyBalloon balloon = null!;
    private GovernanceRosterEvaluationView? evaluation;
    private int? advisorId;
    private bool busy, abandoned, namesMode;
    internal long AdvisorSelectionSequence { get; private set; }
    internal long AutoPolicySelectionSequence { get; private set; }
    internal int? SelectedAdvisorId => advisorId;
    internal bool AdvisorSelectorVisible => selector is not null && selector.GetMeta("governance_adviser",false).AsBool();
    internal bool EffectDialogVisible => effectDialog is { } dialog && GodotObject.IsInstanceValid(dialog) && dialog.IsVisibleInTree();
    internal GovernanceRosterSelection Selection => new(advisorId,source.Departments.Select(group =>
        new GovernanceDepartmentSelection(group.Group,members[group.Group].ToArray())).ToArray());
    internal bool HasChanges => advisorId!=source.Advisor?.Id || source.Departments.Any(group =>
        !members[group.Group].SequenceEqual(group.Members.Select(person=>person.Id)));

    internal void Configure(GameResourceRepository repository, PreviewText text, Theme theme,
        GovernanceRosterPageModel model, Action returned, Action accepted, Action<GovernanceRosterSelection> changed,
        Action? automaticRequested=null, Action? effectsRequested=null,
        Action<int?>? selectionRequested=null, Action<int>? expansionRequested=null)
    {
        this.repository=repository; sprites=new(repository); ui=text; source=model; advisorId=model.Advisor?.Id;
        this.changed=changed;this.automaticRequested=automaticRequested;this.effectsRequested=effectsRequested;
        this.selectionRequested=selectionRequested;this.expansionRequested=expansionRequested;
        Name="GovernanceRosterPage";MouseFilter=MouseFilterEnum.Stop;ZIndex=3;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach(var group in model.Departments)members.Add(group.Group,group.Members.Select(row=>row.Id).ToList());
        chrome=new();AddChild(chrome);
        chrome.Configure(repository,text,0x15,returned,accepted,returnOnly:!model.CanEdit);
        chrome.ApplyDefaultHint(text.Get("play_governance_roster_hint"));
        layout=new(repository,0xAB134AD2);
        // Child layout placeholders are deliberately not drawn twice.
        var background=layout.Art(chrome.Canvas,1,layout.Rectangle(1),chrome.Assets,chrome.SpriteShader);
        chrome.Canvas.MoveChild(background,0);portraitLayer=1;
        foreach(int node in new[]{4,5,8,16,19,25})
        {
            var art=layout.Art(chrome.Canvas,node,layout.Rectangle(node),chrome.Assets,chrome.SpriteShader);
            if(node is 4 or 5 or 8)bookArtwork.Add(art);
        }
        foreach(int node in new[]{32,40})layout.Blur(chrome.Canvas,node,layout.Rectangle(node));
        foreach(int node in new[]{33,41})layout.Art(chrome.Canvas,node,layout.Rectangle(node),chrome.Assets,chrome.SpriteShader);
        adviserActive=layout.Art(chrome.Canvas,42,layout.Rectangle(42),chrome.Assets,chrome.SpriteShader);
        foreach(int node in new[]{6,7,9,15,18,20,21,22,23,34,35,43,44,45})
            texts[node]=layout.Text(chrome.Canvas,node,default,"",chrome.Font);
        texts[6].Text=Data(0x292);texts[20].Text=Data(0x54C);texts[22].Text="/";
        texts[34].Text=Data(0x213);texts[44].Text=Data(0x23A);
        if(model.Flag is {} appearance)
        {
            var flag=new UiSourceForceFlag {Position=layout.Rectangle(12).Position};chrome.Canvas.AddChild(flag);
            flag.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,appearance);
            flag.Scale=layout.Rectangle(12).Size/flag.Size;
        }
        BuildDoctrine();
        rulerPolicy=Policy(36);adviserPolicy=Policy(46);
        adviser=new(){Name="GovernanceAdvisor",Position=layout.Rectangle(48).Position};chrome.Canvas.AddChild(adviser);
        adviser.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,()=>Select(null),()=>Editable);
        var lineLayout=new UiSourceLayout(repository,0xACEA710B);
        int[] mounts=[51,62,63,64,65];
        foreach(var group in source.Departments)
        {
            if((uint)group.Group>=mounts.Length)throw new InvalidDataException("Government department outside authored mounts.");
            Vector2 mount=layout.Rectangle(mounts[group.Group]).Position;
            var line=new GovernanceRosterLineButton{Name="GovernanceDepartment"+group.Group,
                Position=lineLayout.Rectangle(1,mount).Position};chrome.Canvas.AddChild(line);lines.Add(group.Group,line);
            int id=group.Group;
            line.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,()=>Select(id),()=>Editable);
            var rows=new GovernanceRosterSlotButton[7];slots.Add(id,rows);
            for(int row=0;row<rows.Length;row++)
            {
                int ordinal=row;
                var button=new GovernanceRosterSlotButton{Name=$"GovernanceSlot{id}_{row}",
                    Position=lineLayout.Rectangle(7+row*2,mount).Position};chrome.Canvas.AddChild(button);rows[row]=button;
                button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,()=>
                {
                    if(!Editable)return;
                    var current=source.Departments.Single(item=>item.Group==id);
                    if(current.Capacity is int capacity && ordinal>=capacity && ordinal<current.MaximumCapacity)
                    {if(source.CanExpand.GetValueOrDefault(id))expansionRequested?.Invoke(id);}
                    else if(current.Capacity is int limit && ordinal<limit)Select(id);
                },()=>!Blocked);
                button.MouseEntered+=()=>ShowPolicy(SlotPerson(id,ordinal));
                button.MouseExited+=()=>balloon?.Display(null);
            }
            links[id]=layout.Art(chrome.Canvas,26+id,layout.Rectangle(26+id),chrome.Assets,chrome.SpriteShader);
        }
        var footer=new UiSourceLayout(repository,0xD8913AB2);
        effects=Footer(14,0x38A,()=>effectsRequested?.Invoke());
        information=Footer(17,0x38B,()=>{namesMode=!namesMode;Render();});
        automatic=Footer(18,0x2C,()=>automaticRequested?.Invoke());
        balloon=new();balloon.Configure(repository,chrome);
        rulerPolicy.MouseEntered+=()=>ShowPolicy(source.Ruler);
        rulerPolicy.MouseExited+=()=>balloon.Display(null);
        adviserPolicy.MouseEntered+=()=>ShowPolicy(AdvisorPerson());
        adviserPolicy.MouseExited+=()=>balloon.Display(null);
        Render();NotifyChanged();

        GovernanceRosterPolicyButton Policy(int node)
        {
            var control=new GovernanceRosterPolicyButton{Position=layout.Rectangle(node).Position};chrome.Canvas.AddChild(control);
            control.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,()=>{},()=>!Blocked);return control;
        }
        UiSourceBottomButton Footer(int node,int message,Action action)
        {
            var button=new UiSourceBottomButton{Position=footer.Rectangle(node,new(0,904)).Position};chrome.Canvas.AddChild(button);
            button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,Data(message),()=>{if(!Blocked)action();});
            button.SetMeta("source_control_id",node);return button;
        }
    }
    private string Data(int message)=>repository.LoadMessages(ui.SourceLocale,MessageKind.Data).Messages.Sections[1].Records[message].Text
        ??throw new InvalidDataException("Missing government source caption.");
    private bool Blocked=>busy||abandoned||effectDialog is not null||selector is not null;
    private bool Editable=>!Blocked&&source.CanEdit;
    private void Select(int? group){if(Editable)selectionRequested?.Invoke(group);}
    private GovernanceRosterOfficer? Person(int? id)=>id is not int key?null:source.Departments
        .SelectMany(group=>group.Members.Concat(group.Candidates)).Concat(source.AdvisorCandidates)
        .Concat(source.Advisor is {} advisor?[advisor]:[]).FirstOrDefault(row=>row.Id==key);
    private GovernanceRosterOfficer? AdvisorPerson()
    {
        var person=Person(advisorId);if(person is null)return null;
        return person with {PendingValue=source.Advisor is {} previous&&advisorId==previous.Id?previous.PendingValue:source.NewAppointmentPending};
    }
    private GovernanceRosterOfficer? DepartmentPerson(int group,int id)
    {
        var person=Person(id);if(person is null)return null;
        var original=source.Departments.Single(row=>row.Group==group);
        var retained=(original.SlotMembers?.OfType<GovernanceRosterOfficer>()??original.Members)
            .LastOrDefault(row=>row.NativeValid==true&&row.Id==id);
        return person with {PendingValue=retained?.PendingValue??source.NewAppointmentPending};
    }
    private GovernanceRosterOfficer? SlotPerson(int group,int ordinal)
    {
        var definition=source.Departments.Single(row=>row.Group==group);
        if(!editedGroups.Contains(group)&&definition.SlotMembers is {} slots&&ordinal<slots.Count)return slots[ordinal];
        return ordinal<members[group].Count?DepartmentPerson(group,members[group][ordinal]):null;
    }
    private LoadedSprite? Face(GovernanceRosterOfficer? person)
    {
        if(person is not {FaceResourceName:string pack,FaceSpriteName:string name})return null;
        if(faces.TryGetValue((pack,name),out var face))return face;
        return faces[(pack,name)]=sprites.LoadTexturePackSprite(pack,name);
    }
    private void Render()
    {
        if(abandoned)return;
        foreach(var art in bookArtwork)art.Visible=source.ShowCommandBooks;
        foreach(int node in new[]{6,7,9})texts[node].Visible=source.ShowCommandBooks;
        texts[15].Text=source.ForceName;texts[18].Text=source.RemainingExpansions is >0?GovernanceRosterMessages.Numbers(Data(0x38C),source.RemainingExpansions.Value):"";
        texts[35].Text=source.Ruler?.Name??ui.Get("detail_unknown");
        var advisor=AdvisorPerson();texts[43].Text=advisor?.Name??"";
        adviserActive!.Visible=advisor?.PendingValue==0;
        LoadedSprite? rulerFace=source.RulerResourceName is string pack&&source.RulerSpriteName is string name
            ?RulerPortrait(pack,name):null;
        Portrait(2,rulerFace);Portrait(39,Face(advisor));
        texts[45].Text=advisor?.Intelligence?.ToString()??(advisor is null?"":ui.Get("detail_unknown"));
        adviser.Apply(Data(0x243),5,0,Editable&&selectionRequested is not null);
        ApplyPolicy(rulerPolicy,source.Ruler,false);ApplyPolicy(adviserPolicy,advisor,true);
        foreach(var group in source.Departments)
        {
            int count=members[group.Group].Count;
            lines[group.Group].Apply(group.Name,group.Group,AvailableAppointments(group),Editable&&selectionRequested is not null);
            bool active=false;
            for(int row=0;row<7;row++)
            {
                var person=SlotPerson(group.Group,row);
                int mode=group.Capacity is not int capacity?4:row<capacity?person is null||person.NativeValid==false?1:0:row<group.MaximumCapacity?2:4;
                bool pending=person?.PendingValue==0;
                active|=mode==0&&pending;
                bool enabled=Editable&&(mode is 0 or 1&&selectionRequested is not null||mode==2&&source.CanExpand.GetValueOrDefault(group.Group)&&expansionRequested is not null);
                slots[group.Group][row].Apply(new(mode,namesMode?person?.Name??"":PolicyCaption(person),
                    person?.PolicyCost,Face(person),person?.PolicyName is not null,pending,enabled,person?.CostDiscount==true,mode==2&&source.RemainingExpansions>0&&source.ShowCommandBooks));
            }
            links[group.Group].Visible=active;
        }
        RefreshEditors();
    }
    private int AvailableAppointments(GovernanceRosterDepartment group)
    {
        if(!source.ShowCommandBooks||evaluation is not {PolicyCost:int total,PolicyCostLimit:int limit,CommandBooks:int committed}||
            source.CommandBooksAvailable is not int books||group.Capacity is not int capacity)return 0;
        int occupied=!editedGroups.Contains(group.Group)&&group.SlotMembers is {} slots
            ?slots.Count(row=>row?.Id is >0 and <=6000):members[group.Group].Count(id=>id is >0 and <=6000);
        int budget=Math.Max(0,limit-total),spent=0,count=0;
        var existing=Enumerable.Range(0,7).Select(row=>SlotPerson(group.Group,row)).OfType<GovernanceRosterOfficer>()
            .Where(row=>row.NativeValid==true).Select(row=>row.Id).ToHashSet();
        foreach(var candidate in group.Candidates.Where(row=>row.Selectable&&!existing.Contains(row.Id)&&row.Id!=advisorId)
            .OrderBy(row=>row.PolicyCost).ThenBy(row=>row.Id))
        {
            if(candidate.PolicyCost is not int cost)return 0;
            if(cost+spent<=budget){count++;spent+=cost;}
        }
        return Math.Max(0,Math.Min(count,Math.Min(Math.Max(0,capacity-occupied),books-committed)));
    }
    private string PolicyCaption(GovernanceRosterOfficer? person)
    {
        if(person?.PolicyName is not string name)return "";
        if(person.PolicyLevel is not int level)return name;
        string format=Data(0x387);var result=new System.Text.StringBuilder();int names=0,levels=0;
        for(int at=0;at<format.Length;at++)
        {
            if(format[at]=='%'&&at+1<format.Length&&format[at+1]=='s'){result.Append(name);names++;at++;}
            else if(format[at]=='%'&&at+1<format.Length&&format[at+1]=='d'){result.Append(level);levels++;at++;}
            else result.Append(format[at]);
        }
        if(names!=1||levels!=1)throw new InvalidDataException("Government policy caption differs from its source signature.");
        return result.ToString();
    }
    private void ShowPolicy(GovernanceRosterOfficer? person)
    {
        if(Blocked||person is not {NativeValid:true,PolicyId:>0,PolicyDescription:string description}){balloon.Display(null);return;}
        var roster=(source.Ruler is {} ruler?new[]{ruler}:Array.Empty<GovernanceRosterOfficer>())
            .Concat(AdvisorPerson() is {} advisor?new[]{advisor}:[])
            .Concat(source.Departments.SelectMany(group=>Enumerable.Range(0,7).Select(ordinal=>SlotPerson(group.Group,ordinal)).OfType<GovernanceRosterOfficer>()));
        var effects=person.PolicyEffectSlots;
        if(effects is null){balloon.Display(null);return;}
        bool matchEffect=effects.TakeWhile(value=>value is >=1 and <=100).Count()<3;
        int primary=effects[0];
        var contributors=roster.Where(row=>row.NativeValid==true&&row.PolicyId is >0 && (matchEffect
            ?primary is >=1 and <=100&&row.PolicyEffectSlots?.Contains(primary)==true:row.PolicyId==person.PolicyId)).ToArray();
        int? total=contributors.Any(row=>row.PolicyLevel is null)?null:Math.Min(10,contributors.Sum(row=>row.PolicyLevel!.Value));
        balloon.Display(new(description,person.PendingValue,person.PolicyLevel,total,
            contributors.Select(row=>new GovernancePolicyBalloonContributor(Face(row),row.PendingValue!=0)).ToArray()));
    }
    private void ApplyPolicy(GovernanceRosterPolicyButton target,GovernanceRosterOfficer? person,bool advisor)
        =>target.Apply(new(PolicyCaption(person),person?.PolicyIcon is string icon?sprites.LoadCommonSprite(icon):null,advisor?0:person?.PolicyCost,
            !advisor&&person?.CostDiscount==true,person?.PolicyName is not null,person?.PendingValue==0,
            person is {NativeValid:true,PolicyId:>0,PolicyName:not null,PendingValue:<=0}));
    private LoadedSprite RulerPortrait(string pack,string name)
    {
        if(!rulerFaces.TryGetValue((pack,name),out var face))
            rulerFaces[(pack,name)]=face=sprites.LoadTexturePackSprite(pack,name);
        return face;
    }
    private void Portrait(int node,LoadedSprite? face)
    {
        if(portraits.TryGetValue(node,out var old))
        {
            if(ReferenceEquals(old.Face,face))return;
            old.Control.GetParent()?.RemoveChild(old.Control);old.Control.QueueFree();portraits.Remove(node);
        }
        if(face is null)return;
        var rectangle=layout.Rectangle(node);
        var control=new UiSourcePortrait {Name="GovernmentPortrait"+node,Position=rectangle.Position,Size=rectangle.Size};
        chrome.Canvas.AddChild(control);chrome.Canvas.MoveChild(control,portraitLayer);
        control.ConfigureLayoutPane(repository,chrome.Assets,chrome.SpriteShader,face,layout,node);
        portraits[node]=(face,control);
    }
    private void BuildDoctrine()
    {
        var doctrine=new UiSourceLayout(repository,0x6449BD68);Vector2 mount=layout.Rectangle(10).Position;
        foreach(int node in new[]{1,2})doctrine.Art(chrome.Canvas,node,doctrine.Rectangle(node,mount),chrome.Assets,chrome.SpriteShader,
            blend:node==1?UiSourceSprite.Blend.Additive:UiSourceSprite.Blend.Ordinary);
        int[] nodes=[5,7,6,9,8,10];
        if(source.DoctrineId is int id&&id>=1&&id<=nodes.Length)
            doctrine.Art(chrome.Canvas,nodes[id-1],doctrine.Rectangle(nodes[id-1],mount),chrome.Assets,chrome.SpriteShader);
        var progress=source.DoctrineProgress;
        doctrine.Text(chrome.Canvas,11,mount,progress?.Level is int level?GovernanceRosterMessages.Numbers(Data(900),level):ui.Get("detail_unknown"),chrome.Font,
            MapTextPresets.Create()[progress?.Level>=10?50:0]);
        doctrine.Text(chrome.Canvas,12,mount,progress is {WithinLevel:int within,LevelSpan:int span}?GovernanceRosterMessages.Numbers(Data(0x325),within,span):ui.Get("detail_unknown"),chrome.Font);
        if(progress?.Percentage is int percentage)
        {
            var gauge=new UiSourceRadialGauge{Position=mount};chrome.Canvas.AddChild(gauge);
            gauge.Configure(doctrine,chrome.Assets,chrome.SpriteShader,"gauge_ani",3,4,false);
            gauge.ApplySourceFrame(gauge.StartFrame+Math.Clamp(percentage,0,100));
        }
    }
    internal bool ApplyAutomaticSelection(GovernanceRosterSelection selected)
    {
        if(!Editable)return false;
        foreach(var group in source.Departments)editedGroups.Add(group.Group);
        ApplySelection(selected);AutoPolicySelectionSequence++;return true;
    }
    internal void ApplySelection(GovernanceRosterSelection selected)
    {
        if(selected.Departments.Count!=members.Count||selected.Departments.Select(row=>row.Group).Distinct().Count()!=members.Count)
            throw new InvalidDataException("Government selection does not match authored departments.");
        foreach(var row in selected.Departments)
        {
            if(!members.TryGetValue(row.Group,out var target))throw new InvalidDataException("Unknown government department.");
            if(!target.SequenceEqual(row.OfficerIds))editedGroups.Add(row.Group);
            target.Clear();target.AddRange(row.OfficerIds);
        }
        advisorId=selected.AdvisorId;Render();NotifyChanged();
    }
    internal void ApplyPickedSelection(int? group,IReadOnlyList<int> selected)
    {
        if(!Editable)return;
        ApplySelection(Selection.WithPick(group,selected,source.Departments));
        if(group is null)AdvisorSelectionSequence++;
    }
    internal void ApplyModel(GovernanceRosterPageModel model){source=model;Render();NotifyChanged();}
    private void NotifyChanged(){if(!abandoned)changed?.Invoke(Selection);RefreshEditors();}
    internal void ApplyEvaluation(GovernanceRosterEvaluationView value)
    {
        evaluation=value;texts[21].Text=value.PolicyCost?.ToString()??ui.Get("detail_unknown");
        texts[23].Text=value.PolicyCostLimit?.ToString()??ui.Get("detail_unknown");
        texts[7].Text=source.CommandBooksAvailable?.ToString()??ui.Get("detail_unknown");
        if(source.CommandBooksAvailable is int stock&&value.CommandBooks is int cost)
            texts[9].SetRuns(new(Math.Max(0,stock-cost)+"("),new((-cost).ToString(),
                MapTextPresets.Create()[cost>0?16:0].LT.CpuPacked),new(")"));
        else texts[9].Text="";
        texts[21].Configure(chrome.Font,MapFontProfile.FromNode(layout.Nodes[21],layout.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[value.PolicyCost>value.PolicyCostLimit?16:0]));
        foreach(var group in source.Departments)lines[group.Group].Apply(group.Name,group.Group,AvailableAppointments(group),Editable&&selectionRequested is not null);
        RefreshEditors();
    }
    internal void SetBusy(bool value){busy=value;if(value)balloon?.Display(null);Render();}
    internal void ShowSelector(Control child,bool advisor)
    {
        if(Blocked)return;balloon.Display(null);selector=child;child.SetMeta("governance_adviser",advisor);AddChild(child);chrome.Hide();RefreshEditors();
    }
    internal bool TryCloseSelector()
    {
        if(selector is not {} child)return false;selector=null;child.GetParent()?.RemoveChild(child);child.QueueFree();
        if(!abandoned){chrome.Show();Render();}return true;
    }
    internal void ShowEffectDialog(Action<MapInformationListView,Action> configure)
    {
        if(Blocked)return;balloon.Display(null);var dialog=new MapInformationListView{Name="GovernanceEffectsDialog"};effectDialog=dialog;AddChild(dialog);
        try{configure(dialog,()=>TryCloseEffectDialog());chrome.Hide();RefreshEditors();}catch{TryCloseEffectDialog();throw;}
    }
    internal bool TryCloseEffectDialog()
    {
        if(effectDialog is not {} dialog)return false;effectDialog=null;dialog.Abandon();dialog.GetParent()?.RemoveChild(dialog);dialog.QueueFree();
        if(!abandoned){chrome.Show();Render();}return true;
    }
    internal void ApplyHint(string text)=>chrome.ApplyDefaultHint(text);
    private void RefreshEditors()
    {
        chrome.ApplyReturn(!busy&&!abandoned);chrome.ApplyDecision(Editable&&HasChanges&&evaluation is
            {Allowed:true,CommandBooks:not null,ExceedsLevelLimit:not null},source.CanEdit);
        if(automatic is not null)automatic.Disabled=!Editable||automaticRequested is null;
        if(effects is not null)effects.Disabled=Blocked||effectsRequested is null;
        if(information is not null)information.Disabled=Blocked||!members.Values.Any(row=>row.Count>0);
    }
    internal void Abandon()
    {
        abandoned=true;balloon?.Display(null);changed=null;automaticRequested=null;effectsRequested=null;selectionRequested=null;expansionRequested=null;
        TryCloseSelector();TryCloseEffectDialog();chrome.Abandon();Hide();
    }
}
