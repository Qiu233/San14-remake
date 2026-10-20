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
    private Control canvas=null!;
    private DeploymentResourceHeader resourceHeader=null!;
    private DeploymentTargetList selection=null!;
    private UiSourceRoundButton confirm=null!;
    private UiSourceBottomButton facility=null!;
    private Action? decide,back;
    private UiSourceText hintText=null!;
    private string defaultHint="";
    internal IReadOnlyList<int> SelectedOfficers=>selection.SelectedOfficers;
    internal void RequestBack()=>back?.Invoke();

    internal void Configure(GameResourceRepository repository,PreviewText text,CityState city,long availableTroops,
        long? availableBooks,IReadOnlyList<DeploymentTargetGroup> groups,IEnumerable<int> selectedOfficers,
        int scrollSpeedSetting,Action<IReadOnlyList<int>> selectionChanged,Action chooseFacility,Action requestedBack,
        Func<int>? currentScrollSpeed=null)
    {
        Name="DeploymentTarget";ZIndex=3;MouseFilter=MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);back=requestedBack;
        assets=new(repository);font=new(repository,text.SourceLocale);shader=UiSourceSprite.CreateShader();
        canvas=new Control {Name="SourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Ignore};AddChild(canvas);
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        string Message(int id)=>messages.Records[id].Text??throw new InvalidDataException("Missing target-page message.");
        var title=new UiSourceLayout(repository,0xDC8AC8AA);
        title.Art(canvas,1,title.Rectangle(1),assets,shader);
        title.Text(canvas,2,default,Message(0x0223),font,MapTextPresets.Create()[56]).Name="TargetTitle";
        resourceHeader=new DeploymentResourceHeader {Name="DeploymentResourceHeader",Position=new(487,12)};
        canvas.AddChild(resourceHeader);resourceHeader.Configure(repository,assets,shader,font,text,city,availableTroops,availableBooks);
        var footer=new UiSourceLayout(repository,0xD8913AB2);Vector2 footerMount=new(0,904);
        MenuFooterTop=(int)footer.Rectangle(19,footerMount).Position.Y;
        confirm=Round("ConfirmTarget",5,"000_cmn_txt_decision_kettei",()=>{if(!confirm.Disabled)decide?.Invoke();});
        confirm.Disabled=true;
        Round("ReturnTarget",4,"000_cmn_txt_decision_modoru",RequestBack);
        facility=new UiSourceBottomButton {Name="ChooseFacilityTarget",Position=footer.Rectangle(14,footerMount).Position};
        canvas.AddChild(facility);facility.Configure(repository,assets,shader,font,Message(0x0262),chooseFacility);
        selection=new DeploymentTargetList {Name="DeploymentTargets"};canvas.AddChild(selection);
        selection.Configure(repository,assets,shader,font,text,groups,selectedOfficers,scrollSpeedSetting,officers=>
        {facility.Disabled=officers.Count==0;selectionChanged(officers);},currentScrollSpeed);
        facility.Disabled=selection.SelectedOfficers.Count==0;
        var hint=new UiSourceLayout(repository,0xAB6ADAA7);Vector2 hintMount=footer.Rectangle(19,footerMount).Position;
        foreach(var node in hint.Nodes.Where(node=>node.Type is 1 or 3))
            hint.Art(canvas,node.Index,hint.Rectangle(node.Index,hintMount),assets,shader);
        // Global Game message5696 maps to section2/0876. It is a static
        // narration literal, not an index into the UTF-16 Data caption table.
        var narration=repository.LoadMessages(text.SourceLocale,MessageKind.Game).Messages.Sections[2].Records[0x0876];
        defaultHint=narration.Literals.Single().Text;
        hintText=hint.Text(canvas,hint.Nodes.Single(node=>node.Type==2).Index,hintMount,defaultHint,font);hintText.Name="TargetHint";
        // No full-screen blocker, authored blur or fixed pad guide is added.
        // Only the actual interactive controls intercept map pointer input.
        Resized+=Fit;Fit();

        UiSourceRoundButton Round(string name,int slot,string caption,Action pressed)
        {
            var button=new UiSourceRoundButton {Name=name,Position=footer.Rectangle(slot,footerMount).Position,
                ActionMode=BaseButton.ActionModeEnum.Release};canvas.AddChild(button);
            button.Configure(repository,assets,shader,text.SourceLocale,caption,pressed);return button;
        }
    }

    internal void ApplyDraftResources(DeploymentDraftResources resources)=>resourceHeader.Apply(resources);
    internal void ApplyProgress(IReadOnlyList<DeploymentTargetProgress> progress)=>selection.ApplyProgress(progress);
    internal void ApplySelection(IEnumerable<int> officers)
    {selection.ApplySelection(officers);facility.Disabled=selection.SelectedOfficers.Count==0;}
    internal void ApplyDecision(bool enabled,Action requestedDecision)
    {decide=requestedDecision;confirm.Disabled=!enabled;}
    internal void ApplyHint(string? value)=>hintText.Text=value??defaultHint;
    internal void AddMapMenu(DeploymentMoveMenu menu)=>canvas.AddChild(menu);
    internal void AddPolicyPanel(DeploymentPolicyView panel)=>canvas.AddChild(panel);
    internal int MenuFooterTop {get;private set;}

    private void Fit()
    {
        if(canvas is null)return;
        float factor=Math.Min(Size.X/1920,Size.Y/1080);canvas.Scale=Vector2.One*factor;
        canvas.Position=(Size-new Vector2(1920,1080)*factor)/2;
    }
    public override void _ExitTree(){font?.Dispose();assets?.Dispose();shader?.Dispose();}
}
