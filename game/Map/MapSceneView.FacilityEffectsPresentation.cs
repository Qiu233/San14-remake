using Godot;
using San14.Core;
using San14.Data;
using NMatrix = System.Numerics.Matrix4x4;
using NVector = System.Numerics.Vector3;

internal sealed record FacilityEffectsDispatch(bool Accepted, string? UnresolvedInput = null);

public partial class MapSceneView
{
    private readonly Dictionary<CampaignEffectRequest, Task<FacilityEffectsDispatch>> _facilityEffectDispatches =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<CampaignEffectRequest, List<object>> _facilityEffectTickets =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _facilityProjectiles = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<int, object> _facilityEffectObjectOwners = [];
    private readonly Dictionary<int, object> _facilityEffectArmyOwners = [];
    private IReadOnlyDictionary<int, ArmyFireViewSnapshot?>? _facilityArmyViewSnapshots;
    private CancellationTokenSource? _facilityEffectLifetime;

    internal void PublishFacilityArmyViews() => Volatile.Write(ref _facilityArmyViewSnapshots,
        tacticArmyPositions is { } positions ? _d3d11Markers?.CaptureArmyRenderViews(positions) : null);

    internal FacilityArmyViewSnapshot? CaptureFacilityArmyView(ArmyReceiverId receiver)
    {
        var frame = Volatile.Read(ref _facilityArmyViewSnapshots);
        if (frame is null) return null;
        if (!frame.TryGetValue(receiver.Value, out var view)) return new(false, 0, 0, 0);
        return view is null ? null : new(view.Present, view.X, view.Y, view.Z);
    }

    internal Task<FacilityEffectsDispatch> DispatchFacilityEffectsPresentationAsync(
        CampaignEffectRequest request, SceneAudioBinding? audio, CancellationToken cancellation)
    {
        if (_facilityEffectDispatches.TryGetValue(request, out var existing)) return existing.WaitAsync(cancellation);
        if (_facilityEffectLifetime is null)
        {
            EnsureCaptureFeedbackLifetime();
            _facilityEffectLifetime = new();
            TreeExiting += RetireFacilityEffectsPresentation;
        }
        var token = _facilityEffectLifetime.Token;
        var pending = Dispatch();
        _facilityEffectDispatches.Add(request, pending);
        return pending.WaitAsync(cancellation);

        async Task<FacilityEffectsDispatch> Dispatch()
        {
            try
            {
                token.ThrowIfCancellationRequested();
                if (!D3D11MapInteractive || _captureEffects is null)
                    return new(false, "facility.effects.native_scene");
                if (request.FacilityAuraReset is not null)
                {
                    //18F9F0 releases only containers retained by190390.
                    foreach (var owner in _facilityProjectiles.ToArray())
                    {
                        var retired = _captureEffects.Retire(owner); ScheduleD3D11MapFrame();
                        await retired.WaitAsync(token); _captureEffects.Forget(owner);
                        _facilityProjectiles.Remove(owner);
                    }
                    return new(true);
                }
                var visuals = new List<Task<CaptureEffectAdmission>>();
                var sounds = new List<Task<NormalSoundAdmission>>();
                var tickets = new List<object>(); _facilityEffectTickets.Add(request, tickets);
                if (request.FacilityProjectile is { } projectile)
                {
                    int definition = projectile.DefinitionId ?? throw Missing("projectile.source_definition");
                    int kind = definition switch { 5 or 15 => 38, 6 => 40, _ => throw Missing("projectile.definition") };
                    var matrix = FacilityEffectGeometry.Projectile(Hex(projectile.SourceHexOperand),
                        Hex(projectile.TargetHexOperand), definition);
                    Effect(kind, matrix, ObjectOwner(projectile.FacilityReceiverId.Value), persistent: true);
                }
                else if (request.FacilitySupportAura is { } aura)
                {
                    int kind = aura.DefinitionId switch { 3 => 43, 4 => 44, _ => throw Missing("aura.definition") };
                    EffectAt(kind, Hex(aura.TargetHexOperand), aura.Scale, ArmyOwner(aura.TargetReceiverId.Value));
                }
                else if (request.MilitaryMusicPulse is { } pulse)
                {
                    bool positive = pulse.Positive ?? throw Missing("pulse.source_sign");
                    EffectAt(positive ? 31 : 32, View(pulse.SourceView), 1, ObjectOwner(pulse.FacilityReceiverId.Value));
                }
                else if (request.FacilityStatisticPresentation is { } statistic)
                {
                    var origin = View(statistic.SourceView);
                    var point = FacilityEffectGeometry.StatisticOffset(origin, statistic.Dimension);
                    var owner = ObjectOwner(statistic.FacilityReceiverId.Value);
                    EffectAt((statistic.Positive ? 21 : 26) + statistic.Dimension, point, 1, owner);
                    Sound(statistic.Positive ? 185 : 186, point);
                    //1915F0 passes the enabled secondary feedback argument.
                    EffectAt(statistic.Positive ? 65 : 66, origin, 1, owner);
                }
                else if (request.FacilityTrapPresentation is { } trap)
                {
                    var point = Hex(trap.HexId);
                    switch (trap.Operation)
                    {
                        case FacilityTrapPresentationOperation.FireRangeVisual:
                            EffectAt(48, point, trap.Range ?? throw Missing("trap.source_range"), ObjectOwner(trap.ReceiverId.Value)); break;
                        case FacilityTrapPresentationOperation.ActivatedVisual:
                            point.Y += 500; EffectAt(47, point, 7, ObjectOwner(trap.ReceiverId.Value)); break;
                        case FacilityTrapPresentationOperation.FireSmallMapNotice: Sound(199, point); break;
                        case FacilityTrapPresentationOperation.StrongFireSmallMapNotice: Sound(200, point); break;
                        case FacilityTrapPresentationOperation.ActivatedSmallMapNotice: Sound(198, point); break;
                        default: throw Missing("trap.operation");
                    }
                }
                else if (request.ArmyTrapPresentation is { } armyTrap)
                {
                    switch (armyTrap.Operation)
                    {
                        case ArmyTrapPresentationOperation.TargetPreparationVisual:
                            EffectAt(49, Hex(armyTrap.HexId), 1, ArmyOwner(FacilityArmyReceiver(armyTrap))); break;
                        case ArmyTrapPresentationOperation.TargetPreparationSmallMapNotice: Sound(201, Hex(armyTrap.HexId)); break;
                        case ArmyTrapPresentationOperation.AbnormalConfusionStarted:
                        case ArmyTrapPresentationOperation.AbnormalImmobilizationStarted:
                            bool confusion = armyTrap.Operation == ArmyTrapPresentationOperation.AbnormalConfusionStarted;
                            var point = View(armyTrap.SourceView);
                            EffectAt(confusion ? 18 : 19, point, 1, ObjectOwner(armyTrap.ReceiverId.Value));
                            Sound(confusion ? 182 : 183, point); break;
                        case ArmyTrapPresentationOperation.TargetViewRefresh:
                            return ApplyFacilityTargetViewRefresh(armyTrap);
                        default: throw Missing("army_trap.operation");
                    }
                }
                else if (request.ArmyFacilityDamagePresentation is { } damage)
                {
                    if (damage.Operation == ArmyFacilityDamagePresentationOperation.ConditionalFloatingNumber)
                        return ApplyFacilityDamageNumber(damage);
                    Sound(damage.Operation switch
                    {
                        ArmyFacilityDamagePresentationOperation.SmallMapNotice => 161,
                        ArmyFacilityDamagePresentationOperation.LargeMapNotice => 162,
                        _ => throw Missing("damage.operation")
                    }, Hex(damage.HexId));
                }
                else return new(false, "facility.effects.request_kind");
                //Source effect and following sound attempts are independent.
                var results = await Task.WhenAll(visuals);
                await Task.WhenAll(sounds);
                var failed = results.FirstOrDefault(result => !result.Completed);
                return failed is null ? new(true) : new(false, failed.UnresolvedInput);

                void EffectAt(int kind, NVector point, float scale, object sourceOwner) =>
                    Effect(kind, NMatrix.CreateScale(scale) * NMatrix.CreateTranslation(point), sourceOwner);
                void Effect(int kind, NMatrix matrix, object sourceOwner, bool persistent = false)
                {
                    var ticket = new object(); tickets.Add(ticket);
                    visuals.Add(_captureEffects.Enqueue(ticket, kind, matrix, null, token, sourceOwner));
                    if (persistent) _facilityProjectiles.Add(ticket);
                    ScheduleD3D11MapFrame();
                }
                void Sound(int managerId, NVector point)
                {
                    if (audio is null) throw Missing("scene_audio");
                    sounds.Add(DispatchCaptureSoundAsync(managerId, new(point.X, point.Y, point.Z), audio, token));
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { return new(false, error.Message); }
        }
    }

    private NVector Hex(int operand) => FacilityEffectGeometry.HexPosition(operand,
        _sourceMainCameraTerrain ?? throw Missing("source_terrain"));
    private static NVector View(FacilityArmyViewSnapshot? view) => view is null
        ? throw Missing("source_Army_view") : view.Present ? new(view.X, view.Y, view.Z) : NVector.Zero;
    private object ObjectOwner(int receiver) => _facilityEffectObjectOwners.TryGetValue(receiver, out var owner)
        ? owner : _facilityEffectObjectOwners[receiver] = new object();
    private object ArmyOwner(int receiver) => _facilityEffectArmyOwners.TryGetValue(receiver, out var owner)
        ? owner : _facilityEffectArmyOwners[receiver] = new object();
    private static InvalidDataException Missing(string field) => new("facility.effects." + field);

    private static int FacilityArmyReceiver(ArmyTrapPresentationRequest request) =>
        request.TargetReceiverId?.Value ?? throw Missing("army_trap.source_receiver");

    private FacilityEffectsDispatch ApplyFacilityTargetViewRefresh(ArmyTrapPresentationRequest request)
    {
        if (_d3d11Markers is null) return new(false, "facility.danger.info_controller");
        bool accepted = _d3d11Markers.TryDeferFacilityDanger(new(FacilityArmyReceiver(request)), out var missing);
        return new(accepted, missing);
    }

    private FacilityEffectsDispatch ApplyFacilityDamageNumber(ArmyFacilityDamagePresentationRequest request)
    {
        if (request.TargetReceiverId is not { } receiver) return new(false, "facility.damage.source_receiver");
        if (request.DisplayModifier is not sbyte modifier) return new(false, "facility.damage.source_modifier");
        if (_d3d11Markers is null) return new(false, "facility.damage.info_controller");
        bool accepted = _d3d11Markers.TryStartFacilityDamage(receiver, request.GrossAmount, modifier, out var missing);
        return new(accepted, missing);
    }

    internal void ForgetFacilityEffectsPresentation(CampaignEffectRequest request)
    {
        _facilityEffectDispatches.Remove(request);
        if (_facilityEffectTickets.Remove(request, out var tickets))
            foreach (var ticket in tickets)
                if (!_facilityProjectiles.Contains(ticket)) _captureEffects?.Forget(ticket);
    }

    internal void RetireFacilityEffectsPresentation()
    {
        _facilityEffectLifetime?.Cancel(); _facilityEffectLifetime?.Dispose(); _facilityEffectLifetime = null;
        foreach (var ticket in _facilityEffectTickets.Values.SelectMany(value => value).Concat(_facilityProjectiles).Distinct())
        { _captureEffects?.Retire(ticket); _captureEffects?.Forget(ticket); }
        _facilityEffectTickets.Clear(); _facilityEffectDispatches.Clear(); _facilityProjectiles.Clear();
        _facilityEffectObjectOwners.Clear(); _facilityEffectArmyOwners.Clear();
        Volatile.Write(ref _facilityArmyViewSnapshots, null);
    }
}
