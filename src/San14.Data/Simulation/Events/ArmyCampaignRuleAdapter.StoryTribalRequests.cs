using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The ordinary366 service. Its caller resolves the actual Force
    /// and applies the instruction's max(count,1), as the source wrapper does.
    /// This service creates real ordinary Army receivers immediately; there is
    /// no deferred mission, presentation receipt or second task manager.</summary>
    internal (GameState State, int NativeReturn) ApplyStoryTribalRequestCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference force, int count)
        => new StoryTribalRequestCurrent(this, state, current, pool).Run(force, count);

    private sealed partial class StoryTribalRequestCurrent(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool)
    {
        internal (GameState State, int NativeReturn) Run(ForceReference force, int count)
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("same_current_pool");
            if (!N(current.ReadForceNativeValidity(force), "Force_validity") || count < 1) return (state, 0);
            int tribal = N(current.ReadForcePhysicalSlot(force), "Force_receiver");
            int troops = RequestTroops();
            //2243A0 is the first matching receiver in the fixed52 City table,
            //not ruler.Home, primary District numbering or registry90 order.
            var firstCity = FirstForceCity(tribal);
            if (N(current.ReadBaseResource(firstCity, BaseResourceKind.Troops), "first_City3C") < unchecked(troops * count))
                return (state, 0);
            var people = Temporary();
            try
            {
                int healthy = FillHealthyForcePeople(tribal, people);
                //2EAB60 counts qualifiers before attempting each node append.
                //A known dropped allocation must not be replaced by a Person.
                if (healthy < count) return (state, 0);
                SortPeople(people);
                var target = SelectTarget(force);
                if (target is null) return (state, 0);
                int tactic = tribal switch { 46 => 131, 47 => 132, 48 => 133, 49 => 134, 50 => 135, _ => 0 };
                int attempted = 0;
                foreach (var row in Nodes(people))
                {
                    if (row.Payload.Kind != RegistryKind.Person) throw Missing("commander_payload_kind");
                    int person = N(row.Payload.PhysicalSlot, "commander_receiver");
                    ushort homeId = N(current.ReadPersonFirstLocation(person), "commander_E2");
                    var home = BaseLookup.Find(homeId, current);
                    //The actual source packet is troops/14/11/tactic, action4,
                    //targetKind5 and source behavior2/0/1/1. Its1EC980 param2
                    //is explicitly1. This mature shared kernel owns bit0,
                    //numeric creation, troop and formation costs, registration,
                    //governor, groups, book usage and initial route writes.
                    state = package.CreateSpecialForceRaidArmyCurrent(state, current, pool,
                        person, home, target, troops, 14, 11, tactic);
                    if (++attempted == count) break;
                }
                //This tail runs after attempted calls, even if an actual
                //Army allocation failed. It is not conditional on final Units.
                int targetOwner = N(current.ReadBaseForceSlot(target), "target_Force_ordinal");
                var targetForce = current.ReadForceTableReference(targetOwner < 52 ? targetOwner : 0)
                    ?? throw Missing("target_Force_pointer");
                int previous = tribal < 52 ? package.ReadEditedEventFriendship(state, current, targetForce, tribal) : 0;
                state = package.ApplyNormalTurnForceRelationCurrent(state, current, pool,
                    targetForce, force, unchecked(-previous));
                return (state, 1);
            }
            finally { Release(people); }
        }

        private BaseReference FirstForceCity(int force)
        {
            for (int slot = 0; slot < 52; slot++)
            {
                var city = current.ReadCityTableReference(slot) ?? throw Missing("Force_City_table_pointer");
                if (N(current.ReadBaseForceSlot(city), "Force_City_owner") == force) return city;
            }
            throw Missing("first_Force_City_iterator_end");
        }

        private int FillHealthyForcePeople(int force, HexListHandle output)
        {
            int qualifiers = 0;
            if (force is < 1 or > 51) return 0;
            foreach (int? item in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100_registry"))
            {
                int person = N(item, "registered_Person_pointer");
                if (N(current.ReadPersonOldForceSlot(person), "iterator_Person_Force") != force ||
                    N(current.ReadPersonStatus(person), "iterator_Person_status") is not (>= 1 and <= 4)) continue;
                if (!N(current.ReadPersonNativeValidity(person), "available_Person_validity")) continue;
                int position = PersonPositionRepair.DecodeSecondLocation(N(current.ReadPersonSecondLocation(person), "available_Person_E4")).Kind;
                if (position is not (5 or 6) || N(OfficerHealthSourceAdapter.ReadByte(current, person), "available_Person161") != 0) continue;
                qualifiers++;
                Append(output, new(RegistryKind.Person, person));
            }
            return qualifiers;
        }

        private void SortPeople(HexListHandle list)
        {
            var abilities = new TacticRemakeCurrentAbilities(current, package._scenario, package.Seed.Language,
                SimulationRuleIdentity.ForLocale(package.Seed.Language));
            var keys = new Dictionary<int, (int Strength, int Leadership, int Id)>();
            var rows = Nodes(list);
            foreach (var row in rows)
            {
                int physical = N(row.Payload.PhysicalSlot, "sort_Person_pointer");
                int id = N(current.ReadPersonIdentity(physical), "sort_Person_identity");
                keys[physical] = (N(abilities.ResolveDimension(state, id, 1), "sort_Strength"),
                    N(abilities.ResolveDimension(state, id, 0), "sort_Leadership"), id);
            }
            //24F500/2563E0: Strength descending, then Leadership descending,
            //then Person identity ascending. This is not the raid's sum sort.
            Sort(list, rows, (left, right) =>
            {
                var a = keys[N(left.PhysicalSlot, "sort_first_Person")];
                var b = keys[N(right.PhysicalSlot, "sort_second_Person")];
                return a.Strength != b.Strength ? a.Strength > b.Strength :
                    a.Leadership != b.Leadership ? a.Leadership > b.Leadership : a.Id < b.Id;
            });
        }

        private int RequestTroops()
        {
            var parameters = state.NativeArmySimulation?.Parameters ?? throw Missing("parameter_owner");
            //The CE_TC and CE_SC packet rules were independently extracted
            //offline from each edition's actual request data reference.
            if (parameters.Language is "TC" or "SC") return 20000;
            throw Missing("army_troops_rule_identity");
        }

        private HexListHandle Temporary() => current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor");
        private void Release(HexListHandle list) { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        private void Append(HexListHandle list, RegistryObjectReference value)
        {
            var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("temporary_node_knowledge");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, value);
        }
        private List<(HexListNodeReference Node, RegistryObjectReference Payload)> Nodes(HexListHandle list)
        {
            var rows = new List<(HexListNodeReference, RegistryObjectReference)>();
            for (var node = current.ReadHexListHead(list) ?? throw Missing("temporary_head"); node.Identity.HasValue;
                node = current.ReadHexListNodeNext(node) ?? throw Missing("temporary_next"))
                rows.Add((node, current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("temporary_payload")));
            return rows;
        }
        private void Sort(HexListHandle list, List<(HexListNodeReference Node, RegistryObjectReference Payload)> rows,
            Func<RegistryObjectReference, RegistryObjectReference, bool> before)
        {
            if (rows.Count < 2) return;
            for (int index = 1; index < rows.Count; index++)
            {
                var item = rows[index]; int at = index;
                while (at > 0 && before(item.Payload, rows[at - 1].Payload)) { rows[at] = rows[at - 1]; at--; }
                rows[at] = item;
            }
            current.ReorderCurrentList(list, rows.Select(row => row.Node).ToArray());
        }
        private static T N<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("story.tribal-request." + field);
    }
}
