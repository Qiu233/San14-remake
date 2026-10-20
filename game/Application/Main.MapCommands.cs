using Godot;
using San14.Data;

public partial class Main
{
    // A menu opens an editor; it never submits an order. Existing session
    // permissions and rule queries still decide whether a draft can execute.
    private IReadOnlyDictionary<MapMenuCommand, Action> BuildMapMenuEditors()
    {
        MapMenuCommand[] available = [MapMenuCommand.Deploy, MapMenuCommand.Policy,
            MapMenuCommand.Search, MapMenuCommand.Recruit, MapMenuCommand.Items,
            MapMenuCommand.Goodwill, MapMenuCommand.Alliance, MapMenuCommand.CancelAlliance, MapMenuCommand.RequestAttack];
        return available.ToDictionary(command => command,
            command => (Action)(() => RunUiAction(() => OpenMapMenuEditor(command))));
    }

    private void OpenMapMenuEditor(MapMenuCommand command)
    {
        if (HasMapSystemMenu || HasCampaignSavePage) return;
        if (command == MapMenuCommand.Deploy && _playCity.Selected >= 0 && _residentMap is not null)
        {
            OpenDeploymentSetup(_playCity.GetSelectedId());
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
        RefreshPlayControls();
        _playCommandDeck.Select(page);
        SetMapFocus(false);
        Control? focus = command switch
        {
            MapMenuCommand.Search => _playTalentSearch,
            MapMenuCommand.Recruit => _playTalentRecruit,
            MapMenuCommand.Policy => _playGovernanceSlot,
            MapMenuCommand.Deploy => _playOfficer,
            _ => null
        };
        focus?.GrabFocus();
    }
}
