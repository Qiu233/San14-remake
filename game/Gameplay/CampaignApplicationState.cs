namespace San14.Client.Gameplay;

/// <summary>Transient state of one attached campaign. Captured readers remain
/// bound to this owner after a replacement campaign has been constructed.</summary>
internal sealed class CampaignApplicationState(San14.Core.ApplicationGameplayState gameplay)
{
    private bool attached = true;

    internal bool? ReturnToTitlePending => attached ? gameplay.ReturnToTitlePending : null;

    // Normal new-game/load routes do not construct a tutorial controller or
    // special-mode controller. A detached owner cannot answer for a new campaign.
    internal bool? TutorialBlocksAreaReports => attached ? gameplay.Context?.TutorialBlocksAreaReports : null;
    internal bool? TutorialRestrictsFacilities => attached ? gameplay.Context?.TutorialRestrictsFacilities : null;
    internal bool? SpecialModeSuppressesSceneryReport => attached ? gameplay.Context?.SpecialModeSuppressesSceneryReport : null;
    internal bool? SpecialModeSuppressesAssociationReport => attached ? gameplay.Context?.SpecialModeSuppressesAssociationReport : null;

    internal void RequestReturnToTitle()
    {
        if (!attached) throw new InvalidOperationException("The campaign has been detached.");
        gameplay.RequestReturnToTitle();
    }

    // The existing load coordinator does not yet establish the native selected
    // commit/failure lifecycle. A failed load must not manufacture a false value.
    internal void BeginLoad() => gameplay.BeginLoad();

    internal void Detach(San14.Core.ApplicationGameplayState? retainedGameplay = null)
    {
        attached = false;
        //A new host wrapper can retain this same application after a worker
        //publication or an explicit reattachment. Retiring the old wrapper
        //must not retire the current application's shared rule inputs.
        if (!ReferenceEquals(gameplay, retainedGameplay)) gameplay.Detach();
    }
}
