using Godot;
using San14.Data;
using San14.Core;

public partial class Main
{
    // A menu opens an editor; it never submits an order. Existing session
    // permissions and rule queries still decide whether a draft can execute.
    private IReadOnlyDictionary<MapMenuCommand, Action> BuildMapMenuEditors()
    {
        MapMenuCommand[] available = [MapMenuCommand.Deploy, MapMenuCommand.WithdrawAll, MapMenuCommand.Policy, MapMenuCommand.Office,
            MapMenuCommand.Search, MapMenuCommand.Recruit, MapMenuCommand.Items, MapMenuCommand.Reward, MapMenuCommand.Expel,
            MapMenuCommand.Merit, MapMenuCommand.Award, MapMenuCommand.Employ, MapMenuCommand.Learn, MapMenuCommand.Introduce,
            MapMenuCommand.CreateDistrict, MapMenuCommand.EditDistrict, MapMenuCommand.Research,
            MapMenuCommand.EditGroup, MapMenuCommand.MoveGroup,
            MapMenuCommand.ProvokeWar, MapMenuCommand.Incite, MapMenuCommand.Alienate, MapMenuCommand.Poison,
            MapMenuCommand.PlacateArea, MapMenuCommand.Sabotage, MapMenuCommand.Rumor,
            MapMenuCommand.Goodwill, MapMenuCommand.Alliance, MapMenuCommand.CancelAlliance, MapMenuCommand.RequestAttack,
            MapMenuCommand.ReturnCaptives,MapMenuCommand.DemandSurrender,MapMenuCommand.Exchange,
            MapMenuCommand.Proposals, MapMenuCommand.Events, MapMenuCommand.CreatedEvents, MapMenuCommand.Trade, MapMenuCommand.Spy, MapMenuCommand.Scout, MapMenuCommand.Patrol];
        return available.ToDictionary(command => command,
            command => (Action)(() => RunUiAction(() => OpenMapMenuEditor(command))));
    }

    private void OpenMapMenuEditor(MapMenuCommand command)
    {
        var before=MainCommandLaunchOwner(command);
        OpenMapMenuEditorCore(command);
        var after=MainCommandLaunchOwner(command);
        if(after is not null&&!ReferenceEquals(before,after))AcknowledgeMainCommandLaunch(command);
    }

    private void OpenMapMenuEditorCore(MapMenuCommand command)
    {
        if (!TutorialEntryAllowed) return;
        if (HasMapSystemMenu || HasCampaignSavePage || HasCommandWorkspace) return;
        if (command == MapMenuCommand.Events) { OpenCampaignEventCommand(); return; }
        if (command == MapMenuCommand.CreatedEvents) { OpenCreatedEvents(); return; }
        if (command == MapMenuCommand.Proposals) { OpenProposals(); return; }
        if (command == MapMenuCommand.Trade) { OpenForeignTradeOrders(); return; }
        if (command is MapMenuCommand.Spy or MapMenuCommand.Scout or MapMenuCommand.Patrol)
        { OpenIntelligenceOrders(command); return; }
        if (command is MapMenuCommand.EditGroup or MapMenuCommand.MoveGroup)
        { OpenPersonnelGroups(command == MapMenuCommand.EditGroup); return; }
        if (command == MapMenuCommand.Research) { OpenDoctrineResearch(); return; }
        if (command is MapMenuCommand.ProvokeWar or MapMenuCommand.Incite or MapMenuCommand.Alienate or
            MapMenuCommand.Poison or MapMenuCommand.PlacateArea or MapMenuCommand.Sabotage or MapMenuCommand.Rumor)
        {
            OpenStrategicOrders(command switch
            {
                MapMenuCommand.ProvokeWar => 10, MapMenuCommand.Incite => 11, MapMenuCommand.Alienate => 12,
                MapMenuCommand.Poison => 13, MapMenuCommand.PlacateArea => 14, MapMenuCommand.Sabotage => 20,
                _ => 24
            });
            return;
        }
        if (command is MapMenuCommand.CreateDistrict or MapMenuCommand.EditDistrict)
        { OpenDistrictAdministration(command == MapMenuCommand.EditDistrict); return; }
        if (command is MapMenuCommand.Reward or MapMenuCommand.Expel or MapMenuCommand.Merit or MapMenuCommand.Award or
            MapMenuCommand.Employ or MapMenuCommand.Learn or MapMenuCommand.Introduce)
        { OpenPersonnelAdministration(command); return; }
        if (command == MapMenuCommand.Deploy) { OpenCampaignDeployment(); return; }
        if (command == MapMenuCommand.WithdrawAll) { OpenCampaignRetreat(); return; }
        if (command == MapMenuCommand.Office)
        {
            OpenOfficerOffices();
            return;
        }
        if (command is MapMenuCommand.Goodwill or MapMenuCommand.Alliance or MapMenuCommand.CancelAlliance or MapMenuCommand.RequestAttack or
            MapMenuCommand.ReturnCaptives or MapMenuCommand.DemandSurrender or MapMenuCommand.Exchange &&
            _world?.NativeArmySimulation is not null)
        {
            OpenDiplomacyOrders(command switch
            {
                MapMenuCommand.Goodwill => DiplomaticMissionKind.Goodwill,
                MapMenuCommand.Alliance => DiplomaticMissionKind.Alliance,
                MapMenuCommand.CancelAlliance => DiplomaticMissionKind.CancelAlliance,
                MapMenuCommand.ReturnCaptives=>DiplomaticMissionKind.ReturnCaptives,
                MapMenuCommand.DemandSurrender=>DiplomaticMissionKind.DemandSurrender,
                MapMenuCommand.Exchange=>DiplomaticMissionKind.Exchange,
                _ => DiplomaticMissionKind.RequestAttack
            });
            return;
        }
        if (command is MapMenuCommand.Search or MapMenuCommand.Recruit && _world?.NativeArmySimulation is not null)
        {
            OpenTalentOrders(command == MapMenuCommand.Search ? TalentOrderKind.Search : TalentOrderKind.Recruit);
            return;
        }
        if (command == MapMenuCommand.Items && _world?.NativeArmySimulation is not null)
        {
            OpenPersonnelItems();
            return;
        }
        if (command == MapMenuCommand.Policy && _world?.NativeArmySimulation is not null)
        {
            OpenGovernanceRoster();
            return;
        }
        PlayableCommandDeck.Page page = command switch
        {
            MapMenuCommand.Deploy => PlayableCommandDeck.Page.Military,
            MapMenuCommand.Policy => PlayableCommandDeck.Page.Governance,
            MapMenuCommand.Search or MapMenuCommand.Recruit or MapMenuCommand.Items => PlayableCommandDeck.Page.Personnel,
            MapMenuCommand.Goodwill or MapMenuCommand.Alliance or MapMenuCommand.CancelAlliance or MapMenuCommand.RequestAttack => PlayableCommandDeck.Page.Diplomacy,
            _ => throw new InvalidOperationException($"Map command has no editor: {command}.")
        };
        OpenCommandWorkspace(page);
        if (!HasCommandWorkspace) return;
        Control? focus = command switch
        {
            MapMenuCommand.Search => _playTalentSearch,
            MapMenuCommand.Recruit => _playTalentRecruit,
            MapMenuCommand.Policy => _playGovernanceSlot,
            MapMenuCommand.Deploy => _playOfficer,
            _ => null
        };
        if (focus is not null) _commandWorkspace?.FocusCommand(focus);
    }
}
