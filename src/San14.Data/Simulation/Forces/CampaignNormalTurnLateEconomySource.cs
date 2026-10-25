using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyNormalTurnLateEconomyCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int action) => action switch
        {
            57 => ApplyNormalTurnForceServiceRewardsCurrent(state, current, pool),
            58 => ApplyNormalTurnForceRankMaintenanceCurrent(state, current, pool),
            59 => ApplyNormalTurnTrapVisibilityCurrent(state, current),
            //The actual60 position owns2FDC70. It borrows this current view;
            //the compatibility wrapper's second capture is not part of Turn.
            60 => UpdateCampaignExpeditionsCurrent(state, current),
            _ => throw new InvalidDataException("normal.turn.late-economy.action")
        };

    //303C10 changes only the traps' relation-dependent view attachment. No
    //object is destroyed or retired. The presentation consumer, when one is
    //available, checks its own actual model presence and applies this desired
    //attachment; queueing this request does not claim a model was created.
    private GameState ApplyNormalTurnTrapVisibilityCurrent(GameState state, SourceWorldContext current)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("player_Force");
        var objects = CampaignSourceKnowledge.From(state.ResourceState).MapObjectSource ?? throw Missing("Object_identity_owner");
        var directory = new MapObjectSourceDirectory(objects);
        var effects = new List<CampaignEffectRequest>();
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Object) ?? throw Missing("Object_E0"))
        {
            if (entry is not { Kind: RegistryKind.Object, PhysicalSlot: int physical }) throw Missing("Object_E0_payload");
            var obj = new ObjectReference(physical);
            var record = current.ReadPhysicalObject(physical) ?? throw Missing("Object_fields");
            var definition = current.ReadObjectDefinitionReference(record.RawField12 < 31 ? record.RawField12 : 0)
                ?? throw Missing("Object_definition");
            if (!K(current.ReadObjectDefinitionValidity(definition), "definition_validity") ||
                K(current.ReadObjectDefinitionRaw2C(definition), "definition_category") != 4) continue;
            int ownerRaw = K(current.ReadObjectForceSlot13(obj), "Object13");
            var owner = current.ReadForceTableReference(ownerRaw < 52 ? ownerRaw : 0) ?? throw Missing("owner_Force");
            if (AbsorptionPlayer(current, state, owner)) continue;
            int ownerId = K(current.ReadForcePhysicalSlot(owner), "owner_Force_identity");
            bool allied = (ReadReportForceRelationByte(state, player, ownerId, Missing) & 127) != 0;
            //224070's2C4830 gate is independent of an existing view handle.
            //An invalid player cannot construct the trap's actual model.
            if (allied && (!K(current.ReadObjectNativeValidity(obj), "attach_Object_validity") ||
                record.RawField12 is 1 or 2 || !K(current.ReadForceNativeValidity(player), "attach_player_validity"))) continue;
            int hex = K(current.ReadObjectHexIndex(obj), "Object10");
            int definitionId = K(definition.PhysicalSlot, "definition_identity");
            effects.Add(new(null, null) { MapObjectPresentation = new(allied
                ? MapObjectPresentationEvent.RelationVisibilityAttach : MapObjectPresentationEvent.RelationVisibilityDetach,
                directory.ReceiverAtPhysical(physical), hex, MapObjectSourceDirectory.DefinitionKey(definitionId)) });
        }
        if (effects.Count == 0) return state;
        return state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw Missing("pending_owner"))
            .Append(CampaignEffectSource.MonthlyMaintenance, state.NativeClock?.UpdateOrdinal ?? throw Missing("revision"), effects) };

        static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        static InvalidDataException Missing(string field) => new("normal.turn.trap-visibility." + field);
    }
}
