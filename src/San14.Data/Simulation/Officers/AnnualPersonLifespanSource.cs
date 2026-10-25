using San14.Core;
using System.Collections.Immutable;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The reached annual current-person age pipeline. A qualified
    /// death is applied on the same current candidate and shared pool. Missing
    /// later business leaves still discard the enclosing request.</summary>
    internal sealed class AnnualPersonLifespanSource(ArmyCampaignRuleAdapter package, GameState state,
        SourceWorldContext current, WorldIndexContext pool,
        Action<string, GameState>? observe = null)
    {
        internal ImmutableArray<int>? RemainingDeathPersonPhysicalSlots { get; private set; }
        internal bool Stopped { get; private set; }

        internal GameState Run()
        {
            int mode = Mode();
            if (mode == 2) { Observe("lifespan.mode-rejected"); return state; }
            int selector = Known(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selector");
            int lifespan = Known(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x1C4 : 0x2C), "selected_lifespan_setting");
            if (unchecked((uint)lifespan) > 1) { Observe("lifespan.setting-rejected"); return state; }

            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw Missing("current_shared_pool_binding");
            PersonArmyCaptureInputs.BindCurrent(state, current);
            HexListHandle? candidates = null;
            try
            {
                Enter();
                candidates = current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor");
                Exit();
                if (!candidates.Identity.HasValue) throw Missing("temporary_allocation_profile");
                // This is the real current ThirdDate capture. Its low year
                // byte was passed to the native sort, which does not read it.
                _ = state.NativeMapSeason?.ThirdDate ?? throw Missing("current_third_date");
                var registry = current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("registered_person_list");
                foreach (int? entry in registry)
                {
                    int physical = Known(entry, "registered_person_payload");
                    ushort id = Identity(physical);
                    if (unchecked((ushort)(id - 4001)) <= 1099) continue;
                    // The reached bit is already independently owned in the
                    // residual flags channel. Other named behavior bits are
                    // neither queried nor required by this source operation.
                    int flags = current.ReadPersonBehavior(physical)?.UninterpretedFlags
                        ?? throw Missing("current_person_flags");
                    if (flags is < 0 or > ushort.MaxValue) throw Missing("current_person_flags");
                    if ((flags & 0x400) != 0) continue;
                    if (Age(physical, id) < 100) continue;
                    Enter();
                    var node = current.AllocateRegistryNode(new(candidates.Identity)) ?? throw Missing("temporary_node");
                    if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.Person, physical));
                    Exit();
                    Observe($"lifespan.qualified.{physical}");
                }
                Observe("lifespan.scan-completed");
                if (Head(candidates).Identity.HasValue)
                {
                    Sort(candidates);
                    Observe("lifespan.sorted");
                    var deaths = package.ConsumeNormalTurnDeathPeopleCurrent(state, current, pool,
                        Nodes(candidates).Select(Payload).ToArray(), observe);
                    state = deaths.State;
                    RemainingDeathPersonPhysicalSlots = deaths.Remaining;
                    Stopped = deaths.Stop;
                    if (deaths.Remaining is not null || deaths.Stop) return state;
                }
                ClearRelease(candidates); candidates = null;
                var arena = state.ReadArmyLifecycle() ?? throw Missing("lifecycle");
                var result = state.WithArmyLifecycle(arena with { Indexes = pool.ExportOwnedState() });
                Observe("lifespan.completed");
                return result;
            }
            finally { if (candidates is not null) ClearRelease(candidates); }
        }

        private int Mode() => state.CampaignEventWorld?.ScenarioMode ?? throw Missing("current_world_mode");
        private int Age(int physical, ushort id)
        {
            //229FC0 is the existing2A62E0 thunk. Keep the fresh mode read at
            //the reached helper, after the caller's identity/flags gates.
            int mode = Mode();
            if (mode is 2 or 3) throw Missing("special_mode_age");
            if (current.ResolveOfficerSource(id) != physical || !state.Officers.TryGetValue(id, out var person))
                throw Missing("current_person_birth_owner");
            int birth = person.BirthYear ?? throw Missing("current_person_birth_year");
            int year = state.NativeMapSeason?.ThirdDate.Year ?? throw Missing("current_native_year");
            return Math.Max(0, unchecked((short)year - birth + 1));
        }

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
                    var order = Nodes(list);
                    order.RemoveAll(value => value.Identity == node.Identity);
                    int at = order.FindIndex(value => value.Identity == insertion.Identity);
                    if (at < 0) throw Missing("sort_insertion_node");
                    order.Insert(at, node);
                    current.ReorderCurrentList(list, order);
                }
                node = savedNext;
            }
        }

        private bool Before(HexListNodeReference left, HexListNodeReference right)
        {
            int a = Payload(left), b = Payload(right);
            int first = Priority(Known(current.ReadPersonStatus(a), "sort_current_status"));
            int second = Priority(Known(current.ReadPersonStatus(b), "sort_previous_status"));
            if (first != second) return first < second;
            ushort previousId = Identity(b);
            return Identity(a) < previousId;
        }

        private static int Priority(byte status) => status switch
        { 5 => 0, 6 => 1, 8 => 2, 4 => 3, 3 => 4, 2 => 5, 1 => 6, _ => 7 };

        private List<HexListNodeReference> Nodes(HexListHandle list)
        {
            var values = new List<HexListNodeReference>(); var node = Head(list);
            while (node.Identity.HasValue) { values.Add(node); node = Next(node); }
            return values;
        }
        private HexListNodeReference Head(HexListHandle list) => current.ReadHexListHead(list) ?? throw Missing("temporary_head");
        private HexListNodeReference Next(HexListNodeReference node) => current.ReadHexListNodeNext(node) ?? throw Missing("temporary_next");
        private int Payload(HexListNodeReference node)
        {
            var value = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("temporary_payload");
            if (value.Kind != RegistryKind.Person) throw Missing("temporary_payload_kind");
            return Known(value.PhysicalSlot, "temporary_person_pointer");
        }
        private ushort Identity(int physical) => Known(current.ReadPersonIdentity(physical), "current_person_identity");
        private void ClearRelease(HexListHandle list)
        { Enter(); current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); Exit(); }
        private void Enter() { if (Known(current.ReadRegistryLockEnabled(), "list_lock_enabled")) current.EnterRegistryLock(); }
        private void Exit() { if (Known(current.ReadRegistryLockEnabled(), "list_unlock_enabled")) current.ExitRegistryLock(); }
        private void Observe(string point) { if (observe is not null) observe(point, GameStateCopy.Copy(state)); }
        private static T Known<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
        private static InvalidDataException Missing(string owner) => new("annual-person-lifespan." + owner);
    }
}
