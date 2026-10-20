using San14.Core;

namespace San14.Data;

public sealed record PersonFreedomHomeReceipt(int InitialCityPhysical, int[] CandidateCityOrder,
    int[] ShuffledCityOrder, int[] SortedCityOrder, int[] CandidateAreaOrder, int[] ShuffledAreaOrder,
    int CityDescriptor, int? AreaDescriptor);
public sealed record PersonFreedomHomeResult(int? NativeReturn, BaseReference? City,
    AreaReference? Area, PersonFreedomHomeReceipt? Receipt, string? UnresolvedOwner);

/// <summary>265250's null placement argument:22F430,1D7220,24DB90/254F70,
/// then managerB0 Area selection. Uses the operation's actual shared pool.
/// It does not write Person fields or publish the pool; a failed caller must
/// discard its operation-local context.</summary>
public sealed class PersonFreedomHomeSource
{
    private readonly Dictionary<BaseReference, byte[]> links = [];

    public PersonFreedomHomeSource(LoadedScenario source)
    {
        if (source.Metadata.FormatMarker != 0x66 || source.Snapshot.Cities.Count != 52 || source.Snapshot.Gates.Count != 11)
            throw Missing("source.265250_CE_fixed_Base_links");
        foreach (var city in source.Snapshot.Cities)
            links.Add(new(BaseKind.City, city.Slot), LinkWindow(city.LinkedLocationSlots));
        foreach (var gate in source.Snapshot.Gates)
            links.Add(new(BaseKind.Gate, gate.Slot), LinkWindow(gate.LinkedLocationSlots));
    }

    public PersonFreedomHomeResult Resolve(GameState state, SourceWorldContext current,
        int personPhysical, BaseReference oldHome, TacticSourceSamplerWorld sampler)
    {
        HexListHandle? cities = null, areas = null;
        try
        {
            var oldArea = FindBaseArea229FD0(current, oldHome);
            byte rawCity = Need(current.ReadAreaCitySlot(oldArea), "current.265250_old_Area35");
            var initial = current.ReadCityTableReference(rawCity < 52 ? rawCity : 0)
                ?? throw Missing("current.265250_initial_City_pointer");
            cities = Temporary(current);
            FillCities22F430(current, initial, cities);
            var candidateCities = Read(current, cities);
            int[] cityOrder = candidateCities.Select(x => Physical(x.Payload)).ToArray();
            int initialPhysical = Need(initial.PhysicalSlot, "current.initial_City_ordinal");
            if (candidateCities.Count == 0)
                return new(0, null, null, new(initialPhysical, [], [], [], [], [], cities.Identity!.Value, null), null);

            Shuffle(current, candidateCities, sampler);
            var shuffled = candidateCities.Select(x => Physical(x.Payload)).ToArray();
            var ordinal = candidateCities.Select((x, i) => (x.Payload, i)).ToDictionary(x => x.Payload, x => x.i);
            //24DB90 moves nodes, whereas1D7220 swaps only their payloads.
            //Stable insertion preserves the source's strict comparator ties.
            for (int index = 1; index < candidateCities.Count; index++)
            {
                var value = candidateCities[index]; int at = index;
                while (at > 0 && Preferred(current, state, personPhysical, value.Payload, candidateCities[at - 1].Payload, ordinal))
                { candidateCities[at] = candidateCities[at - 1]; at--; }
                candidateCities[at] = value;
            }
            Relink(current, cities, candidateCities);
            var chosenCity = Base(candidateCities[0].Payload);
            int chosenPhysical = Need(chosenCity.PhysicalSlot, "current.selected_City_ordinal");

            areas = Temporary(current);
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Area)
                ?? throw Missing("current.managerB0_Area_chain"))
            {
                var area = Area(entry ?? throw Missing("current.managerB0_Area_payload"));
                if (Need(current.ReadAreaCitySlot(area), "current.Area35") == chosenPhysical ||
                    (Need(current.ReadAreaValidityCode(area), "current.Area34") == chosenPhysical &&
                     Need(current.ReadAreaCurrentForceSlot(area), "current.22CD40_Area3A_Hex14") == 0))
                    Append(current, areas, new(RegistryKind.Area, area.PhysicalSlot));
            }
            var candidateAreas = Read(current, areas);
            int[] areaOrder = candidateAreas.Select(x => Physical(x.Payload)).ToArray();
            Shuffle(current, candidateAreas, sampler);
            if (candidateAreas.Count == 0) throw Missing("branch.265250_empty_selected_Area_native_head_dereference");
            var chosenArea = Area(candidateAreas[0].Payload);
            //265C50 first places E4 at Area. Its final Home uses Area35's
            //City if valid, otherwise Area34, without a second validity gate.
            byte homeSlot = Need(current.ReadAreaCitySlot(chosenArea), "current.selected_Area35");
            var homeCity = current.ReadCityTableReference(homeSlot < 52 ? homeSlot : 0)
                ?? throw Missing("current.selected_Area35_City_pointer");
            if (!Need(current.ReadBaseNativeValidity(homeCity), "current.selected_Area35_City_validity"))
            {
                byte fallback = Need(current.ReadAreaValidityCode(chosenArea), "current.selected_Area34");
                homeCity = current.ReadCityTableReference(fallback < 52 ? fallback : 0)
                    ?? throw Missing("current.selected_Area34_City_pointer");
            }
            return new(1, homeCity, chosenArea, new(initialPhysical,
                cityOrder, shuffled,
                candidateCities.Select(x => Physical(x.Payload)).ToArray(), areaOrder,
                candidateAreas.Select(x => Physical(x.Payload)).ToArray(), cities.Identity!.Value, areas.Identity), null);
        }
        catch (InvalidDataException error) { return new(null, null, null, null, error.Message); }
        finally
        {
            //265250 destroys Area temporary before its City list.
            if (areas is not null) ClearRelease(current, areas);
            if (cities is not null) ClearRelease(current, cities);
        }
    }

    public static AreaReference FindBaseArea229FD0(SourceWorldContext current, BaseReference home)
    {
        var hex = current.FindBaseHex(home) ?? throw Missing("current.22D640_Base_Hex");
        if (hex.PhysicalSlot is null)
            return current.ReadAreaTableReference(0) ?? throw Missing("current.Area0_pointer");
        ushort raw = Need(current.ReadHexAreaSlot(hex), "current.229FD0_Hex12");
        return current.ReadAreaTableReference(raw <= 500 ? raw : 0) ?? throw Missing("current.229FD0_Area_pointer");
    }

    internal bool AreForceCitiesAdjacent(SourceWorldContext current, int first, int second)
    {
        if (first == 0 || second == 0) return false;
        //2243A0 iterates all52 City pointers, including placeholders; its
        //predicate is only virtualB0 ownership, not native validity.
        for (int slot = 0; slot < 52; slot++)
        {
            var city = current.ReadCityTableReference(slot) ?? throw Missing("current.force_adjacency_City_pointer");
            if (Need(current.ReadBaseForceSlot(city), "current.force_adjacency_City_owner") != first) continue;
            var neighbors = Temporary(current);
            try
            {
                FillCities22F430(current, city, neighbors);
                foreach (var neighbor in Read(current, neighbors))
                    if (Need(current.ReadBaseForceSlot(Base(neighbor.Payload)), "current.force_adjacency_neighbor_owner") == second) return true;
            }
            finally { ClearRelease(current, neighbors); }
        }
        return false;
    }

    private void FillCities22F430(SourceWorldContext current, BaseReference initial, HexListHandle output)
    {
        current.ClearTemporaryHexList(output);
        foreach (var city in CandidateReferences(current, initial))
        {
            //Native pointer de-duplication only considers successful appends.
            if (!Read(current, output).Any(x => x.Payload == city)) Append(current, output, city);
        }
    }

    private IEnumerable<RegistryObjectReference> CandidateReferences(SourceWorldContext current, BaseReference initial)
    {
        ushort origin = Need(current.ReadBaseObjectLink(initial), "current.22F430_origin4E");
        foreach (byte first in Links(initial))
        {
            if (first == 0 || first == origin) continue;
            var firstBase = BaseLookup.Find(first, current);
            if (!Need(current.ReadBaseNativeValidity(firstBase), "current.22F430_first_Base_validity")) continue;
            if (Need(current.ReadBaseObjectLink(firstBase), "current.22F430_first4E") < 52)
            { yield return Registry(firstBase); continue; }
            foreach (byte second in Links(firstBase))
            {
                if (second == 0 || second == origin) continue;
                var secondBase = BaseLookup.Find(second, current);
                if (!Need(current.ReadBaseNativeValidity(secondBase), "current.22F430_second_Base_validity")) continue;
                if (Need(current.ReadBaseObjectLink(secondBase), "current.22F430_second4E") < 52)
                { yield return Registry(secondBase); continue; }
                ushort firstLocation = Need(current.ReadBaseObjectLink(firstBase), "current.22F430_first_Gate4E");
                foreach (byte third in Links(secondBase))
                {
                    if (third == 0 || third == origin || third == firstLocation) continue;
                    var thirdBase = BaseLookup.Find(third, current);
                    if (Need(current.ReadBaseNativeValidity(thirdBase), "current.22F430_third_Base_validity") &&
                        Need(current.ReadBaseObjectLink(thirdBase), "current.22F430_third4E") < 52)
                        yield return Registry(thirdBase);
                }
            }
        }
    }

    private static bool Preferred(SourceWorldContext current, GameState state, int person,
        RegistryObjectReference left, RegistryObjectReference right,
        IReadOnlyDictionary<RegistryObjectReference, int> ordinal)
    {
        int leftForce = Need(current.ReadBaseForceSlot(Base(left)), "current.254F70_left_Force");
        int rightForce = Need(current.ReadBaseForceSlot(Base(right)), "current.254F70_right_Force");
        int leftRuler = Ruler(leftForce), rightRuler = Ruler(rightForce);
        bool leftSpecial = leftForce is >= 46 and <= 50, rightSpecial = rightForce is >= 46 and <= 50;
        if (leftSpecial != rightSpecial) return !leftSpecial;
        int target = Need(current.ReadResentmentTarget(person)?.PhysicalSlot, "current.254F70_Person162_lookup_pointer");
        if (Need(current.ReadPersonNativeValidity(target), "current.254F70_Person162_validity") && (leftRuler == target) != (rightRuler == target))
            return leftRuler != target;
        if (Need(current.ReadPersonNativeValidity(leftRuler), "current.254F70_left_ruler_validity") &&
            Need(current.ReadPersonNativeValidity(rightRuler), "current.254F70_right_ruler_validity"))
        {
            var relations = OwnedPersonRelationInputs.ReadCurrentPersonFields(state, person)
                ?? throw Missing("current.254F70_PersonD0_eight_ids");
            bool hatesLeft = relations.D0.Contains(Need(current.ReadPersonIdentity(leftRuler), "current.left_ruler_ID10"));
            bool hatesRight = relations.D0.Contains(Need(current.ReadPersonIdentity(rightRuler), "current.right_ruler_ID10"));
            if (hatesLeft != hatesRight) return !hatesLeft;
        }
        byte leftFlags = 0, rightFlags = 0;
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Area) ?? throw Missing("current.254F70_managerB0"))
        {
            var area = Area(entry ?? throw Missing("current.managerB0_Area_payload"));
            byte city = Need(current.ReadAreaValidityCode(area), "current.254F70_Area34");
            if (city == Physical(left)) leftFlags |= Need(current.ReadAreaFlags38(area), "current.254F70_left_Area38");
            else if (city == Physical(right)) rightFlags |= Need(current.ReadAreaFlags38(area), "current.254F70_right_Area38");
        }
        if ((leftFlags & 1) != (rightFlags & 1)) return (leftFlags & 1) == 0;
        if ((leftFlags & 4) != (rightFlags & 4)) return (leftFlags & 4) == 0;
        return ordinal[left] < ordinal[right];

        int Person(int id) => Need((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot,
            "current.254F70_Person_lookup_pointer");
        int Ruler(int force)
        {
            ushort id = current.ReadForceRecord(force)?.RawRuler10 ?? throw Missing("current.254F70_Force10");
            return Person(id);
        }
    }

    internal static void Shuffle(SourceWorldContext current, List<Entry> nodes, TacticSourceSamplerWorld sampler)
    {
        int salt = 0, count = nodes.Count;
        for (int rounds = (count / 2) * count; rounds > 0; rounds--)
        {
            int first = TacticEffectSourceSamplerMath.Sample(count, 1, salt, 0, 0, 0, sampler);
            int second = count - 1 - TacticEffectSourceSamplerMath.Sample(count, 1, salt + 1, 0, 0, 0, sampler);
            salt = unchecked(salt + 2);
            if (first == second) continue;
            var left = nodes[first]; var right = nodes[second];
            current.WriteRegistryNodePayload(new(left.Node.Identity), right.Payload);
            current.WriteRegistryNodePayload(new(right.Node.Identity), left.Payload);
            nodes[first] = left with { Payload = right.Payload }; nodes[second] = right with { Payload = left.Payload };
        }
    }

    private static List<Entry> Read(SourceWorldContext current, HexListHandle list)
    {
        var result = new List<Entry>();
        var node = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("current.265250_temporary_head");
        while (node.Identity is int)
        {
            result.Add(new(new(node.Identity), current.ReadRegistryNodePayload(node) ?? throw Missing("current.265250_temporary_payload")));
            node = current.ReadRegistryNodeNext(node) ?? throw Missing("current.265250_temporary_next");
        }
        return result;
    }
    private static void Append(SourceWorldContext current, HexListHandle list, RegistryObjectReference payload)
    {
        var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("current.265250_node_allocation");
        if (node.Identity is not null) current.WriteRegistryNodePayload(node, payload);
    }
    private static void Relink(SourceWorldContext current, HexListHandle list, List<Entry> nodes)
    {
        current.ReorderCurrentList(list, nodes.Select(row => row.Node).ToArray());
    }
    private byte[] Links(BaseReference reference) => links.GetValueOrDefault(reference) ?? throw Missing("source.Base58_static_link_window");
    private static byte[] LinkWindow(IReadOnlyList<byte> values)
    { var result = new byte[10]; for (int i = 0; i < Math.Min(10, values.Count); i++) result[i] = values[i]; return result; }
    private static HexListHandle Temporary(SourceWorldContext current) =>
        current.CreateTemporaryHexList() ?? throw Missing("current.265250_temporary_allocation");
    private static void ClearRelease(SourceWorldContext current, HexListHandle list)
    { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
    private static RegistryObjectReference Registry(BaseReference value) => new(value.Kind == BaseKind.City ? RegistryKind.City : RegistryKind.Gate, value.PhysicalSlot);
    private static BaseReference Base(RegistryObjectReference value) => value.Kind == RegistryKind.City
        ? new(BaseKind.City, value.PhysicalSlot) : throw Missing("current.265250_candidate_not_City");
    private static AreaReference Area(RegistryObjectReference value) => value.Kind == RegistryKind.Area
        ? new(value.PhysicalSlot) : throw Missing("current.265250_candidate_not_Area");
    private static int Physical(RegistryObjectReference value) => Need(value.PhysicalSlot, "current.265250_pointer_ordinal");
    private static T Need<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new(owner);
    internal sealed record Entry(HexListNodeReference Node, RegistryObjectReference Payload);
}
