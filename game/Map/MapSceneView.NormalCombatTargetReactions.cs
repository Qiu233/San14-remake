using San14.Core;
using San14.Client.Gameplay;

public partial class MapSceneView
{
    // This owner contains only immutable commands and presentation identities.
    // A native worker never receives the checkpoint World or a closure over it.
    //Scalar facts from the exact source head and World used to create a
    //command. Retain no World/current/Unit in the asynchronous native batch.
    private sealed record TargetReactionSourceFacts(CampaignEffectSource Source, long BatchSequence,
        int RequestIndex, int? SourcePhysicalSlot, bool PresenceAvailable, bool RowExists,
        int? PresencePhysicalSlot, bool? ArmyValid, bool? IsExcluded, bool? IsAtHome,
        int? HomeLocationId, bool? VisibleSetContains);
    private sealed class TargetReactionBatch(object owner, D3D11MapSurface surface,
        D3D11ArmyPublication publication, D3D11ArmyTargetReaction[] commands,
        TargetReactionSourceFacts[] sourceFacts)
    {
        internal readonly object Owner = owner;
        internal readonly D3D11MapSurface Surface = surface;
        internal readonly D3D11ArmyPublication Publication = publication;
        internal readonly D3D11ArmyTargetReaction[] Commands = commands;
        internal readonly TargetReactionSourceFacts[] SourceFacts = sourceFacts;
        internal readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private TargetReactionBatch? targetReactionBatch;
    private ulong nextTargetReactionId;

    internal Task PresentNormalCombatTargetReactions(object owner, GameState world,
        IReadOnlyList<ArmyViewPresentationHead> prefix, CampaignPeriodPlayback? playback = null)
    {
        if (targetReactionBatch is not null) throw new InvalidOperationException("combat-reaction.batch_already_owned");
        if (!IsPlayableMapReady || !UseD3D11Map || _d3d11Army is null ||
            (playback is not null ? !OwnsPeriodArmyViewBorrow(playback, world) : _periodMapActive) ||
            _d3d11Map is not { IsClosed: false, LastError: null } surface)
            throw new InvalidDataException("combat-reaction.map_owner");
        // The absent fact and publication come from the exact same World.
        // A previous receipt may already have replaced its outbox owner.
        var publication = playback is null ? _d3d11Army.Capture(world) :
            ReferenceEquals(_periodBorrowedCandidate, world) && _periodMapFrame?.Army is { } captured ? captured :
                _d3d11Army.CapturePlaybackTargets(world);
        // The publication has already captured this immutable World's presence.
        // Reuse that same owner, then read only the requested receiver's validity.
        var presence = CaptureCurrentArmyPresence(world);
        var commands = new List<D3D11ArmyTargetReaction>(prefix.Count);
        var sourceFacts = new List<TargetReactionSourceFacts>(prefix.Count);
        foreach (var head in prefix)
        {
            var reaction = head.Request.NormalCombatTargetReaction ?? throw new InvalidDataException("combat-reaction.prefix");
            // A known false32FB90 input is the original no-op. Position,
            // visibility, culling and absent labels do not prove no model view.
            bool sourceExists = world.Units.TryGetValue(reaction.ArmyId, out var sourceUnit);
            var row = presence?.Armies.GetValueOrDefault(reaction.ArmyId);
            if (sourceExists && row is not null && row.ArmyValid == false) continue;
            commands.Add(new(checked(++nextTargetReactionId), reaction.ArmyId, reaction.Variant,
                SourceArmyAbsent: !sourceExists));
            sourceFacts.Add(new(head.Source, head.BatchSequence, head.RequestIndex,
                sourceUnit?.NativeArmyOrigin?.NativeSlotIndex, presence is not null, row is not null,
                row?.ArmyPhysicalSlot, row?.ArmyValid, row?.IsExcluded, row?.IsAtHome, row?.HomeLocationId,
                presence is null ? null : presence.VisibleUnitIds.Contains(reaction.ArmyId)));
        }
        if (commands.Count == 0) return Task.CompletedTask;
        var batch = new TargetReactionBatch(owner, surface, publication, commands.ToArray(), sourceFacts.ToArray());
        targetReactionBatch = batch;
        ScheduleD3D11MapFrame();
        return batch.Completion.Task;
    }

    private D3D11ArmyPublication? WithNormalCombatTargetReactions(D3D11ArmyPublication? publication)
        => targetReactionBatch is { } batch ? batch.Publication.WithTargetReactions(batch.Commands) :
            publication?.WithTargetReactions([]);

    private void AcceptNormalCombatTargetReactions(D3D11MapSurface surface, D3D11MapInput input, D3D11MapReceipt receipt)
    {
        if (targetReactionBatch is not { } batch || !ReferenceEquals(batch.Surface, surface) ||
            input.ArmyWorld?.TargetReactions is not { } sent || !sent.SequenceEqual(batch.Commands)) return;
        if (receipt.TargetReactions.Count != batch.Commands.Length)
        { FailNormalCombatTargetReactions(new InvalidDataException("combat-reaction.receipt_count")); return; }
        for (int i = 0; i < batch.Commands.Length; i++)
        {
            var expected = batch.Commands[i]; var actual = receipt.TargetReactions[i];
            if (actual.RequestId != expected.RequestId || actual.ArmyId != expected.ArmyId || actual.Variant != expected.Variant)
            { FailNormalCombatTargetReactions(new InvalidDataException("combat-reaction.receipt_identity")); return; }
            if (actual.Status is not (D3D11ArmyTargetReactionStatus.Accepted or D3D11ArmyTargetReactionStatus.KnownNoView) ||
                actual.Status == D3D11ArmyTargetReactionStatus.KnownNoView && !expected.SourceArmyAbsent)
            {
                var source = batch.SourceFacts[i];
                Godot.GD.PushWarning($"combat-reaction.source_facts army={expected.ArmyId}; request={expected.RequestId}; source={source.Source}; batch={source.BatchSequence}; index={source.RequestIndex}; sourcePhysical={source.SourcePhysicalSlot?.ToString() ?? "Unknown"}; presence={source.PresenceAvailable}; row={source.RowExists}; presencePhysical={source.PresencePhysicalSlot?.ToString() ?? "Unknown"}; valid={source.ArmyValid?.ToString() ?? "Unknown"}; excluded={source.IsExcluded?.ToString() ?? "Unknown"}; atHome={source.IsAtHome?.ToString() ?? "Unknown"}; home={source.HomeLocationId?.ToString() ?? "Unknown"}; visibleSet={source.VisibleSetContains?.ToString() ?? "Unknown"}; capturedRevision={batch.Publication.Revision}; capturedGeneration={batch.Publication.ResourceContextGeneration}");
                var sentWorld = input.ArmyWorld!;
                var sourceIssue = sentWorld.Unavailable.FirstOrDefault(row => row.UnitId == expected.ArmyId)?.Reason;
                string actorKeys = string.Join(" | ", sentWorld.Actors.Where(row => row.Key.UnitId == expected.ArmyId)
                    .Take(20).Select(row => row.Key.ToString()));
                bool hasMode = sentWorld.UnitModelModes.TryGetValue(expected.ArmyId, out uint mode);
                bool hasLand = sentWorld.UnitSelectedFormations.TryGetValue(expected.ArmyId, out uint land);
                bool hasNaval = sentWorld.UnitNavalFormations.TryGetValue(expected.ArmyId, out uint naval);
                Godot.GD.PushWarning($"combat-reaction.sent_owner army={expected.ArmyId}; request={expected.RequestId}; revision={sentWorld.Revision}; generation={sentWorld.ResourceContextGeneration}; center={sentWorld.UnitCenters.ContainsKey(expected.ArmyId)}; centers={sentWorld.UnitCenters.Count}; mode={(hasMode ? mode.ToString() : "Unknown")}; land={(hasLand ? land.ToString() : "Unknown")}; naval={(hasNaval ? naval.ToString() : "Unknown")}; unavailable={sourceIssue ?? "None"}; actorKeys={actorKeys}");
                FailNormalCombatTargetReactions(new InvalidDataException(
                    $"combat-reaction.native_owner_unavailable Army={expected.ArmyId} variant={expected.Variant} request={expected.RequestId} status={actual.Status}"));
                return;
            }
        }
        targetReactionBatch = null;
        batch.Completion.TrySetResult();
    }

    private void FailNormalCombatTargetReactions(Exception failure)
    {
        var batch = targetReactionBatch; targetReactionBatch = null;
        batch?.Completion.TrySetException(failure);
    }
    internal void RetireNormalCombatTargetReactions(object? owner = null)
    {
        if (targetReactionBatch is not { } batch || owner is not null && !ReferenceEquals(batch.Owner, owner)) return;
        targetReactionBatch = null;
        batch.Completion.TrySetCanceled();
    }
}
