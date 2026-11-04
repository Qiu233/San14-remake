using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class NormalCombatReactionWorkspace(GameplaySession session, GameState world,
        MapSceneView map, CampaignPeriodPlayback? playback, IReadOnlyList<ArmyViewPresentationHead> prefix)
    {
        internal readonly object RenderOwner = new();
        internal readonly GameplaySession Session = session;
        internal readonly GameState World = world;
        internal readonly MapSceneView Map = map;
        internal readonly CampaignPeriodPlayback? Playback = playback;
        internal readonly IReadOnlyList<ArmyViewPresentationHead> Prefix = prefix;
    }
    private NormalCombatReactionWorkspace? normalCombatReactions;
    private GameState? normalCombatReactionFailedWorld;
    private bool HasNormalCombatTargetReactions => normalCombatReactions is not null;
    private bool IsNormalCombatTargetReactionsCurrent => normalCombatReactions is { } owner && IsCombatReactionCurrent(owner);
    private bool IsCombatReactionCurrent(NormalCombatReactionWorkspace owner) =>
        ReferenceEquals(normalCombatReactions, owner) && ReferenceEquals(_session, owner.Session) &&
        ReferenceEquals(_residentMap, owner.Map) && !_failed && !_shuttingDown &&
        (owner.Playback is { } playback ? ReferenceEquals(_periodPlayback, playback) && ReferenceEquals(playback.Candidate, owner.World) :
            ReferenceEquals(PersonnelPresentationOwner(owner.Session), owner.World));

    private bool BeginNormalCombatTargetReactions(GameState world, MapSceneView map, CampaignPeriodPlayback? playback = null)
    {
        var prefix = ArmyViewPresentationPlaybackRules.TargetReactionPrefix(world);
        if (prefix.Count == 0 || ReferenceEquals(normalCombatReactionFailedWorld, world)) return false;
        if (normalCombatReactions is not null) return true;
        var owner = new NormalCombatReactionWorkspace(_session ?? throw new InvalidOperationException("combat-reaction.session"),
            world, map, playback, prefix);
        normalCombatReactions = owner;
        if (playback is not null) playback.TargetReactionPresentationOpen = true;
        _ = Present();
        return true;
        async Task Present()
        {
            try
            {
                await map.PresentNormalCombatTargetReactions(owner.RenderOwner, world, prefix, playback);
                if (!ReferenceEquals(normalCombatReactions, owner)) return;
                if (!IsCombatReactionCurrent(owner)) throw new InvalidOperationException("combat-reaction.source_owner_changed");
                bool accepted = playback is not null ? playback.TryCompleteTargetReactions(world, prefix) :
                    owner.Session.TryCompleteNormalCombatTargetReactions(world, prefix);
                if (!accepted) throw new InvalidOperationException("combat-reaction.source_prefix_receipt");
                normalCombatReactions = null;
                if (playback is not null)
                {
                    playback.TargetReactionPresentationOpen = false;
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
            catch (OperationCanceledException) when (!ReferenceEquals(normalCombatReactions, owner)) { }
            catch (Exception error)
            {
                if (!ReferenceEquals(normalCombatReactions, owner)) return;
                normalCombatReactions = null;
                if (playback is not null) playback.FailPresentation(error);
                else { normalCombatReactionFailedWorld = world; _personnelPresentationInspectedOwner = world; ReportPlayFailure(error); }
            }
        }
    }

    private void RetireNormalCombatTargetReactions(CampaignPeriodPlayback? playback = null)
    {
        if (normalCombatReactions is not { } owner || playback is not null && !ReferenceEquals(owner.Playback, playback)) return;
        normalCombatReactions = null;
        if (GodotObject.IsInstanceValid(owner.Map)) owner.Map.RetireNormalCombatTargetReactions(owner.RenderOwner);
        if (owner.Playback is not null) owner.Playback.TargetReactionPresentationOpen = false;
    }
}
