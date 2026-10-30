using Godot;
using San14.Data;

internal partial class DeploymentUnitInformationView
{
    private Control? tacticChoices;
    private readonly List<(DeploymentTacticButton Button,bool Allowed)> tacticButtons=[];
    private Action<int>? chooseTactic;
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

    internal void ApplyTacticChoices(DeploymentTacticChoices? query,Action<int>? choose)
    {
        if(abandoned)return;
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
    {chooseTactic=null;RefreshTacticButtons();}
}
