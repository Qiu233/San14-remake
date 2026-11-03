using Godot;
using San14.Data;

internal partial class DeploymentUnitInformationView
{
    private Control? tacticChoices;
    private readonly List<(DeploymentTacticButton Button,bool Allowed)> tacticButtons=[];
    private Action<int>? chooseTactic;
    private CollectionTacticBalloonView tacticBalloon=null!;
    private Func<bool>? tacticBalloonAdmission;
    private readonly Dictionary<Control,AreaAdministrationQuery<DeploymentTacticBalloon>?> tacticBalloonOwners=[];
    private bool tutorialTacticButtonsEnabled=true;
    internal bool TacticSelectionPresent=>!abandoned&&tacticChoices is { } choices&&
        !choices.IsQueuedForDeletion()&&choices.IsInsideTree()&&choices.IsVisibleInTree();
    internal bool TacticSelectionActive=>TacticSelectionPresent&&CanProcess()&&chooseTactic is not null&&
        tacticButtons.Any(item=>!item.Button.Disabled);
    internal bool TacticButtonsEnabled=>TacticSelectionPresent&&tutorialTacticButtonsEnabled;
    internal IReadOnlyList<int> DisplayedTacticIds {get;private set;}=[];
    internal IReadOnlyList<int> SelectedTacticIds {get;private set;}=[];
    internal int? TutorialTacticMarkerId {get;private set;}
    internal bool TutorialTacticMarkerAnimationDeferred=>TutorialTacticMarkerId.HasValue;

    private void ConfigureTacticBalloon()
    {
        tacticBalloon=new CollectionTacticBalloonView {Name="DeploymentTacticBalloon"};
        GetParent<Control>().AddChild(tacticBalloon);
        tacticBalloon.Configure(repository,assets,shader,font);
        tacticBalloon.Name="DeploymentTacticBalloon";
        VisibilityChanged+=()=>{if(!IsVisibleInTree())tacticBalloon.Hide();};
        TreeExiting+=()=>
        {
            tacticBalloonOwners.Clear();tacticBalloonAdmission=null;
            if(GodotObject.IsInstanceValid(tacticBalloon))
            {tacticBalloon.Hide();tacticBalloon.QueueFree();}
        };
    }

    internal void ApplyTacticChoices(DeploymentTacticChoices? query,Action<int>? choose,
        Func<bool>? canShowBalloon=null)
    {
        if(abandoned)return;
        tacticBalloon.Hide();tacticBalloonOwners.Clear();tacticBalloonAdmission=canShowBalloon;
        chooseTactic=choose;tacticButtons.Clear();DisplayedTacticIds=[];SelectedTacticIds=[];
        if(tacticChoices is { } previous)
        {previous.Hide();previous.Name="PreviousTacticChoices";previous.QueueFree();tacticChoices=null;}
        if(query?.Choices is not { } choices)return;
        tacticChoices=new Control {Name="DeploymentTacticChoices",MouseFilter=MouseFilterEnum.Ignore};AddChild(tacticChoices);
        DisplayedTacticIds=Array.AsReadOnly(choices.Select(choice=>choice.TacticId).ToArray());
        SelectedTacticIds=Array.AsReadOnly(choices.Where(choice=>choice.Selected).Select(choice=>choice.TacticId).ToArray());
        foreach(var choice in choices)
        {
            int node=91+choice.Occurrence*2;var box=source.Rectangle(node);
            var button=new DeploymentTacticButton {Name="DeploymentTactic"+choice.Occurrence,
                Position=box.Position,Size=box.Size,Selected=choice.Selected};
            tacticChoices.AddChild(button);
            button.Configure(repository,assets,shader,font,choice,index=>
            {if(!abandoned&&tutorialTacticButtonsEnabled)chooseTactic?.Invoke(index);});
            tacticButtons.Add((button,choice.Enabled));
            tacticBalloonOwners.Add(button,choice.Balloon);
        }
        RefreshTacticButtons();
    }

    /// <summary>The lesson changes the real controls' input state. Marker92's
    /// source arrow animation is deferred; it never substitutes a tactic choice.</summary>
    internal bool TryApplyTutorialTacticCue(int? markerId,bool enabled)
    {
        if(!TacticSelectionPresent||!CanProcess()||chooseTactic is null||tacticButtons.Count==0)return false;
        tutorialTacticButtonsEnabled=enabled;TutorialTacticMarkerId=markerId;
        RefreshTacticButtons();
        SetMeta("tutorial_tactic_marker_animation_deferred",markerId.HasValue);
        return tacticButtons.All(item=>item.Button.Disabled==(!enabled||!item.Allowed));
    }

    private void RefreshTacticButtons()
    {
        foreach(var item in tacticButtons)
            item.Button.Disabled=abandoned||chooseTactic is null||!tutorialTacticButtonsEnabled||!item.Allowed;
    }

    private void AbandonTacticInteraction()
    {
        chooseTactic=null;tacticBalloonAdmission=null;tacticBalloonOwners.Clear();
        tacticBalloon.Hide();RefreshTacticButtons();
    }

    private void RefreshTacticBalloon(Control? hovered)
    {
        // Disabled candidates still own the original explanatory balloon.
        // Use actual GUI picking; a retained button behind a child cannot win.
        if(abandoned||!IsVisibleInTree()||!CanProcess()||HasCalculator||
            tacticBalloonAdmission?.Invoke()!=true||hovered is null||
            hovered.IsQueuedForDeletion()||!hovered.IsVisibleInTree()||
            !tacticBalloonOwners.TryGetValue(hovered,out var query)||query?.Value is not { } value)
        {tacticBalloon.Hide();return;}
        tacticBalloon.Display(value.Body,value.DisabledReason);
    }
}
