using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal sealed partial class PersonDeathSource
    {
        private HexListHandle? relationshipGroup;

        private void ResetRelations(int person)
            => latest = OwnedPersonRelationInputs.ResetCurrentDeathRelations(latest, person);

        //The mother keeps this descriptor across the Base and registry tail.
        private void ReleaseRelationships()
        {
            if (relationshipGroup is not { } list) return;
            RelationRelease(list);
            relationshipGroup = null;
        }

        private void ApplyRelationships(int person, TransferPersonReference capturedBC)
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("relationships_current_pool");
            if (relationshipGroup is not null) throw Missing("relationships_group_lifetime");
            HexListHandle group = RelationAllocate();
            relationshipGroup = group;
            // This is the physical table, not manager100 and not a native-valid filter.
            for (int slot = 0; slot < 1851; slot++)
            {
                var pointer = current.ReadTransferPersonTableReference(slot) ?? throw Missing("relationships_physical_table");
                int other = Known(pointer.PhysicalSlot, "relationships_physical_person");
                ushort sworn = RelationWord(other, 0xBC);
                if (sworn is >= 1 and <= 6000 &&
                    sworn == RelationIdentity(Known(capturedBC.PhysicalSlot, "relationships_captured_BC_pointer")) &&
                    RelationIdentity(other) != RelationIdentity(person))
                    RelationAppend(group, other);

                if (RelationWord(other, 0xBA) == RelationIdentity(person))
                    latest = OwnedPersonRelationInputs.WriteCurrentRelationIdentity(latest, other, 0xBA, 0);
                if (RelationRawHit(other, person, true)) RebuildWithoutPersonPointer(other, person, true);
                if (RelationRawHit(other, person, false)) RebuildWithoutPersonPointer(other, person, false);
            }
            Observe("death.relationships.scanned");
            int count = Known(current.ReadTemporaryHexListCount(group), "relationships_group_count");
            if (count == 1)
            {
                int only = RelationPayload(RelationHead(group));
                latest = OwnedPersonRelationInputs.WriteCurrentRelationIdentity(latest, only, 0xBC, 0);
            }
            else if (count > 1 &&
                RelationIdentity(Known(capturedBC.PhysicalSlot, "relationships_captured_BC_pointer")) == RelationIdentity(person))
            {
                RelationSort(group);
                ushort leader = (ushort)Math.Min((int)RelationIdentity(RelationPayload(RelationHead(group))), 6000);
                var node = RelationHead(group);
                while (node.Identity.HasValue)
                {
                    int other = RelationPayload(node);
                    latest = OwnedPersonRelationInputs.WriteCurrentRelationIdentity(latest, other, 0xBC, leader);
                    node = RelationNext(node);
                }
            }
            Observe("death.relationships.recompiled");
        }

        private bool RelationRawHit(int other, int person, bool like)
        {
            if (!Known(current.ReadPersonNativeValidity(person), "relationships_target_validity")) return false;
            ushort id = RelationIdentity(person);
            for (int i = 0; i < 8; i++)
                if (RelationListWord(other, i, like) == id) return true;
            return false;
        }

        //228570/228250 share the same operation; only the selected eight-word field differs.
        private void RebuildWithoutPersonPointer(int other, int person, bool like)
        {
            HexListHandle list = RelationAllocate();
            try
            {
                for (int i = 0; i < 8; i++)
                {
                    ushort id = RelationListWord(other, i, like);
                    if (id is < 1 or > 6000) continue;
                    var pointer = current.ReadPersonLookupReference(id) ?? throw Missing("relationships_list_lookup");
                    if (pointer.PhysicalSlot is int physical) RelationAppend(list, physical);
                }
                var node = RelationHead(list);
                while (node.Identity.HasValue)
                {
                    if (RelationNullablePayload(node) == person)
                    {
                        RelationEnter();
                        int key = current.ReadPersonDeathRelationKey(new(list.Identity));
                        node = current.ReadPersonDeathRelationRemovalHead(key);
                        while (node.Identity.HasValue)
                        {
                            if (RelationNullablePayload(node) == person)
                            {
                                var savedNext = RelationNext(node);
                                current.RemovePersonDeathRelationNode(key, new(node.Identity));
                                node = savedNext;
                            }
                            else node = RelationNext(node);
                        }
                        RelationExit();
                        break;
                    }
                    node = RelationNext(node);
                }

                //Clear all sixteen bytes even when the target pointer was absent.
                latest = like ? OwnedPersonRelationInputs.ClearCurrentLikeIdentities(latest, other)
                    : OwnedPersonRelationInputs.ClearCurrentDislikeIdentities(latest, other);
                int index = 0;
                node = RelationHead(list);
                while (node.Identity.HasValue)
                {
                    int? physical = RelationNullablePayload(node);
                    if (physical.HasValue)
                    {
                        ushort id = (ushort)Math.Min((int)RelationIdentity(physical.Value), 6000);
                        if (index < 8)
                            latest = like ? OwnedPersonRelationInputs.WriteCurrentLikeIdentity(latest, other, index, id)
                                : OwnedPersonRelationInputs.WriteCurrentDislikeIdentity(latest, other, index, id);
                        index++;
                    }
                    node = RelationNext(node);
                }
                Observe(like ? "death.relationships.C0-rebuilt" : "death.relationships.D0-rebuilt");
            }
            finally { RelationRelease(list); }
        }

        private void RelationSort(HexListHandle list)
        {
            var node = RelationHead(list);
            if (!node.Identity.HasValue) return;
            node = RelationNext(node);
            while (node.Identity.HasValue)
            {
                var savedNext = RelationNext(node);
                var previous = RelationPrevious(node);
                ushort birth = RelationBirth(RelationPayload(node));
                ushort previousBirth = RelationBirth(RelationPayload(previous));
                bool before = birth < previousBirth;
                if (birth == previousBirth)
                {
                    ushort previousId = RelationIdentity(RelationPayload(previous));
                    before = RelationIdentity(RelationPayload(node)) < previousId;
                }
                if (before)
                {
                    var insertion = previous;
                    var earlier = RelationPrevious(previous);
                    if (earlier.Identity.HasValue)
                    {
                        //250150 refreshes and then caches current AC for the reverse walk.
                        ushort cachedBirth = RelationBirth(RelationPayload(node));
                        while (earlier.Identity.HasValue)
                        {
                            ushort earlierBirth = RelationBirth(RelationPayload(earlier));
                            bool stop = earlierBirth < cachedBirth;
                            if (earlierBirth == cachedBirth)
                            {
                                ushort earlierId = RelationIdentity(RelationPayload(earlier));
                                stop = earlierId <= RelationIdentity(RelationPayload(node));
                            }
                            if (stop) break;
                            insertion = earlier;
                            earlier = RelationPrevious(earlier);
                        }
                    }
                    var order = RelationNodes(list);
                    order.RemoveAll(value => value.Identity == node.Identity);
                    int at = order.FindIndex(value => value.Identity == insertion.Identity);
                    if (at < 0) throw Missing("relationships_sort_insertion");
                    order.Insert(at, node);
                    current.ReorderCurrentList(list, order);
                }
                node = savedNext;
            }
        }

        private ushort RelationBirth(int physical)
            => Known(Owner(physical).BirthYear, "relationships_current_birth_year");

        private ushort RelationIdentity(int physical) => Known(current.ReadPersonIdentity(physical), "relationships_current_ID10");
        private ushort RelationWord(int physical, int field)
            => Known(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(latest, physical, field), "relationships_current_word");
        private ushort RelationListWord(int physical, int index, bool like)
            => Known(like ? OwnedPersonRelationInputs.ReadCurrentLikeIdentity(latest, physical, index)
                : OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(latest, physical, index), "relationships_current_list_slot");

        private HexListHandle RelationAllocate()
        {
            RelationEnter();
            var list = current.CreateTemporaryHexList() ?? throw Missing("relationships_descriptor_knowledge");
            RelationExit();
            return list; //Known-null descriptor uses the existing null-key allocation/head profile.
        }

        private void RelationAppend(HexListHandle list, int physical)
        {
            RelationEnter();
            var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("relationships_node_knowledge");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.Person, physical));
            RelationExit();
        }

        private void RelationRelease(HexListHandle list)
        {
            if (!list.Identity.HasValue) return;
            RelationEnter(); current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); RelationExit();
        }

        private HexListNodeReference RelationHead(HexListHandle list)
            => new((current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("relationships_head")).Identity);
        private HexListNodeReference RelationNext(HexListNodeReference node)
            => current.ReadHexListNodeNext(node) ?? throw Missing("relationships_next");
        private HexListNodeReference RelationPrevious(HexListNodeReference node)
            => current.ReadHexListNodePrevious(node) ?? throw Missing("relationships_previous");
        private int RelationPayload(HexListNodeReference node)
            => Known(RelationNullablePayload(node), "relationships_payload_pointer");
        private int? RelationNullablePayload(HexListNodeReference node)
        {
            var payload = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("relationships_payload_knowledge");
            if (payload.Kind != RegistryKind.Person) throw Missing("relationships_payload_kind");
            return payload.PhysicalSlot;
        }
        private List<HexListNodeReference> RelationNodes(HexListHandle list)
        {
            var result = new List<HexListNodeReference>();
            var node = RelationHead(list);
            while (node.Identity.HasValue) { result.Add(node); node = RelationNext(node); }
            return result;
        }
        private void RelationEnter() { if (Known(current.ReadRegistryLockEnabled(), "relationships_lock")) current.EnterRegistryLock(); }
        private void RelationExit() { if (Known(current.ReadRegistryLockEnabled(), "relationships_unlock")) current.ExitRegistryLock(); }
    }
}

public sealed partial class SourceWorldContext
{
    internal int ReadPersonDeathRelationKey(RegistryListHandle descriptor) => RegistryKey(descriptor);

    internal HexListNodeReference ReadPersonDeathRelationRemovalHead(int key)
    {
        bool heads = (RegistryHeadTablePresenceReader is null ? RegistryHeadTablePresent : RegistryHeadTablePresenceReader())
            ?? throw new InvalidDataException("Current death relation head table is unknown.");
        return heads && NativeListKeyInRange(key) ? ReadHexListHead(new(key))
            ?? throw new InvalidDataException("Current death relation removal head is unknown.") : new(null);
    }

    internal void RemovePersonDeathRelationNode(int key, RegistryListNodeReference node)
        => RemoveAddressableRegistryNode(key, node);
}
