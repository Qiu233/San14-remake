using Godot;
using San14.Data;
using San14.Data.Formats;
using System.Globalization;

internal enum CampaignDeploymentUiStage { Target, District, Footholds }
internal sealed record CampaignDeploymentBarSegment(float Fraction, MapCornerColors? Colors);
internal sealed record CampaignDeploymentBarValues(long? OtherTroops, long? SelectedTroops,
    IReadOnlyList<CampaignDeploymentBarSegment> Segments, IReadOnlyList<LoadedSprite?> Portraits,
    string AdditionalArmiesText);

/// <summary>The source campaign page over an interactive map. Province,
/// district and foothold picking belong to the retained command draft.</summary>
internal partial class CampaignDeploymentView : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!, bar = null!, fills = null!, faces = null!, numbersHost = null!;
    private Texture2D segmentTexture = null!;
    private UiSourceLayout barLayout = null!;
    private UiSourceHeader header = null!;
    private UiSourceRoundButton firstBack = null!, sourceBack = null!, confirm = null!;
    private UiSourceText hint = null!, otherTroops = null!, selectedTroops = null!, additional = null!;
    private Control arrow = null!;
    private Action? returned, accepted;
    private Func<bool>? admitted;
    private CampaignDeploymentUiStage stage;
    private string defaultHint = "",returnHint = "",emptyHint = "",confirmHint = "";
    private string? hoveredHint;
    private bool abandoned, busy;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        Func<bool> admitted, Action returned, Action accepted)
    {
        this.repository = repository; this.text = text; this.admitted = admitted;
        this.returned = returned; this.accepted = accepted;
        Name = "CampaignDeployment"; ZIndex = 3; MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        canvas = new() { Name = "SourceCanvas", Size = new(1920,1080), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(canvas);
        header = new(); canvas.AddChild(header);
        header.Configure(repository,assets,shader,font,
            repository.LoadMessages(text.SourceLocale,San14.Data.Formats.MessageKind.Data).Messages.Sections[1].Records[0x34].Text
            ?? throw new InvalidDataException("Missing campaign title."));
        var root = new UiSourceLayout(repository,0xA8DC5CD6);
        barLayout = new(repository,0x46F8F9C3);
        bar = new() { Name = "CampaignArmyBar", Position = root.Rectangle(1).Position,
            Size = barLayout.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        canvas.AddChild(bar);
        barLayout.Art(bar,1,barLayout.Rectangle(1),assets,shader);
        barLayout.Art(bar,3,barLayout.Rectangle(3),assets,shader);
        //870B30 resource9 selects the common bar sprite through930580.
        //The authored node4 remains suppressed; it is not this resource.
        segmentTexture=assets.SrgbSprite("000_cmn_bar",new LocalizedUiSpriteSource(repository).LoadCommonSprite("000_cmn_bar"));
        // The authored grd is submitted in the loaded Root hierarchy.
        //870B30 appends its seventeen dynamic panes after those children.
        barLayout.Art(bar,5,barLayout.Rectangle(5),assets,shader);
        fills = new() { Name = "CampaignTroopDistribution", MouseFilter = MouseFilterEnum.Ignore };
        faces = new() { Name = "CampaignFirstCommanders", MouseFilter = MouseFilterEnum.Ignore }; bar.AddChild(faces);
        additional = barLayout.Text(bar,27,default,"",font);
        var numbers = new UiSourceLayout(repository,0xADD1B984);
        numbersHost=new() {Name="CampaignTroopComparison",Position=barLayout.Rectangle(28).Position,
            Size=numbers.Rectangle(0).Size,MouseFilter=MouseFilterEnum.Ignore};
        bar.AddChild(numbersHost);
        var presets=MapTextPresets.Create();
        otherTroops = numbers.Text(numbersHost,1,default,"",font,presets[48]);
        selectedTroops = numbers.Text(numbersHost,2,default,"",font,presets[47]);
        arrow = numbers.Art(numbersHost,3,numbers.Rectangle(3),assets,shader);
        numbers.Art(numbersHost,4,numbers.Rectangle(4),assets,shader);
        bar.AddChild(fills);
        var footer = new UiSourceLayout(repository,0xD8913AB2); Vector2 footerMount = new(0,904);
        firstBack = Button("CampaignReturnFirst",1,"000_cmn_txt_decision_modoru",RequestBack,UiAudioCue.Cancel);
        sourceBack = Button("CampaignReturnSource",4,"000_cmn_txt_decision_modoru",RequestBack,UiAudioCue.Cancel);
        confirm = Button("CampaignConfirm",5,"000_cmn_txt_decision_kettei",()=>
            { if (ReadAdmission() && !confirm.Disabled) this.accepted?.Invoke(); },UiAudioCue.ButtonSelect);
        var guide = new UiSourceLayout(repository,0xAB6ADAA7);
        Vector2 guideMount = footer.Rectangle(19,footerMount).Position;
        foreach (var node in guide.Nodes.Where(node=>node.Type is 1 or 3))
            guide.Art(canvas,node.Index,guide.Rectangle(node.Index,guideMount),assets,shader);
        hint = guide.Text(canvas,guide.Nodes.Single(node=>node.Type==2).Index,guideMount,"",font);
        returnHint = Literal(0x2733); emptyHint = Literal(0x5686); confirmHint = Literal(0x580C);
        Resized += Fit; Fit(); SetBusy(false);
        SetMeta("source_layout",root.Source.Source.ResourceUri);

        string Literal(int id)=>repository.LoadMessages(text.SourceLocale,MessageKind.Game)
            .Messages.Sections[id/10000].Records[id%10000].Literals.Single().Text;

        UiSourceRoundButton Button(string name,int slot,string caption,Action pressed,UiAudioCue cue)
        {
            var button = new UiSourceRoundButton { Name = name, Position = footer.Rectangle(slot,footerMount).Position,
                ActionMode = BaseButton.ActionModeEnum.Release };
            canvas.AddChild(button); button.Configure(repository,assets,shader,text.SourceLocale,caption,pressed,cue);
            return button;
        }
    }

    internal void RegisterHeader(UiSourceHeaderStack owner) => header.Publish(owner);

    internal void Apply(CampaignDeploymentUiStage stage, string guide, bool canConfirm, CampaignDeploymentBarValues values)
    {
        if (abandoned) return;
        this.stage = stage; defaultHint = guide; hoveredHint=null; hint.Text = guide;
        firstBack.Visible = stage != CampaignDeploymentUiStage.Footholds;
        sourceBack.Visible = confirm.Visible = stage == CampaignDeploymentUiStage.Footholds;
        confirm.Disabled = busy || !canConfirm;
        otherTroops.Text = Number(values.OtherTroops);
        selectedTroops.Text = Number(values.SelectedTroops);
        selectedTroops.Visible = arrow.Visible = stage == CampaignDeploymentUiStage.Footholds;
        bool footholds=stage==CampaignDeploymentUiStage.Footholds;
        additional.Text = values.AdditionalArmiesText;additional.Visible=faces.Visible=footholds;
        float sourceWidth=barLayout.Rectangle(4).Size.X;
        int boundary=values.OtherTroops==0?(int)sourceWidth:
            values.SelectedTroops is long own&&values.OtherTroops is long other&&own+other!=0?
                (int)(sourceWidth*((float)own/(own+other))):0;
        numbersHost.Position=new(footholds?boundary-94:796,numbersHost.Position.Y);
        ClearChildren(fills); ClearChildren(faces);
        Rect2 area = barLayout.Rectangle(4);
        float x = area.Position.X;
        foreach (var segment in values.Segments.Take(17))
        {
            int width = (int)(area.Size.X * segment.Fraction);
            if (width > 0 && segment.Colors is { } colors)
            {
                var pane=new UiSourceSprite {Position=new(x,area.Position.Y),Size=new(width,area.Size.Y)};
                fills.AddChild(pane);pane.Configure(shader,segmentTexture,UiSourceSprite.Blend.Ordinary,colors);
            }
            x += width;
        }
        int[] mounts = [6,9,12,15,18,21,24];
        var face = new UiSourceLayout(repository,0x14AA7945);
        for (int index=0;index<Math.Min(7,values.Portraits.Count);index++)
        {
            var host = new Control { Position = barLayout.Rectangle(mounts[index]).Position,
                Size = face.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            faces.AddChild(host); face.Art(host,1,face.Rectangle(1),assets,shader);
            if (values.Portraits[index] is { } sprite)
            {
                var portrait = new UiSourcePortrait(); host.AddChild(portrait);
                portrait.ConfigureCampaign(repository,assets,shader,sprite);
            }
        }
    }

    private string Number(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? text.Get("detail_unknown");
    private bool ReadAdmission() => !abandoned && !busy && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    internal void ApplyHoverHint(string? value) { if (!abandoned) { hoveredHint=value; hint.Text=value??defaultHint; } }
    internal void ApplyNotice(string value) { if (!abandoned) { defaultHint=value; hint.Text=value; } }
    internal void SetBusy(bool value)
    { if (abandoned) return; busy=value; firstBack.Disabled=sourceBack.Disabled=value; if(value)confirm.Disabled=true; }
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    internal void Abandon() { abandoned=true; admitted=null; returned=accepted=null; Hide(); }
    public override void _Process(double delta)
    {
        if(abandoned||!IsVisibleInTree())return;
        var control=GetViewport().GuiGetHoveredControl();
        hint.Text=control==firstBack||control==sourceBack?returnHint:
            stage==CampaignDeploymentUiStage.Footholds&&control==confirm?
                (confirm.Disabled?emptyHint:confirmHint):hoveredHint??defaultHint;
    }
    private static void ClearChildren(Control parent)
    { foreach (Node child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); } }
    private void Fit()
    { float scale=Math.Min(Size.X/1920,Size.Y/1080); canvas.Scale=Vector2.One*scale; canvas.Position=(Size-new Vector2(1920,1080)*scale)/2; }
    public override void _ExitTree() { Abandon(); font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
}
