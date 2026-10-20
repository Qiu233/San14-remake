namespace San14.Data;

/// <summary>Presentation presence for the existing finite return profile.</summary>
public sealed record ArmyReturnTraitPresentationOwners(bool? WeakArmyViewPresent, bool? ManagerView20Present);
public sealed record ArmyReturnTraitCleanupResolution(ArmyReturnTraitCleanupPacket? Packet, string? UnresolvedOwner);
public sealed class ArmyReturnTraitCleanupPacket
{
    public ArmyUnitReference Army { get; }
    public int ClearedRelatedSlotCount { get; }
    public int RemovedRelatedNodeCount { get; }
    internal ArmyReturnTraitCleanupPacket(ArmyUnitReference army, int slots, int nodes)
    { Army = army; ClearedRelatedSlotCount = slots; RemovedRelatedNodeCount = nodes; }
}

/// <summary>Shared single-slot cleanup. The return consumer keeps its original
/// nine-then36 composition and known-absent presentation profile.</summary>
public static class ArmyReturnTraitCleanup
{
    internal static ArmyReturnTraitCleanupResolution ApplyCurrent(ArmyUnitReference army,
        SourceWorldContext context, WorldIndexContext pool, ArmyReturnTraitPresentationOwners presentation)
    {
        ArgumentNullException.ThrowIfNull(army); ArgumentNullException.ThrowIfNull(presentation);
        try
        {
            // Keep the established return adapter's lookup-zero fallback. The
            // new source manager independently uses the real base-Person fallback.
            var operation = new Current(context, pool, preserveLegacyPersonFallback: true);
            if (!Get(context.ReadArmyUnitNativeValidity(army), "current.Army_validity")) throw Missing("branch.invalid_army");
            if (presentation.WeakArmyViewPresent != false) throw Missing("current.2A2340_weak_view_refresh1A3D70");
            for (int i = 0; i < 9; i++) operation.ClearCommanderSlot(army, i);
            for (int i = 0; i < 36; i++) operation.ClearReceivedSlot(army, i, presentation.ManagerView20Present);
            return new(new(army, operation.ClearedRelatedSlotCount, operation.RemovedRelatedNodeCount), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    internal sealed class Current
    {
        private readonly SourceWorldContext context;
        private readonly WorldIndexContext pool;
        private readonly Action<ArmyUnitReference>? refresh;
        private readonly Action<int>? personChanged;
        private readonly bool preserveLegacyPersonFallback;
        internal int ClearedRelatedSlotCount { get; private set; }
        internal int RemovedRelatedNodeCount { get; private set; }
        internal Current(SourceWorldContext context, WorldIndexContext pool,
            Action<ArmyUnitReference>? refresh = null, Action<int>? personChanged = null,
            bool preserveLegacyPersonFallback = false)
        {
            ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(pool);
            if (!ReferenceEquals(pool.ArmyReturnOwner, context) || !pool.OwnsArmyReturnProfile()) throw Missing("current.complete_owned_shared_pool");
            if (context.RegistryLockEnabledReader is not null || context.RegistryDescriptorKeyReader is not null ||
                context.RegistryHeadTablePresenceReader is not null || context.RegistryLockEnabled != false || context.RegistryHeadTablePresent != true)
                throw Missing("current.owned_sequential_list_profile");
            this.context = context; this.pool = pool; this.refresh = refresh;
            this.personChanged = personChanged; this.preserveLegacyPersonFallback = preserveLegacyPersonFallback;
        }
        internal void ClearCommanderSlot(ArmyUnitReference army, int index)
        {
            ushort commanderId = Get(context.ReadArmyUnitCommanderId(army), "current.Army_commander12");
            int commander = Person(commanderId);
            ushort mask = Get(context.ReadPersonTraitMask1BC(commander), "current.Commander_trait184");
            context.WritePersonTraitMask1BC(commander, (ushort)(mask & ~(1 << index)));
            context.WritePersonTraitExtra1BE(commander, index, 0);
            personChanged?.Invoke(commander);
            ushort key = TraitKey(commander, index);
            int slot = MatchingTrait(army, key);
            var descriptor = Fields(army).Descriptors160[slot < 0 ? 0 : slot];
            foreach (var receiver in Receivers(descriptor))
            {
                if (!Get(context.ReadArmyUnitNativeValidity(receiver), "current.related_Army_validity")) continue;
                for (int i = 0; i < 36; i++)
                {
                    var fields = Fields(receiver);
                    if (fields.PersonsAC[i] != commanderId || fields.Traits64[i] != key) continue;
                    var actualOwner = PersonArmy(Person(fields.PersonsAC[i]));
                    _ = Get(context.ReadArmyUnitNativeValidity(actualOwner), "current.related_source_owner_validity");
                    context.WriteArmyReturnTraitFields(Physical(receiver), fields.ClearSlot(i));
                    ClearedRelatedSlotCount++;
                }
            }
            slot = MatchingTrait(army, key);
            if (slot >= 0) context.ClearTemporaryHexList(new(Fields(army).Descriptors160[slot].Identity));
            refresh?.Invoke(army);
        }
        internal void ClearReceivedSlot(ArmyUnitReference army, int index, bool? managerViewPresent = false)
        {
            var fields = Fields(army); var related = PersonArmy(Person(fields.PersonsAC[index]));
            if (Get(context.ReadArmyUnitNativeValidity(related), "current.2A2540_related_Army_validity"))
            {
                int slot = MatchingTrait(related, fields.Traits64[index]);
                if (slot >= 0)
                    foreach (var node in Nodes(Fields(related).Descriptors160[slot]))
                    {
                        var payload = context.ReadRegistryNodePayload(node) ?? throw Missing("current.related_node_payload");
                        if (payload.Kind != RegistryKind.Army) throw Missing("current.related_node_pointer_kind");
                        if (payload.PhysicalSlot == army.PhysicalSlot)
                        { context.UnlinkArmyReturnTraitNode(Fields(related).Descriptors160[slot], node); RemovedRelatedNodeCount++; }
                    }
                if (refresh is not null) refresh(related);
                else if (managerViewPresent != false) throw Missing("current.2A2540_manager_view_refresh1A3D70");
            }
            context.WriteArmyReturnTraitFields(Physical(army), fields.ClearSlot(index));
        }
        internal ArmyReturnTraitFields Fields(ArmyUnitReference army)
        {
            var result = context.ReadArmyReturnTraitFields(army) ?? throw Missing("current.Army64_AC_F4_descriptor160");
            if (result.Descriptors160.Any(x => !pool.OwnsArmyReturnDescriptor(x))) throw Missing("current.Army160_descriptor_pool_owner");
            return result;
        }
        internal int Person(ushort id)
        {
            var reference = id <= 6000 || preserveLegacyPersonFallback
                ? context.ReadPersonLookupReference(id <= 6000 ? id : 0) : context.ReadTransferPersonTableReference(0);
            return reference?.PhysicalSlot ?? throw Missing("current.Person_pointer");
        }
        internal ushort TraitKey(int person, int index)
        {
            ushort id = Get(context.ReadPersonIdentity(person), "current.trait_Person_id10");
            if (id is >= 5101 and <= 5200 && index >= 5) return 0;
            ushort key = Get(context.ReadPersonTraitSlot150(person, index), "current.trait_Person_word118");
            if (id is < 5101 or > 5200)
            {
                var definition = context.ReadPersonalityTableReference(key <= 355 ? key : 0) ?? throw Missing("current.Personality_table");
                if (Get(context.ReadSkillPersonalityValidity(definition), "current.Personality_validity") &&
                    (Get(context.ReadSkillPersonalityFlags(definition), "current.Personality_flag4") & 0x10) == 0) key = 0;
            }
            return index < 5 ? key : (ushort)0;
        }
        internal int MatchingTrait(ArmyUnitReference army, ushort key)
        {
            int person = Person(Get(context.ReadArmyUnitCommanderId(army), "current.related_Commander12"));
            for (int i = 0; i < 9; i++) if (TraitKey(person, i) == key) return i;
            return -1;
        }
        private ArmyUnitReference PersonArmy(int person)
        {
            ushort location = Get(context.ReadPersonSecondLocation(person), "current.Person_secondE4");
            var decoded = PersonPositionRepair.DecodeSecondLocation(location);
            var value = context.ReadArmyUnitTableReference(decoded.Kind == 27 ? decoded.Index : 0) ?? throw Missing("current.Person_Army_table");
            ushort id = Get(context.ReadPersonIdentity(person), "current.Person_id10");
            return Get(context.ReadArmyUnitCommanderId(value), "current.Person_Army_commander12") == id ? value :
                context.ReadArmyUnitTableReference(0) ?? throw Missing("current.Person_Army_fallback0");
        }
        internal IReadOnlyList<RegistryListNodeReference> Nodes(RegistryListHandle descriptor)
        {
            var result = new List<RegistryListNodeReference>(); var seen = new HashSet<int>();
            var node = context.ReadRegistryHead(descriptor) ?? throw Missing("current.related_list_head");
            while (node.Identity is int token)
            {
                if (!seen.Add(token)) throw Missing("current.related_list_cycle");
                result.Add(node); node = context.ReadRegistryNodeNext(node) ?? throw Missing("current.related_list_next");
            }
            return result;
        }
        internal IReadOnlyList<ArmyUnitReference> Receivers(RegistryListHandle descriptor) => Nodes(descriptor).Select(node =>
        {
            var payload = context.ReadRegistryNodePayload(node) ?? throw Missing("current.related_list_Army_payload");
            return payload.Kind == RegistryKind.Army ? new ArmyUnitReference(payload.PhysicalSlot) : throw Missing("current.related_list_Army_kind");
        }).ToArray();
        private static int Physical(ArmyUnitReference army) => army.PhysicalSlot ?? throw Missing("current.Army_pointer");
    }
    private static T Get<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new(field);
}
