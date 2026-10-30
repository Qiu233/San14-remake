using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum ForeignTradeUiPage { Other=0, Top=38, Country=39, Person=40 }
internal sealed record ForeignTradeOrdersPage(string Title,string CountryName,
    AreaAdministrationOfficerValues Executor,string ExecutorNames,int Gold,int Supplies,int Troops,
    string ItemName,int GoldMinimum,int SuppliesMinimum,int? GoldAvailable,int? SuppliesAvailable,int? TroopsAvailable,
    IReadOnlyList<int> TroopOptions,int? BooksAvailable,int? BooksRequired,int? TravelDays,
    bool CanConfirm,bool CanChooseCountry,bool CanChooseExecutor,bool CanEditOffers,bool CanChooseItem,string? UnresolvedOwner,
    string? Friendship,string? Rivals);

/// <summary>The original overseas trade preparation page, separate from the
/// diplomacy goodwill form. Only the caller's unsubmitted trade draft is edited.</summary>
internal partial class ForeignTradeOrdersView : Control
{
    private AdministrationPageChrome chrome=null!;
    private UiSourceCommandButton country=null!,gold=null!,supplies=null!,item=null!,clearItem=null!;
    private AreaAdministrationOfficerView executor=null!;
    private UiSourceText executorNames=null!,goldAvailable=null!,suppliesAvailable=null!,troopsAvailable=null!,
        books=null!,remaining=null!,cost=null!,duration=null!,friendship=null!,rivals=null!;
    private readonly List<UiSourceRadioButton> troopChoices=[];
    private PreviewText text=null!;
    private Func<bool>? admitted;
    private Action? returned;
    private ForeignTradeOrdersPage? page;
    private bool abandoned,busy;
    private string defaultHint="";

    internal void Configure(GameResourceRepository repository,PreviewText text,Func<bool> admitted,
        Action returned,Action accepted,Action chooseCountry,Action chooseExecutors,
        Action<bool> chooseAmount,Action<int> chooseTroops,Action chooseItem,Action resetItem)
    {
        this.text=text;this.admitted=admitted;this.returned=returned;
        Name="ForeignTradeOrders";ZIndex=3;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop = new UiSourceCommonBlur(); AddChild(backdrop); backdrop.Configure(repository);
        chrome=new();AddChild(chrome);
        chrome.Configure(repository,text,0x0700,RequestBack,()=>
        {if(ReadAdmission()&&page?.CanConfirm==true)accepted();});
        var source=new UiSourceLayout(repository,0xF1DA2D4C);Vector2 mount=new(460,147);
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        string Message(int id)=>messages.Records[id].Text??throw new InvalidDataException($"Missing foreign trade caption {id:X4}.");
        int[] replaced=[6,32,107,118,129,133,137,141,150,156,179];
        foreach(var node in source.Nodes)
        {
            if(InsideReplacement(node.Index)||node.Index==17||node.Index==187)continue;
            if(node.Type is 1 or 3)source.Art(chrome.Canvas,node.Index,source.Rectangle(node.Index,mount),chrome.Assets,chrome.SpriteShader);
            else if(node.Type==6)source.Blur(chrome.Canvas,node.Index,source.Rectangle(node.Index,mount));
        }
        foreach(var binding in new[]{(5,0x701),(14,0x703),(21,0x702),(30,0x541),(100,0x3D7),
            (106,0x20C),(117,0x20D),(128,0x20E),(149,0x246),(166,0x26C),(173,0x292),(169,0x469)})
            source.Text(chrome.Canvas,binding.Item1,mount,Message(binding.Item2),chrome.Font);
        friendship=Value(18);rivals=Value(23);
        country=Button(6,"",chooseCountry);gold=Button(107,"",()=>chooseAmount(true));
        supplies=Button(118,"",()=>chooseAmount(false));item=Button(150,"",chooseItem);
        clearItem=Button(156,Message(0x029F),resetItem);
        executor=new() {Name="ForeignTradeExecutors",Position=source.Rectangle(32,mount).Position};
        chrome.Canvas.AddChild(executor);
        executor.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,text,
            ()=>{if(ReadAdmission())chooseExecutors();},chrome.Canvas);
        executorNames=Value(102);goldAvailable=Value(113);suppliesAvailable=Value(124);troopsAvailable=Value(145);
        duration=Value(168);books=Value(175);remaining=Value(177);cost=Value(178);
        int[] radioNodes=[129,133,137,141];
        for(int index=0;index<radioNodes.Length;index++)
        {
            int occurrence=index;var box=source.Rectangle(radioNodes[index],mount);
            var button=new UiSourceRadioButton {Name="ForeignTradeTroops"+index,Position=box.Position,
                Size=box.Size,InputAdmission=ReadAdmission};chrome.Canvas.AddChild(button);
            button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,
                index==0?UiSourceRadioSegment.Left:index==3?UiSourceRadioSegment.Right:UiSourceRadioSegment.Centre,"",
                ()=>{if(ReadAdmission()&&page is { } current&&occurrence<current.TroopOptions.Count)
                    chooseTroops(current.TroopOptions[occurrence]);});
            troopChoices.Add(button);
        }
        chrome.ApplyReturnHint(new AdministrationMessages(repository,text.SourceLocale).Format(0x2733));
        defaultHint=new AdministrationMessages(repository,text.SourceLocale).Format(0x5BE7);
        chrome.ApplyDefaultHint(defaultHint);
        SetMeta("source_layout",source.Source.Source.ResourceUri);SetMeta("source_page",38);
        RefreshAdmission();

        bool InsideReplacement(int index)
        {
            for(int? parent=index;parent is int current;parent=source.Nodes[current].ParentIndex)
                if(replaced.Contains(current))return true;
            return false;
        }
        UiSourceText Value(int node)=>source.Text(chrome.Canvas,node,mount,"",chrome.Font);
        UiSourceCommandButton Button(int node,string caption,Action action)
        {
            var box=source.Rectangle(node,mount);
            var button=new UiSourceCommandButton {Name="ForeignTradeField"+node,Position=box.Position,
                Size=box.Size,InputAdmission=ReadAdmission};chrome.Canvas.AddChild(button);
            button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,caption,
                ()=>{if(ReadAdmission())action();});return button;
        }
    }

    internal void Apply(ForeignTradeOrdersPage value)
    {
        if(abandoned)return;page=value;chrome.ApplyTitle(value.Title);
        country.Caption=value.CountryName;executor.Apply(value.Executor,!busy&&value.CanChooseExecutor);
        friendship.Text=value.Friendship??text.Get("detail_unknown");rivals.Text=value.Rivals??text.Get("detail_unknown");
        executorNames.Text=value.ExecutorNames;gold.Caption=Number(value.Gold);supplies.Caption=Number(value.Supplies);
        item.Caption=value.ItemName;goldAvailable.Text="/"+Number(value.GoldAvailable);suppliesAvailable.Text="/"+Number(value.SuppliesAvailable);
        troopsAvailable.Text="/"+Number(value.TroopsAvailable);duration.Text=Number(value.TravelDays);
        books.Text=Number(value.BooksAvailable);remaining.Text=Number(value.BooksAvailable is int available&&value.BooksRequired is int used?available-used:null);
        cost.Text=value.BooksRequired is int required?$"(-{Number(required)})":text.Get("detail_unknown");
        for(int index=0;index<troopChoices.Count;index++)
        {
            var button=troopChoices[index];bool exists=index<value.TroopOptions.Count;
            button.Visible=exists;
            if(!exists)continue;
            button.Caption=Number(value.TroopOptions[index]);button.Selected=value.Troops==value.TroopOptions[index];
            button.Disabled=busy||!value.CanEditOffers||value.TroopsAvailable is not int total||value.TroopOptions[index]>total;
        }
        chrome.ApplyDefaultHint(value.UnresolvedOwner is null?defaultHint:text.Get("detail_unknown"));RefreshAdmission();
    }
    private string Number(int? value)=>value?.ToString(System.Globalization.CultureInfo.InvariantCulture)??text.Get("detail_unknown");
    private bool ReadAdmission()=>!abandoned&&!busy&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    private void RefreshAdmission()
    {
        chrome.ApplyReturn(!busy);chrome.ApplyDecision(!busy&&page?.CanConfirm==true,true);
        if(page is not { } current)return;
        country.Disabled=busy||!current.CanChooseCountry;
        gold.Disabled=busy||!current.CanEditOffers||current.GoldAvailable is not int goldTotal||goldTotal<current.GoldMinimum;
        supplies.Disabled=busy||!current.CanEditOffers||current.SuppliesAvailable is not int suppliesTotal||suppliesTotal<current.SuppliesMinimum;
        item.Disabled=busy||!current.CanEditOffers||!current.CanChooseItem;clearItem.Disabled=busy;
    }
    internal void SetBusy(bool value){if(abandoned)return;busy=value;if(page is { } current)Apply(current);else RefreshAdmission();}
    internal void ApplyNotice(string notice){if(!abandoned)chrome.ApplyDefaultHint(notice);}
    internal void RequestBack(){if(ReadAdmission())returned?.Invoke();}
    internal void Abandon()
    {
        abandoned=true;admitted=null;returned=null;
        country?.Abandon();gold?.Abandon();supplies?.Abandon();item?.Abandon();clearItem?.Abandon();executor?.Abandon();
        foreach(var button in troopChoices)button.Abandon();chrome?.Abandon();Hide();
    }
}
