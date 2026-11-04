using Godot;
using San14.Data;

/// <summary>A retained, already prepared caravan report. The reward settlement
/// belongs to its pending task; advancing text never performs it again.</summary>
internal partial class ForeignTradeReportView : Control
{
    private GameResourceRepository repository=null!;
    private PreviewText text=null!;
    private AdministrationPageChrome.Resources? resources;
    private ForeignTradeReturnReportPublication publication=null!;
    private LocalizedUiSpriteSource sprites=null!;
    private Func<bool>? admitted;
    private Func<AutomaticMessageAdvanceMode>? automatic;
    private Action? completed;
    private Func<ForeignTradeReportStep,Control>? rewardPanel;
    private readonly Dictionary<int,IReadOnlyList<LoadedSprite>> actors=[];
    private readonly Dictionary<(string Resource,string Sprite),IReadOnlyList<LoadedSprite>> portraits=[];
    private DeploymentNarrationView? message;
    private Control? reward;
    private bool abandoned,finished;
    internal int StepIndex {get;private set;}
    internal DeploymentNarrationView? Message=>message;
    internal Func<InputEvent,bool>? KeyboardInput {get;set;}
    internal Action<ForeignTradeReportStep>? StepEntered {get;set;}

    internal void Configure(GameResourceRepository repository,PreviewText text,
        ForeignTradeReturnReportPublication publication,Func<bool> admitted,
        Func<AutomaticMessageAdvanceMode> automatic,Func<ForeignTradeReportStep,Control> rewardPanel,Action completed,
        AdministrationPageChrome.Resources? resources=null)
    {
        this.repository=repository;this.text=text;this.publication=publication;this.admitted=admitted;
        this.resources=resources;
        this.automatic=automatic;this.completed=completed;this.rewardPanel=rewardPanel;
        sprites=new(repository);Name="ForeignTradeReturnReport";ZIndex=20;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);PresentStep();
    }
    private bool ReadAdmission()=>!abandoned&&!finished&&IsInsideTree()&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    private void PresentStep()
    {
        if(abandoned||finished)return;
        if(StepIndex>=publication.Steps.Count)
        {finished=true;var callback=completed;completed=null;callback?.Invoke();return;}
        var step=publication.Steps[StepIndex];
        if(step.Kind==ForeignTradeReportStepKind.Speech)
        {
            if(step.SpeakerChannel is not (3 or 4))
                throw new InvalidDataException("foreign-trade.report.current_speaker_channel");
            if(step.Portrait is { } portrait)
            {
                var key=(portrait.ResourceName,portrait.SpriteNames[0]);
                if(!portraits.TryGetValue(key,out var layers))
                {layers=portrait.LoadSprites(sprites.LoadTexturePackSprite);portraits.Add(key,layers);}
                actors[step.SpeakerChannel]=layers;
            }
            else actors.Remove(step.SpeakerChannel); //The publication knows this speaker is not native-valid.
        }
        else
        {
            actors.Clear();
            if(step.Kind==ForeignTradeReportStepKind.Reward&&step.MessageId!=0x32E0)
            {reward=rewardPanel!(step);AddChild(reward);}
        }
        var page=new DeploymentNarrationView {KeyboardInput=input=>KeyboardInput?.Invoke(input)??false};
        message=page;AddChild(page);
        StepEntered?.Invoke(step);
        if(step.Kind==ForeignTradeReportStepKind.Speech)
            page.ConfigureRecruitConversation(repository,text,step.SpeakerName,step.Text,
                actors.GetValueOrDefault(3)??[],actors.GetValueOrDefault(4)??[],step.SpeakerChannel==3,
                Consumed,automatic,ReadAdmission,resources:resources);
        else page.ConfigureAnnouncement(repository,text,step.Text,Consumed,automatic,ReadAdmission,resources:resources);
        SetMeta("source_message",step.MessageId);
        SetMeta("source_report_task",publication.TaskId);

        void Consumed()
        {
            if(!ReadAdmission()||message!=page)return;
            message=null;page.Abandon();page.QueueFree();
            if(reward is { } panel){reward=null;panel.Hide();panel.QueueFree();}
            StepIndex++;PresentStep();
        }
    }
    internal void Abandon()
    {
        abandoned=true;admitted=null;automatic=null;completed=null;rewardPanel=null;KeyboardInput=null;
        StepEntered=null;message?.Abandon();actors.Clear();portraits.Clear();Hide();
    }
}
