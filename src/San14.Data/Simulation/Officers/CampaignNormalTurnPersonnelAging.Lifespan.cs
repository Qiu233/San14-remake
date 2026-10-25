using San14.Core;
using System.Collections.Immutable;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal sealed partial class CampaignNormalTurnPersonnelAgingSource
    {
        internal ImmutableArray<int>? RemainingDeathPersonPhysicalSlots { get; private set; }
        internal bool Stopped { get; private set; }

        private const string LifespanYearExtensionKey = "simulation.person-lifespan.long-year-extension";

        /// <summary>27C3F0's monthly ordinary lifespan scan and ordered death
        /// consumers. Both native Person lists remain on the caller's pool.</summary>
        internal GameState RunLifespan()
        {
            var world = state.CampaignEventWorld ?? throw Missing("lifespan_World_mode");
            if (world.ScenarioMode == 2) return state;
            int setting = LifespanSetting();
            if (unchecked((uint)setting) > 1) return state;
            var season = state.NativeMapSeason ?? throw Missing("lifespan_World34");
            if (unchecked(world.ElapsedPeriodOffset + (int)season.AdvancedPeriods) <= 36) return state;
            if (package.Seed.Language != "TC") throw Missing("lifespan_SC_27C3F0_owner");
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw Missing("lifespan_shared_pool");
            //Normal Turn's route capture does not bind the separately saved
            //Item47/48/4A owner. Bind that existing owner on this same current;
            //this does not install parameters, register Items or import a pool.
            PersonArmyCaptureInputs.BindCurrent(state, current);

            HexListHandle? excluded = null, candidates = null;
            try
            {
                excluded = LifespanTemporary();
                var commands = pool.AssociatedTaskHandle ?? throw Missing("lifespan_associated_descriptor");
                var taskNode = current.ReadAssociatedChainHead(new(commands.Identity))
                    ?? throw Missing("lifespan_associated_head");
                var queries = new PersonReturnGovernorQueries(state, package._governorInputs, current);
                while (taskNode.Identity.HasValue)
                {
                    var task = current.ReadAssociatedChainPayload(taskNode)
                        ?? throw Missing("lifespan_associated_receiver");
                    if (Need(queries.ReadCurrentAssociatedVirtual98(task), "lifespan_associated_virtual98") == 16)
                    {
                        var members = current.ReadLinkedEntityMemberIds(task)
                            ?? throw Missing("lifespan_native16_members");
                        if (members.Count != 0)
                        {
                            int raw = unchecked((int)Need(members[0], "lifespan_native16_first_member"));
                            if (raw < 0) throw Missing("lifespan_native16_negative_member");
                            int physical = Need((raw <= 6000 ? current.ReadPersonLookupReference(raw)
                                : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "lifespan_native16_Person");
                            LifespanAppend(excluded, physical);
                        }
                    }
                    taskNode = current.ReadRegistryNodeNext(taskNode)
                        ?? throw Missing("lifespan_associated_next");
                }

                //The native tree retains Item pointers and source-time owner
                //identities. Its pointer ordering affects no numeric writer:
                //each later query is only a maximum over matching Items.
                var itemOwners = new Dictionary<int, ushort>();
                foreach (var entry in current.ReadRegistryReferences(RegistryKind.Item)
                    ?? throw Missing("lifespan_Item110"))
                {
                    if (entry is not { Kind: RegistryKind.Item, PhysicalSlot: int item })
                        throw Missing("lifespan_Item_receiver");
                    if (Need(current.ReadCaptureItemSubtype47(item), "lifespan_Item47") != 2) continue;
                    ushort owner = Need(current.ReadCaptureItemOwner4A(item), "lifespan_Item4A");
                    if (owner is >= 1 and <= 6000) itemOwners[item] = owner;
                }

                var date = state.NativeMapSeason?.ThirdDate ?? throw Missing("lifespan_candidate_date");
                candidates = LifespanTemporary();
                foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots()
                    ?? throw Missing("lifespan_Person100"))
                {
                    int person = Need(entry, "lifespan_Person_receiver");
                    ushort id = Need(current.ReadPersonIdentity(person), "lifespan_Person10");
                    ushort flags = Need(current.ReadOwnedReturnPersonFlags(person), "lifespan_Person15E");
                    if (id >= 4001 && (flags & 0x4000) == 0) continue;
                    if (Need(current.ReadPersonStatus(person), "lifespan_PersonE6") == 9) continue;
                    if ((flags & 0x4000) != 0 && Need(current.ReadPersonStatus(person), "lifespan_special_PersonE6") == 5) continue;
                    if ((flags & 0x400) != 0) continue;
                    int addedYears = (flags & 8) != 0 ? LifespanYearExtension() : 0;
                    if (setting == 1) addedYears = unchecked(addedYears + LifespanYearExtension());
                    int itemExtension = 0;
                    if (addedYears == 0)
                        foreach (var item in itemOwners)
                            if (item.Value == id)
                                itemExtension = Math.Max(itemExtension,
                                    Math.Min((int)Need(current.ReadItemLifespanExtension(item.Key), "lifespan_Item48"), 10));
                    var life = LifespanPerson(person);
                    int typeExtension = life.Kind == 1 ? 10 : 0;
                    int threshold = unchecked(life.TerminalYear + typeExtension + itemExtension + addedYears);
                    if (threshold > unchecked((short)date.Year)) continue;
                    if (LifespanContains(excluded, person)) continue;

                    int rate = LifespanDeathRate(person, itemExtension);
                    //3FCA40 adds actual World450/45C even though its final two
                    //explicit salts are0. This is not an App32 draw.
                    var sampler = package.ReadArmyDestructionSampler(state, current)
                        ?? throw Missing("lifespan_World450_45C");
                    int salt = unchecked((short)date.Year + unchecked((sbyte)date.Month) + unchecked((sbyte)date.Day));
                    int sample = TacticEffectSourceSamplerMath.Sample(100, 1, salt,
                        Need(current.ReadPersonIdentity(person), "lifespan_sample_Person10"), 0, 0, sampler);
                    if (sample < rate) LifespanAppend(candidates, person);
                }

                LifespanSort(candidates);
                var ordered = new List<int>();
                var node = LifespanHead(candidates);
                while (node.Identity.HasValue)
                {
                    ordered.Add(LifespanPayload(node));
                    node = LifespanNext(node);
                }
                var deaths = package.ConsumeNormalTurnDeathPeopleCurrent(state, current, pool, ordered, observe);
                state = deaths.State;
                RemainingDeathPersonPhysicalSlots = deaths.Remaining;
                Stopped = deaths.Stop;
                if (deaths.Remaining is not null || deaths.Stop) return state;
                Observe("normal.turn.lifespan.completed");
                return state;
            }
            finally
            {
                if (candidates is not null) LifespanRelease(candidates);
                if (excluded is not null) LifespanRelease(excluded);
            }
        }

        private int LifespanSetting()
        {
            int selector = Need(BattleRemakeCurrentSettings.ReadSelectorE58(state), "lifespan_SettingsE58");
            return Need(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x1C4 : 0x2C), "lifespan_Setting");
        }

        private int LifespanYearExtension()
        {
            var parameters = state.NativeArmySimulation?.Parameters ?? throw Missing("lifespan_parameters");
            if (parameters.Float32Values.ContainsKey(LifespanYearExtensionKey))
                throw Missing("lifespan_year_extension_integer");
            if (parameters.Int32Values.TryGetValue(LifespanYearExtensionKey, out int saved)) return saved;
            //TC typed/data.tsv254848: immutable1A4A430 signed i32=20.
            //A saved semantic override remains authoritative; no old owner or
            //unrelated notification offset is installed or substituted here.
            return package.Seed.Language == "TC" ? 20 : throw Missing("lifespan_year_extension_SC");
        }

        private OfficerLifespanState LifespanPerson(int physical)
        {
            ushort id = Need(current.ReadPersonIdentity(physical), "lifespan_owner_Person10");
            return current.ResolveOfficerSource(id) == physical && state.Officers.TryGetValue(id, out var person)
                ? person.Lifespan ?? throw Missing("lifespan_AE_B0")
                : throw Missing("lifespan_Person_owner");
        }

        private int LifespanDeathRate(int person, int itemExtension)
        {
            //269F50 recaptures date/settings before its Person validity gate.
            var date = state.NativeMapSeason?.ThirdDate ?? throw Missing("lifespan_rate_date");
            int setting = LifespanSetting();
            if (!Need(current.ReadPersonNativeValidity(person), "lifespan_rate_Person_validity")) return 0;
            int health = Need(OfficerHealthSourceAdapter.ReadByte(current, person), "lifespan_rate_health161");
            int healthFactor = health == 3 ? 4 : health;
            ushort flags = Need(current.ReadOwnedReturnPersonFlags(person), "lifespan_rate_flags15E");
            int addedYears = (flags & 8) != 0 ? LifespanYearExtension() : 0;
            int extension = (flags & 8) != 0 ? 0 : itemExtension;
            if (setting == 1)
            {
                addedYears = unchecked(addedYears + LifespanYearExtension());
                extension = 0;
            }
            var life = LifespanPerson(person);
            int years = unchecked((short)date.Year - (life.Kind == 1 ? 10 : 0) - life.TerminalYear - addedYears - extension + 1);
            int scale = setting == 1 || setting == 0 && years < 6 ? 4 : 8;
            return Math.Clamp(unchecked((healthFactor + 1) * years * scale), 0, 100);
        }

        private HexListHandle LifespanTemporary()
        {
            LifespanLock(true);
            try { return current.CreateTemporaryHexList() ?? throw Missing("lifespan_temporary_descriptor"); }
            finally { LifespanLock(false); }
        }
        private void LifespanAppend(HexListHandle handle, int person)
        {
            LifespanLock(true);
            try
            {
                var node = current.AllocateRegistryNode(new(handle.Identity)) ?? throw Missing("lifespan_temporary_node");
                if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.Person, person));
            }
            finally { LifespanLock(false); }
        }
        private bool LifespanContains(HexListHandle handle, int person)
        {
            var node = LifespanHead(handle);
            while (node.Identity.HasValue)
            {
                if (LifespanPayload(node) == person) return true;
                node = LifespanNext(node);
            }
            return false;
        }
        private HexListNodeReference LifespanHead(HexListHandle handle) =>
            current.ReadHexListHead(handle) ?? throw Missing("lifespan_temporary_head");
        private HexListNodeReference LifespanNext(HexListNodeReference node) =>
            current.ReadHexListNodeNext(node) ?? throw Missing("lifespan_temporary_next");
        private int LifespanPayload(HexListNodeReference node)
        {
            var payload = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("lifespan_temporary_payload");
            if (payload.Kind != RegistryKind.Person) throw Missing("lifespan_temporary_Person_kind");
            return Need(payload.PhysicalSlot, "lifespan_temporary_Person_pointer");
        }
        private void LifespanRelease(HexListHandle handle)
        {
            LifespanLock(true);
            try { current.ClearTemporaryHexList(handle); current.ReleaseTemporaryHexList(handle); }
            finally { LifespanLock(false); }
        }
        private void LifespanLock(bool enter)
        {
            if (!Need(current.ReadRegistryLockEnabled(), "lifespan_primary_lock")) return;
            if (enter) current.EnterRegistryLock(); else current.ExitRegistryLock();
        }
        private void LifespanSort(HexListHandle handle)
        {
            //24CAA0 stable insertion, ordered5/6/8/4/3/2/1/other then ID.
            var node = LifespanHead(handle);
            if (!node.Identity.HasValue) return;
            node = LifespanNext(node);
            while (node.Identity.HasValue)
            {
                var next = LifespanNext(node);
                var previous = current.ReadHexListNodePrevious(node) ?? throw Missing("lifespan_sort_previous");
                if (Before(node, previous))
                {
                    var insertion = previous;
                    var earlier = current.ReadHexListNodePrevious(previous) ?? throw Missing("lifespan_sort_previous");
                    while (earlier.Identity.HasValue && Before(node, earlier))
                    { insertion = earlier; earlier = current.ReadHexListNodePrevious(earlier) ?? throw Missing("lifespan_sort_previous"); }
                    var order = new List<HexListNodeReference>();
                    for (var cursor = LifespanHead(handle); cursor.Identity.HasValue; cursor = LifespanNext(cursor)) order.Add(cursor);
                    order.RemoveAll(value => value.Identity == node.Identity);
                    int index = order.FindIndex(value => value.Identity == insertion.Identity);
                    if (index < 0) throw Missing("lifespan_sort_insertion");
                    order.Insert(index, node);
                    current.ReorderCurrentList(handle, order);
                }
                node = next;
            }
            bool Before(HexListNodeReference first, HexListNodeReference second)
            {
                int a = LifespanPayload(first), b = LifespanPayload(second);
                int left = Priority(Need(current.ReadPersonStatus(a), "lifespan_sort_status"));
                int right = Priority(Need(current.ReadPersonStatus(b), "lifespan_sort_status"));
                if (left != right) return left < right;
                ushort oldId = Need(current.ReadPersonIdentity(b), "lifespan_sort_ID");
                return Need(current.ReadPersonIdentity(a), "lifespan_sort_ID") < oldId;
            }
            static int Priority(byte value) => value switch
            { 5 => 0, 6 => 1, 8 => 2, 4 => 3, 3 => 4, 2 => 5, 1 => 6, _ => 7 };
        }
    }
}
