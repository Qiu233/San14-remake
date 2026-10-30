using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal IReadOnlyList<int> ReadOriginalAiForceGemDefinitionsCurrent(GameState state, SourceWorldContext current, int requestedForce)
    {
        var force = current.ReadForceTableReference(requestedForce is >= 0 and < 52 ? requestedForce : 0)
            ?? throw AiDefinitionMissing("inventory_Force_pointer");
        var result = new List<int>();
        for (int lookup = 1; lookup <= 30; lookup++)
        {
            var gem = current.ReadSkillGemReference(lookup) ?? throw AiDefinitionMissing("inventory_Gem_pointer");
            int count = DefinitionNeed(TalentGemAcquisitionSource.Read(state.GemForceInventory, force, new(lookup)), "Force122_count");
            int physical = DefinitionNeed(gem.PhysicalSlot, "inventory_Gem_identity");
            for (int i = 0; i < count; i++) result.Add(physical);
        }
        return result;
    }

    internal IReadOnlyList<int>? ReadOriginalAiPossibleFacilityDefinitionsCurrent(GameState state,
        SourceWorldContext current, int officerId, int physicalHex, bool prepareConstructionInputs)
    {
        int person = (officerId is >= 0 and <= 6000 ? current.ReadPersonLookupReference(officerId)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot ?? throw AiDefinitionMissing("possible_Person_pointer");
        var hex = new HexReference(physicalHex);
        // CHexData slot3 is the shared constant true predicate. A typed Hex
        // reference is already a non-null source pointer; no terrain guess.
        if (!DefinitionNeed(current.ReadPersonNativeValidity(person), "possible_Person_validity")) return null;
        // Bind the mature construction inputs on this already borrowed current;
        // neither the query nor its31 definitions captures another world.
        if (prepareConstructionInputs) BindFacilityConstructionInputs(state, current);
        var result = new List<int>();
        for (int lookup = 0; lookup < 31; lookup++)
        {
            var definition = current.ReadObjectDefinitionReference(lookup) ?? throw AiDefinitionMissing("possible_Facility_pointer");
            if (DefinitionNeed(current.ReadObjectDefinitionValidity(definition), "possible_Facility_validity") &&
                FacilityConstructionLegality.CheckCommander(state, current, person, hex, definition))
                result.Add(DefinitionNeed(definition.PhysicalSlot, "possible_Facility_identity"));
        }
        int owner = DefinitionNeed(current.ReadPersonOldForceSlot(person), "possible_Person_Force");
        int player = DefinitionNeed(OwnedCurrentPlayerForce.ReadCurrentReference(state, current)?.PhysicalSlot, "possible_player_Force");
        if (owner == player)
        {
            int raw = DefinitionNeed(current.ReadPersonDistrictSlot(person), "possible_PersonE0");
            int district = DefinitionNeed(current.ReadDistrictTableReference(raw < 52 ? raw : 0)?.PhysicalSlot, "possible_District_pointer");
            uint mask = 0;
            for (int i = 0; i < 4; i++) mask |= (uint)DefinitionNeed(current.ReadDistrictRawByte(district, 0x1C + i), "possible_District1C") << (i * 8);
            // The native second pass removes disallowed definition pointers
            // for lookup1..30; it does not reorder the retained list.
            for (int lookup = 1; lookup <= 30; lookup++)
                if ((mask & (1u << lookup)) == 0)
                {
                    int physical = DefinitionNeed(current.ReadObjectDefinitionReference(lookup)?.PhysicalSlot, "possible_permission_Facility");
                    result.RemoveAll(value => value == physical);
                }
        }
        return result;
    }

    private static T DefinitionNeed<T>(T? value, string field) where T : struct => value ?? throw AiDefinitionMissing(field);
    private static InvalidDataException AiDefinitionMissing(string field) => new("original_ai.definition." + field);
}
