using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private ArmyReturnCaptivePresentation? _automaticCaptivePresentationAttempt;
    private void OpenArmyReturnCaptiveDecision(RulerDecisionWorkspace owner,
        ArmyReturnCaptiveContinuation continuation,int player,Action<GameOrder> submit)
    {
        if(continuation.PendingSelection is {} request)
        {
            var publication=owner.Composition.PrepareArmyQueries(owner.World)
                .QueryArmyReturnCaptiveSelection(owner.World,player);
            var source=publication.Value??throw new InvalidDataException(publication.UnresolvedInput??"captive-selection.current_publication");
            var view=new ArmyReturnCaptiveSelectionView();owner.View=view;owner.Abandon=view.Abandon;AddChild(view);
            view.Configure(_repository!,_ui,TryBorrowApplicationUiResources(view),request,source.Pages,source.AlteredHealth,
                id=>ReadCurrentOfficerDetail(owner.World,id),()=>ReadNeutralDetail(),source.DestinationName,
                source.DestinationGold.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ()=>IsRulerDecisionCurrent&&!_advancingPeriod&&owner.View==view,
                order=>RunUiAction(()=>{if(IsRulerDecisionCurrent&&owner.View==view)submit(order);}),CurrentListScrollSpeed);
            return;

            OfficerDetailPublication ReadNeutralDetail()
            {
                var result=owner.Composition.PrepareArmyQueries(owner.World)
                    .QueryArmyReturnCaptiveNeutralDetail(owner.World,player);
                return result.Value??throw new InvalidDataException(result.UnresolvedInput??"captive-selection.neutral_publication");
            }
        }
        if(continuation.ExecutionConfirmation is {} confirmation)
        {
            var view=new DeploymentTargetConfirmation();owner.View=view;owner.Abandon=view.Abandon;AddChild(view);
            view.ConfigureWrappedMessage(_repository!,_ui,confirmation.TextAtSource,confirmed=>RunUiAction(()=>
            {
                if(IsRulerDecisionCurrent&&owner.View==view&&!_advancingPeriod)
                    submit(new ConfirmArmyReturnCaptiveExecutionOrder(player,confirmation.BatchId,confirmation.Round,confirmed));
            }),TryBorrowApplicationUiResources(view));
            view.Name="ArmyReturnCaptiveExecutionConfirmation";view.ZIndex=97;
            return;
        }
        throw new InvalidDataException("The army return has no displayable captive decision.");
    }
}
