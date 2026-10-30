using San14.Core;
using static San14.Data.FacilityEffectCandidates;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>149's actual190A20 presentation tail. Borrows the caller's
    /// current owner and list pool; no periodic numerical handler is run.</summary>
    internal GameState ApplyStoryFacilityAuraRefreshCurrent(GameState state, SourceWorldContext current)
    {
        var effects = state.CampaignEffectRequests ?? throw Missing("story_refresh_effect_owner");
        long revision = state.NativeClock?.UpdateOrdinal ?? throw Missing("story_refresh_revision");
        var requests = new List<CampaignEffectRequest> { new(null, null) { FacilityAuraReset = new() } };
        var candidates = current.CreateNearbyTemporary();
        try
        {
            Fill(state, current, candidates);
            var node = current.ReadRegistryHead(new(candidates.Identity)) ?? throw Missing("story_refresh_candidate_head");
            while (node.Identity.HasValue)
            {
                var entry = current.ReadRegistryNodePayload(node) ?? throw Missing("story_refresh_candidate_payload");
                if (entry.Kind != RegistryKind.Object) throw Missing("story_refresh_candidate_kind");
                var facility = new ObjectReference(entry.PhysicalSlot);
                var record = current.ReadPhysicalObject(K(facility.PhysicalSlot, "story_refresh_Object_pointer"))
                    ?? throw Missing("story_refresh_Object_record");
                int rawKind = record.RawField12 <= 30 ? record.RawField12 : 0;
                var definition = current.ReadObjectDefinitionReference(rawKind)
                    ?? throw Missing("story_refresh_definition_pointer");
                //190A20's outer22DDA0 gate admits only these three definition
                //identities. Its other1902C0 branches are not this caller.
                if (K(current.ReadObjectDefinitionValidity(definition), "story_refresh_definition_validity") &&
                    K(definition.PhysicalSlot, "story_refresh_definition_identity") is 5 or 6 or 15)
                {
                    var channels = new List<HexListHandle>(2);
                    try
                    {
                        channels.Add(current.CreateNearbyTemporary());
                        channels.Add(current.CreateNearbyTemporary());
                        var fields = current.ReadFacilityEffectDefinition(definition)
                            ?? throw Missing("story_refresh_definition_fields");
                        FacilityEffectTargets.Fill(state, current, facility, fields, rawKind, channels.ToArray());
                        if (channels.Any(channel => K(current.ReadTemporaryHexListCount(channel), "story_refresh_channel_count") != 0))
                            FacilityProjectilePresentationSource.AppendCurrent(state, current, facility, channels, requests);
                    }
                    finally
                    {
                        for (int index = channels.Count - 1; index >= 0; index--)
                            current.ClearNearbyTemporary(channels[index], true);
                    }
                }
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("story_refresh_candidate_next");
            }
        }
        finally { current.ClearNearbyTemporary(candidates, true); }
        var lifecycle = state.ReadArmyLifecycle() ?? throw Missing("story_refresh_lifecycle");
        var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("story_refresh_shared_pool");
        var working = state.WithArmyLifecycle(lifecycle with { Indexes = pool.ProjectOwnedRoles() });
        return working with
        {
            CampaignEffectRequests = effects.Append(CampaignEffectSource.FacilityEffects, revision, requests)
        };
    }
}

/// <summary>190390's existing ordinary projectile projection, shared by the
/// periodic facility operation and the event-triggered reset/rebuild tail.</summary>
internal static class FacilityProjectilePresentationSource
{
    internal static void AppendCurrent(GameState state, SourceWorldContext current, ObjectReference facility,
        IReadOnlyList<HexListHandle> channels, List<CampaignEffectRequest> requests)
    {
        if (!K(current.ReadObjectNativeValidity(facility), "projectile_Object_validity")) return;
        byte kind = (current.ReadPhysicalObject(K(facility.PhysicalSlot, "projectile_Object_pointer"))
            ?? throw Missing("projectile_Object_record")).RawField12;
        if (kind is not (5 or 6 or 15)) throw Missing("projectile_Object_kind");
        foreach (var channel in channels)
            foreach (var hex in FacilityEffectTargets.ReadHexes(current, channel))
            {
                int identity = K(current.ReadHexPhysicalSlot(hex), "projectile_Hex_identity");
                var indexed = current.ReadHexTableReference(identity < 48400 ? identity : 0)
                    ?? throw Missing("projectile_indexed_Hex_pointer");
                if (!indexed.PhysicalSlot.HasValue) continue;
                int selected = unchecked((short)K(current.ReadHexRaw1C(indexed), "projectile_indexed_signed1C"));
                if (selected is < 1 or > 500) continue;
                //2C6D20 reads the indexed Hex;231660 independently reads the
                //original channel Hex. Neither adds an Army-validity gate.
                int raw = unchecked((short)K(current.ReadHexRaw1C(hex), "projectile_original_signed1C"));
                if (raw < 0) throw Missing("projectile_negative_Army_pointer");
                var army = current.ReadArmyUnitTableReference(raw <= 500 ? raw : 0)
                    ?? throw Missing("projectile_Army_pointer");
                var arena = state.ReadArmyLifecycle() ?? throw Missing("projectile_Army_lifecycle");
                int physical = K(army.PhysicalSlot, "projectile_Army_physical");
                requests.Add(new(null, null)
                {
                    FacilityProjectile = new((current.CurrentMapObjectDirectory ?? throw Missing("projectile_Object_directory"))
                        .ReceiverAtPhysical(K(facility.PhysicalSlot, "projectile_Object_pointer")),
                        arena.Slots[physical].ReceiverId,
                        K(current.ReadObjectHexIndex(facility), "projectile_source_Hex10"),
                        K(current.ReadArmyUnitRawHex2A(army), "projectile_target_Hex2A")) { DefinitionId = kind }
                });
            }
    }
}
