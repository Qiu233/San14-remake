using San14.Data;
using Godot;
using NMatrix = System.Numerics.Matrix4x4;
using San14.Core;

public partial class MapSceneView
{
    private Action? ordinaryTacticUpdate;
    internal void SetOrdinaryTacticDriver(Action? update) => ordinaryTacticUpdate = update;

    internal OrdinaryTacticViewPose QueryTacticViewPose(OrdinaryTacticViewIdentity view) =>
        _d3d11Markers?.TacticViewPose(view) ?? new(false, null, "tactic.view.native_map_not_ready");

    internal void StartOrdinaryTacticName(OrdinaryTacticPresentationPublication source, GameState world, int? playerForce,
        OrdinaryTacticNamePublication? armyName)
    {
        var view = source.Beams.PrimaryView;
        var actor = source.Beams.PrimaryActor;
        if (view.Kind == OrdinaryTacticViewKind.Army)
        {
            _d3d11Markers?.StartArmyTacticName(view.Id, armyName ?? throw new InvalidDataException("tactic.name.Army_layout_preparation"));
            return;
        }
        int? force = view.Kind == OrdinaryTacticViewKind.City
            ? world.Cities.GetValueOrDefault(view.Id)?.ForceId : world.Gates.GetValueOrDefault(view.Id)?.ForceId;
        _d3d11Markers?.StartBaseTacticName(view, actor.Source.Name, actor.Source.NamePreset(force is not null && force == playerForce));
    }

    internal void PublishOrdinaryTacticNumbers(OrdinaryTacticResultPublication publication) =>
        _d3d11Markers?.PublishTacticNumbers(publication);

    internal Task<CaptureEffectAdmission> EnqueueTacticBeam(object owner, OrdinaryTacticActorPublication source, NMatrix transform,
        CancellationToken cancellationToken)
    {
        if (_captureEffects is not { } effects || !D3D11MapInteractive)
            return Task.FromResult(new CaptureEffectAdmission(false, "tactic.beam.native_map_owner"));
        var pending = effects.Enqueue(owner, 9, transform, null, cancellationToken, source.Actor);
        ScheduleD3D11MapFrame(); return pending;
    }

    internal bool HasLiveTacticBeam(object owner) => _captureEffects?.IsLive(owner) == true;
    internal void UpdateTacticBeam(object owner, NMatrix transform) => _captureEffects?.SetTransform(owner, transform);
    internal async Task RetireTacticBeam(object owner)
    {
        if (_captureEffects is not { } effects) return;
        var retired = effects.Retire(owner); ScheduleD3D11MapFrame();
        await retired; effects.Forget(owner);
    }

    internal async Task<CaptureEffectAdmission> DispatchTacticResultEventsAsync(
        OrdinaryTacticResultPublication publication, SceneAudioBinding audio, CancellationToken cancellationToken)
    {
        var visuals = new List<(object Owner, Task<CaptureEffectAdmission> Result)>();
        var sounds = new List<Task<NormalSoundAdmission>>();
        // Submit each original attempt in order. A sound after an effect is
        // independent of that effect's allocation/native publication result.
        foreach (var entry in publication.Events)
        {
            var position = new Vector3(entry.Position.X, entry.Position.Y, entry.Position.Z);
            if (entry.Operation == OrdinaryTacticResultOperation.Effect)
            {
                int kind = entry.EffectId ?? throw new InvalidDataException("tactic.result.effect_identity");
                visuals.Add((entry, AdmitCaptureVisualAsync(entry,
                    new(kind, entry.Target.HexId, null, position, entry.Scale, null), cancellationToken)));
            }
            else
                sounds.Add(DispatchCaptureSoundAsync(entry.SoundId ??
                    throw new InvalidDataException("tactic.result.sound_identity"), position, audio, cancellationToken));
        }
        try
        {
            var results = await Task.WhenAll(visuals.Select(item => item.Result));
            await Task.WhenAll(sounds);
            return results.FirstOrDefault(result => !result.Completed) ?? new(results.Any(result => result.Accepted))
            { Culled = results.Length > 0 && results.All(result => result.Culled), NoVisualRequired = results.Length == 0 };
        }
        finally { foreach (var item in visuals) _captureEffects?.Forget(item.Owner); }
    }
}
