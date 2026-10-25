using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //42A790: place one dormant Item on the map. The enclosing NormalTurn
    //already owns the month boundary. This operation neither discovers the
    //item for a person nor displays an announcement or a player decision.
    internal GameState ApplyNormalTurnMonthlyItemEventsCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
        => new NormalTurnMonthlyItemEventsSource(this, state, current, pool).Run();

    private sealed class NormalTurnMonthlyItemEventsSource(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool)
    {
        private TacticSourceSamplerWorld? sampler;

        internal GameState Run()
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("shared_current_pool");
            var items = Temporary();
            try
            {
                //The physical Item pointer table contains202 entries, including
                //its actual slot0. It is independent of the held-Item registry.
                //No Item native-validity gate precedes this raw state test.
                for (int slot = 0; slot < 202; slot++)
                {
                    var item = current.ReadRegistryTableReference(RegistryKind.Item, slot)
                        ?? throw Missing("Item_table_pointer");
                    if (Need(current.ReadRegistryItemRawField50(item), "Item50") == 5) Add(items, item);
                }
                var itemNodes = Nodes(items);
                if (itemNodes.Count == 0) return state;
                if (itemNodes.Count > 1) PersonFreedomHomeSource.Shuffle(current, itemNodes, Sampler());
                var selected = itemNodes[0].Payload;
                int itemPhysical = Need(selected.PhysicalSlot, "selected_Item_physical");

                //32C0F0 converts the current World34 date independently of
                //the mutable App stream.3FCA40 also consumes World450/45C as
                //pure salts. TC1A4A880 is the immutable occurrence rate10.
                var date = state.NativeMapSeason?.ThirdDate ?? throw Missing("World34_date");
                int dateSalt = unchecked(date.Day - 31 + (date.Month + date.Year * 12) * 30);
                int roll = TacticEffectSourceSamplerMath.Sample(100, 1, itemPhysical, dateSalt, 0, 0, Sampler());
                if (roll >= 10) return state;

                var areas = Temporary();
                try
                {
                    foreach (var entry in current.ReadRegisteredAreaReferences() ?? throw Missing("AreaB0"))
                    {
                        var area = entry ?? throw Missing("AreaB0_payload");
                        if (!Need(current.ReadAreaNativeValidity(area), "Area_validity")) continue;
                        int rawForce = Need(current.ReadAreaCurrentForceSlot(area), "Area_Hex14");
                        var force = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0)
                            ?? throw Missing("Area_Force_pointer");
                        int forcePhysical = Need(current.ReadForcePhysicalSlot(force), "Area_Force60");
                        if (forcePhysical is >= 46 and <= 50) continue;
                        int areaPhysical = Need(area.PhysicalSlot, "Area_physical");
                        if (areaPhysical < 339) Add(areas, new(RegistryKind.Area, areaPhysical));
                    }
                    var areaNodes = Nodes(areas);
                    if (areaNodes.Count == 0) return state;
                    if (areaNodes.Count > 1) PersonFreedomHomeSource.Shuffle(current, areaNodes, Sampler());
                    var selectedArea = areaNodes[0].Payload;
                    int placement = Need(selectedArea.PhysicalSlot, "selected_Area_physical");
                    if (!Need(current.ReadAreaNativeValidity(new(placement)), "selected_Area_validity")) return state;

                    var table = state.Items ?? throw Missing("Item_owner_table");
                    var owner = table.GetValueOrDefault(itemPhysical) ?? throw Missing("selected_Item_owner");
                    if (owner.Id != itemPhysical) throw Missing("selected_Item_identity");
                    var lifecycle = owner.Lifecycle ?? throw Missing("selected_Item_lifecycle");
                    //These are direct50/4C stores. In particular42A790 does
                    //not clear4A, unregister/register the Item, or clear flags.
                    current.SetRegistryItemRawField50(itemPhysical, 2);
                    state = state with { Items = new(table) { [itemPhysical] = owner with
                    {
                        Lifecycle = lifecycle with
                        {
                            Status = ItemStatusKnowledge.Exact(2),
                            RetainedLocationCode = (ushort)Math.Clamp(placement, 0, 500)
                        }
                    } } };
                    return state;
                }
                finally { Release(areas); }
            }
            finally { Release(items); }
        }

        private TacticSourceSamplerWorld Sampler() => sampler ??= package.ReadArmyDestructionSampler(state, current)
            ?? throw Missing("World450_45C");

        private HexListHandle Temporary()
        {
            Enter();
            try { return current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor"); }
            finally { Exit(); }
        }
        private void Add(HexListHandle list, RegistryObjectReference payload)
        {
            Enter();
            try
            {
                var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("temporary_node");
                if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, payload);
            }
            finally { Exit(); }
        }
        private List<PersonFreedomHomeSource.Entry> Nodes(HexListHandle list)
        {
            var nodes = new List<PersonFreedomHomeSource.Entry>();
            var node = current.ReadHexListHead(list) ?? throw Missing("temporary_head");
            while (node.Identity.HasValue)
            {
                var payload = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("temporary_payload");
                nodes.Add(new(node, payload));
                node = current.ReadHexListNodeNext(node) ?? throw Missing("temporary_next");
            }
            return nodes;
        }
        private void Release(HexListHandle list)
        {
            if (!list.Identity.HasValue) return;
            Enter();
            try { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
            finally { Exit(); }
        }
        private void Enter() { if (Need(current.ReadRegistryLockEnabled(), "registry_lock")) current.EnterRegistryLock(); }
        private void Exit() { if (Need(current.ReadRegistryLockEnabled(), "registry_unlock")) current.ExitRegistryLock(); }
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("normal-turn.monthly-item-events." + field);
    }
}
