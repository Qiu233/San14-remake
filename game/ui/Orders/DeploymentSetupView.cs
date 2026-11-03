using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

internal enum DeploymentSetupBatchAction { Automatic, TroopsTogether, MaximumTroops }

/// <summary>The original unit-setup page. An unsubmitted presenter owns its
/// selection and edits; this view does not publish campaign orders.</summary>
internal partial class DeploymentSetupView : Control
{
    private bool ownsResources;
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Control canvas = null!;
    private UiSourceHeader sourceHeader = null!;
    private Action? close;
    private DeploymentResourceHeader resourceHeader=null!;
    internal AdministrationPageChrome.Resources SourceResources => new(assets!,font!,shader!);
    internal UiSourceCommonBlur CommonBackdrop { get; private set; } = null!;


    private DeploymentSummaryList summaries=null!;
    private UiSourceRoundButton confirm=null!,returnButton=null!;
    private string returnHint="";
    private bool returnHintActive;
    private Action? decide;
    private UiSourceText hintText=null!;
    private string defaultHint="";
    private string normalHint="",insufficientGoldHint="";
    private string noRowsHint="",insufficientTroopsHint="",disabledTroopsHint="";
    private UiSourceOneLineBalloon? balloon;
    private Control? balloonOwner;
    private readonly List<UiSourceBottomButton> batchButtons=[];
    private Action<DeploymentSetupBatchAction>? batchRequested;
    private bool abandoned;
    private Control? selectionChild;
    private bool selectionCanvasVisible,selectionBackdropVisible;
    internal DeploymentUnitInformationView UnitInformation {get;private set;}=null!;
    internal DeploymentOfficerInformationView OfficerInformation {get;private set;}=null!;
    internal void RequestClose() {if(!abandoned&&selectionChild is null)close?.Invoke();}
    internal void Abandon()
    {
        abandoned=true;selectionChild=null;close=null;decide=null;batchRequested=null;OfficerInformation?.Abandon();
        foreach(var button in batchButtons)button.Disabled=true;
        balloonOwner=null;balloon?.DisplayText(null);ClearReturnHint();
        Hide();
    }

    internal void Configure(GameResourceRepository repository, PreviewText text, CityState city,
        long availableTroops, long? books, Action closed, Action edit, AdministrationPageChrome.Resources? resources = null)
    {
        Name = "DeploymentSetup";
        ZIndex = 3;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        close = closed;

        ownsResources = resources is null;
        assets = resources?.Assets ?? new(repository);
        font = resources?.Font ?? new(repository, text.SourceLocale);
        shader = resources?.SpriteShader ?? UiSourceSprite.CreateShader();

        CommonBackdrop = new UiSourceCommonBlur(); AddChild(CommonBackdrop); CommonBackdrop.Configure(repository,assets.SourceLayout(0xAEF1609E));
        canvas = new Control { Name = "SourceCanvas", Size = new(1920,1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException($"Missing deployment message {id}.");
        var page = assets.SourceLayout(0xFC796D9B);
        sourceHeader = new UiSourceHeader { Name = "DeploymentTitle" };
        canvas.AddChild(sourceHeader);
        sourceHeader.Configure(repository,assets,shader,font,Message(0x059F));
        resourceHeader=new DeploymentResourceHeader {Name="DeploymentResourceHeader",Position=page.Rectangle(1).Position};
        canvas.AddChild(resourceHeader);
        resourceHeader.Configure(repository,assets,shader,font,text,city,availableTroops,books);
        page.Blur(canvas, 39, page.Rectangle(39));
        Art(page, 40);
        summaries=new DeploymentSummaryList {Name="DeploymentSummaries"};
        canvas.AddChild(summaries);
        summaries.Configure(repository,assets,shader,font,[],null,0,_=>{},_=>{},edit);
        Vector2 unitMount = page.Rectangle(51).Position;
        var unitPanel=new DeploymentUnitInformationView {Name="UnitInformation",Position=unitMount};
        UnitInformation=unitPanel;canvas.AddChild(unitPanel);unitPanel.Configure(repository,assets,shader,font,text,this);
        var officerPanel=new DeploymentOfficerInformationView {Name="OfficerInformation",Position=unitMount,Visible=false};
        OfficerInformation=officerPanel;canvas.AddChild(officerPanel);
        officerPanel.Configure(repository,assets,shader,font,text,this);
        var tabs=new List<Action>();
        Tab(0x172EDC24, page.Rectangle(41), Message(0x035D), true);
        Tab(0xD45873AA, page.Rectangle(43), Message(0x035E), false);
        confirm=Round(new(827,955), "000_cmn_txt_decision_kettei", disabled:true, ()=>decide?.Invoke());
        returnButton=Round(new(965,955), "000_cmn_txt_decision_modoru", disabled:false, RequestClose, cue:UiAudioCue.Cancel);
        returnButton.MouseEntered+=RefreshReturnHint;
        returnButton.MouseExited+=ClearReturnHint;
        int[] bottomMessages=[0x032F,0x005F,0x0060];
        string[] batchNames=["AutomaticDeployment","SetAllDeploymentTroops","MaximizeDeploymentTroops"];
        for(int i=0;i<3;i++)
        {
            var action=(DeploymentSetupBatchAction)i;
            var button=new UiSourceBottomButton {Name=batchNames[i],Position=new(189+160*i,948),Disabled=true};
            canvas.AddChild(button);batchButtons.Add(button);
            button.Configure(repository,assets,shader,font,Message(bottomMessages[i]),()=>batchRequested?.Invoke(action));
            if(i>0)
            {
                button.MouseEntered+=()=>{if(button.Disabled)ApplyBalloon(button,disabledTroopsHint);};
                button.MouseExited+=()=>ApplyBalloon(button,null);
            }
        }
        var hint = assets.SourceLayout(0xAB6ADAA7);
        // The common footer's narration is a separate child at y912.
        foreach(var n in hint.Nodes.Where(n=>n.Type is 1 or 3)) Art(hint,n.Index,new(0,912));
        // This source narration has one literal and no dynamic substitution.
        // Game messages retain bytecode; Text is reserved for data tables.
        var narration=repository.LoadMessages(text.SourceLocale,MessageKind.Game).Messages.Sections[2].Records[0x085F];
        string prompt=narration.Literals.Single().Text;
        returnHint=new AdministrationMessages(repository,text.SourceLocale).Format(0x2733);
        normalHint=defaultHint=prompt;
        insufficientGoldHint=repository.LoadMessages(text.SourceLocale,MessageKind.Game).Messages.Sections[2]
            .Records[0x08AB].Literals.Single().Text;
        var gameMessages=repository.LoadMessages(text.SourceLocale,MessageKind.Game).Messages.Sections[2];
        noRowsHint=disabledTroopsHint=gameMessages.Records[0x0866].Literals.Single().Text;
        insufficientTroopsHint=gameMessages.Records[0x0867].Literals.Single().Text;
        var hintNode=hint.Nodes.Single(n=>n.Type==2);
        hintText=hint.Text(canvas,hintNode.Index,new(0,912),prompt,font);
        balloon=new UiSourceOneLineBalloon();canvas.AddChild(balloon);balloon.Configure(repository,assets,shader,font);
        SetMeta("source_layout",page.Source.Source.ResourceUri);
        SetMeta("source_city_id",city.Id);
        SetMeta("deployment_available_troops",availableTroops);
        SetMeta("deployment_empty",true);
        VisibilityChanged+=()=>{if(!IsVisibleInTree())ClearReturnHint();};
        Resized += Fit; Fit();

        void Art(UiSourceLayout layout,int index,Vector2 mount=default) => layout.Art(canvas,index,layout.Rectangle(index,mount),assets,shader);
        UiSourceRoundButton Round(Vector2 position,string caption,bool disabled,Action? action,UiAudioCue cue=UiAudioCue.ButtonSelect)
        {
            var button=new UiSourceRoundButton { Name=disabled?"ConfirmDeployment":"ReturnDeployment",Position=position,Disabled=disabled };
            canvas.AddChild(button);
            button.Configure(repository,assets,shader,text.SourceLocale,caption,action,cue);
            return button;
        }
        void Tab(uint file,Rect2 target,string caption,bool selected)
        {
            var button=new UiSourceTabButton {Name=selected?"UnitTab":"OfficerTab",Position=target.Position,Size=target.Size,
                Selected=selected};
            canvas.AddChild(button);
            button.Configure(repository,assets,shader,font,caption,file);
            bool isUnit=selected;
            tabs.Add(()=>button.Selected=isUnit?unitPanel.Visible:officerPanel.Visible);
            button.Pressed+=()=>
            {
                unitPanel.Visible=isUnit;officerPanel.Visible=!isUnit;
                foreach(var refresh in tabs)refresh();
            };
        }
    }

    internal void RegisterHeader(UiSourceHeaderStack owner) => sourceHeader.Publish(owner);

    // Selection children are actual siblings of this canvas. Keep the page
    // itself alive and visible so their own admission/lifetime remains real.
    internal void BeginChildSelection(Control owner, bool keepBackdrop = false)
    {
        if(abandoned||selectionChild is not null||!IsInsideTree()||IsQueuedForDeletion())
            throw new InvalidOperationException("The deployment setup cannot acquire another selection child.");
        if(!ReferenceEquals(owner.GetParent(),this))
            throw new InvalidOperationException("The deployment selection child must belong to this setup.");
        selectionCanvasVisible=canvas.Visible;selectionBackdropVisible=CommonBackdrop.Visible;
        selectionChild=owner;balloonOwner=null;balloon?.DisplayText(null);ClearReturnHint();
        canvas.Hide();if(!keepBackdrop)CommonBackdrop.Hide();
    }

    internal void EndChildSelection(Control owner, bool restore = true)
    {
        if(!ReferenceEquals(selectionChild,owner))return;
        selectionChild=null;
        if(!restore||abandoned||IsQueuedForDeletion()||!IsInsideTree())return;
        if(GodotObject.IsInstanceValid(canvas)&&!canvas.IsQueuedForDeletion())canvas.Visible=selectionCanvasVisible;
        if(GodotObject.IsInstanceValid(CommonBackdrop)&&!CommonBackdrop.IsQueuedForDeletion())CommonBackdrop.Visible=selectionBackdropVisible;
    }

    internal void ApplyDraftResources(DeploymentDraftResources resources)
    {
        resourceHeader.Apply(resources);
        defaultHint=resources.Gold is {RemainingGold:<0}?insufficientGoldHint:normalHint;
        if(!returnHintActive)hintText.Text=defaultHint;
    }
    internal void ApplyHint(string? value)
    {returnHintActive=false;hintText.Text=value??defaultHint;}
    internal void AddAutomaticMenu(DeploymentAutomaticMenu menu)=>canvas.AddChild(menu);
    internal void ApplyBalloon(Control owner,string? value)
    {
        if(abandoned)return;
        if(value is not null){balloonOwner=owner;balloon!.DisplayText(value);}
        else if(balloonOwner==owner){balloonOwner=null;balloon!.DisplayText(null);}
    }
    internal void ApplyBatchActions(bool automaticEnabled,bool togetherEnabled,bool maximumEnabled,
        Action<DeploymentSetupBatchAction> requested,bool hasRows=false)
    {
        if(abandoned)return;
        batchRequested=requested;
        disabledTroopsHint=hasRows?insufficientTroopsHint:noRowsHint;
        bool[] enabled=[automaticEnabled,togetherEnabled,maximumEnabled];
        for(int index=0;index<batchButtons.Count;index++)batchButtons[index].Disabled=!enabled[index];
        RefreshBatchBalloon();
    }

    private void RefreshBatchBalloon()
    {
        if(abandoned||balloon is null)return;
        // 8A1710 reads the UI manager's actual hover identity every frame.
        // An unchanged pointer must also observe a changed registration or
        // reason; rectangle containment would ignore intervening dialogs.
        var hovered=GetViewport().GuiGetHoveredControl();
        for(int index=1;index<batchButtons.Count;index++)
        {
            var button=batchButtons[index];
            if(hovered==button&&button.Disabled&&button.IsVisibleInTree())
            {ApplyBalloon(button,disabledTroopsHint);return;}
        }
        if(balloonOwner is UiSourceBottomButton owner&&batchButtons.Contains(owner))
            ApplyBalloon(owner,null);
    }

    private void RefreshReturnHint()
    {
        if(abandoned||hintText is null||returnButton is null)return;
        bool hovered=selectionChild is null&&IsVisibleInTree()&&canvas.IsVisibleInTree()&&CanProcess()&&
            returnButton.IsVisibleInTree()&&!returnButton.Disabled&&GetViewport().GuiGetHoveredControl()==returnButton;
        if(!hovered){ClearReturnHint();return;}
        if(returnHintActive)return;
        returnHintActive=true;hintText.Text=returnHint;
    }

    private void ClearReturnHint()
    {
        // An old exit must not erase a later writer of the footer channel.
        if(!returnHintActive)return;
        returnHintActive=false;if(hintText is not null)hintText.Text=defaultHint;
    }

    public override void _Process(double delta)
    {RefreshBatchBalloon();RefreshReturnHint();}

    internal void ApplyDraftSummaries(GameResourceRepository repository,IReadOnlyList<DeploymentSummaryRowContent> contents,
        int? selectedOfficer,Action<int> selected,Action<int> removed,Action add,int scrollSpeedSetting=0,Func<int>? currentScrollSpeed=null)
    {
        int scroll=summaries.ScrollValue,index=summaries.GetIndex();
        summaries.Hide();summaries.Name="PreviousDeploymentSummaries";summaries.QueueFree();
        summaries=new DeploymentSummaryList {Name="DeploymentSummaries"};canvas.AddChild(summaries);canvas.MoveChild(summaries,index);
        summaries.Configure(repository,assets!,shader!,font!,contents,selectedOfficer,scrollSpeedSetting,selected,removed,add,currentScrollSpeed);
        summaries.SetScrollValue(scroll);SetMeta("deployment_empty",contents.Count==0);
    }

    internal void ApplyDraftTroops(int officerId,int troops,DeploymentDraftFigures? figures,IReadOnlyList<string>? tacticNames=null)=>
        summaries.ApplyTroops(officerId,troops,figures,tacticNames);
    internal void ApplyDraftDecision(bool enabled,Action accepted)
    {
        decide=accepted;confirm.Disabled=!enabled;
    }


    private void Fit()
    {
        if(canvas is null)return;
        float factor=Math.Min(Size.X/1920,Size.Y/1080);
        canvas.Scale=Vector2.One*factor;
        canvas.Position=(Size-new Vector2(1920,1080)*factor)/2;
    }
    public override void _ExitTree()
    {
        returnHintActive=false;selectionChild=null;close=null;decide=null;batchRequested=null;CommonBackdrop=null!;
        if (ownsResources) { font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
    }
}
