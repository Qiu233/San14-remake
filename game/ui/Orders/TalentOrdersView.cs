using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

internal sealed record TalentOrdersPage(string TargetName, AreaAdministrationOfficerValues? TargetCard,
    AreaAdministrationOfficerValues ExecutorCard, int ExecutorCount, int? AvailableBooks,
    int? RequiredBooks, int? TravelDays, bool CanSubmit, bool CanChooseExecutors,
    int RecruitMode, bool CanChooseRecruitMode, string? Notice = null, OfficerPortraitRoute? AdvisorPortrait = null);

/// <summary>The original search/employment panes. Candidate and assignment
/// semantics belong to the current talent draft; no game rules run in this view.</summary>
internal partial class TalentOrdersView : Control
{
    private AdministrationPageChrome chrome = null!;
    private UiSourceLayout source = null!;
    private AreaAdministrationOfficerView executor = null!;
    private AreaAdministrationOfficerView? target;
    private UiSourceAdviceView advice = null!;
    private UiSourceCommandButton? city;
    private UiSourceRoundButton continueOrders = null!;
    private UiSourceBottomButton? automatic;
    private readonly List<UiSourceRadioButton> recruitModes = [];
    private UiSourceText books = null!, remaining = null!, duration = null!, count = null!, cost = null!;
    private PreviewText text = null!;
    private Func<bool>? admitted;
    private Action? returned, accepted, continued, chooseTarget, chooseExecutors;
    private Action<int>? chooseRecruitMode;
    private Action? automaticRequested;
    private bool abandoned, busy;
    private TalentOrdersPage? page;

    internal void Configure(GameResourceRepository repository, PreviewText text, TalentOrderKind kind,
        Func<bool> admitted, Action returned, Action accepted, Action continued, Action chooseTarget, Action chooseExecutors,
        Action<int> chooseRecruitMode,Action automaticRequested, AdministrationPageChrome.Resources? resources = null)
    {
        this.text = text; this.admitted = admitted; this.returned = returned; this.accepted = accepted;
        this.continued=continued;
        this.chooseTarget = chooseTarget; this.chooseExecutors = chooseExecutors;
        this.chooseRecruitMode = chooseRecruitMode;
        this.automaticRequested=automaticRequested;
        bool search = kind == TalentOrderKind.Search;
        Name = search ? "TalentSearch" : "TalentRecruit"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop = new UiSourceCommonBlur(); AddChild(backdrop); backdrop.Configure(repository);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, search ? 0x0276 : 0x0277, RequestBack,
            () => { if (ReadAdmission() && page?.CanSubmit == true) this.accepted?.Invoke(); },threeButtonFooter:true, resources: resources);
        var footer=new UiSourceLayout(repository,0xD8913AB2);
        continueOrders=new() { Name="ContinueTalentOrders",Position=footer.Rectangle(3,new(0,904)).Position,
            ActionMode=BaseButton.ActionModeEnum.Release,InputAdmission=ReadAdmission,Disabled=true };
        chrome.Canvas.AddChild(continueOrders);
        continueOrders.ConfigureContinue(repository,chrome.Assets,chrome.SpriteShader,text.SourceLocale,
            ()=>{if(ReadAdmission()&&page?.CanSubmit==true)this.continued?.Invoke();});
        source = new(repository, search ? 0x639D10C6u : 0x2854C93Bu);
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        string Message(int id)=>messages.Records[id].Text??throw new InvalidDataException($"Missing talent text {id:X4}.");
        if(search)
        {
            automatic=new() { Name="OpenAutomaticSearch",Position=footer.Rectangle(14,new(0,904)).Position,InputAdmission=ReadAdmission };
            chrome.Canvas.AddChild(automatic);
            automatic.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,Message(0x0374),
                ()=>{if(ReadAdmission())this.automaticRequested?.Invoke();});
        }
        Vector2 mount = search ? new(434, 272) : new(434, 119);
        foreach (int index in PageNodeIndices(source, search))
        {
            var node = source.Nodes[index];
            if (node.Type is 1 or 3) source.Art(chrome.Canvas, node.Index, source.Rectangle(node.Index, mount), chrome.Assets, chrome.SpriteShader);
            else if (node.Type == 6) source.Blur(chrome.Canvas, node.Index, source.Rectangle(node.Index, mount));
        }
        if (search)
        {
            var box = source.Rectangle(5, mount);
            city = new() { Name = "TalentSearchDestination", Position = box.Position, Size = box.Size,InputAdmission=ReadAdmission };
            chrome.Canvas.AddChild(city);
            city.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text.Get("talent_choose_destination"),
                () => { if (ReadAdmission()) this.chooseTarget?.Invoke(); });
            Label(4,0x0474);Label(14,0x026C);Label(17,0x0469);
            Label(21,0x0541);Label(24,0x03D7);Label(26,0x0540);Label(95,0x0292);
        }
        else
        {
            target = Officer(7, () => { if (ReadAdmission()) this.chooseTarget?.Invoke(); });
            Label(5,0x0542);Label(80,0x0541);Label(71,0x0219);Label(73,0x026C);Label(75,0x0469);Label(167,0x0292);
            source.Text(chrome.Canvas,150,mount,Message(0x085C),chrome.Font);
            int[] mounts=[151,155,159], modes=[0,10,30], captions=[0x00B4,0x00B5,0x00B6];
            for(int i=0;i<mounts.Length;i++)
            {
                int mode=modes[i];var box=source.Rectangle(mounts[i],mount);
                var radio=new UiSourceRadioButton { Name="RecruitAbsence"+mode,Position=box.Position,Size=box.Size,InputAdmission=ReadAdmission };
                chrome.Canvas.AddChild(radio);
                radio.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,
                    i==0?UiSourceRadioSegment.Left:i==2?UiSourceRadioSegment.Right:UiSourceRadioSegment.Centre,
                    Message(captions[i]),()=>{if(ReadAdmission()&&page?.CanChooseRecruitMode==true)this.chooseRecruitMode?.Invoke(mode);});
                recruitModes.Add(radio);
            }
        }
        executor = Officer(search ? 27 : 82, () => { if (ReadAdmission()) this.chooseExecutors?.Invoke(); });
        books = Value(search ? 97 : 169); remaining = Value(search ? 99 : 171);
        cost=Value(search?100:172);
        duration = Value(search ? 16 : 74);
        count = search ? Value(25) : Value(72);
        advice=new() { Name="TalentAdvice" };chrome.Canvas.AddChild(advice);
        advice.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,source,search?105:177,mount);
        chrome.ApplyReturnHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2733));
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetBusy(false);

        UiSourceText Value(int node) => source.Text(chrome.Canvas, node, mount, "", chrome.Font);
        void Label(int node,int message) => source.Text(chrome.Canvas,node,mount,Message(message),chrome.Font);
        AreaAdministrationOfficerView Officer(int node, Action choose)
        {
            var card = new AreaAdministrationOfficerView { Name = "TalentOfficerCard" + node, Position = source.Rectangle(node, mount).Position };
            chrome.Canvas.AddChild(card);
            card.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text, choose, chrome.Canvas);
            card.SetMeta("source_mount_node", node); return card;
        }
    }

    // CPU preparation and presentation consume the same authored panes.
    // Replaced subtrees belong to their dedicated live controls.
    internal static IEnumerable<int> PageNodeIndices(UiSourceLayout source, bool search)
    {
        int[] replaced = search ? [5, 27, 105] : [7, 82, 151, 155, 159, 177];
        foreach (var node in source.Nodes)
        {
            bool insideReplacement = false;
            for (int? parent = node.Index; parent is int current; parent = source.Nodes[current].ParentIndex)
                if (replaced.Contains(current)) { insideReplacement = true; break; }
            if (!insideReplacement) yield return node.Index;
        }
    }

    internal void Apply(TalentOrdersPage publication)
    {
        if (abandoned) return;
        page = publication;
        if (city is not null) city.Caption = publication.TargetName;
        target?.Apply(publication.TargetCard ?? new(text.Get("talent_choose_target"), null, null, null, null), !busy);
        var value = publication.ExecutorCard;
        if (publication.ExecutorCount > 1) value = value with
            { Name = text.Format("talent_executor_group", value.Name, publication.ExecutorCount) };
        executor.Apply(value, !busy && publication.CanChooseExecutors);
        books.Text = Number(publication.AvailableBooks);
        remaining.Text = Number(publication.AvailableBooks is int available && publication.RequiredBooks is int required
            ? available - required : null);
        duration.Text = Number(publication.TravelDays);
        cost.Text=publication.RequiredBooks is int used?$"(-{Number(used)})":text.Get("detail_unknown");
        count.Text = city is not null ? publication.ExecutorCount.ToString() : publication.TargetName;
        int[] modes=[0,10,30];
        for(int i=0;i<recruitModes.Count;i++)
        {
            recruitModes[i].Selected=publication.RecruitMode==modes[i];
            recruitModes[i].Disabled=busy||!publication.CanChooseRecruitMode;
        }
        advice.Apply(publication.AdvisorPortrait,publication.Notice??"");
        RefreshAdmission();
    }
    private string Number(int? value) => value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? text.Get("detail_unknown");
    private bool ReadAdmission() => !abandoned && !busy && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    private void RefreshAdmission()
    {
        chrome.ApplyReturn(!busy); chrome.ApplyDecision(!busy && page?.CanSubmit == true, true);
        continueOrders.Disabled=busy||page?.CanSubmit!=true;
        if(automatic is not null)automatic.Disabled=busy;
        if (city is not null) city.Disabled = busy;
    }
    internal void SetBusy(bool value)
    {
        if (abandoned) return;
        busy = value; RefreshAdmission();
        if (page is not null) Apply(page);
    }
    internal void ApplyNotice(string message) { if (!abandoned) chrome.ApplyDefaultHint(message); }
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = accepted = continued = chooseTarget = chooseExecutors = null;
        chooseRecruitMode=null;
        automaticRequested=null;if(automatic is not null)automatic.Disabled=true;
        foreach(var radio in recruitModes)radio.Abandon();
        if(continueOrders is not null)continueOrders.Disabled=true;
        city?.Abandon(); target?.Abandon(); executor?.Abandon(); chrome?.Abandon(); Hide();
    }
}
