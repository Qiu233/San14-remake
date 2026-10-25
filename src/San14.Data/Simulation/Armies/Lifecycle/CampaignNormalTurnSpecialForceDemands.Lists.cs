using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class NormalTurnSpecialForceRaidsSource
    {
        private HexListHandle Temporary() => current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor");
        private int Count(HexListHandle list) => K(current.ReadTemporaryHexListCount(list), "temporary_count");
        private void Release(HexListHandle list) { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        private void Append(HexListHandle list, BaseReference at) => Append(list,
            new RegistryObjectReference(at.Kind == BaseKind.City ? RegistryKind.City : RegistryKind.Gate, at.PhysicalSlot));
        private void Append(HexListHandle list, RegistryObjectReference value)
        {
            var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("temporary_node");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, value);
        }
        private List<(HexListNodeReference Node, RegistryObjectReference Value)> Nodes(HexListHandle list)
        {
            var result = new List<(HexListNodeReference, RegistryObjectReference)>();
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("temporary_head");
            while (node.Identity.HasValue)
            {
                result.Add((new(node.Identity), current.ReadRegistryNodePayload(node) ?? throw Missing("temporary_payload")));
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("temporary_next");
            }
            return result;
        }
        private IEnumerable<BaseReference> Bases(HexListHandle list)
        {
            foreach (var row in Nodes(list)) yield return Base(row.Value);
        }
        private static BaseReference Base(RegistryObjectReference value) => new(value.Kind switch
        {
            RegistryKind.City => BaseKind.City, RegistryKind.Gate => BaseKind.Gate, _ => throw Missing("temporary_Base_kind")
        }, value.PhysicalSlot);
        private IEnumerable<int> Persons(HexListHandle list)
        {
            foreach (var row in Nodes(list))
                yield return row.Value.Kind == RegistryKind.Person ? K(row.Value.PhysicalSlot, "temporary_Person") : throw Missing("temporary_Person_kind");
        }

        private void FillHealthyHomePeople(BaseReference home, HexListHandle people, HexListHandle excluded)
        {
            //2D7550 clears only its output. Its source iterator uses E2 and
            //status1..4, then byte1C7, E4, return flag and linked-command gates.
            current.ClearTemporaryHexList(people);
            ushort location = K(current.ReadBaseObjectLink(home), "people_Base4E");
            foreach (int person in EnumerateBaseResidents(current, location, Missing))
            {
                if ((K(current.ReadPersonResetWord1FF(person), "people_Person1C7") & 255) != 0) continue;
                if (K(current.ReadPersonSecondLocation(person), "people_PersonE4") != location) continue;
                if ((K(current.ReadOwnedReturnPersonFlags(person), "people_Person15E") & 1) != 0) continue;
                if ((current.ReadPersonLinkedEntity(person) ?? throw Missing("people_linked_command")).Identity.HasValue) continue;
                Append(people, new RegistryObjectReference(RegistryKind.Person, person));
            }
            foreach (int person in Persons(people))
                if (K(OfficerHealthSourceAdapter.ReadByte(current, person), "people_Person161") is 2 or 3)
                    Append(excluded, new RegistryObjectReference(RegistryKind.Person, person));
            foreach (int person in Persons(excluded))
                foreach (var row in Nodes(people))
                    if (row.Value is { Kind: RegistryKind.Person } && row.Value.PhysicalSlot == person)
                        current.RemoveFacilityHexTemporary(people, row.Node);
        }

        private void SortTargets(HexListHandle list, BaseReference origin)
        {
            Sort(list, (left, right) =>
            {
                var a = Base(left); var b = Base(right);
                int at = current.ReadBaseCurrentHexIndex(origin);
                int first = ArmyRouteSearch.Distance(at, current.ReadBaseCurrentHexIndex(a));
                int second = ArmyRouteSearch.Distance(at, current.ReadBaseCurrentHexIndex(b));
                if (first != second) return first < second;
                int aTroops = K(current.ReadBaseResource(a, BaseResourceKind.Troops), "sort_first_troops");
                int bTroops = K(current.ReadBaseResource(b, BaseResourceKind.Troops), "sort_second_troops");
                return aTroops != bTroops ? aTroops < bTroops :
                    K(current.ReadBaseObjectLink(a), "sort_first_Base4E") < K(current.ReadBaseObjectLink(b), "sort_second_Base4E");
            });
        }
        private void SortCommanders(HexListHandle list)
        {
            var abilities = new TacticRemakeCurrentAbilities(current, package._scenario, package.Seed.Language,
                SimulationRuleIdentity.ForLocale(package.Seed.Language));
            Sort(list, (left, right) =>
            {
                if (left.Kind != RegistryKind.Person || right.Kind != RegistryKind.Person) throw Missing("sort_Person_kind");
                int a = Identity(K(left.PhysicalSlot, "sort_first_Person")), b = Identity(K(right.PhysicalSlot, "sort_second_Person"));
                int first = unchecked(K(abilities.ResolveDimension(state, a, 1), "sort_first_Strength") +
                    K(abilities.ResolveDimension(state, a, 0), "sort_first_Leadership"));
                int second = unchecked(K(abilities.ResolveDimension(state, b, 1), "sort_second_Strength") +
                    K(abilities.ResolveDimension(state, b, 0), "sort_second_Leadership"));
                return first == second ? a < b : first >= second;
            });
        }
        private void Sort(HexListHandle list, Func<RegistryObjectReference, RegistryObjectReference, bool> before)
        {
            var nodes = Nodes(list);
            for (int i = 1; i < nodes.Count; i++)
            {
                var item = nodes[i]; int at = i;
                while (at > 0 && before(item.Value, nodes[at - 1].Value))
                { nodes[at] = nodes[at - 1]; at--; }
                nodes[at] = item;
            }
            current.ReorderCurrentList(list, nodes.Select(row => row.Node).ToArray());
        }
    }
}
