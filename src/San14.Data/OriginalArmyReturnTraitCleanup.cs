namespace San14.Data;

/// <summary>Actual presentation owner presence consumed by2A2340/2A2540.
/// This numeric branch supports known absent owners; present/unknown owners
/// remain explicit until their actual presentation operation is supplied.</summary>
public sealed record OriginalArmyReturnTraitPresentationOwners(bool? WeakArmyViewPresent, bool? ManagerView20Present);
public sealed record OriginalArmyReturnTraitCleanupResolution(OriginalArmyReturnTraitCleanupPacket? Packet, string? UnresolvedOwner);
public sealed class OriginalArmyReturnTraitCleanupPacket
{
    public OriginalArmyUnitReference Army { get; }
    public int ClearedRelatedSlotCount { get; }
    public int RemovedRelatedNodeCount { get; }
    internal OriginalArmyReturnTraitCleanupPacket(OriginalArmyUnitReference army, int slots, int nodes)
    { Army = army; ClearedRelatedSlotCount = slots; RemovedRelatedNodeCount = nodes; }
}

/// <summary>29F220's exact nine2A2340 then36x2A2540 prefix. Static trait key
/// lookup230730, actual related descriptor160 iteration/clear226770 and
/// related-pointer removal execute within the enclosing return operation.
/// Neither Army-manager60 nor all Core units can substitute for these lists.</summary>
public static class OriginalArmyReturnTraitCleanup
{
    internal static OriginalArmyReturnTraitCleanupResolution ApplyCurrent(OriginalArmyUnitReference army,
        OriginalEventInitializationContext context, WorldIndexContext pool, OriginalArmyReturnTraitPresentationOwners presentation)
    {
        ArgumentNullException.ThrowIfNull(army); ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(pool); ArgumentNullException.ThrowIfNull(presentation);
        try
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, context) || !pool.OwnsArmyReturnProfile()) throw Missing("current.complete_owned_shared_pool");
            if (context.RegistryLockEnabledReader is not null || context.RegistryDescriptorKeyReader is not null ||
                context.RegistryHeadTablePresenceReader is not null || context.RegistryLockEnabled != false || context.RegistryHeadTablePresent != true)
                throw Missing("current.owned_sequential_list_profile");
            if (!Get(context.ReadArmyUnitNativeValidity(army), "current.Army_validity")) throw Missing("branch.invalid_army");
            if (presentation.WeakArmyViewPresent != false) throw Missing("current.2A2340_weak_view_refresh1A3D70");
            var draft = context; var draftPool = pool;
            ushort commanderId = Get(context.ReadArmyUnitCommanderId(army), "current.Army_commander12");
            int commander = Person(commanderId);
            int cleared = 0, removed = 0;
            for (int index = 0; index < 9; index++)
            {
                ushort mask = Get(draft.ReadPersonTraitMask1BC(commander), "current.Commander_trait184");
                //186 is a write-only zero in this exact source branch.
                draft.WritePersonTraitMask1BC(commander, (ushort)(mask & ~(1 << index)));
                draft.WritePersonTraitExtra1BE(commander, index, 0);
                ushort key = TraitKey(commander, index);
                int slot = MatchingTrait(army, key); //234E90 falls back to descriptor0.
                var descriptor = Fields(army).Descriptors160[slot < 0 ? 0 : slot];
                foreach (var receiver in Receivers(descriptor))
                {
                    if (!Get(context.ReadArmyUnitNativeValidity(receiver), "current.related_Army_validity")) continue;
                    for (int i = 0; i < 36; i++)
                    {
                        var fields = Fields(receiver);
                        if (fields.PersonsAC[i] != commanderId || fields.Traits64[i] != key) continue;
                        //2A2451/2459 read owner+validity; the result does NOT
                        //gate the following slot stores in2A2463..246C.
                        var actualOwner = PersonArmy(Person(fields.PersonsAC[i]));
                        _ = Get(context.ReadArmyUnitNativeValidity(actualOwner), "current.related_source_owner_validity");
                        draft.SetArmyReturnTraitFields(Physical(receiver), fields.ClearSlot(i)); cleared++;
                    }
                }
                //226770 finds an actual match, has no fallback and retains
                //the persistent descriptor pointer while returning its nodes.
                slot = MatchingTrait(army, key);
                if (slot >= 0) draft.ClearTemporaryHexList(new(Fields(army).Descriptors160[slot].Identity));
            }
            for (int index = 0; index < 36; index++)
            {
                var fields = Fields(army); var related = PersonArmy(Person(fields.PersonsAC[index]));
                if (Get(context.ReadArmyUnitNativeValidity(related), "current.2A2540_related_Army_validity"))
                {
                    int slot = MatchingTrait(related, fields.Traits64[index]);
                    if (slot >= 0)
                    {
                        var descriptor = Fields(related).Descriptors160[slot];
                        foreach (var node in Nodes(descriptor))
                        {
                            var payload = draft.ReadRegistryNodePayload(node) ?? throw Missing("current.related_node_payload");
                            if (payload.Kind != OriginalRegistryKind.Army) throw Missing("current.related_node_pointer_kind");
                            if (payload.PhysicalSlot == army.PhysicalSlot)
                            { draft.UnlinkArmyReturnTraitNode(descriptor, node); removed++; }
                        }
                    }
                    if (presentation.ManagerView20Present != false) throw Missing("current.2A2540_manager_view_refresh1A3D70");
                }
                draft.SetArmyReturnTraitFields(Physical(army), fields.ClearSlot(index));
            }
            return new(new(army, cleared, removed), null);

            OriginalArmyReturnTraitFields Fields(OriginalArmyUnitReference value)
            {
                var result = draft.ReadArmyReturnTraitFields(value) ?? throw Missing("current.Army64_AC_F4_descriptor160");
                if (result.Descriptors160.Any(x => !draftPool.OwnsArmyReturnDescriptor(x))) throw Missing("current.Army160_descriptor_pool_owner");
                return result;
            }
            int Physical(OriginalArmyUnitReference value) => value.PhysicalSlot ?? throw Missing("current.Army_pointer");
            int Person(ushort id)
            {
                var person = context.ReadPersonLookupReference(id <= 6000 ? id : 0) ?? throw Missing("current.Person_lookup");
                return person.PhysicalSlot ?? throw Missing("current.Person_pointer");
            }
            ushort TraitKey(int person, int index)
            {
                ushort id = Get(context.ReadPersonIdentity(person), "current.trait_Person_id10");
                if (id is >= 5101 and <= 5200 && index >= 5)
                    return 0;
                ushort raw = Get(context.ReadPersonTraitSlot150(person, index), "current.trait_Person_word118");
                ushort key = raw;
                if (id is < 5101 or > 5200)
                {
                    var definition = context.ReadPersonalityTableReference(raw <= 355 ? raw : 0) ?? throw Missing("current.Personality_table");
                    bool valid = Get(context.ReadSkillPersonalityValidity(definition), "current.Personality_validity");

                    if (valid)
                    {
                        byte flags = Get(context.ReadSkillPersonalityFlags(definition), "current.Personality_flag4");
                        if ((flags & 0x10) == 0) key = 0;
                    }
                }
                return index < 5 ? key : (ushort)0;
            }
            int MatchingTrait(OriginalArmyUnitReference receiver, ushort key)
            {
                int person = Person(Get(context.ReadArmyUnitCommanderId(receiver), "current.related_Commander12"));
                for (int i = 0; i < 9; i++) if (TraitKey(person, i) == key) return i;
                return -1;
            }
            OriginalArmyUnitReference PersonArmy(int person)
            {
                ushort location = Get(context.ReadPersonSecondLocation(person), "current.Person_secondE4");
                var decoded = OriginalPersonPositionRepair.DecodeSecondLocation(location);
                var value = context.ReadArmyUnitTableReference(decoded.Kind == 27 ? decoded.Index : 0) ?? throw Missing("current.Person_Army_table");
                ushort currentId = Get(context.ReadPersonIdentity(person), "current.Person_id10");
                ushort actualCommander = Get(context.ReadArmyUnitCommanderId(value), "current.Person_Army_commander12");

                return actualCommander == currentId ? value : context.ReadArmyUnitTableReference(0) ?? throw Missing("current.Person_Army_fallback0");
            }
            List<OriginalRegistryListNodeReference> Nodes(OriginalRegistryListHandle descriptor)
            {
                var result = new List<OriginalRegistryListNodeReference>(); var seen = new HashSet<int>();
                var node = draft.ReadRegistryHead(descriptor) ?? throw Missing("current.related_list_head");
                while (node.Identity is int token)
                {
                    if (!seen.Add(token)) throw Missing("current.related_list_cycle");
                    result.Add(node); node = draft.ReadRegistryNodeNext(node) ?? throw Missing("current.related_list_next");
                }
                return result;
            }
            IEnumerable<OriginalArmyUnitReference> Receivers(OriginalRegistryListHandle descriptor)
            {
                foreach (var node in Nodes(descriptor))
                {
                    var payload = draft.ReadRegistryNodePayload(node) ?? throw Missing("current.related_list_Army_payload");
                    if (payload.Kind != OriginalRegistryKind.Army) throw Missing("current.related_list_Army_kind");
                    yield return new(payload.PhysicalSlot);
                }
            }
        }
        catch (MissingOwner error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, "current.TraitCleanup_input: " + error.Message); }
    }
    private static T Get<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static MissingOwner Missing(string field) => new(field);
    private sealed class MissingOwner(string message) : Exception(message);
}
