using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

internal sealed record DiplomacyOrdersPage(string ForceName, string CityName, string TargetForceName,
    string RelationName, AreaAdministrationOfficerValues Executor,
    int? TravelDays, int AvailableBooks, int RequiredBooks, string GiftDescription, int SelectedGiftKind,
    string MonthsDescription, string ExistingAllianceDescription, bool CanSubmit, bool CanChooseExecutor,
    bool CanChooseCity, bool CanEditGold, bool CanEditSupplies, bool CanEditItem, bool CanEditArea,
    string? Notice, LoadedSprite? AdvisorPortrait);

/// <summary>The four authored diplomacy preparation layouts share a draft owner,
/// while keeping their distinct original mounts and controls.</summary>
internal partial class DiplomacyOrdersView : Control
{
    private AdministrationPageChrome chrome = null!;
    private AreaAdministrationOfficerView executor = null!;
    private UiSourceCommandButton force = null!;
    private UiSourceCommandButton? city, months, amount;
    private readonly List<UiSourceRadioButton> gifts = [];
    private UiSourceText relation = null!, duration = null!, books = null!, remaining = null!, cost = null!;
    private UiSourceAdviceView advice = null!;
    private UiSourceText? gift, existingMonths, targetForce;
    private PreviewText text = null!;
    private Func<bool>? admitted;
    private Action? returned, accepted;
    private bool abandoned, busy;
    private DiplomacyOrdersPage? page;
    internal Rect2 MonthsRectangle { get; private set; }

    internal void Configure(GameResourceRepository repository, PreviewText text, DiplomaticMissionKind kind,
        Func<bool> admitted, Action returned, Action accepted, Action chooseForce, Action chooseCity,
        Action chooseExecutor, Action chooseMonths, Action<int> chooseGift, Action resetGift)
    {
        this.text = text; this.admitted = admitted; this.returned = returned; this.accepted = accepted;
        bool alliance = kind == DiplomaticMissionKind.Alliance, cancel = kind == DiplomaticMissionKind.CancelAlliance;
        bool request = kind == DiplomaticMissionKind.RequestAttack;
        var (file, title, mount) = kind switch
        {
            DiplomaticMissionKind.Goodwill => (0x3A32FE49u, 0x0288, new Vector2(460,237)),
            DiplomaticMissionKind.Alliance => (0x8547338Bu, 0x0289, new Vector2(434,174)),
            DiplomaticMissionKind.CancelAlliance => (0x0A49C6CDu, 0x0266, new Vector2(434,251)),
            DiplomaticMissionKind.RequestAttack => (0x00A872A8u, 0x028C, new Vector2(460,177)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        Name = "Diplomacy" + kind; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, title, RequestBack, () => { if (ReadAdmission() && page?.CanSubmit == true) this.accepted?.Invoke(); });
        var source = new UiSourceLayout(repository, file);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException($"Missing diplomacy UI message {id:X4}.");
        int card = request ? 42 : 25;
        int face = request ? 154 : alliance ? 153 : cancel ? 116 : 137;
        int[] radios = request ? [114,118,122,159] : alliance ? [113,117,121,164] : cancel ? [] : [97,101,105,142];
        int amountNode = request ? 126 : alliance ? 125 : 109;
        var replaced = new HashSet<int>(radios) { 6, card, face };
        if (request) replaced.Add(24);
        if (alliance) replaced.Add(99);
        if (!cancel) replaced.Add(amountNode);
        // These authored fields are explicitly hidden by both source callers.
        if (alliance || cancel) foreach (int index in new[]{89,90,91,92,93}) replaced.Add(index);
        foreach (var node in source.Nodes)
        {
            if (InsideReplacement(node.Index)) continue;
            if (node.Type is 1 or 3) source.Art(chrome.Canvas, node.Index, source.Rectangle(node.Index,mount), chrome.Assets,chrome.SpriteShader);
            else if (node.Type == 6) source.Blur(chrome.Canvas,node.Index,source.Rectangle(node.Index,mount));
        }
        force = Button(6, Message(0x0632), chooseForce);
        if (request) city = Button(24, Message(0x0474), chooseCity);
        executor = new() { Name = "DiplomacyExecutor", Position = source.Rectangle(card,mount).Position };
        chrome.Canvas.AddChild(executor);
        executor.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,text,
            () => { if (ReadAdmission()) chooseExecutor(); },chrome.Canvas);
        Label(5,0x0632); Label(14,0x0413); Label(request ? 40 : 23,0x0541);
        relation = Value(18);
        if (request) { Label(23,0x0474); Label(33,0x01FD); targetForce = Value(35); }
        int day = request ? 139 : alliance ? 138 : cancel ? 101 : 122;
        int book = request ? 146 : alliance ? 145 : cancel ? 108 : 129;
        Label(day-2,0x026C); duration = Value(day); Value(day+1).Text = Message(0x0469);
        Label(book-2,0x0292); books = Value(book); remaining = Value(book+2);
        cost = Value(book+3);
        advice=new() { Name="DiplomacyAdvice" };
        chrome.Canvas.AddChild(advice);
        advice.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,source,face,mount);
        if (alliance)
        {
            Label(98,0x0414); months = Button(99,"",chooseMonths); MonthsRectangle = source.Rectangle(99,mount);
            Label(161,0x0414);
            var box=source.Rectangle(162,mount);
            existingMonths=source.Text(chrome.Canvas,162,mount,"",chrome.Font,
                rectangle:new(box.Position,new(box.Size.X+source.Rectangle(163,mount).Size.X,box.Size.Y)));
        }
        if (!cancel)
        {
            Label(request ? 111 : alliance ? 110 : 94,0x03CE);
            gift = Value(request ? 113 : alliance ? 112 : 96);
            for (int i=0;i<radios.Length;i++)
            {
                int index = i; var box = source.Rectangle(radios[i],mount);
                var radio = new UiSourceRadioButton { Position=box.Position, Size=box.Size, InputAdmission=ReadAdmission };
                chrome.Canvas.AddChild(radio);
                radio.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,
                    i==0 ? UiSourceRadioSegment.Left : i==3 ? UiSourceRadioSegment.Right : UiSourceRadioSegment.Centre,
                    Message(new[]{0x020C,0x020D,0x0246,0x0202}[i]),
                    () => { if (ReadAdmission()) chooseGift(index); });
                gifts.Add(radio);
            }
            amount = Button(amountNode,Message(0x029F),resetGift);
        }
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        SetBusy(false);

        bool InsideReplacement(int index)
        {
            for (int? parent=index;parent is int at;parent=source.Nodes[at].ParentIndex)
                if (replaced.Contains(at)) return true;
            return false;
        }
        UiSourceText Value(int index) => source.Text(chrome.Canvas,index,mount,"",chrome.Font);
        void Label(int index,int message) => source.Text(chrome.Canvas,index,mount,Message(message),chrome.Font);
        UiSourceCommandButton Button(int index,string caption,Action clicked)
        {
            var box=source.Rectangle(index,mount);
            var button=new UiSourceCommandButton { Position=box.Position,Size=box.Size,InputAdmission=ReadAdmission };
            chrome.Canvas.AddChild(button);
            button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,caption,
                () => { if (ReadAdmission()) clicked(); });
            return button;
        }
    }

    internal void Apply(DiplomacyOrdersPage value)
    {
        if (abandoned) return;
        page=value; force.Caption=value.ForceName;
        if (city is not null) { city.Caption=value.CityName; city.Disabled=busy||!value.CanChooseCity; }
        if (targetForce is not null) targetForce.Text=value.TargetForceName;
        relation.Text=value.RelationName; executor.Apply(value.Executor,!busy&&value.CanChooseExecutor);
        duration.Text=Number(value.TravelDays); books.Text=Number(value.AvailableBooks);
        remaining.Text=Number(value.AvailableBooks-value.RequiredBooks);
        cost.Text=$"(-{Number(value.RequiredBooks)})";
        if (gift is not null) gift.Text=value.GiftDescription;
        if (months is not null) months.Caption=value.MonthsDescription;
        if (existingMonths is not null) existingMonths.Text=value.ExistingAllianceDescription;
        for (int i=0;i<gifts.Count;i++)
        {
            gifts[i].Selected=i==value.SelectedGiftKind;
            gifts[i].Disabled=busy||!new[]{value.CanEditGold,value.CanEditSupplies,value.CanEditItem,value.CanEditArea}[i];
        }
        if (amount is not null) amount.Disabled=busy;
        advice.Apply(value.AdvisorPortrait,value.Notice??""); RefreshAdmission();
    }
    private string Number(int? value) => value?.ToString(System.Globalization.CultureInfo.InvariantCulture)??text.Get("detail_unknown");
    private bool ReadAdmission() => !abandoned&&!busy&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    private void RefreshAdmission()
    { chrome.ApplyReturn(!busy); chrome.ApplyDecision(!busy&&page?.CanSubmit==true,true); force.Disabled=busy; if(months is not null)months.Disabled=busy; }
    internal void SetBusy(bool value) { if(abandoned)return;busy=value;RefreshAdmission();if(page is not null)Apply(page); }
    internal void ApplyNotice(string message) { if(!abandoned)chrome.ApplyDefaultHint(message); }
    internal void AddSourceOverlay(Control control) => chrome.Canvas.AddChild(control);
    internal void RequestBack() { if(ReadAdmission())returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned=true;admitted=null;returned=accepted=null;
        force?.Abandon();city?.Abandon();months?.Abandon();amount?.Abandon();executor?.Abandon();
        foreach(var radio in gifts)radio.Abandon();chrome?.Abandon();Hide();
    }
}
