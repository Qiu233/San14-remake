using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private DeploymentTargetConfirmation? _personnelConfirmation;
    private GameplaySession? _personnelConfirmationSession;
    private GameState? _personnelConfirmationWorld;
    private MapSceneView? _personnelConfirmationMap;
    private CommandWorkspaceView? _personnelConfirmationParent;
    private PersonnelItemPage? _personnelConfirmationItemParent;
    private ProcessModeEnum _personnelConfirmationBrowserMode;
    private bool HasPersonnelConfirmation => _personnelConfirmation is not null || HasSelfRecommendation || HasCoalitionInvitation || HasRulerDecision;

    private bool TryAdoptPersonnelItemConfirmationSpeechWorld(PersonnelItemPage page, GameplaySession session,
        GameState previous, GameState replacement)
    {
        if (_personnelConfirmation is null) return true;
        if (_personnelConfirmationItemParent != page || _personnelConfirmationSession != session ||
            _personnelConfirmationMap != _personnelItemMap ||
            !ReferenceEquals(_personnelConfirmationWorld, previous)) return false;
        _personnelConfirmationWorld = replacement;
        return true;
    }

    private void RequestPersonnelOrder(GameOrder order, PersonnelItemPage? itemPage = null)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasPersonnelConfirmation ||
            HasMapSystemMenu || HasCampaignSavePage || HasAdministrationPage && (itemPage is null || !IsPersonnelItemPageCurrent(itemPage)) ||
            HasMilitaryOrderPage || HasActionReport ||
            _detailsView is not null || _repository is null || _session is not { PlayerForceId: int player } session ||
            session.World.Phase != TurnPhase.Strategy || order.ForceId != player) return;
        var world = session.World;
        PersonnelChange change;
        string prompt;
        int officer;
        switch (order)
        {
            case GrantItemOrder grant when world.Items?.GetValueOrDefault(grant.ItemId) is { Holder.OfficerId: int previousOwner } grantedItem &&
                world.Officers.TryGetValue(grant.RecipientOfficerId, out var recipient):
                officer = recipient.Id;
                change = new(PersonnelActionKind.GrantItem, officer, grantedItem.Id, previousOwner, officer);
                prompt = _ui.Format("play_personnel_grant_confirm", world.ItemDefinitions[grantedItem.DefinitionId].Name, recipient.Name);
                break;
            case RevokeItemOrder revoke when world.Items?.GetValueOrDefault(revoke.ItemId) is { Holder.OfficerId: int previousHolder } revokedItem &&
                previousHolder == revoke.OwnerOfficerId && world.Officers.TryGetValue(previousHolder, out var holder) &&
                world.Forces[player].RulerId is int ruler:
                officer = holder.Id;
                change = new(PersonnelActionKind.RevokeItem, officer, revokedItem.Id, previousHolder, ruler);
                prompt = _ui.Format("play_personnel_revoke_confirm", holder.Name, world.ItemDefinitions[revokedItem.DefinitionId].Name);
                break;
            default:
                ApplyPlayableOrder(order);
                return;
        }
        var evaluation = session.EvaluatePersonnelChange(change);
        if (evaluation.Allowed != true || evaluation.Quote is not { } quote)
        {
            _playMessage.Text = _ui.CommandError(evaluation.MissingRuleKey ?? "personnel.ineligible");
            return;
        }
        // The ordinary item page decides once. Its original person narration
        // is prepared first; the real transfer follows its return, then the page exits.
        if (itemPage is not null)
        {
            if (!IsPersonnelItemPageCurrent(itemPage)) return;
            BeginPersonnelItemTransfer(order, itemPage);
            return;
        }
        prompt += "\n" + _ui.Format("play_personnel_confirm_cost", quote.CommandBooks, quote.Gold);
        if (world.Officers[officer].Loyalty is int loyalty)
            prompt += "\n" + _ui.Format("play_personnel_confirm_loyalty", loyalty,
                Math.Clamp(checked(loyalty + quote.LoyaltyDelta), 0, 100));
        var view = new DeploymentTargetConfirmation();
        _personnelConfirmation = view; _personnelConfirmationSession = session;
        _personnelConfirmationWorld = world; _personnelConfirmationMap = _residentMap;
        _personnelConfirmationParent = _commandWorkspace;
        _personnelConfirmationItemParent = itemPage;
        _personnelConfirmationBrowserMode = _browserContent.ProcessMode;
        AddChild(view);
        try
        {
            if (_personnelConfirmationParent is { } parent) parent.ProcessMode = ProcessModeEnum.Disabled;
            itemPage?.SetBusy(true);
            _browserContent.ProcessMode = ProcessModeEnum.Disabled;
            view.ConfigureWrappedMessage(_repository, _ui, prompt, accepted => RunPlayAction(() =>
            {
                if (_personnelConfirmation != view) return;
                bool current = ReferenceEquals(_session, session) && _personnelConfirmationSession == session &&
                    ReferenceEquals(_world, _personnelConfirmationWorld) &&
                    ReferenceEquals(session.World, _personnelConfirmationWorld) && _residentMap == _personnelConfirmationMap &&
                    (itemPage is null ? _commandWorkspace == _personnelConfirmationParent :
                        _personnelConfirmationItemParent == itemPage && IsPersonnelItemPageCurrent(itemPage));
                ClosePersonnelConfirmation();
                if (accepted && current)
                {
                    bool applied = ApplyPlayableOrder(order, itemPage is not null ? ClosePersonnelItems : null);
                    if (!applied && itemPage is not null && IsPersonnelItemPageCurrent(itemPage)) itemPage.ApplyHint(_playMessage.Text);
                }
            }));
        }
        catch { ClosePersonnelConfirmation(); throw; }
    }

    private void ClosePersonnelConfirmation(bool resumeParent = true)
    {
        if (_personnelConfirmation is not { } view) return;
        var session = _personnelConfirmationSession; var world = _personnelConfirmationWorld;
        var map = _personnelConfirmationMap; var parent = _personnelConfirmationParent;
        var itemPage = _personnelConfirmationItemParent;
        _personnelConfirmation = null; _personnelConfirmationSession = null; _personnelConfirmationWorld = null;
        _personnelConfirmationMap = null; _personnelConfirmationParent = null; _personnelConfirmationItemParent = null;
        view.Abandon();
        if (view.GetParent() is Node owner) owner.RemoveChild(view);
        view.QueueFree();
        bool current = ReferenceEquals(_session, session) && ReferenceEquals(_world, world) && _residentMap == map;
        if (current) _browserContent.ProcessMode = _personnelConfirmationBrowserMode;
        if (resumeParent && !_failed && !_shuttingDown && current && parent is not null && _commandWorkspace == parent)
            parent.ProcessMode = ProcessModeEnum.Inherit;
        if (resumeParent && current && itemPage is not null && IsPersonnelItemPageCurrent(itemPage)) itemPage.SetBusy(false);
    }
}
