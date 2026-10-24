using San14.Core;

namespace San14.Data;

internal sealed record ArmyChainCurrentCandidate(int PhysicalSlot, int UnitId);
internal sealed record ArmyChainCurrentCandidates(IReadOnlyList<ArmyChainCurrentCandidate> ReachedPrefix,
    int? Count, string? UnresolvedOwner)
{
    /// <summary>TC2AFC20 / SC2AFA40 local ring and native candidate filters.
    /// The prefix preserves traversal order and duplicates. Unknown after a
    /// reached candidate cannot be published as an empty or complete chain.</summary>
    internal static ArmyChainCurrentCandidates Read(GameState state, SourceWorldContext current, UnitState actor)
    {
        var result = new List<ArmyChainCurrentCandidate>();
        try
        {
            var arena = state.ReadArmyLifecycle() ?? throw Missing("receiver_owner");
            var slots = arena.ReceiverSlots(); // Indexed view; does not enumerate all receivers.
            int actorSlot = K(actor.NativeArmyOrigin?.NativeSlotIndex, "actor_slot");
            if (actorSlot is < 1 or > 500 || actorSlot >= slots.Length) throw Missing("actor_slot_range");
            var actorReference = new ArmyUnitReference(actorSlot);
            current.BindUnspecifiedOwnedArmyQueryHeader(slots[actorSlot]);
            if (!K(current.ReadArmyUnitNativeValidity(actorReference), "actor_validity")) return Complete();
            if (K(current.ReadArmyUnitCommanderId(actorReference), "actor_commander") != actor.OfficerId)
                throw Missing("actor_commander_owner");
            int? actorPerson = Person(actor.OfficerId);
            if (actorPerson is not int commander) return Complete();
            if ((K(current.ReadArmyReturnPersonFlags196(commander), "actor_flags") & 0x4000) != 0) return Complete();
            var native = actor.NativeTacticRuntime ?? throw Missing("actor_abnormal_counters");
            if (native.Abnormal23 != 0 || native.Abnormal24 != 0 || native.Abnormal25 != 0) return Complete();
            int rawCenter = K(ArmyMarchSourceAdapter.ReadCurrentHex(state, actor), "actor_hex");
            //2A78D0 first resolves raw Army2A through the Hex table; 2A7EE0
            //then uses that receiver's physical identity for ring geometry.
            var centerReference = current.ReadHexTableReference(rawCenter < 48400 ? rawCenter : 0)
                ?? throw Missing("center_hex_pointer");
            int center = K(centerReference.PhysicalSlot, "center_hex_identity");
            if (state.NativeArmySimulation?.Parameters is not { } parameters ||
                !parameters.Int32Values.TryGetValue(SimulationParameterKeys.Tactics.ChainSearchRadius, out int radius)) throw Missing("radius");
            if (radius is < 0 or > 255) throw Missing("radius_range");
            current.BindArmyMapOccupancy(state);
            foreach (int hex in TacticRemakeCurrentContext.NativeHexOrder(center, radius).Skip(1))
            {
                var hexReference = current.ReadHexTableReference(hex) ?? throw Missing("ring_hex_pointer");
                //Native validity rejects a known null receiver. An unknown
                //table entry above must not take that known-invalid branch.
                if (hexReference.PhysicalSlot is null) continue;
                int raw = unchecked((short)K(current.ReadHexRaw1C(hexReference), "ring_occupancy"));
                if (raw is < 1 or > 500) continue;
                var reference = current.ReadArmyUnitTableReference(raw);
                if (reference is null)
                {
                    // Same owned allocation join as FieldHexOccupantSource:
                    // bind only the selected receiver, never a global proof.
                    if (raw >= slots.Length || slots[raw].PhysicalSlot != raw) throw Missing("candidate_pointer");
                    current.SetArmyUnitTablePointer(raw, raw);
                    reference = new(raw);
                }
                if (reference.PhysicalSlot is not int physical) continue;
                if (physical < 0 || physical >= slots.Length) throw Missing("candidate_slot_range");
                var slot = slots[physical]; current.BindUnspecifiedOwnedArmyQueryHeader(slot);
                if (!K(current.ReadArmyUnitNativeValidity(reference), "candidate_validity")) continue;
                ushort personId = K(current.ReadArmyUnitCommanderId(reference), "candidate_commander");
                int? candidatePerson = Person(personId);
                if (candidatePerson is not int candidateCommander) continue;
                if ((K(current.ReadArmyReturnPersonFlags196(candidateCommander), "candidate_flags") & 0x4000) != 0) continue;
                var candidate = slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : slot.PendingImage;
                if (candidate is null || candidate.NativeArmyOrigin?.NativeSlotIndex != physical || candidate.OfficerId != personId)
                    throw Missing("candidate_projection");
                current.BindUnspecifiedOwnedArmyQueryPosition(physical, ArmyMarchSourceAdapter.ReadCurrentHex(state, candidate));
                if (current.IsArmyUnitMapWriteSkipped(reference)) continue;
                if (K(current.ReadArmyExclusionForQuerySnapshot(state, physical), "candidate_exclusion")) continue;
                //22BC70 is the commander's raw District, not Force22CD80.
                if (K(current.ReadPersonDistrictSlot(candidateCommander), "candidate_raw_district") !=
                    K(current.ReadPersonDistrictSlot(commander), "actor_raw_district")) continue;
                var abnormal = candidate.NativeTacticRuntime ?? throw Missing("candidate_abnormal_counters");
                if (abnormal.Abnormal23 != 0 || abnormal.Abnormal24 != 0 || abnormal.Abnormal25 != 0) continue;
                // Cached TC data.tsv has threshold3. No SC source byte has
                // yet been established for this reached nonempty branch.
                if (parameters.Language != "TC") throw Missing("relation_threshold_profile");
                byte relation = K(current.ReadDestructionRelationTier2A6EE0(commander, candidateCommander, 1), "commander_relation");
                if (relation >= 3) result.Add(new(physical, candidate.Id));
            }
            return Complete();

            int? Person(int id)
            {
                var person = (id is >= 0 and <= 6000 ? current.ReadPersonLookupReference(id)
                    : current.ReadTransferPersonTableReference(0)) ?? throw Missing("person_pointer");
                if (person.PhysicalSlot is not int physical) return null;
                return K(current.ReadPersonNativeValidity(physical), "person_validity") ? physical : null;
            }
        }
        catch (InvalidDataException error) { return new(result.AsReadOnly(), null, error.Message); }
        ArmyChainCurrentCandidates Complete() => new(result.AsReadOnly(), result.Count, null);
    }
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("current.army_chain." + field);
}
