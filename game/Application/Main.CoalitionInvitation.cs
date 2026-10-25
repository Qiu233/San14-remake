using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private DeploymentTargetConfirmation? _coalitionInvitation;
    private GameplaySession? _coalitionInvitationSession;
    private NormalPeriodOperation? _coalitionInvitationPending;
    private MapSceneView? _coalitionInvitationMap;
    private bool HasCoalitionInvitation => _coalitionInvitation is not null;
    private bool IsCoalitionInvitationCurrent => _coalitionInvitation is not null &&
        ReferenceEquals(_session, _coalitionInvitationSession) &&
        ReferenceEquals(_session?.PendingNormalPeriod, _coalitionInvitationPending) &&
        ReferenceEquals(_residentMap, _coalitionInvitationMap);

    private void OpenCoalitionInvitation(GameplaySession session, NormalPeriodOperation pending,
        CampaignComposition composition, MapSceneView map, int player, CoalitionInvitationRequest request)
    {
        if (request.PlayerForceId != player)
            throw new InvalidDataException("The coalition invitation does not belong to the local player.");
        var question = new DeploymentTargetConfirmation();
        _coalitionInvitation = question; _coalitionInvitationSession = session;
        _coalitionInvitationPending = pending; _coalitionInvitationMap = map;
        AddChild(question);
        try
        {
            RetirePeriodReportView(); CloseActionReport();
            // 216200 presents the formatted 5E88 directly through the common
            // Yes/No window; its operand actors do not add a spoken portrait.
            question.ConfigureWrappedMessage(_repository!, _ui, request.QuestionTextAtSource, accepted => RunUiAction(() =>
            {
                if (!IsCoalitionInvitationCurrent || _advancingPeriod || _failed || _shuttingDown) return;
                var order = new ConfirmCoalitionInvitationOrder(player, request.TargetForceId, accepted);
                CloseCoalitionInvitation();
                _advancingPeriod = true;
                _periodAdvanceTask = AdvancePlayableAsync(session, composition, campaignChoice: order);
            }));
            question.Name = "CoalitionInvitation"; question.ZIndex = 96;
            map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true);
            SetMapFocus(false); HideDeploymentHud();
        }
        catch { CloseCoalitionInvitation(); throw; }
    }

    private void CloseCoalitionInvitation()
    {
        var question = _coalitionInvitation; var map = _coalitionInvitationMap;
        _coalitionInvitation = null; _coalitionInvitationSession = null;
        _coalitionInvitationPending = null; _coalitionInvitationMap = null;
        if (question is not null)
        {
            question.Abandon(); question.GetParent()?.RemoveChild(question); question.QueueFree();
        }
        if (map is not null && GodotObject.IsInstanceValid(map) && ReferenceEquals(_residentMap, map))
        {
            map.SetDetailsOpen(false);
            if (!_failed && !_shuttingDown) SetMapFocus(_mapFocused);
        }
    }
}
