using San14.Core;

namespace San14.Data;

public static partial class PersonReturnArrivalSource
{
    //24E4C0 insertion-sort moves existing nodes; it neither replaces the
    //task's member array nor changes the shared pool's list ownership.
    private static void SortMembers(GameState state, SourceWorldContext current,
        HexListHandle list, IArmyReturnGovernorCurrentQueries queries)
    {
        var node = Head();
        if (!node.Identity.HasValue) return;
        node = Next(node);
        while (node.Identity.HasValue)
        {
            var next = Next(node);
            var previous = Previous(node);
            if (Preferred(Person(node), Person(previous)))
            {
                var insertion = previous;
                var earlier = Previous(previous);
                while (earlier.Identity.HasValue && Preferred(Person(node), Person(earlier)))
                { insertion = earlier; earlier = Previous(earlier); }
                var ordered = new List<HexListNodeReference>();
                for (var at = Head(); at.Identity.HasValue; at = Next(at))
                    if (at.Identity != node.Identity) ordered.Add(at);
                int index = ordered.FindIndex(value => value.Identity == insertion.Identity);
                if (index < 0) throw Unknown("sort.insertion_node");
                ordered.Insert(index, node);
                current.ReorderCurrentList(list, ordered);
            }
            node = next;
        }

        bool Preferred(int left, int right)
        {
            //255970 consumes status equality twice before Office E7, then
            //2A62E0 (age, not merit), and finally the raw identity word.
            bool Status(int physical, byte value) => Known(current.ReadPersonStatus(physical), "sort.PersonE6") == value;
            if (Status(left, 1) && !Status(right, 1)) return true;
            if (!Status(left, 1) && Status(right, 1)) return false;
            if (Status(left, 2) && !Status(right, 2)) return true;
            if (!Status(left, 2) && Status(right, 2)) return false;
            byte l = Known(queries.ReadCurrentComparatorFields(left)?.PersonOfficeE7, "sort.Person_officeE7");
            byte r = Known(queries.ReadCurrentComparatorFields(right)?.PersonOfficeE7, "sort.Person_officeE7");
            if (l != 0 && r != 0)
            {
                if (l < r) return true;
                if (l != r) return false;
            }
            int leftAge = Age(left), rightAge = Age(right);
            if (rightAge < leftAge) return true;
            leftAge = Age(left); rightAge = Age(right);
            return rightAge <= leftAge && Identity(left) < Identity(right);
        }

        int Age(int person) => PersonCurrentAgeSource.Read(state, current, person);
        ushort Identity(int person) => Known(current.ReadPersonIdentity(person), "sort.Person10");
        int Person(HexListNodeReference at)
        {
            var payload = current.ReadRegistryNodePayload(new(at.Identity)) ?? throw Unknown("sort.Person_payload");
            if (payload.Kind != RegistryKind.Person) throw Unknown("sort.Person_payload_kind");
            return Known(payload.PhysicalSlot, "sort.Person_pointer");
        }
        HexListNodeReference Head() => current.ReadHexListHead(list) ?? throw Unknown("sort.head");
        HexListNodeReference Next(HexListNodeReference at) => current.ReadHexListNodeNext(at) ?? throw Unknown("sort.next");
        HexListNodeReference Previous(HexListNodeReference at) => current.ReadHexListNodePrevious(at) ?? throw Unknown("sort.previous");
    }
}
