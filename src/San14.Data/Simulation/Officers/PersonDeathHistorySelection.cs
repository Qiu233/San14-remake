using San14.Core;

namespace San14.Data;

/// <summary>The current22E930 and2B44E0(District,null,0) selection used by
/// death history. No appointment or persistent report is written here.</summary>
internal sealed class PersonDeathHistorySelection(GameState state, SourceWorldContext current,
    WorldIndexContext pool, TacticRemakeCurrentAbilities abilities,
    OwnedArmyGovernorComparatorInputs governorInputs, Func<int, int, int?>? panelReader = null)
{
    internal DistrictReference PrimaryDistrict(ForceReference retainedForce)
    {
        foreach (var entry in current.ReadArmyReturnRegisteredDistricts() ?? throw Missing("District_D0"))
        {
            var district = entry ?? throw Missing("District_payload");
            int physical = K(district.PhysicalSlot, "District_pointer");
            int force = ForceOrdinal(retainedForce);
            if (force is < 1 or > 51 || K(current.ReadDistrictForceId(physical), "District10_filter") != force) continue;
            ushort leader = K(current.ReadDistrictLeader(physical), "District12");
            int freshForce = ForceOrdinal(retainedForce);
            ushort ruler = freshForce == 0 ? (ushort)5014 :
                K(current.ReadForceRecord(K(retainedForce.PhysicalSlot, "Force_pointer"))?.RawRuler10, "Force10");
            if (leader == ruler) return district;
            if (K(current.ReadDistrictNumber(physical), "District11") == 1) return district;
        }
        return current.ReadDistrictTableReference(0) ?? throw Missing("WorldE0D0");
    }

    internal TransferPersonReference Select(DistrictReference retainedDistrict)
    {
        if (!K(current.ReadDistrictNativeValidity(retainedDistrict), "District_validity") || !PlayerOwns(retainedDistrict))
            return Lookup(5012); //World9DF8 is the actual5012 lookup slot, including its alias/null knowledge.
        var force = DistrictForce(retainedDistrict);
        ushort advisorId = K(current.ReadAssignmentForceAdvisor52(K(force.PhysicalSlot, "advisor_Force_pointer")), "Force52");
        var advisor = Lookup(advisorId);
        if (Valid(advisor)) return advisor;

        //The invalid-advisor branch resolves and retains its own fresh current ruler.
        var freshForce = DistrictForce(retainedDistrict);
        var sovereign = Lookup(K(current.ReadForceRecord(K(freshForce.PhysicalSlot, "ruler_Force_pointer"))?.RawRuler10, "ruler_Force10"));
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("current_pool_binding");
        Enter();
        var candidates = current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor");
        Exit();
        try
        {
            int districtOrdinal = K(retainedDistrict.PhysicalSlot, "iterator_District_ordinal");
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100"))
            {
                int person = K(entry, "Person100_payload");
                if (districtOrdinal is < 1 or > 51 || K(current.ReadPersonDistrictSlot(person), "iterator_PersonE0") != districtOrdinal) continue;
                byte status = K(current.ReadPersonStatus(person), "iterator_status");
                if (status is < 1 or > 4) continue;
                if (PersonPositionRepair.DecodeSecondLocation(K(current.ReadPersonSecondLocation(person), "classification_E4")).Kind == 27) continue;
                if (K(current.ReadPersonStatus(person), "candidate_status") == 1) continue;
                if (SpecialForce(person)) continue;
                if (SpecialPerson(person)) continue;
                if ((K(current.ReadArmyReturnPersonFlags196(person), "flags15E") & 0x4000) != 0) continue;
                if (new PersonCurrentSeniority(state, current).IsSenior(person, sovereign)) continue;
                Enter();
                var node = current.AllocateRegistryNode(new(candidates.Identity)) ?? throw Missing("temporary_node");
                if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.Person, person));
                Exit();
            }
            if (!candidates.Identity.HasValue) throw Missing("null_descriptor_count_profile");
            int count = K(current.ReadTemporaryHexListCount(candidates), "candidate_count");
            if (count == 0) return Lookup(5014); //World9E08, not a synthesized Person0.
            //The source has an empty conditional-lock pair before the actual sort.
            Enter(); Exit();
            Sort(candidates);
            return new(Payload(Head(candidates)));
        }
        finally
        {
            if (candidates.Identity.HasValue)
            { Enter(); current.ClearTemporaryHexList(candidates); current.ReleaseTemporaryHexList(candidates); Exit(); }
        }
    }

    private bool PlayerOwns(DistrictReference district)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("233880_player_pointer");
        if (!K(current.ReadForceNativeValidity(player), "233880_player_validity")) return false;
        byte owner = K(current.ReadDistrictForceId(K(district.PhysicalSlot, "233880_District_pointer")), "233880_District10");
        return owner == ForceOrdinal(player);
    }
    private ForceReference DistrictForce(DistrictReference district)
    {
        byte raw = K(current.ReadDistrictForceId(K(district.PhysicalSlot, "District_pointer")), "District10");
        return current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("Force_table");
    }
    private bool SpecialForce(int person)
    {
        ushort first = K(current.ReadPersonFirstLocation(person), "232A50_E2");
        if (first is < 1 or > 61) return false;
        return PersonForce(person) is >= 46 and <= 50;
    }
    private bool SpecialPerson(int person)
    {
        ushort id = Identity(person);
        bool band = false;
        for (int i = 0; i < 5; i++)
            if (id >= 4111 + i * 20 && id <= 4120 + i * 20) { band = true; break; }
        if (!band) return false;
        ushort first = K(current.ReadPersonFirstLocation(person), "233240_E2");
        return first is >= 1 and <= 61 && PersonForce(person) is not (>= 46 and <= 50);
    }
    private int PersonForce(int person) => ForceOrdinal(current.ReadPersonForce(person) ?? throw Missing("Person_Force_pointer"));
    private TransferPersonReference Lookup(ushort raw)
        => (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0)) ?? throw Missing("Person_lookup");
    private bool Valid(TransferPersonReference person) => person.PhysicalSlot is int physical && K(current.ReadPersonNativeValidity(physical), "Person_validity");
    private int ForceOrdinal(ForceReference force) => K(current.ReadForcePhysicalSlot(force), "Force_ordinal");

    private bool Before(HexListNodeReference left, HexListNodeReference right)
    {
        int a = Payload(left), b = Payload(right);
        int first = Ability(a, 2), second = Ability(b, 2);
        if (first != second) { first = Ability(a, 2); second = Ability(b, 2); return second < first; }
        first = Ability(a, 3); second = Ability(b, 3);
        if (first != second) { first = Ability(a, 3); second = Ability(b, 3); return second < first; }
        ushort firstWord = Word166(a), secondWord = Word166(b);
        bool preferred = firstWord < secondWord;
        if (firstWord == secondWord) preferred = Identity(a) < Identity(b);
        return preferred;
    }
    private int Ability(int physical, int dimension)
    {
        //22FE40's invalid receiver result is0; valid receivers use the existing FFF path.
        if (!K(current.ReadPersonNativeValidity(physical), "ability_Person_validity")) return 0;
        ushort id = Identity(physical);
        if (current.ResolveOfficerSource(id) != physical || !state.Officers.ContainsKey(id)) throw Missing("ability_current_binding");
        return abilities.ResolveDimension(state, id, dimension, panelReader) ?? throw Missing("effective_ability_" + dimension);
    }
    private ushort Word166(int physical) => K(governorInputs.ReadCurrentPhysicalWord166(state, physical), "word166");
    private ushort Identity(int physical) => K(current.ReadPersonIdentity(physical), "Person_ID10");

    private void Sort(HexListHandle list)
    {
        var node = Head(list);
        if (!node.Identity.HasValue) return;
        node = Next(node);
        while (node.Identity.HasValue)
        {
            var savedNext = Next(node);
            var previous = current.ReadHexListNodePrevious(node) ?? throw Missing("sort_previous");
            if (Before(node, previous))
            {
                var insertion = previous;
                var earlier = current.ReadHexListNodePrevious(previous) ?? throw Missing("sort_previous");
                while (earlier.Identity.HasValue && Before(node, earlier))
                { insertion = earlier; earlier = current.ReadHexListNodePrevious(earlier) ?? throw Missing("sort_previous"); }
                var order = new List<HexListNodeReference>();
                var scan = Head(list);
                while (scan.Identity.HasValue) { order.Add(scan); scan = Next(scan); }
                order.RemoveAll(value => value.Identity == node.Identity);
                int at = order.FindIndex(value => value.Identity == insertion.Identity);
                if (at < 0) throw Missing("sort_insertion");
                order.Insert(at, node); current.ReorderCurrentList(list, order);
            }
            node = savedNext;
        }
    }
    private HexListNodeReference Head(HexListHandle list) =>
        new((current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("temporary_head")).Identity);
    private HexListNodeReference Next(HexListNodeReference node) => current.ReadHexListNodeNext(node) ?? throw Missing("temporary_next");
    private int Payload(HexListNodeReference node)
    {
        var value = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("temporary_payload");
        if (value.Kind != RegistryKind.Person) throw Missing("temporary_payload_kind");
        return K(value.PhysicalSlot, "temporary_Person_pointer");
    }
    private void Enter() { if (K(current.ReadRegistryLockEnabled(), "lock_enabled")) current.EnterRegistryLock(); }
    private void Exit() { if (K(current.ReadRegistryLockEnabled(), "unlock_enabled")) current.ExitRegistryLock(); }
    private static T K<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new("person.death-history-selection." + owner);
}
