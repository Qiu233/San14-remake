using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class ArmyViewRetirementWorkspace(GameplaySession session, GameState world,
        MapSceneView map, ArmyViewPresentationHead head, CampaignPeriodPlayback? playback)
    {
        internal readonly object RenderOwner = new();
        internal readonly GameplaySession Session = session;
        internal readonly GameState World = world;
        internal readonly MapSceneView Map = map;
        internal readonly ArmyViewPresentationHead Head = head;
        internal readonly CampaignPeriodPlayback? Playback = playback;
    }
    private ArmyViewRetirementWorkspace? armyViewRetirement;
    private GameState? armyViewRetirementFailedWorld;
    private bool HasArmyViewRetirement => armyViewRetirement is not null;
    private bool IsArmyViewRetirementCurrent => armyViewRetirement is { } owner && IsArmyViewRetirementOwnerCurrent(owner);
    private bool IsArmyViewRetirementOwnerCurrent(ArmyViewRetirementWorkspace owner) =>
        ReferenceEquals(armyViewRetirement, owner) && ReferenceEquals(_session, owner.Session) &&
        ReferenceEquals(_residentMap, owner.Map) && !_failed && !_shuttingDown &&
        (owner.Playback is { } period ? ReferenceEquals(_periodPlayback, period) && ReferenceEquals(period.Candidate, owner.World) :
            ReferenceEquals(PersonnelPresentationOwner(owner.Session), owner.World));

    private bool BeginArmyViewRetirement(GameState world, MapSceneView map, ArmyViewPresentationHead head,
        CampaignPeriodPlayback? playback = null)
    {
        if (head.Request.ArmyViewRetirement is not { } request || ReferenceEquals(armyViewRetirementFailedWorld, world)) return false;
        if (armyViewRetirement is not null) return true;
        var owner = new ArmyViewRetirementWorkspace(_session ?? throw new InvalidOperationException("army-view-retirement.session"),
            world, map, head, playback);
        armyViewRetirement = owner;
        if (playback is not null) playback.ArmyViewRetirementPresentationOpen = true;
        _ = Present();
        return true;
        async Task Present()
        {
            try
            {
                await map.PresentArmyViewRetirement(owner.RenderOwner, world, request, playback);
                if (!ReferenceEquals(armyViewRetirement, owner)) return;
                if (!IsArmyViewRetirementOwnerCurrent(owner)) throw new InvalidOperationException("army-view-retirement.source_owner_changed");
                bool completed = playback is not null ? playback.TryCompleteArmyViewRetirement(world, head) :
                    owner.Session.TryCompleteArmyViewRetirement(world, head.BatchSequence, head.RequestIndex, request);
                if (!completed) throw new InvalidOperationException("army-view-retirement.source_head_receipt");
                armyViewRetirement = null;
                // Native accepted the real view/retirement-manager command.
                // Its actual model clock continues without a Core animation wait.
                if (playback is not null)
                {
                    playback.ArmyViewRetirementPresentationOpen = false;
                    PublishPeriodPlayback(playback, map, playback.Candidate!);
                }
                else
                {
                    SynchronizePersonnelPresentationOwner(owner.Session);
                    UpdateResidentMap();
                    RefreshPlayablePeriodReport(); RefreshPlayControls();
                    Callable.From(() => { TryOpenPersonnelPresentation(); }).CallDeferred();
                }
            }
            catch (OperationCanceledException) when (!ReferenceEquals(armyViewRetirement, owner)) { }
            catch (Exception error)
            {
                if (!ReferenceEquals(armyViewRetirement, owner)) return;
                armyViewRetirement = null;
                if (playback is not null) playback.FailPresentation(error);
                else { armyViewRetirementFailedWorld = world; _personnelPresentationInspectedOwner = world; ReportPlayFailure(error); }
            }
        }
    }

    private void RetireArmyViewRetirement(CampaignPeriodPlayback? playback = null)
    {
        if (armyViewRetirement is not { } owner || playback is not null && !ReferenceEquals(owner.Playback, playback)) return;
        armyViewRetirement = null;
        if (GodotObject.IsInstanceValid(owner.Map)) owner.Map.RetireArmyViewRetirement(owner.RenderOwner);
        if (owner.Playback is not null) owner.Playback.ArmyViewRetirementPresentationOpen = false;
    }
}
