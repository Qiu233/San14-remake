using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record TalentAutoSearchPage(int? ExecutorPolicy,int? TravelRange,int? AllocationPolicy,
    int? AvailableBooks,int? RequiredBooks,int? ExecutorCount,bool CanPrepareSelection,string? Notice);

/// <summary>The original automatic-search configuration. Selection and route
/// allocation belong to its captured Data draft, never to view refreshes.</summary>
internal partial class TalentAutoSearchView : Control
{
    private AdministrationPageChrome chrome=null!;
    private UiSourceText books=null!,remaining=null!,cost=null!,people=null!;
    private readonly List<(int Group,int Value,UiSourceRadioButton View)> choices=[];
    private PreviewText text=null!;
    private string peopleFormat="";
    private Func<bool>? admitted;
    private Action? returned,accepted;
    private TalentAutoSearchPage? page;
    private bool busy,abandoned;

    internal void Configure(GameResourceRepository repository,PreviewText text,
        Func<bool> admitted,Action returned,Action accepted,Action<int,int> selected)
    {
        this.text=text;this.admitted=admitted;this.returned=returned;this.accepted=accepted;
        Name="TalentAutoSearch";ZIndex=3;MouseFilter=MouseFilterEnum.Stop;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome=new();AddChild(chrome);
        chrome.Configure(repository,text,0x0374,RequestBack,()=>{if(ReadAdmission()&&page?.CanPrepareSelection==true)this.accepted?.Invoke();});
        var source=new UiSourceLayout(repository,0x01358D6E);
        // 874E70 centres the control's Root1044x282 about (960,514).
        // The authored window398 height does not replace that Root height.
        Vector2 mount=new(438,373);
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        string Message(int id)=>messages.Records[id].Text??throw new InvalidDataException($"Missing automatic search text {id:X4}.");
        int[][] roots=[[9,12,15,18],[24,27,30],[36,39]];
        var replaced=roots.SelectMany(value=>value).ToHashSet();
        foreach(var node in source.Nodes)
        {
            if(InsideReplacement(node.Index))continue;
            if(node.Type is 1 or 3)source.Art(chrome.Canvas,node.Index,source.Rectangle(node.Index,mount),chrome.Assets,chrome.SpriteShader);
            else if(node.Type==6)source.Blur(chrome.Canvas,node.Index,source.Rectangle(node.Index,mount));
        }
        int[][] captions=[[0x0377,0x0378,0x0379,0x037A],[0x0446,0x0447,0x0448],[0x037B,0x037C]];
        for(int group=0;group<roots.Length;group++)
            for(int index=0;index<roots[group].Length;index++)
            {
                int selectedGroup=group,value=index;var box=source.Rectangle(roots[group][index],mount);
                var radio=new UiSourceRadioButton { Name=$"AutoSearchChoice{group}_{index}",Position=box.Position,Size=box.Size,InputAdmission=ReadAdmission };
                chrome.Canvas.AddChild(radio);
                radio.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,
                    index==0?UiSourceRadioSegment.Left:index==roots[group].Length-1?UiSourceRadioSegment.Right:UiSourceRadioSegment.Centre,
                    Message(captions[group][index]),()=>{if(ReadAdmission())selected(selectedGroup,value);});
                choices.Add((group,index,radio));
            }
        Label(8,0x0541);Label(23,0x0376);Label(35,0x03A0);Label(45,0x0292);Label(54,0x0541);
        books=Value(47);remaining=Value(49);cost=Value(50);people=Value(55);peopleFormat=Message(0x0323);
        if(peopleFormat.Split("%d").Length!=2)throw new InvalidDataException("Unsupported automatic search count format.");
        chrome.ApplyReturnHint(new AdministrationMessages(repository,text.SourceLocale).Format(0x2733));
        SetMeta("source_layout",source.Source.Source.ResourceUri);SetBusy(false);

        bool InsideReplacement(int index)
        {
            for(int? parent=index;parent is int at;parent=source.Nodes[at].ParentIndex)
                if(replaced.Contains(at))return true;
            return false;
        }
        UiSourceText Value(int index)=>source.Text(chrome.Canvas,index,mount,"",chrome.Font);
        void Label(int index,int id)=>source.Text(chrome.Canvas,index,mount,Message(id),chrome.Font);
    }
    internal void Apply(TalentAutoSearchPage value)
    {
        if(abandoned)return;
        page=value;int?[] selected=[value.ExecutorPolicy,value.TravelRange,value.AllocationPolicy];
        foreach(var choice in choices){choice.View.Selected=selected[choice.Group]==choice.Value;choice.View.Disabled=busy;}
        books.Text=Number(value.AvailableBooks);
        remaining.Text=Number(value.AvailableBooks is int available&&value.RequiredBooks is int required?available-required:null);
        cost.Text=value.RequiredBooks is int used?$"(-{Number(used)})":text.Get("detail_unknown");
        people.Text=value.ExecutorCount is int count?peopleFormat.Replace("%d",Number(count)):text.Get("detail_unknown");
        chrome.ApplyDefaultHint(value.Notice??"");RefreshAdmission();
    }
    private string Number(int? value)=>value?.ToString(System.Globalization.CultureInfo.InvariantCulture)??text.Get("detail_unknown");
    private bool ReadAdmission()=>!abandoned&&!busy&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    private void RefreshAdmission(){chrome.ApplyReturn(!busy);chrome.ApplyDecision(!busy&&page?.CanPrepareSelection==true,true);}
    internal void SetBusy(bool value){if(abandoned)return;busy=value;RefreshAdmission();if(page is not null)Apply(page);}
    internal void ApplyNotice(string message){if(!abandoned)chrome.ApplyDefaultHint(message);}
    internal void RequestBack(){if(ReadAdmission())returned?.Invoke();}
    internal void Abandon()
    {
        abandoned=true;admitted=null;returned=accepted=null;
        foreach(var choice in choices)choice.View.Abandon();chrome?.Abandon();Hide();
    }
}
