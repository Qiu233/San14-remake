using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private object? MainCommandLaunchOwner(MapMenuCommand command)=>command switch
    {
        MapMenuCommand.Research=>_doctrineResearch,
        MapMenuCommand.Employ or MapMenuCommand.Merit or MapMenuCommand.Award=>_personnelAdministrationPage,
        MapMenuCommand.Spy or MapMenuCommand.Scout or MapMenuCommand.Patrol=>_intelligenceWorld,
        MapMenuCommand.Rumor=>_strategicOrders,
        _=>null
    };

    private void AcknowledgeMainCommandLaunch(MapMenuCommand command)
    {
        if(_session is not { } session||_campaignComposition is not { } composition)return;
        if(composition.PrepareArmyQueries(session.World).MarkMainCommandFeatureSeen(session.World,command))
            RefreshMainCommandPresentation();
    }

    private void RefreshMainCommandPresentation()
    {
        if(_session is { PlayerForceId:int player } session&&_forceHud is { } hud)
            hud.Refresh(CurrentForceHudView(session.World,player));
    }
}
