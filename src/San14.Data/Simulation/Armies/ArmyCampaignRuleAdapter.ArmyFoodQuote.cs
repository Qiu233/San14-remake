using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //24BAA0/2538C0 orders the already collected HomeArmies before269850.
    //It moves actual nodes on strict comparisons and retains equal receivers.
    internal void SortArmyFoodExpenseTemporaryCurrent(GameState state, SourceWorldContext current, HexListHandle list)
    {
        TacticRemakeCurrentAbilities? abilities = null;
        var nodes = new List<HexListNodeReference>();
        var node = current.ReadHexListHead(list) ?? throw ArmyFoodQuoteMissing("sort_head");
        while (node.Identity.HasValue)
        {
            nodes.Add(node);
            node = current.ReadHexListNodeNext(node) ?? throw ArmyFoodQuoteMissing("sort_next");
        }
        for (int index = 1; index < nodes.Count; index++)
        {
            var moving = nodes[index]; int at = index;
            while (at > 0 && Before(moving, nodes[at - 1])) at--;
            if (at == index) continue;
            nodes.RemoveAt(index); nodes.Insert(at, moving);
            current.ReorderCurrentList(list, nodes);
        }
        bool Before(HexListNodeReference leftNode, HexListNodeReference rightNode)
        {
            int left = Commander(leftNode), right = Commander(rightNode);
            byte a = Need(current.ReadPersonStatus(left), "sort_left_status");
            byte b = Need(current.ReadPersonStatus(right), "sort_right_status");
            if (a != b) return a < b;
            int rightLeadership = Leadership(right), leftLeadership = Leadership(left);
            if (rightLeadership < leftLeadership) return true;
            rightLeadership = Leadership(right); leftLeadership = Leadership(left);
            if (leftLeadership < rightLeadership) return false;
            return Need(current.ReadPersonIdentity(left), "sort_left_identity") < Need(current.ReadPersonIdentity(right), "sort_right_identity");
        }
        int Commander(HexListNodeReference cursor)
        {
            var pointer = current.ReadRegistryNodePayload(new(cursor.Identity)) ?? throw ArmyFoodQuoteMissing("sort_Army_payload");
            if (pointer.Kind != RegistryKind.Army) throw ArmyFoodQuoteMissing("sort_Army_kind");
            ushort id = Need(current.ReadArmyUnitCommanderId(new(Need(pointer.PhysicalSlot, "sort_Army_receiver"))), "sort_Commander12");
            return Need((current.ReadPersonLookupReference(id <= 6000 ? id : 0)
                ?? throw ArmyFoodQuoteMissing("sort_Person_pointer")).PhysicalSlot, "sort_Person_receiver");
        }
        int Leadership(int person)
        {
            int id = Need(current.ReadPersonIdentity(person), "sort_Person_identity");
            abilities ??= new(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            return Need(abilities.ResolveDimension(state, id, 0,
                (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect)), "sort_leadership");
        }
        static T Need<T>(T? value, string name) where T : struct => value ?? throw ArmyFoodQuoteMissing(name);
    }
    private static InvalidDataException ArmyFoodQuoteMissing(string field) => new("army.food_quote." + field);
}
