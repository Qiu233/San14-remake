using San14.Core;
using static San14.Data.FacilityEffectCandidates;

namespace San14.Data;

internal static class FacilityEffectMilitaryMusicSource
{
    internal static GameState Apply(GameState state, SourceWorldContext current,
        NativeArmyProgressStageMoment moment)
    {
        var effects = state.CampaignEffectRequests ?? throw Missing("effect_owner");
        var requests = new List<CampaignEffectRequest> { new(null, null) { FacilityAuraReset = new() } };
        var candidates = current.CreateNearbyTemporary();
        var working = state;
        try
        {
            Fill(state, current, candidates);
            var node = current.ReadRegistryHead(new(candidates.Identity)) ?? throw Missing("candidate_head");
            while (node.Identity.HasValue)
            {
                var entry = current.ReadRegistryNodePayload(node) ?? throw Missing("candidate_payload");
                var facility = new ObjectReference(entry.PhysicalSlot);
                if (K(current.ReadObjectNativeValidity(facility), "consumer_Object_validity"))
                    working = ApplyFacility(working, current, facility, requests);
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("candidate_next");
            }
        }
        finally { current.ClearNearbyTemporary(candidates, true); }
        // Publish only after every candidate and every destructor succeeds.
        var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("publication_pool");
        working = working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ExportOwnedState() });
        return working with { CampaignEffectRequests = effects.Append(CampaignEffectSource.FacilityEffects,
            moment.CalendarCycleOrdinal, requests) };
    }

    private static GameState ApplyFacility(GameState state, SourceWorldContext current,
        ObjectReference facility, List<CampaignEffectRequest> requests)
    {
        var channels = new List<HexListHandle>(2);
        try
        {
            channels.Add(current.CreateNearbyTemporary()); channels.Add(current.CreateNearbyTemporary());
            var record = current.ReadPhysicalObject(K(facility.PhysicalSlot, "consumer_Object_pointer")) ?? throw Missing("consumer_Object");
            int kind = record.RawField12 <= 30 ? record.RawField12 : 0;
            var reference = current.ReadObjectDefinitionReference(kind) ?? throw Missing("consumer_definition_pointer");
            if (!K(current.ReadObjectDefinitionValidity(reference), "consumer_definition_validity")) return state;
            if (kind != 7) throw Missing("enabled_kind_" + kind);
            var definition = current.ReadFacilityEffectDefinition(reference) ?? throw Missing("definition_effect_fields");
            FacilityEffectTargets.Fill(state, current, facility, definition, kind, channels.ToArray());
            if (channels.All(x => K(current.ReadTemporaryHexListCount(x), "channel_count") == 0)) return state;
            // kind7 has no1902C0 persistent aura branch. Its15 handler never
            // adds a Force victim or damage aggregate. Keep real empty lists
            // for the normal Force-list constructor/sort/destructor lifecycle.
            var forces = current.CreateNearbyTemporary();
            try
            {
                for (int channel = 0; channel < 2; channel++)
                {
                    if (K(current.ReadTemporaryHexListCount(channels[channel]), "dispatch_count") == 0) continue;
                    var copy = current.CreateNearbyTemporary();
                    try
                    {
                        foreach (var hex in FacilityEffectTargets.ReadHexes(current, channels[channel])) current.AppendNearbyHexTemporary(copy, hex);
                        var effect = definition.Channel(channel);
                        if (effect.Effect != 15) throw Missing("handler_" + effect.Effect);
                        foreach (var hex in FacilityEffectTargets.ReadHexes(current, copy))
                        {
                            int at = K(hex.PhysicalSlot, "handler_Hex");
                            var selected = FieldHexOccupantSource.ReadCurrent(state, current, at);
                            var target = selected.Receipt ?? throw Missing(selected.UnresolvedOwner ?? "handler16EB20");
                            if (target.NativeKind != 27) continue;
                            var army = current.ReadArmyUnitTableReference(target.NativePhysicalSlot <= 500 ? target.NativePhysicalSlot : 0)
                                ?? throw Missing("handler_Army_pointer");
                            if (!K(current.ReadArmyUnitNativeValidity(army), "handler_Army_validity")) continue;
                            //1914A0 makes its independent22EB30 read BEFORE2709A0's three getters.
                            _ = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
                            var adjusted = ArmyMoraleNonzeroCurrent.Apply(state, current, army, effect.Amount);
                            if (adjusted.NativeReturn is null || adjusted.Receipt is not { } receipt)
                                throw Missing(adjusted.UnresolvedOwner ?? "morale_callback");
                            int id = target.GameplayArmyId ?? throw Missing("handler_Army_campaign_identity");
                            var image = state.Units.GetValueOrDefault(id) ?? throw Missing("handler_Army_image");
                            state = state with { Units = new(state.Units) { [id] = ArmyMoraleSourceAdapter.WithCurrent(image, receipt.After1A) } };
                            var directory = current.CurrentMapObjectDirectory ?? throw Missing("pulse_facility_identity");
                            requests.Add(new(null, null) { MilitaryMusicPulse = new(directory.ReceiverAtPhysical(facility.PhysicalSlot!.Value), id, at) });
                        }
                    }
                    finally { current.ClearNearbyTemporary(copy, true); }
                }
                var sortedForces = current.CreateNearbyTemporary();
                try { /* True15 slice leaves both Force lists empty. */ }
                finally { current.ClearNearbyTemporary(sortedForces, true); }
            }
            finally { current.ClearNearbyTemporary(forces, true); }
            return state;
        }
        finally { for (int i = channels.Count - 1; i >= 0; i--) current.ClearNearbyTemporary(channels[i], true); }
    }
}
