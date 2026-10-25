using San14.Core;

namespace San14.Data;

public sealed partial class ForceUnregisterSource
{
    private GameState RemoveOwnedObjects(GameState state, SourceWorldContext current,
        int victim, Action<GameState>? publishReached, List<CampaignEffectRequest>? borrowedEffects = null)
    {
        // This is the native lookup-table traversal, including aliases. City
        // and Gate map objects are not facilities merely because their owner
        // is the defeated Force.
        for (int slot = 0; slot < 3001; slot++)
        {
            var obj = current.ReadObjectTableReference(slot) ?? throw Missing("Object_table");
            if (!Known(current.ReadObjectNativeValidity(obj), "Object_validity")) continue;
            if (Need(current.ReadObjectForceSlot13(obj), "Object13") != victim) continue;
            int physical = Need(obj.PhysicalSlot, "Object_physical");
            byte kind = current.ReadPhysicalObject(physical)?.RawField12 ?? throw Missing("Object12");
            var definition = current.ReadObjectDefinitionReference(kind <= 30 ? kind : 0)
                ?? throw Missing("Object_definition_pointer");
            if (!Known(current.ReadObjectDefinitionValidity(definition), "Object_definition_validity")) continue;
            if (Need(current.ReadObjectDefinitionRaw2C(definition), "Object_definition_category") is not (2 or 4)) continue;
            package.RemoveForceUnregisterObject(state, current, obj,
                reached => { state = reached; publishReached?.Invoke(state); },
                request =>
                {
                    if (borrowedEffects is not null) { borrowedEffects.Add(request); return; }
                    var pending = state.CampaignEffectRequests ?? throw Missing("Object_effect_owner");
                    long revision = state.NativeClock?.UpdateOrdinal ?? throw Missing("Object_effect_revision");
                    state = state with { CampaignEffectRequests = pending.Append(CampaignEffectSource.ForceUnregister,
                        revision, [request]) };
                    publishReached?.Invoke(state);
                });
        }
        publishReached?.Invoke(state);
        return state;
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    // The Force caller has no actor Army. Reuse the same object remover as
    // demolition/traps, with reached writes and unconsumed presentation work.
    internal void RemoveForceUnregisterObject(GameState state, SourceWorldContext current,
        ObjectReference obj, Action<GameState> publishReached, Action<CampaignEffectRequest> emit)
        => RemoveMapObjectWithoutActor(state, current, obj, publishReached, emit);
}
