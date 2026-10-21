using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

internal enum DeploymentSetupBatchAction { Automatic, TroopsTogether, MaximumTroops }

/// <summary>The original unit-setup page. An unsubmitted presenter owns its
/// selection and edits; this view does not publish campaign orders.</summary>
internal partial class DeploymentSetupView : Control
{
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Control canvas = null!;
    private Action? close;
    private DeploymentResourceHeader resourceHeader=null!;


    private DeploymentSummaryList summaries=null!;
    private UiSourceRoundButton confirm=null!;
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
    internal DeploymentUnitInformationView UnitInformation {get;private set;}=null!;
    internal void RequestClose() => close?.Invoke();
    internal void Abandon()
    {
        abandoned=true;close=null;decide=null;batchRequested=null;
        foreach(var button in batchButtons)button.Disabled=true;
        balloonOwner=null;balloon?.DisplayText(null);
        Hide();
    }

    internal void Configure(GameResourceRepository repository, PreviewText text, CityState city,
        long availableTroops, long? books, Action closed, Action edit)
    {
        Name = "DeploymentSetup";
        ZIndex = 3;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        close = closed;

        assets = new(repository); font = new(repository, text.SourceLocale);
        shader = UiSourceSprite.CreateShader();
        var textPresets=MapTextPresets.Create();

        canvas = new Control { Name = "SourceCanvas", Size = new(1920,1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        canvas.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
        canvas.AddChild(new MapDialogBackdrop { Size = canvas.Size });
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException($"Missing deployment message {id}.");
        var page = new UiSourceLayout(repository, 0xFC796D9B);
        var header = new UiSourceLayout(repository, 0xDC8AC8AA);
        Art(header, 1);
        header.Text(canvas, 2, default, Message(0x059F), font,textPresets[56]);
        resourceHeader=new DeploymentResourceHeader {Name="DeploymentResourceHeader",Position=page.Rectangle(1).Position};
        canvas.AddChild(resourceHeader);
        resourceHeader.Configure(repository,assets,shader,font,text,city,availableTroops,books);
        Art(page, 40);
        summaries=new DeploymentSummaryList {Name="DeploymentSummaries"};
        canvas.AddChild(summaries);
        summaries.Configure(repository,assets,shader,font,[],null,0,_=>{},_=>{},edit);
        Vector2 unitMount = page.Rectangle(51).Position;
        var unitPanel=new DeploymentUnitInformationView {Name="UnitInformation",Position=unitMount};
        UnitInformation=unitPanel;canvas.AddChild(unitPanel);unitPanel.Configure(repository,assets,shader,font,text,this);
        var officerPanel=new Control { Name="OfficerInformation",Size=canvas.Size,Visible=false,MouseFilter=MouseFilterEnum.Ignore };
        canvas.AddChild(officerPanel);
        var officer=new UiSourceLayout(repository,0xC11503D0);
        foreach(int i in new[]{27,28,29,32,40,46,52,54,60,66,72,74,84})
            officer.Art(officerPanel,i,officer.Rectangle(i,unitMount),assets,shader);
        foreach(var binding in new[]{(30,0x03D2),(16,0x023D),(33,0x042C),(34,0x042D),(35,0x042E),(55,0x02BF),(75,0x02BC),(85,0x02BD)})
            officer.Text(officerPanel,binding.Item1,unitMount,Message(binding.Item2),font,textPresets[13]);
        var personRadar=new UiSourceLayout(repository,0x7E8AD398);
        Vector2 personRadarMount=officer.Rectangle(3,unitMount).Position;
        foreach(int i in new[]{1,4}) personRadar.Art(officerPanel,i,personRadar.Rectangle(i,personRadarMount),assets,shader);
        for(int i=0;i<5;i++)personRadar.Text(officerPanel,5+i*2,personRadarMount,Message(0x0238+i),font,textPresets[13]);
        var tabs=new List<Action>();
        Tab(0x172EDC24, page.Rectangle(41), Message(0x035D), true);
        Tab(0xD45873AA, page.Rectangle(43), Message(0x035E), false);
        confirm=Round(new(827,955), "000_cmn_txt_decision_kettei", disabled:true, ()=>decide?.Invoke());
        Round(new(965,955), "000_cmn_txt_decision_modoru", disabled:false, RequestClose, cue:UiAudioCue.Cancel);
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
        var hint = new UiSourceLayout(repository,0xAB6ADAA7);
        // The common footer's narration is a separate child at y912.
        foreach(var n in hint.Nodes.Where(n=>n.Type is 1 or 3)) Art(hint,n.Index,new(0,912));
        // This source narration has one literal and no dynamic substitution.
        // Game messages retain bytecode; Text is reserved for data tables.
        var narration=repository.LoadMessages(text.SourceLocale,MessageKind.Game).Messages.Sections[2].Records[0x085F];
        string prompt=narration.Literals.Single().Text;
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

    internal void ApplyDraftResources(DeploymentDraftResources resources)
    {
        resourceHeader.Apply(resources);
        defaultHint=resources.Gold is {RemainingGold:<0}?insufficientGoldHint:normalHint;
        hintText.Text=defaultHint;
    }
    internal void ApplyHint(string? value)=>hintText.Text=value??defaultHint;
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

    public override void _Process(double delta)=>RefreshBatchBalloon();

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
        close=null;decide=null;batchRequested=null;
        font?.Dispose(); assets?.Dispose(); shader?.Dispose();
    }
}
