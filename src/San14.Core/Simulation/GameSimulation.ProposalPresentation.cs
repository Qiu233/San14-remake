namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Only the actual host may receipt the reached proposal display
    /// or audio operation. Earlier same-source unknown work remains pending.</summary>
    public bool TryCompleteProposalPresentation(GameState expectedOwner, long batchSequence, int requestIndex)
    {
        if (!TryReadSourcePresentationHead(expectedOwner, CampaignEffectSource.ProposalCommands,
            batchSequence, requestIndex, out var owner, out var pending, out var effects,
            out int batch, out var request)) return false;
        bool supported = request.ProposalResultTable is not null || request.ProposalOfficerDetail is not null || request.TalentCommandPresentation is
            { Operation: TalentCommandPresentationOperation.SoundCue or TalentCommandPresentationOperation.Announcement or
                TalentCommandPresentationOperation.ConditionalOfficerText } || request.MonthlyMaintenance is
            { Operation: MonthlyMaintenanceOperation.PersonnelSceneEnter, EventKind: 9 or 10 or 32 or 35 or 44 or 54 } or
            { Operation: MonthlyMaintenanceOperation.PersonnelSceneExit };
        if (!supported) return false;
        var scene = request.MonthlyMaintenance?.Operation switch
        {
            MonthlyMaintenanceOperation.PersonnelSceneEnter => new MonthlySceneState(MonthlySceneFamily.Personnel, request.MonthlyMaintenance.EventKind!.Value),
            MonthlyMaintenanceOperation.PersonnelSceneExit => null,
            _ => owner.ActiveMonthlyScene
        };
        CompleteSourcePresentationHead(owner, pending, effects, batch, requestIndex, scene, owner.CoalitionCameraPresentation);
        return true;
    }
}
