using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The ordinary final review page. Returning preserves the local
/// draft; only its final decision submits the existing new-game start request.</summary>
internal partial class NewGameConfirmationView : Control
{
    private AdministrationPageChrome chrome = null!;
    private NewGameConfirmationList list = null!;
    private readonly UiSourceTabButton[] tabs = new UiSourceTabButton[2];
    private readonly int[] offsets = new int[2];
    private IReadOnlyList<NewGameConfirmationRow>[] rows = null!;
    private UiSourceRoundButton abort = null!;
    private Func<bool>? admitted;
    private Action? returned, confirmed, aborted;
    private bool busy, abandoned;
    internal int CurrentTab { get; private set; }

    internal void Configure(GameResourceRepository repository, PreviewText text, NewGameForcePageHeader header,
        NewGameForcePreview force, LoadedSprite portrait, IReadOnlyList<NewGameConfirmationSetting> settings,
        NewGameConfirmationExtras extras, Func<bool> admitted, Action returned, Action confirmed, Action aborted,
        Func<int> scrollSpeed)
    {
        this.admitted = admitted; this.returned = returned; this.confirmed = confirmed; this.aborted = aborted;
        Name = "NewGameConfirmation"; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new AdministrationPageChrome { Name = "NewGameConfirmationChrome" }; AddChild(chrome);
        chrome.Configure(repository, text, 0x615, RequestBack, RequestConfirm, threeButtonFooter: true);
        var messages = new AdministrationMessages(repository, text.SourceLocale);
        chrome.ApplyDefaultHint(messages.Format(0x2761)); chrome.ApplyReturnHint(messages.Format(0x2733));
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        abort = new UiSourceRoundButton { Name = "AbortNewGameConfirmation", Position = footer.Rectangle(3, new(0,904)).Position,
            InputAdmission = ReadAdmission }; chrome.Canvas.AddChild(abort);
        abort.ConfigureAbort(repository, chrome.Assets, chrome.SpriteShader, text.SourceLocale,
            () => { if (ReadAdmission()) this.aborted?.Invoke(); });
        abort.MouseEntered += () => { if (ReadAdmission()) chrome.ApplyHoverHint(messages.Format(0x273E)); };
        abort.MouseExited += () => chrome.ApplyHoverHint(null);
        var body = new Control { Name = "ConfirmationBody", Size = new(1920,1080), MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(body); chrome.Canvas.MoveChild(body,0);
        var source = new UiSourceLayout(repository,0x92D34610);
        Rect2 faceBox = source.Rectangle(2);
        var faceSource = new UiSourceLayout(repository,0xAB21CD5F).ResizedSubtree(0,faceBox.Size,true);
        var face = new UiSourcePortrait { Name = "ConfirmationRuler", Position = faceBox.Position, Size = faceBox.Size };
        body.AddChild(face); face.ConfigureLayoutPane(repository,chrome.Assets,chrome.SpriteShader,portrait,faceSource,1);
        BuildForceSummary(repository,text,source,body,force);
        source.Art(body,9,source.Rectangle(9),chrome.Assets,chrome.SpriteShader);
        var banner = new UiSourceLayout(repository,0x6C522E54);Vector2 bannerMount = source.Rectangle(10).Position;
        foreach(var node in banner.Nodes.Where(node => node.Type is 1 or 3))
            banner.Art(body,node.Index,banner.Rectangle(node.Index,bannerMount),chrome.Assets,chrome.SpriteShader);
        var preset = MapTextPresets.Create()[27];
        banner.Text(body,6,bannerMount,extras.Year,chrome.Font,preset);
        banner.Text(body,7,bannerMount,extras.Month,chrome.Font,preset);
        banner.Text(body,8,bannerMount,header.Title,chrome.Font,preset);
        var rightSource = new UiSourceLayout(repository,0xC2A59862);
        var right = new Control { Name = "ConfirmationRight", Position = source.Rectangle(7).Position,
            Size = rightSource.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore }; body.AddChild(right);
        rightSource.Blur(right,1,rightSource.Rectangle(1));
        rightSource.Art(right,2,rightSource.Rectangle(2),chrome.Assets,chrome.SpriteShader);
        var data = repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1].Records;
        for(int index=0;index<2;index++)
        {
            int selected=index;Rect2 box=rightSource.Rectangle(index==0?4:8);
            var tab=new UiSourceTabButton { Name="ConfirmationTab"+index,Position=box.Position,Size=box.Size,InputAdmission=ReadAdmission };
            right.AddChild(tab);tabs[index]=tab;
            tab.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,data[index==0?0x614:0x6AD].Text
                ??throw new InvalidDataException("Confirmation tab caption is missing."),index==0?0x172EDC24u:0xD45873AAu);
            tab.Pressed+=()=>{if(ReadAdmission())SelectTab(selected);};
        }
        rows = [settings.Select(setting => new NewGameConfirmationRow(setting.TitleMessageId switch
            {0x4C3=>0x249,0x4C8=>0x54F,_=>setting.TitleMessageId},setting.Value)).ToArray(),extras.Rows];
        list=new NewGameConfirmationList();right.AddChild(list);
        list.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,rightSource,text.SourceLocale,
            text.Get("detail_unknown"),ReadAdmission,scrollSpeed,value=>offsets[CurrentTab]=value);
        if(extras.CollectionEligible==false)
        {
            var notice=new UiSourceLayout(repository,0x0C6A5ACD);Vector2 mount=rightSource.Rectangle(3).Position;
            notice.Blur(right,1,notice.Rectangle(1,mount));
            notice.Art(right,2,notice.Rectangle(2,mount),chrome.Assets,chrome.SpriteShader);
            // This LCSK pane supplies its vertex colours directly, like an
            // authored text profile. Runtime palettes are explicit replacements.
            var chip=LcskSpriteNodeReader.Read(notice.Source.Layout,notice.Nodes[3]);
            AreaMarkerColor Tint(int corner)
            {
                uint packed=System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(
                    chip.RawTintBytes.AsSpan(corner*4,4));
                return new(packed,packed);
            }
            notice.Art(right,3,notice.Rectangle(3,mount),chrome.Assets,chrome.SpriteShader,
                new MapCornerColors(Tint(0),Tint(1),Tint(2),Tint(3)));
            notice.Text(right,4,mount,data[0x6BF].Text??throw new InvalidDataException("Confirmation notice is missing."),
                chrome.Font,MapTextPresets.Create()[54]);
        }
        if(extras.UnresolvedInput is {} missing)SetMeta("confirmation_unresolved_inputs",missing);
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        SelectTab(0);SetBusy(false);
    }

    private void BuildForceSummary(GameResourceRepository repository,PreviewText text,UiSourceLayout main,Control parent,NewGameForcePreview force)
    {
        var source=new UiSourceLayout(repository,0x3AB8DAB2);Vector2 mount=main.Rectangle(4).Position;
        foreach(int node in new[]{2,3,4,5,6,13,17,21,25,29,33})
        {
            if(source.Nodes[node].Type==6)source.Blur(parent,node,source.Rectangle(node,mount));
            else source.Art(parent,node,source.Rectangle(node,mount),chrome.Assets,chrome.SpriteShader);
        }
        var data=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1].Records;
        int[] nodes=[12,16,19,23,28,31], ids=[0x20C,0x20D,0x20E,0x21B,0x207,0x218];
        var presets=MapTextPresets.Create();
        for(int i=0;i<nodes.Length;i++)source.Text(parent,nodes[i],mount,data[ids[i]].Text
            ??throw new InvalidDataException("Confirmation force caption is missing."),chrome.Font,presets[13]);
        string Number(int? value)=>value?.ToString(CultureInfo.InvariantCulture)??text.Get("detail_unknown");
        source.Text(parent,8,mount,force.RulerName??text.Get("detail_unknown"),chrome.Font,presets[27]);
        source.Text(parent,9,mount,force.DoctrineName??text.Get("detail_unknown"),chrome.Font,presets[41]);
        string[] values=[Number(force.Gold),Number(force.Food),Number(force.Troops),force.RankName??text.Get("detail_unknown"),
            Number(force.CityCount),Number(force.ActiveOfficerCount)];
        int[] valueNodes=[11,15,20,24,27,32];
        for(int i=0;i<valueNodes.Length;i++)source.Text(parent,valueNodes[i],mount,values[i],chrome.Font,presets[0]);
        if(force.FlagAppearance is {} appearance)
        {
            var flag=new UiSourceLayout(repository,0xEBC7990C);Vector2 flagMount=source.Rectangle(7,mount).Position;
            foreach(int node in new[]{2,3,4})
            {
                var fill=appearance.Fill;
                flag.Art(parent,node,flag.Rectangle(node,flagMount),chrome.Assets,chrome.SpriteShader,
                    node==3?new MapCornerColors(fill,fill,fill,fill):null);
            }
            var letter=flag.Text(parent,5,flagMount,force.ForceDisplayName??appearance.Glyph,chrome.Font);
            uint colour=MapSpritePalettes.Create().ForceLetter(0).CpuPacked;
            var profile=MapFontProfile.FromNode(flag.Nodes[5],flag.Source.Source.ResourceUri);
            letter.Configure(chrome.Font,profile with{ColourLT=colour,ColourRT=colour,ColourLB=colour,ColourRB=colour});
        }
    }
    private bool ReadAdmission()=>!abandoned&&!busy&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    private void SelectTab(int tab)
    {
        if(abandoned)return;
        offsets[CurrentTab]=list.Offset;
        CurrentTab=tab;for(int i=0;i<tabs.Length;i++)tabs[i].Selected=i==tab;
        list.Bind(rows[tab],offsets[tab]);
    }
    internal void RequestBack(){if(ReadAdmission())returned?.Invoke();}
    internal void RequestConfirm(){if(ReadAdmission())confirmed?.Invoke();}
    internal void SetBusy(bool value)
    {busy=value;chrome.ApplyReturn(!value);chrome.ApplyDecision(!value,true);abort.Disabled=value;foreach(var tab in tabs)tab.Disabled=value;}
    internal void Abandon()
    {abandoned=true;admitted=null;returned=null;confirmed=null;aborted=null;list?.Abandon();chrome?.Abandon();Hide();}
}
