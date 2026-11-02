using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>Source target-page composition over an interactive map. The owner
/// supplies actual groups and decisions; this view never submits an order.</summary>
internal partial class DeploymentTargetView : Control
{
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private bool ownsResources;
    private Control canvas=null!;
    private UiSourceHeader sourceHeader=null!;
    private DeploymentResourceHeader? resourceHeader;
    private DeploymentTargetList selection=null!;
    private UiSourceRoundButton confirm=null!;
    private UiSourceRoundButton returnTarget=null!;
    private UiSourceBottomButton facility=null!;
    private Control selectionBlocker=null!;
    private bool facilityMode,decisionEnabled;
    private Action? decide,back;
    private UiSourceText hintText=null!;
    private string defaultHint="";
    private UnitWaypointMarkerLayer? sourceRoute;
    internal AdministrationPageChrome.Resources SourceResources => new(assets!,font!,shader!);
    internal IReadOnlyList<int> SelectedOfficers=>selection.SelectedOfficers;
    internal void RequestBack()=>back?.Invoke();

    internal void Configure(GameResourceRepository repository,PreviewText text,CityState? city,long availableTroops,
        long? availableBooks,IReadOnlyList<DeploymentTargetGroup> groups,IEnumerable<int> selectedOfficers,
        int scrollSpeedSetting,Action<IReadOnlyList<int>> selectionChanged,Action chooseFacility,Action requestedBack,
        Func<int>? currentScrollSpeed=null, bool transportMode=false, GeneralUnitTargetHeader? generalHeader=null,
        AdministrationPageChrome.Resources? resources=null)
    {
        Name="DeploymentTarget";ZIndex=3;MouseFilter=MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);back=requestedBack;
        ownsResources=resources is null;
        assets=resources?.Assets??new(repository);font=resources?.Font??new(repository,text.SourceLocale);
        shader=resources?.SpriteShader??UiSourceSprite.CreateShader();
        canvas=new Control {Name="SourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Ignore};AddChild(canvas);
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        string Message(int id)=>messages.Records[id].Text??throw new InvalidDataException("Missing target-page message.");
        sourceHeader=new UiSourceHeader {Name="TargetTitle"};
        canvas.AddChild(sourceHeader);
        sourceHeader.Configure(repository,assets,shader,font,Message(transportMode?0x033A:0x0223));
        if (generalHeader is not null) ConfigureGeneralHeader(repository, text, generalHeader);
        else
        {
            if (city is null) throw new ArgumentNullException(nameof(city));
            resourceHeader=new DeploymentResourceHeader {Name="DeploymentResourceHeader",Position=new(487,12)};
            canvas.AddChild(resourceHeader);resourceHeader.Configure(repository,assets,shader,font,text,city,availableTroops,availableBooks);
        }
        var footer=assets.SourceLayout(0xD8913AB2);Vector2 footerMount=new(0,904);
        MenuFooterTop=(int)footer.Rectangle(19,footerMount).Position.Y;
        confirm=Round("ConfirmTarget",5,"000_cmn_txt_decision_kettei",()=>{if(!confirm.Disabled)decide?.Invoke();});
        confirm.Disabled=true;
        returnTarget=Round("ReturnTarget",4,"000_cmn_txt_decision_modoru",RequestBack);
        facility=new UiSourceBottomButton {Name="ChooseFacilityTarget",Position=footer.Rectangle(14,footerMount).Position};
        canvas.AddChild(facility);facility.Configure(repository,assets,shader,font,Message(0x0262),chooseFacility);
        facility.Visible=!transportMode;
        selection=new DeploymentTargetList {Name="DeploymentTargets"};canvas.AddChild(selection);
        selection.Configure(repository,assets,shader,font,text,groups,selectedOfficers,scrollSpeedSetting,officers=>
        {facility.Disabled=officers.Count==0;selectionChanged(officers);},currentScrollSpeed);
        facility.Disabled=selection.SelectedOfficers.Count==0;
        selectionBlocker=new Control {Name="FacilitySelectionInputOwner",MouseFilter=MouseFilterEnum.Stop,Visible=false};
        selection.AddChild(selectionBlocker);selectionBlocker.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var hint=assets.SourceLayout(0xAB6ADAA7);Vector2 hintMount=footer.Rectangle(19,footerMount).Position;
        foreach(var node in hint.Nodes.Where(node=>node.Type is 1 or 3))
            hint.Art(canvas,node.Index,hint.Rectangle(node.Index,hintMount),assets,shader);
        // Global Game message5696 maps to section2/0876. It is a static
        // narration literal, not an index into the UTF-16 Data caption table.
        var narration=repository.LoadMessages(text.SourceLocale,MessageKind.Game).Messages.Sections[2].Records[transportMode?0x0879:0x0876];
        defaultHint=narration.Literals.Single().Text;
        hintText=hint.Text(canvas,hint.Nodes.Single(node=>node.Type==2).Index,hintMount,defaultHint,font);hintText.Name="TargetHint";
        // No full-screen blocker, authored blur or fixed pad guide is added.
        // Only the actual interactive controls intercept map pointer input.
        Resized+=Fit;Fit();

        UiSourceRoundButton Round(string name,int slot,string caption,Action pressed)
        {
            var button=new UiSourceRoundButton {Name=name,Position=footer.Rectangle(slot,footerMount).Position,
                ActionMode=BaseButton.ActionModeEnum.Release};canvas.AddChild(button);
            button.Configure(repository,assets,shader,text.SourceLocale,caption,pressed,
                cue:slot is 1 or 4?UiAudioCue.Cancel:UiAudioCue.ButtonSelect);return button;
        }
    }

    internal void RegisterHeader(UiSourceHeaderStack owner)=>sourceHeader.Publish(owner);

    // Opt-in for an actual caller's source footer binding. Existing target
    // callers keep their current hint policy until they explicitly supply it.
    internal void ConfigureReturnHint(string value,Func<bool> admitted)
    {
        returnTarget.MouseEntered+=()=>{if(admitted()&&!returnTarget.Disabled)ApplyHint(value);};
        returnTarget.MouseExited+=()=>{if(admitted())ApplyHint(null);};
    }

    internal void ApplyDraftResources(DeploymentDraftResources resources)=>resourceHeader?.Apply(resources);
    internal void ApplyTransportResources(long? remainingTroops,long? remainingGold,long? remainingSupplies,
        long? currentBooks,long? remainingBooks,int? spentBooks)
        =>resourceHeader?.ApplyTransport(remainingTroops,remainingGold,remainingSupplies,currentBooks,remainingBooks,spentBooks);
    internal void ApplyProgress(IReadOnlyList<DeploymentTargetProgress> progress)=>selection.ApplyProgress(progress);
    internal void ApplySelection(IEnumerable<int> officers)
    {selection.ApplySelection(officers);facility.Disabled=facilityMode||selection.SelectedOfficers.Count==0;}
    internal void ApplyDecision(bool enabled,Action requestedDecision)
    {decide=requestedDecision;decisionEnabled=enabled;confirm.Disabled=facilityMode||!enabled;}
    internal void SetFacilitySelectionMode(bool active,bool placing=false)
    {
        facilityMode=active;confirm.Disabled=active||!decisionEnabled;
        facility.Disabled=active||selection.SelectedOfficers.Count==0;
        returnTarget.Disabled=active&&!placing;selectionBlocker.Visible=active;
        if(sourceRoute is not null)sourceRoute.Visible=!active;
    }
    internal void ApplyHint(string? value)=>hintText.Text=value??defaultHint;
    internal void AddMapMenu(Control menu)=>canvas.AddChild(menu);
    internal void AddPolicyPanel(Control panel)=>canvas.AddChild(panel);
    internal void ConfigureSourceRoute(GameResourceRepository repository,Func<int,Vector2?> project)
    {
        sourceRoute=new();canvas.AddChild(sourceRoute);canvas.MoveChild(sourceRoute,0);
        sourceRoute.Configure(repository,assets!,shader!,font!,project);
    }
    internal void ApplySourceWaypoints(IReadOnlyList<int> points,int? preview=null)=>sourceRoute?.Apply(points,preview);
    internal void Abandon(){decide=null;back=null;sourceRoute?.Abandon();Hide();}
    internal int MenuFooterTop {get;private set;}

    private void Fit()
    {
        if(canvas is null)return;
        float factor=Math.Min(Size.X/1920,Size.Y/1080);canvas.Scale=Vector2.One*factor;
        canvas.Position=(Size-new Vector2(1920,1080)*factor)/2;
    }
    public override void _ExitTree(){if(ownsResources){font?.Dispose();assets?.Dispose();shader?.Dispose();}}
}
