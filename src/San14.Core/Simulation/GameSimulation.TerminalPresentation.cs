namespace San14.Core;

public sealed partial class GameSimulation
{
    private GameState SourcePresentationOwner => _normalPeriod?.Candidate ??
        _progress?.ManualTactic?.Candidate ?? _progress?.ArmyProgress?.Candidate ?? _progress?.Story?.Candidate ?? State;

    private void PublishSourcePresentationOwner(GameState completed, NormalPeriodOperation? pending)
    {
        if (pending is not null) _normalPeriod = pending with { Candidate = completed };
        else if (_progress?.ManualTactic is { } manual) _progress = new(null, manual with { Candidate = completed });
        else if (_progress?.ArmyProgress is { } movement) _progress = new(null, null, movement with { Candidate = completed });
        else if (_progress?.Story is { } story) _progress = new(story with { Candidate = completed }, null);
        else State = completed;
    }

    /// <summary>Records actual acceptance by the music host, preserving source order.</summary>
    public bool TryCompleteMapMusicIntent(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex)
    {
        if (!TerminalSource(expectedSource) || !TryReadSourcePresentationHead(expectedOwner, expectedSource,
            batchSequence, requestIndex, out var owner, out var pending, out var effects, out int batch,
            out var request) || request.MapMusicIntent is null) return false;
        CompleteSourcePresentationHead(owner, pending, effects, batch, requestIndex,
            owner.ActiveMonthlyScene, owner.CoalitionCameraPresentation);
        return true;
    }

    /// <summary>Consumes the exact calendar music selection after actual host acceptance.
    /// A newer selection or a replaced world invalidates an earlier receipt.</summary>
    public bool TryCompletePendingMapMusicIntent(GameState expectedOwner, MapMusicIntent expectedIntent)
    {
        var pending = _normalPeriod;
        var owner = SourcePresentationOwner;
        if (!MapMusicIntentCompletion.TryComplete(owner, expectedOwner, expectedIntent,
            out var completed)) return false;
        PublishSourcePresentationOwner(completed, pending);
        return true;
    }

    /// <summary>Does not perform the operation: the application or audio host must
    /// have accepted this precise operation before issuing its receipt.</summary>
    public bool TryCompleteApplicationExit(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex, ApplicationExitOperation operation)
    {
        if (!Enum.IsDefined(operation) || !TerminalSource(expectedSource) ||
            !TryReadSourcePresentationHead(expectedOwner, expectedSource, batchSequence, requestIndex,
                out var owner, out var pending, out var effects, out int batch, out var request) ||
            request.ApplicationExit?.Operation != operation) return false;
        CompleteSourcePresentationHead(owner, pending, effects, batch, requestIndex,
            owner.ActiveMonthlyScene, owner.CoalitionCameraPresentation);
        return true;
    }

    /// <summary>Records actual camera, still or relation-layer acceptance.
    /// Focus retains the host's real pre-focus camera for saving and restoring.</summary>
    public bool TryCompleteForceCoalitionVisual(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex, ForceCoalitionVisualOperation operation,
        CampaignCameraSnapshot? capturedCamera = null)
    {
        if (expectedSource is not (CampaignEffectSource.MonthlyMaintenance or CampaignEffectSource.StrategicCommands or
            CampaignEffectSource.ArmyBattleDefeat) || !Enum.IsDefined(operation) ||
            !TryReadSourcePresentationHead(expectedOwner, expectedSource, batchSequence, requestIndex,
                out var owner, out var pending, out var effects, out int batch, out var request) ||
            request.ForceCoalitionVisual is not { } visual || visual.Operation != operation ||
            expectedSource == CampaignEffectSource.ArmyBattleDefeat &&
            !(operation == ForceCoalitionVisualOperation.CityProvinceAdvantageIndicatorsRefresh ||
              operation == ForceCoalitionVisualOperation.NarrationEnter && visual.SceneKind == 24 ||
              operation == ForceCoalitionVisualOperation.NarrationExit))
            return false;
        var scene = owner.ActiveMonthlyScene;
        var camera = owner.CoalitionCameraPresentation;
        if (operation != ForceCoalitionVisualOperation.CameraFocus && capturedCamera is not null) return false;
        switch (operation)
        {
            case ForceCoalitionVisualOperation.CameraFocus:
                if (camera is not null || capturedCamera is null || !CoalitionCameraPresentationValidation.Camera(capturedCamera) ||
                    visual.TargetHexId is not (>= 0 and <= 48400)) return false;
                camera = new(capturedCamera, visual.TargetHexId.Value);
                break;
            case ForceCoalitionVisualOperation.CameraRestore:
                if (camera is null) return false;
                camera = null;
                break;
            case ForceCoalitionVisualOperation.NarrationEnter:
                if (visual.SceneKind is not (24 or 40 or 52 or 53 or 54)) return false;
                scene = new(MonthlySceneFamily.Coalition, visual.SceneKind.Value);
                break;
            case ForceCoalitionVisualOperation.NarrationExit:
                if (scene?.Family != MonthlySceneFamily.Coalition) return false;
                scene = null;
                break;
        }
        CompleteSourcePresentationHead(owner, pending, effects, batch, requestIndex, scene, camera);
        return true;
    }

    public bool TryCompleteEmperorScene(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex, MonthlyMaintenanceOperation operation)
    {
        if (!TerminalSource(expectedSource) ||
            operation is not (MonthlyMaintenanceOperation.EmperorSceneEnter or MonthlyMaintenanceOperation.EmperorSceneExit) ||
            !TryReadSourcePresentationHead(expectedOwner, expectedSource, batchSequence, requestIndex,
                out var owner, out var pending, out var effects, out int batch, out var request) ||
            request.MonthlyMaintenance is not { } scene || scene.Operation != operation) return false;
        MonthlySceneState? active;
        if (operation == MonthlyMaintenanceOperation.EmperorSceneEnter)
        {
            if (scene.EventKind is not (6 or 45 or 55) ||
                expectedSource == CampaignEffectSource.ArmyBattleDefeat && scene.EventKind is not (45 or 55) ||
                expectedSource == CampaignEffectSource.DiplomacyCommands && scene.EventKind != 55) return false;
            active = new(MonthlySceneFamily.Emperor, scene.EventKind.Value);
        }
        else
        {
            if (scene.EventKind is not null || owner.ActiveMonthlyScene?.Family != MonthlySceneFamily.Emperor ||
                expectedSource == CampaignEffectSource.DiplomacyCommands && owner.ActiveMonthlyScene.EventKind != 55) return false;
            active = null;
        }
        CompleteSourcePresentationHead(owner, pending, effects, batch, requestIndex, active, owner.CoalitionCameraPresentation);
        return true;
    }

    private static bool TerminalSource(CampaignEffectSource source) =>
        source is CampaignEffectSource.MonthlyMaintenance or CampaignEffectSource.ArmyBattleDefeat or CampaignEffectSource.DiplomacyCommands;

    private bool TryReadSourcePresentationHead(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex, out GameState owner, out NormalPeriodOperation? pending,
        out CampaignEffectRequests effects, out int batchIndex, out CampaignEffectRequest request)
    {
        pending = _normalPeriod;
        owner = SourcePresentationOwner;
        effects = owner.CampaignEffectRequests!;
        batchIndex = -1;
        request = null!;
        if (!ReferenceEquals(owner, expectedOwner) || effects is null) return false;
        // History has already been written. Leave its notification untouched;
        // every other earlier same-source request, including an unknown kind,
        // blocks this receipt until its actual consumer has completed it.
        for (int batch = 0; batch < effects.PendingBatches.Length; batch++)
        {
            var item = effects.PendingBatches[batch];
            if (item.Source != expectedSource) continue;
            for (int index = 0; index < item.Requests.Length; index++)
            {
                if (item.Requests[index].HistoryNotification is not null) continue;
                if (item.Sequence != batchSequence || index != requestIndex) return false;
                batchIndex = batch;
                request = item.Requests[index];
                return true;
            }
        }
        return false;
    }

    private void CompleteSourcePresentationHead(GameState owner, NormalPeriodOperation? pending,
        CampaignEffectRequests effects, int batchIndex, int requestIndex, MonthlySceneState? scene,
        CoalitionCameraPresentationState? camera)
    {
        var batch = effects.PendingBatches[batchIndex];
        CampaignEffectBatch[] batches;
        if (batch.Requests.Length == 1)
        {
            batches = new CampaignEffectBatch[effects.PendingBatches.Length - 1];
            Array.Copy(effects.PendingBatches, 0, batches, 0, batchIndex);
            Array.Copy(effects.PendingBatches, batchIndex + 1, batches, batchIndex, batches.Length - batchIndex);
        }
        else
        {
            var requests = new CampaignEffectRequest[batch.Requests.Length - 1];
            Array.Copy(batch.Requests, 0, requests, 0, requestIndex);
            Array.Copy(batch.Requests, requestIndex + 1, requests, requestIndex, requests.Length - requestIndex);
            batches = (CampaignEffectBatch[])effects.PendingBatches.Clone();
            batches[batchIndex] = batch with { Requests = requests };
        }
        var completed = owner with { CampaignEffectRequests = effects with { PendingBatches = batches },
            ActiveMonthlyScene = scene, CoalitionCameraPresentation = camera };
        // Source-head identity was checked before the synchronous formatter.
        // Its returned state is a new value, not the original owner reference.
        PublishSourcePresentationOwner(completed, pending);
    }
}
