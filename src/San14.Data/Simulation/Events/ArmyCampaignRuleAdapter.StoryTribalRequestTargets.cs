using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class StoryTribalRequestCurrent
    {
        private BaseReference? SelectTarget(ForceReference tribalForce)
        {
            int tribal = N(current.ReadForcePhysicalSlot(tribalForce), "target_tribal_receiver");
            int? selectedProvince = PersonReturnPathSource.ReadTribalRequestProvinceCurrent(state, current, tribal);
            if (selectedProvince is not int province) return null;
            var cities = Temporary();
            try
            {
                foreach (var row in current.ReadRegistryReferences(RegistryKind.City) ?? throw Missing("target_City90_registry"))
                {
                    if (row is not { Kind: RegistryKind.City, PhysicalSlot: int physical }) throw Missing("target_City_payload");
                    var city = current.ReadCityTableReference(physical < 52 ? physical : 0) ?? throw Missing("target_City_pointer");
                    if (!N(current.ReadBaseNativeValidity(city), "target_City_validity")) continue;
                    var actualProvince = current.ReadConditionCityState(physical) ?? throw Missing("target_City_Province");
                    if (N(actualProvince.PhysicalSlot, "target_City_Province_receiver") != province || physical == 28 && tribal == 49 || IsPlayerCity(city)) continue;
                    int owner = N(current.ReadBaseForceSlot(city), "target_City_Force_ordinal");
                    var force = current.ReadForceTableReference(owner < 52 ? owner : 0) ?? throw Missing("target_City_Force_pointer");
                    var qualifies = PersonReturnPathSource.ReadSpecialPersonnelEligibility(state, current,
                        force, tribal, package.ReadApplicationGameplay());
                    if (qualifies.Value is not bool known) throw Missing(qualifies.UnresolvedOwner ?? "target_City_geographical_qualification");
                    if (known) Append(cities, new(RegistryKind.City, physical));
                }
                var rows = Nodes(cities);
                var keys = new Dictionary<int, (int Troops, int People, int Size)>();
                foreach (var row in rows)
                {
                    int physical = N(row.Payload.PhysicalSlot, "target_sort_City_receiver");
                    var city = current.ReadCityTableReference(physical) ?? throw Missing("target_sort_City_pointer");
                    ushort home = N(current.ReadBaseObjectLink(city), "target_sort_City4E");
                    //2304B0 is exactly CFootholdPersonIterator mode0. The
                    //shared reader preserves registry order and status1..4;
                    //there is no extra native-valid or deployed-Person gate.
                    int serving = EnumerateBaseResidents(current, home, Missing).Count();
                    keys[physical] = (N(current.ReadBaseResource(city, BaseResourceKind.Troops), "target_sort_City3C"),
                        serving, N(current.ReadCitySizeB8(city), "target_sort_CityB8"));
                }
                Sort(cities, rows, (left, right) =>
                {
                    int first = N(left.PhysicalSlot, "target_sort_first_City");
                    int second = N(right.PhysicalSlot, "target_sort_second_City");
                    var a = keys[first]; var b = keys[second];
                    return a.Troops != b.Troops ? a.Troops > b.Troops : a.People != b.People ? a.People > b.People :
                        a.Size != b.Size ? a.Size > b.Size : first > second;
                });
                return rows.Count == 0 ? null : current.ReadCityTableReference(
                    N(rows[0].Payload.PhysicalSlot, "selected_target_City")) ?? throw Missing("selected_target_City_pointer");
            }
            finally { Release(cities); }
        }

        private bool IsPlayerCity(BaseReference city)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("target_player_Force");
            if (!N(current.ReadForceNativeValidity(player), "target_player_validity")) return false;
            int owner = N(current.ReadBaseForceSlot(city), "target_player_City_Force");
            return current.ReadForceTableReference(owner < 52 ? owner : 0) == player;
        }
    }
}
