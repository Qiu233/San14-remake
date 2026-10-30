using San14.Core;

namespace San14.Data;

internal sealed record StoryCaptiveOccurrence(ForceReference Force, int PersonPhysicalSlot);
internal sealed record StoryPersonItemStateResult(GameState State, int NativeReturn,
    IReadOnlyList<StoryCaptiveOccurrence> OrderedCaptured);

public sealed partial class ArmyCampaignRuleAdapter
{
    internal StoryPersonItemStateResult ApplyStoryPersonItemStateCurrent(GameState state,
        SourceWorldContext current, int code, IReadOnlyList<int> arguments,
        Func<int, int> resolvePerson, Func<int, ForceReference> resolveForce)
    {
        if (arguments.Count != 8) throw StoryPersonItemMissing("operands");
        switch (code)
        {
            case 170:
            {
                int person = resolvePerson(arguments[0]);
                if (!StoryPersonItemNeed(current.ReadPersonNativeValidity(person), "lifespan_Person_validity")) return new(state, 1, []);
                ushort before = StoryPersonItemNeed(current.ReadArmyReturnPersonFlags196(person), "lifespan_Person_flags");
                current.WriteArmyReturnPersonFlags196(person, (ushort)(before | 8));
                int id = StoryPersonItemNeed(current.ReadPersonIdentity(person), "lifespan_Person_identity");
                var owner = state.Officers.GetValueOrDefault(id) ?? throw StoryPersonItemMissing("lifespan_Person_owner");
                return new(state with { Officers = new(state.Officers) { [id] = owner with
                    { Behavior = current.ReadPersonBehavior(person) ?? throw StoryPersonItemMissing("lifespan_Person_behavior") } } }, 1, []);
            }
            case 254:
            {
                int mask = arguments[0] switch { 5006 => 1, 5008 => 2, _ => 0 };
                if (mask == 0) return new(state, 1, []);
                var world = state.CampaignEventWorld ?? throw StoryPersonItemMissing("encounter_World");
                byte flags = StoryPersonItemNeed(world.SpecialPersonEncounterFlags, "encounter_World_flags");
                byte after = (byte)(flags & ~mask);
                current.ConditionSpecialPersonEncounterFlags = after;
                return new(state with { CampaignEventWorld = world with { SpecialPersonEncounterFlags = after } }, 1, []);
            }
            case 330:
            {
                //Both selectors resolve before either validity test, in source order.
                var force = resolveForce(arguments[0]);
                int person = resolvePerson(arguments[1]);
                if (!StoryPersonItemNeed(current.ReadForceNativeValidity(force), "captive_Force_validity") ||
                    !StoryPersonItemNeed(current.ReadPersonNativeValidity(person), "captive_Person_validity")) return new(state, 1, []);
                byte status = StoryPersonItemNeed(current.ReadPersonStatus(person), "captive_Person_status");
                if (status is 1 or 9) return new(state, 1, []);
                var oldForce = current.ReadPersonForce(person) ?? throw StoryPersonItemMissing("captive_Person_Force");
                if (oldForce == force) return new(state, 1, []);
                state = new EventPersonMovementCurrent(this, state, current,
                    new EditedEventResultSelectors(current, null, () => BindCampaignStoryInputs(state, current, false)))
                    .ApplyStoryForceCaptiveCurrent(person, force);
                bool manager = StoryPersonItemNeed(current.ReadEventManagerPresent(), "capture_result_manager");
                return new(state, 1, manager && StoryPersonItemNeed(current.ReadForceNativeValidity(force), "capture_result_Force_validity") &&
                    StoryPersonItemNeed(current.ReadPersonNativeValidity(person), "capture_result_Person_validity") &&
                    (StoryPersonItemNeed(current.ReadArmyReturnPersonFlags196(person), "capture_result_Person_flags") & 0x4000) == 0
                        ? new[] { new StoryCaptiveOccurrence(force, person) } : []);
            }
            case 380:
            case 445:
                return ApplyStoryItemPlacementCurrent(state, current, code, arguments);
            default: throw StoryPersonItemMissing("opcode");
        }
    }

    private static StoryPersonItemStateResult ApplyStoryItemPlacementCurrent(GameState state,
        SourceWorldContext current, int code, IReadOnlyList<int> arguments)
    {
        if (arguments[0] < 0) throw StoryPersonItemMissing("negative_Item_table_selector");
        var item = current.ReadRegistryTableReference(RegistryKind.Item, arguments[0] < 202 ? arguments[0] : 0)
            ?? throw StoryPersonItemMissing("Item_table_pointer");
        if (!StoryPersonItemNeed(current.ReadRegistryObjectNativeValidity(item), "Item_validity")) return new(state, 0, []);
        int? hiddenLocation = null;
        if (code == 380)
        {
            if (arguments[1] < 0) throw StoryPersonItemMissing("negative_Area_table_selector");
            var area = current.ReadAreaTableReference(arguments[1] < 501 ? arguments[1] : 0)
                ?? throw StoryPersonItemMissing("hidden_Item_Area_pointer");
            if (!StoryPersonItemNeed(current.ReadAreaNativeValidity(area), "hidden_Item_Area_validity")) return new(state, 0, []);
            hiddenLocation = StoryPersonItemNeed(area.PhysicalSlot, "hidden_Item_Area_identity");
        }
        int physical = StoryPersonItemNeed(item.PhysicalSlot, "Item_receiver");
        var items = state.Items ?? throw StoryPersonItemMissing("Item_inventory");
        var owner = items.GetValueOrDefault(physical) ?? throw StoryPersonItemMissing("Item_owner");
        var lifecycle = owner.Lifecycle ?? throw StoryPersonItemMissing("Item_lifecycle");
        byte flags = StoryPersonItemNeed(lifecycle.RetainedFlags, "Item_flags");
        if (code == 380)
        {
            //Caller clears reservation before the independent324580 Area gate.
            lifecycle = lifecycle with { RetainedFlags = (byte)(flags & 0xF7) };
            owner = owner with { Lifecycle = lifecycle };
            int location = hiddenLocation!.Value;
            if (location is >= 1 and <= 500)
            {
                current.SetCaptureItemOwner4A(physical, 0);
                current.SetRegistryItemStatus(physical, ItemStatusKnowledge.Exact(2));
                owner = owner with { Holder = new(null), Lifecycle = lifecycle with
                    { Status = ItemStatusKnowledge.Exact(2), RetainedLocationCode = (ushort)location } };
                _ = current.RemoveAllRegisteredItemReferences(item);
                state = PublishStoryItemRegistry(state, current);
            }
        }
        else
        {
            //324580 mode3 precedes the caller's separate flags/kind stores.
            current.SetRegistryItemStatus(physical, ItemStatusKnowledge.Exact(3));
            current.SetCaptureItemOwner4A(physical, 0);
            owner = owner with { Holder = new(null), Lifecycle = lifecycle with
                { Status = ItemStatusKnowledge.Exact(3), RetainedLocationCode = 0 } };
            _ = current.RemoveAllRegisteredItemReferences(item);
            state = PublishStoryItemRegistry(state, current);
            owner = owner with { Lifecycle = owner.Lifecycle! with { RetainedFlags = (byte)(flags & 0xFA) } };
            var definition = state.ItemDefinitions.GetValueOrDefault(owner.DefinitionId)
                ?? throw StoryPersonItemMissing("broken_Item_definition");
            var appearance = definition.Appearance ?? throw StoryPersonItemMissing("broken_Item_current_kind");
            state = state with { ItemDefinitions = new(state.ItemDefinitions)
                { [owner.DefinitionId] = definition with { Appearance = appearance with { Kind = 0 } } } };
        }
        return new(state with { Items = new(state.Items!) { [physical] = owner } }, 1, []);
    }

    private static GameState PublishStoryItemRegistry(GameState state, SourceWorldContext current)
    {
        var pool = current.ReadOwnedCurrentListPool() ?? throw StoryPersonItemMissing("Item_registry_pool");
        var lifecycle = state.ReadArmyLifecycle() ?? throw StoryPersonItemMissing("Item_registry_owner");
        return state.WithArmyLifecycle(lifecycle with { Indexes = pool.ExportOwnedState() });
    }

    private static T StoryPersonItemNeed<T>(T? value, string field) where T : struct => value ?? throw StoryPersonItemMissing(field);
    private static InvalidDataException StoryPersonItemMissing(string field) => new("story.person_item_state." + field);
}
