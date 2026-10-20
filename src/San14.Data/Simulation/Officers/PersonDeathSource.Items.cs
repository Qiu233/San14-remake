using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal sealed partial class PersonDeathSource
    {
        private void ApplyCapturedItems(int person, ForceReference originalForce)
        {
            var home = BaseLookup.Find(Known(current.ReadPersonFirstLocation(person), "captured_items_E2"), current);
            int forceSlot = Known(CurrentBaseAffiliationSource.ReadForce(latest, current, home), "captured_items_Base_force");
            var receiver = current.ReadForceTableReference(forceSlot <= 51 ? forceSlot : 0)
                ?? throw Missing("captured_items_Force_lookup");
            foreach (var item in PersonItems(person))
            {
                ClearItemFlag3(item);
                int ruler = Known(current.ReadMoraleWaveRuler22EAC0(receiver), "captured_items_ruler22EAC0");
                WriteDirectHeldItem(item, (ushort)Math.Clamp(ruler, 0, 6000));
                Observe("death.item.captured-held");
                if (IsPlayer(originalForce) || IsPlayer(receiver))
                    throw Missing("items_report_2A3B");
            }
        }

        private void ApplyItems(int person, PersonLinkedEntityReference associated)
        {
            // This prefix is unconditional, including when the later Item
            // iterator is empty. The annual second Person argument is null.
            var home = BaseLookup.Find(Known(current.ReadPersonFirstLocation(person), "items_original_E2"), current);
            var capturedCity = CurrentGateMapQueries.FindCity(home, current);
            var selected = new TransferPersonReference(null);
            HexListHandle? candidates = Temporary();
            try
            {
                var descriptor = current.ReadRegistryDescriptor(RegistryKind.Person) ?? throw Missing("items_Person_registry");
                var node = current.ReadRegistryHead(descriptor) ?? throw Missing("items_Person_head");
                while (node.Identity.HasValue)
                {
                    var pointer = ItemPayload(new(node.Identity), RegistryKind.Person);
                    int other = Known(pointer.PhysicalSlot, "items_candidate_pointer");
                    byte otherForce = Known(current.ReadPersonOldForceSlot(other), "items_candidate_force22CF60");
                    byte victimForce = Known(current.ReadPersonOldForceSlot(person), "items_victim_force22CF60");
                    if (otherForce == victimForce &&
                        (ItemRelative(other, person, 0xB4) || ItemRelative(other, person, 0xB6) ||
                         ItemRelative(other, person, 0xBA) || ItemSworn(other, person)) &&
                        !ItemRawHit(person, other, like: false) && Status(other) != 9 &&
                        Status(other) is not (6 or 7) && other != person)
                        Append(candidates, pointer);
                    // The source takes live Next after all reached helpers.
                    node = current.ReadRegistryNodeNext(node) ?? throw Missing("items_Person_next");
                }
                Observe("death.items.candidates-collected");
                if (ItemHead(candidates).Identity.HasValue)
                {
                    SortItemRecipients(candidates, person); //Also called for one node.
                    selected = new(ItemPayload(ItemHead(candidates), RegistryKind.Person).PhysicalSlot);
                    Observe("death.items.recipient-selected");
                }
            }
            finally { if (candidates is not null) ClearRelease(candidates); }

            HexListHandle pendingRemoval = Temporary();
            try
            {
                foreach (var item in PersonItems(person))
                {
                    if (Known(current.ReadLinkedEntityNativeValidity(associated), "items_associated_validity") &&
                        ApplyAssociatedItem(person, item, associated)) continue;

                    //32FB90(null second Person) is known false. Its special
                    //232A50 branch is therefore not reached in this profile.
                    if (Valid(selected))
                    {
                        ClearItemFlag3(item);
                        int recipient = Physical(selected, "items_selected_pointer");
                        ushort holder = Identity(Physical(ItemRecipientRuler(recipient), "items_selected_ruler_pointer"));
                        WriteDirectHeldItem(item, (ushort)Math.Min((int)holder, 6000));
                        Observe("death.item.recipient-held");
                        if (Serving(recipient) && (IsPlayer(Force(person)) || IsPlayer(Force(recipient))))
                            throw Missing("items_report_2A3B");
                    }
                    else if (Serving(person) && Status(person) != 1)
                    {
                        int ruler = Known(current.ReadMoraleWaveRuler22EAC0(Force(person)), "items_fallback_ruler22EAC0");
                        ClearItemFlag3(item);
                        WriteDirectHeldItem(item, (ushort)Math.Clamp(ruler, 0, 6000));
                        Observe("death.item.force-held");
                    }
                    else
                    {
                        Append(pendingRemoval, item);
                        int bound = 0;
                        foreach (var unused in ItemCityAreas(capturedCity)) bound = unchecked(bound + 1);
                        int chosen = DrawItemApp32(bound);
                        int ordinal = 0;
                        foreach (var area in ItemCityAreas(capturedCity))
                        {
                            if (ordinal == chosen)
                            {
                                ClearItemFlag3(item);
                                int location = Known(current.ReadAreaPhysicalSlot(new(area.PhysicalSlot)), "items_area_location22DB40");
                                _ = WriteItem324580(item, 0, location, 2, 0);
                                Observe("death.item.location-written");
                                break;
                            }
                            ordinal = unchecked(ordinal + 1);
                        }
                        //Even an unmatched/invalid location retains the
                        //already appended pending-unregistration reference.
                    }
                }
                var node = ItemHead(pendingRemoval);
                while (node.Identity.HasValue)
                {
                    _ = UnregisterItem(ItemPayload(node, RegistryKind.Item));
                    Observe("death.item.unregistered");
                    node = ItemNext(node);
                }
            }
            finally { ClearRelease(pendingRemoval); }
        }

        private bool ApplyAssociatedItem(int person, RegistryObjectReference item, PersonLinkedEntityReference associated)
        {
            int kind = ItemAssociatedKind(associated);
            if (unchecked((uint)(kind - 4)) <= 5 || kind == 0x13)
            {
                ushort owner = Known(current.ReadCaptureItemOwner4A(ItemPhysical(item)), "items_protected_owner22DF30");
                return owner == ItemAssociatedWord76(associated);
            }
            //These are independent fresh virtual calls, not one cached tag.
            if (ItemAssociatedKind(associated) != 0x11 && ItemAssociatedKind(associated) != 0x12) return false;
            ushort linkedOwner = ItemAssociatedWord72(associated);
            if (linkedOwner != Known(current.ReadCaptureItemOwner4A(ItemPhysical(item)), "items_member_owner22DF30")) return false;
            var members = current.ReadLinkedEntityMemberIds(associated) ?? throw Missing("items_associated_members");
            foreach (uint? entry in members)
            {
                uint raw = Known(entry, "items_associated_member_value");
                if (raw == Identity(person)) continue;
                int signed = unchecked((int)raw);
                if (signed < 0) throw Missing("items_negative_member_lookup");
                var member = ItemLookup(signed);
                if (!Valid(member) || !Serving(Physical(member, "items_member_pointer"))) continue;
                ClearItemFlag3(item);
                _ = WriteItem324580(item, signed, 0, 1, 1);
                Observe("death.item.associated-member-held");
                return true;
            }
            //This is Force.word10 -> Person lookup, without an extra Force
            //validity gate and without substituting22EAC0's5014 convention.
            var fallback = ItemLookup(ItemForceWord10(Force(person)));
            if (!Valid(fallback)) return false;
            ClearItemFlag3(item);
            _ = WriteItem324580(item, Identity(Physical(fallback, "items_member_fallback_pointer")), 0, 1, 1);
            Observe("death.item.associated-fallback-held");
            return true;
        }

        private int ItemAssociatedKind(PersonLinkedEntityReference associated) =>
            new PersonReturnGovernorQueries(latest, package._governorInputs, current).ReadCurrentAssociatedVirtual98(associated)
                ?? throw Missing("items_associated_virtual98");
        private static ushort ItemAssociatedWord76(PersonLinkedEntityReference associated)
            => throw Missing("items_associated_word76");
        private static ushort ItemAssociatedWord72(PersonLinkedEntityReference associated)
            => throw Missing("items_associated_word72");

        private IEnumerable<RegistryObjectReference> PersonItems(int person)
        {
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Item) ?? throw Missing("items_registry110");
            var node = current.ReadRegistryHead(descriptor) ?? throw Missing("items_iterator_head");
            while (node.Identity.HasValue)
            {
                //The iterator's only filter is the actual current Person ID
                //range and owner4A. It adds neither native-validity nor4F gates.
                ushort id = Identity(person);
                if (id is >= 1 and <= 6000)
                {
                    var item = current.ReadRegistryNodePayload(node) ?? throw Missing("items_iterator_payload");
                    if (Known(current.ReadCaptureItemOwner4A(ItemPhysical(item)), "items_iterator_owner4A") == id)
                        yield return item;
                }
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("items_iterator_live_next");
            }
        }

        private IEnumerable<RegistryObjectReference> ItemCityAreas(BaseReference city)
        {
            int physicalCity = Known(city.PhysicalSlot, "items_City_pointer");
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Area) ?? throw Missing("items_CityArea_registry");
            var node = current.ReadRegistryHead(descriptor) ?? throw Missing("items_CityArea_head");
            while (node.Identity.HasValue)
            {
                if (physicalCity is >= 1 and <= 51)
                {
                    var area = current.ReadRegistryNodePayload(node) ?? throw Missing("items_CityArea_payload");
                    if (area.Kind != RegistryKind.Area) throw Missing("items_CityArea_kind");
                    if (Known(current.ReadAreaCitySlot(new(area.PhysicalSlot)), "items_CityArea35") == physicalCity)
                        yield return area;
                }
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("items_CityArea_live_next");
            }
        }

        private int DrawItemApp32(int bound)
        {
            if (bound < 2) return 0;
            var saved = latest.CampaignAppRandom32 ?? throw Missing("items_App32_authority");
            var random = new SharedSourceRandom32(saved.State);
            int value = random.Next(bound);
            latest = latest with { CampaignAppRandom32 = new(random.State) };
            return value;
        }

        private void SortItemRecipients(HexListHandle list, int person)
        {
            var cursor = ItemHead(list);
            if (!cursor.Identity.HasValue) return;
            cursor = ItemNext(cursor);
            while (cursor.Identity.HasValue)
            {
                var savedNext = ItemNext(cursor);
                var previous = current.ReadHexListNodePrevious(cursor) ?? throw Missing("items_sort_previous");
                if (ItemRecipientBefore(person, ItemPerson(cursor), ItemPerson(previous)))
                {
                    var insertion = previous;
                    var earlier = current.ReadHexListNodePrevious(previous) ?? throw Missing("items_sort_previous");
                    while (earlier.Identity.HasValue && ItemRecipientBefore(person, ItemPerson(cursor), ItemPerson(earlier)))
                    {
                        insertion = earlier;
                        earlier = current.ReadHexListNodePrevious(earlier) ?? throw Missing("items_sort_previous");
                    }
                    var order = new List<HexListNodeReference>();
                    var node = ItemHead(list);
                    while (node.Identity.HasValue) { order.Add(node); node = ItemNext(node); }
                    order.RemoveAll(node => node.Identity == cursor.Identity);
                    int at = order.FindIndex(node => node.Identity == insertion.Identity);
                    if (at < 0) throw Missing("items_sort_insertion_node");
                    order.Insert(at, cursor);
                    current.ReorderCurrentList(list, order);
                }
                cursor = savedNext;
            }
        }

        private bool ItemRecipientBefore(int person, int a, int b)
        {
            if (ItemParent(a, person) && !ItemParent(b, person)) return true;
            if (!ItemParent(a, person) && ItemParent(b, person)) return false;
            ushort first = Relation(a, 0xB2), victim = Relation(person, 0xB2);
            if (first == victim)
            {
                if (Relation(b, 0xB2) != victim) return true;
                if (Relation(a, 0xB2) != victim && Relation(b, 0xB2) == victim) return false;
            }
            else if (Relation(b, 0xB2) == victim) return false;
            //Both-sworn is deliberately true. This is not a strict weak
            //ordering and must keep the source node insertion algorithm.
            if (ItemSworn(a, person)) return true;
            if (ItemSworn(b, person)) return false;
            if (ItemRawHit(person, a, like: true) && !ItemRawHit(person, b, like: true)) return true;
            if (!ItemRawHit(person, a, like: true) && ItemRawHit(person, b, like: true)) return false;
            int firstAge = Age(a), secondAge = Age(b);
            if (firstAge > secondAge) return true;
            firstAge = Age(a); secondAge = Age(b);
            if (firstAge < secondAge) return false;
            ushort previousId = Identity(b);
            return Identity(a) < previousId;
        }

        private bool ItemParent(int person, int other) =>
            ItemRelative(person, other, 0xB4) || ItemRelative(person, other, 0xB6);
        private bool ItemPair(int person, int other) =>
            Known(current.ReadPersonNativeValidity(person), "items_relation_first_validity") &&
            Valid(new(other)) && Identity(person) != Identity(other);
        private bool ItemRelative(int person, int other, int field)
        {
            if (!ItemPair(person, other)) return false;
            ushort relation = Relation(person, field);
            return relation != 0 && relation == Identity(other);
        }
        private bool ItemSworn(int person, int other)
        {
            if (!ItemPair(person, other)) return false;
            ushort value = Relation(person, 0xBC);
            return value != 0 && value == Relation(other, 0xBC);
        }
        private bool ItemRawHit(int person, int other, bool like)
        {
            if (!Valid(new(other))) return false;
            ushort target = Identity(other);
            for (int index = 0; index < 8; index++)
            {
                ushort value = Known(like ? OwnedPersonRelationInputs.ReadCurrentLikeIdentity(latest, person, index)
                    : OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(latest, person, index), "items_raw_relation_slot");
                if (value == target) return true;
            }
            return false;
        }

        private TransferPersonReference ItemLookup(int id) =>
            (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
                ?? throw Missing("items_Person_lookup");
        private ushort ItemForceWord10(ForceReference force) =>
            Known(current.ReadForceRecord(Known(force.PhysicalSlot, "items_Force_pointer"))?.RawRuler10, "items_Force_word10");
        private TransferPersonReference ItemRecipientRuler(int person)
        {
            var force = Force(person);
            return Known(current.ReadForceNativeValidity(force), "items_recipient_Force_validity")
                ? ItemLookup(ItemForceWord10(force)) : ItemLookup(0);
        }

        private bool WriteItem324580(RegistryObjectReference item, int owner, int location, int mode, int unregister)
        {
            if (!item.PhysicalSlot.HasValue) return false;
            if (unchecked((uint)(owner - 1)) <= 5999)
            {
                bool wasHeld = Known(current.ReadRegistryItemListEligibility(item), "items_writer_old50_equals1");
                WriteItemHolder(item, (ushort)Math.Clamp(owner, 0, 6000));
                if (wasHeld) return true;
                WriteItemStatus(item, 1);
                _ = ObjectRegistration.Register(item, current);
                return true;
            }
            if (unchecked((uint)(location - 1)) <= 499 && mode == 2)
            {
                WriteItemHolder(item, 0);
                WriteItemLocation(item, (ushort)Math.Clamp(location, 0, 500));
                WriteItemStatus(item, 2);
            }
            else if (mode is 3 or 5)
            {
                WriteItemStatus(item, (byte)mode);
                WriteItemHolder(item, 0);
                WriteItemLocation(item, 0);
            }
            else return false;
            if (unregister != 0) _ = UnregisterItem(item);
            return true;
        }

        private void ClearItemFlag3(RegistryObjectReference item)
        {
            var owner = ItemOwner(item);
            byte flags = Known(owner.Lifecycle?.RetainedFlags, "items_current_flags4F");
            ReplaceItem(item, owner with { Lifecycle = owner.Lifecycle! with { RetainedFlags = (byte)(flags & 0xF7) } });
        }
        private void WriteDirectHeldItem(RegistryObjectReference item, ushort holder)
        { WriteItemHolder(item, holder); WriteItemStatus(item, 1); }
        private void WriteItemHolder(RegistryObjectReference item, ushort holder)
        {
            int physical = ItemPhysical(item);
            current.SetCaptureItemOwner4A(physical, holder);
            //This is a publication-identity boundary, not an extra native
            //validity/permission gate in324580. Keep the actual current store.
            if (holder != 0 && (!latest.Officers.ContainsKey(holder) || current.ResolveOfficerSource(holder) is not int person ||
                current.ReadPersonIdentity(person) != holder)) throw Missing("items_holder_domain_identity");
            ReplaceItem(item, ItemOwner(item) with { Holder = new(holder == 0 ? null : holder) });
        }
        private void WriteItemLocation(RegistryObjectReference item, ushort location)
        {
            var owner = ItemOwner(item);
            ReplaceItem(item, owner with { Lifecycle = (owner.Lifecycle ?? new(null, null, null)) with { RetainedLocationCode = location } });
        }
        private void WriteItemStatus(RegistryObjectReference item, byte status)
        {
            current.SetRegistryItemStatus(ItemPhysical(item), ItemStatusKnowledge.Exact(status));
            var owner = ItemOwner(item);
            ReplaceItem(item, owner with { Lifecycle = (owner.Lifecycle ?? new(null, null, null)) with { Status = ItemStatusKnowledge.Exact(status) } });
        }
        private ItemState ItemOwner(RegistryObjectReference item)
        {
            int physical = ItemPhysical(item);
            return latest.Items is { } items && items.TryGetValue(physical, out var owner) && owner.Id == physical
                ? owner : throw Missing("items_canonical_Item_owner");
        }
        private void ReplaceItem(RegistryObjectReference item, ItemState owner)
        {
            var items = latest.Items ?? throw Missing("items_domain_table");
            latest = latest with { Items = new Dictionary<int, ItemState>(items) { [ItemPhysical(item)] = owner } };
        }
        private static int ItemPhysical(RegistryObjectReference item) => item.Kind == RegistryKind.Item
            ? Known(item.PhysicalSlot, "items_physical_pointer") : throw Missing("items_pointer_kind");
        private HexListNodeReference ItemHead(HexListHandle list) => current.ReadHexListHead(list) ?? throw Missing("items_temporary_head");
        private HexListNodeReference ItemNext(HexListNodeReference node) => current.ReadHexListNodeNext(node) ?? throw Missing("items_temporary_next");
        private RegistryObjectReference ItemPayload(HexListNodeReference node, RegistryKind kind)
        {
            var value = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("items_temporary_payload");
            return value.Kind == kind ? value : throw Missing("items_temporary_payload_kind");
        }
        private int ItemPerson(HexListNodeReference node) => Known(ItemPayload(node, RegistryKind.Person).PhysicalSlot, "items_sort_person_pointer");
        private bool UnregisterItem(RegistryObjectReference item)
        {
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Item) ?? throw Missing("items_unregister_descriptor");
            var node = current.ReadRegistryHead(descriptor) ?? throw Missing("items_unregister_head");
            while (node.Identity.HasValue)
            {
                if ((current.ReadRegistryNodePayload(node) ?? throw Missing("items_unregister_payload")) == item)
                { _ = current.RemoveAllRegisteredItemReferences(item); return true; }
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("items_unregister_next");
            }
            return false;
        }
    }
}

public sealed partial class SourceWorldContext
{
    //32B790's external first search is kept by its actual caller above.
    //The generic removal then rereads this permanent descriptor under lock,
    //deletes every matching pointer and leaves all Item fields untouched.
    internal int RemoveAllRegisteredItemReferences(RegistryObjectReference saved)
    {
        ConditionalRegistryEnter();
        var descriptor = ReadRegistryDescriptor(RegistryKind.Item)
            ?? throw new InvalidDataException("Current locked Item descriptor110 is unknown.");
        int key = RegistryKey(descriptor);
        bool heads = (RegistryHeadTablePresenceReader is null ? RegistryHeadTablePresent : RegistryHeadTablePresenceReader())
            ?? throw new InvalidDataException("Current locked Item head table is unknown.");
        RegistryListNodeReference node = heads && NativeListKeyInRange(key)
            ? ReadHexListHead(new(key)) is { } head ? new(head.Identity)
                : throw new InvalidDataException("Current locked Item head is unknown.") : new(null);
        int removed = 0;
        while (node.Identity.HasValue)
        {
            var payload = ReadRegistryNodePayload(node) ?? throw new InvalidDataException("Current locked Item payload is unknown.");
            var next = ReadRegistryNodeNext(node) ?? throw new InvalidDataException("Current locked Item next is unknown.");
            if (payload == saved) { RemoveAddressableRegistryNode(key, node); removed++; }
            node = next;
        }
        ConditionalRegistryExit();
        return removed;
    }
}
