using System.Collections.Immutable;
using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Capture the normal TC strategy-entry Force frontier/rear
    /// classifications. Call at the actual strategy preparation boundary,
    /// never as a fallback for missing saved/phase-owned information lists.</summary>
    public ImmutableArray<OriginalAiForceMemorySnapshot> CaptureOriginalAiForceMemoryCurrent(GameState state)
    {
        if (Seed.Language != "TC") throw ForceMemoryMissing("SC_strategy_preparation_contract");
        var current = CaptureCurrentCampaignEventQueries(state);
        return OriginalAiForceMemoryProducer.Rebuild(new ForceMemoryInputs(this, state, current));
    }

    private sealed class ForceMemoryInputs : IOriginalAiForceMemoryInputs
    {
        private readonly GameState state;
        private readonly SourceWorldContext current;
        private readonly Dictionary<BaseReference, IReadOnlyList<byte>> links = [];
        private readonly IReadOnlyList<BaseReference?> registeredBases;
        public IReadOnlyList<int> RegisteredForces { get; }

        internal ForceMemoryInputs(ArmyCampaignRuleAdapter adapter, GameState state, SourceWorldContext current)
        {
            this.state = state; this.current = current;
            var registry = current.ReadRegistryReferences(RegistryKind.Force) ?? throw ForceMemoryMissing("Force_registry");
            var forces = new List<int>(registry.Count);
            foreach (var entry in registry)
            {
                if (entry is not { Kind: RegistryKind.Force, PhysicalSlot: int physical })
                    throw ForceMemoryMissing("Force_registry_receiver");
                forces.Add(physical);
            }
            RegisteredForces = forces;
            registeredBases = current.ReadRegisteredBases() ?? throw ForceMemoryMissing("Foothold_registry");
            foreach (var city in adapter._scenario.Snapshot.Cities)
                links.Add(new(BaseKind.City, city.Slot), city.LinkedLocationSlots);
            foreach (var gate in adapter._scenario.Snapshot.Gates)
                links.Add(new(BaseKind.Gate, gate.Slot), gate.LinkedLocationSlots);
        }

        public IReadOnlyList<OriginalAiEntity> OwnedFootholds(int force)
        {
            var result = new List<OriginalAiEntity>();
            // The filtered native iterator checks force identity range but
            // does not add another native validity gate for each own base.
            if (force is < 1 or > 51) return result;
            foreach (var entry in registeredBases)
            {
                var at = entry ?? throw ForceMemoryMissing("Foothold_registry_pointer");
                if (Known(current.ReadBaseForceSlot(at), "Foothold_owner") == force) result.Add(Entity(at));
            }
            return result;
        }

        public IReadOnlyList<OriginalAiEntity> AdjacentFootholds(OriginalAiEntity foothold)
        {
            var at = Base(foothold);
            int home = Known(current.ReadBaseObjectLink(at), "adjacency_self_location");
            if (!links.TryGetValue(at, out var row) || row.Count < 10)
                throw ForceMemoryMissing("Base_ten_neighbor_slots");
            var result = new List<OriginalAiEntity>(10);
            // The source reads all ten serialized links, preserving their
            // order and duplicates, then validates each actual target pointer.
            for (int index = 0; index < 10; index++)
            {
                int location = row[index];
                if (location == 0) continue;
                var neighbor = BaseLookup.Find(location, current);
                if (!Known(current.ReadBaseNativeValidity(neighbor), "adjacency_target_validity")) continue;
                if (location == home) continue;
                result.Add(Entity(neighbor));
            }
            return result;
        }

        public int Owner(OriginalAiEntity foothold) => Known(current.ReadBaseForceSlot(Base(foothold)), "neighbor_owner");
        public bool IsCurrentPlayer(int force)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw ForceMemoryMissing("current_player_pointer");
            int identity = Known(current.ReadForcePhysicalSlot(player), "current_player_identity");
            return identity != 0 && identity == force;
        }
        public bool HasCoalitionFlag(int force) => Known(current.ReadConditionForceCoalitionMembership(new(force)), "Force_coalition_flag");
        public byte AllianceByte(int force, int other) => ReadReportForceRelationByte(state, new(force), other, ForceMemoryMissing);
        public int FriendshipLevel(int force, int other) => Known(current.ReadConditionForceFriendshipLevel(new(force), new(other)), "Force_friendship_level");
        public int RulerAllegianceDisposition(int force)
        {
            ushort raw = current.ReadForceRecord(force)?.RawRuler10 ?? throw ForceMemoryMissing("ruler_lookup_identity");
            var person = raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0);
            int physical = person?.PhysicalSlot ?? throw ForceMemoryMissing("ruler_person_pointer");
            return Known(current.ReadAllegianceDisposition(physical), "ruler_allegiance_disposition");
        }

        private BaseReference Base(OriginalAiEntity entity)
        {
            var at = BaseLookup.Find(entity.Id, current);
            if (entity.Kind != (at.Kind == BaseKind.City ? WorldEntityKind.City : WorldEntityKind.Gate))
                throw ForceMemoryMissing("foothold_kind");
            return at;
        }
        private OriginalAiEntity Entity(BaseReference at) => new(at.Kind == BaseKind.City ? WorldEntityKind.City : WorldEntityKind.Gate,
            Known(current.ReadBaseObjectLink(at), "foothold_location_identity"));
        private static T Known<T>(T? value, string field) where T : struct => value ?? throw ForceMemoryMissing(field);
    }

    private static InvalidDataException ForceMemoryMissing(string field) => new("original_ai.force_memory." + field);
}
