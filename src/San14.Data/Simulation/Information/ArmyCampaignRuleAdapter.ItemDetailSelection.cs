using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The actual caller has already admitted/retained these source identities.
    //No holder/status filter, registry order, de-duplication or initial sort
    //is repeated here. Custom display names never replace the independent key.
    private static IReadOnlyList<InformationDetailSelectionItem> ReadItemDetailSelectionCurrent(
        GameState state, IReadOnlyList<int> ids, string locale)
    {
        var result = new InformationDetailSelectionItem[ids.Count];
        for (int index = 0; index < ids.Count; index++)
        {
            int id = ids[index];
            string? caption = null, key = null, missing = null;
            try
            {
                if (id is < 1 or > 201) throw Missing("Item_receiver");
                var item = state.Items?.GetValueOrDefault(id) ?? throw Missing("Item_current_owner");
                var definition = state.ItemDefinitions.GetValueOrDefault(item.DefinitionId)
                    ?? throw Missing("Item_current_definition");
                string name = definition.Name ?? throw Missing("Item_display_name10");
                int end = name.IndexOf('\0');
                caption = end < 0 ? name : name[..end];
                key = definition.NameSortKey;
            }
            catch (InvalidDataException error) { missing = error.Message; }
            result[index] = new(id, caption, missing)
            {
                //Both editions' actual tagged getters read the independently
                //saved Item+1E key. Their current comparers use bounded
                //unsigned UTF16 ordering, without a display-name fallback.
                SortText = locale is "TC" or "SC" ? key : null,
                SortUnresolvedOwner = missing ?? (key is null ? Missing("Item_name_key1E").Message :
                    locale is not ("TC" or "SC") ? Missing("Item_header_comparator_" + locale).Message : null)
            };
        }
        return result;

        static InvalidDataException Missing(string field) => new("information-detail.selection." + field);
    }
}
