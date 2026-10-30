using San14.Core;

public partial class MapSceneView
{
    private readonly Dictionary<CampaignEffectRequest, FacilityEffectsDispatch> _damageDispatches =
        new(ReferenceEqualityComparer.Instance);

    internal Task<FacilityEffectsDispatch> DispatchDamagePresentationAsync(CampaignEffectRequest request,
        SceneAudioBinding? audio, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (_damageDispatches.TryGetValue(request, out var existing)) return Task.FromResult(existing);
        var result = Dispatch(); _damageDispatches.Add(request, result); return Task.FromResult(result);

        FacilityEffectsDispatch Dispatch()
        {
            if (!D3D11MapInteractive || _d3d11Markers is null) return new(false, "damage.native_info_owner");
            DamagePresentationSnapshot? source;
            bool refresh;
            int? amount;
            SettlementBase? target = null;
            if (request.FireDamagePresentation is { } fire)
            {
                source = fire.SourceSnapshot; amount = fire.GrossAmount; target = fire.BaseTarget;
                refresh = fire.Operation == FireDamagePresentationOperation.ArmyTargetViewRefresh;
            }
            else if (request.TerrainDamagePresentation is { } terrain)
            {
                source = terrain.SourceSnapshot; amount = terrain.RequestedAmount;
                refresh = terrain.Operation == TerrainDamagePresentationOperation.ArmyTargetViewRefresh;
            }
            else return new(false, "damage.request_kind");
            if (source is null) return new(false, "damage.source_snapshot");
            bool accepted;
            string? missing;
            if (refresh)
            {
                if (source.ArmyReceiverId is not { } receiver) return new(false, "damage.source_Army_receiver");
                //1A3B20 restarts danger; it does not recalculate the model or World.
                accepted = _d3d11Markers.TryDeferFacilityDanger(receiver, out missing);
            }
            else
            {
                if (amount is not int gross || source.DisplayModifier is not sbyte modifier)
                    return new(false, "damage.source_number_arguments");
                if (target is not null)
                {
                    if (source.BaseReceiverId is not { } receiver) return new(false, "damage.source_Base_receiver");
                    accepted = _d3d11Markers.TryStartBaseDamage(target, receiver, gross, modifier, out missing);
                }
                else
                {
                    if (source.ArmyReceiverId is not { } receiver) return new(false, "damage.source_Army_receiver");
                    accepted = _d3d11Markers.TryStartArmyDamage(receiver, gross, modifier, 3, false, out missing);
                }
            }
            return new(accepted, missing);
        }
    }

    internal void ForgetDamagePresentation(CampaignEffectRequest request) => _damageDispatches.Remove(request);
    internal void RetireDamagePresentation()
    { _damageDispatches.Clear(); _d3d11Markers?.RetireDamageFeedback(); }
}
