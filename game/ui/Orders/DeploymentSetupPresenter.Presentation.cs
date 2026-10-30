using San14.Data;

internal sealed partial class DeploymentSetupPresenter
{
    private readonly Dictionary<(int Officer,int Formation),AreaAdministrationQuery<DeploymentUnitPanePublication>> unitPanes=[];
    private int? unitSpeechOfficer;
    private AreaAdministrationQuery<DeploymentUnitSpeechPublication>? unitSpeech;

    //7FE630 prepares speech once on the actual selected-commander change.
    //Troop/tactic edits and redraws retain it; A -> B -> A draws again.
    private void RefreshUnitPresentation(int? officer,int? formation,bool? specialNavalText)
    {
        if(unitSpeechOfficer!=officer)
        {
            unitSpeechOfficer=officer;
            unitSpeech=officer is int person?
                rules.PrepareDeploymentUnitSpeech(world,person,text.SourceLocale,nextApplication):null;
        }
        AreaAdministrationQuery<DeploymentUnitPanePublication>? pane=null;
        if(officer is int id&&formation is int land)
        {
            var key=(id,land);
            if(!unitPanes.TryGetValue(key,out pane))
                unitPanes[key]=pane=rules.QueryDeploymentUnitPane(world,id,land);
        }
        view.UnitInformation.ApplyUnitPresentation(pane,unitSpeech,specialNavalText);
    }

    private void AbandonUnitPresentations()
    {unitSpeechOfficer=null;unitSpeech=null;unitPanes.Clear();}
}
