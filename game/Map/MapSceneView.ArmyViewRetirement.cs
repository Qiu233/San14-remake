using San14.Core;
using San14.Client.Gameplay;

public partial class MapSceneView
{
    private sealed class ArmyViewRetirementBatch(object owner, D3D11MapSurface surface,
        D3D11ArmyPublication publication, D3D11ArmyViewRetirement[] commands)
    {
        internal readonly object Owner = owner;
        internal readonly D3D11MapSurface Surface = surface;
        internal readonly D3D11ArmyPublication Publication = publication;
        internal readonly D3D11ArmyViewRetirement[] Commands = commands;
        internal readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private ArmyViewRetirementBatch? armyViewRetirementBatch;
    private ulong nextArmyViewRetirementId;

    internal Task PresentArmyViewRetirement(object renderOwner, GameState world,
        ArmyViewRetirementRequest request, CampaignPeriodPlayback? playback = null)
    {
        if (armyViewRetirementBatch is not null || targetReactionBatch is not null)
            throw new InvalidOperationException("army-view-retirement.command_already_owned");
        if (!IsPlayableMapReady || !UseD3D11Map || _d3d11Army is null ||
            (playback is not null ? !OwnsPeriodArmyViewBorrow(playback, world) : _periodMapActive) ||
            _d3d11Map is not { IsClosed: false, LastError: null } surface)
            throw new InvalidDataException("army-view-retirement.map_owner");
        // The producer has actually removed this Army before publishing its
        // request. Never re-submit the old pre-reset centers as a new view.
        if (world.Units.ContainsKey(request.ArmyId))
            throw new InvalidDataException("army-view-retirement.old_active_army_present");
        var publication = _d3d11Army.CapturePlaybackTargets(world);
        if (publication.UnitCenters.ContainsKey(request.ArmyId))
            throw new InvalidDataException("army-view-retirement.old_active_model_present");
        var commands = new[] { new D3D11ArmyViewRetirement(checked(++nextArmyViewRetirementId), request.ArmyId, request.AnimateRetirement) };
        var batch = new ArmyViewRetirementBatch(renderOwner, surface, publication, commands);
        armyViewRetirementBatch = batch;
        ScheduleD3D11MapFrame();
        return batch.Completion.Task;
    }

    private D3D11ArmyPublication? CaptureArmyViewCommandPublication()
    {
        // A command batch owns its immutable capture. Do not reconstruct a
        // previous committed World while a worker's post-reset borrow is held.
        if (armyViewRetirementBatch is { } retired)
            return retired.Publication.WithTargetReactions([]).WithViewRetirements(retired.Commands);
        if (targetReactionBatch is not null)
            return WithNormalCombatTargetReactions(null)?.WithViewRetirements([]);
        var publication = _periodMapFrame is { } period ? period.Army : _d3d11Army?.Capture(_logicalWorld!);
        return publication?.WithTargetReactions([]).WithViewRetirements([]);
    }

    private void AcceptArmyViewRetirement(D3D11MapSurface surface, D3D11MapInput input, D3D11MapReceipt receipt)
    {
        if (armyViewRetirementBatch is not { } batch || !ReferenceEquals(batch.Surface, surface) ||
            input.ArmyWorld?.ViewRetirements is not { } sent || !sent.SequenceEqual(batch.Commands)) return;
        if (receipt.ViewRetirements.Count != batch.Commands.Length)
        { FailArmyViewRetirement(new InvalidDataException("army-view-retirement.receipt_count")); return; }
        for (int index = 0; index < batch.Commands.Length; index++)
        {
            var expected = batch.Commands[index]; var actual = receipt.ViewRetirements[index];
            if (actual.RequestId != expected.RequestId || actual.ArmyId != expected.ArmyId)
            { FailArmyViewRetirement(new InvalidDataException("army-view-retirement.receipt_identity")); return; }
            if (actual.Status is not (D3D11ArmyViewRetirementStatus.Accepted or D3D11ArmyViewRetirementStatus.KnownNoView))
            {
                FailArmyViewRetirement(new InvalidDataException(
                    $"army-view-retirement.native_owner_unavailable Army={expected.ArmyId} request={expected.RequestId} status={actual.Status}"));
                return;
            }
        }
        armyViewRetirementBatch = null;
        batch.Completion.TrySetResult();
    }

    private void FailArmyViewRetirement(Exception error)
    {
        var batch = armyViewRetirementBatch; armyViewRetirementBatch = null;
        batch?.Completion.TrySetException(error);
    }
    internal void RetireArmyViewRetirement(object? renderOwner = null)
    {
        if (armyViewRetirementBatch is not { } batch || renderOwner is not null && !ReferenceEquals(batch.Owner, renderOwner)) return;
        armyViewRetirementBatch = null;
        batch.Completion.TrySetCanceled();
    }
}
