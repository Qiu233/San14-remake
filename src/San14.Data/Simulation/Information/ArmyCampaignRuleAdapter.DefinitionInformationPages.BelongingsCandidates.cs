using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class DefinitionInformationCurrent
    {
        // The source uses the actual current-player pointer, not a caller's
        // prospective Force selection. Item status is deliberately unfiltered.
        private int[] BelongingsItemCandidates()
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw Missing("belongings.current_player_Force");
            var result = new List<int>();
            for (int lookup = 0; lookup < 202; lookup++)
            {
                try
                {
                    var item = current.ReadRegistryTableReference(RegistryKind.Item, lookup)
                        ?? throw Missing("belongings.Item_table");
                    if (!Need(current.ReadRegistryObjectNativeValidity(item), "belongings.Item_validity")) continue;
                    int physical = Need(item.PhysicalSlot, "belongings.Item_receiver");
                    ushort raw = Need(current.ReadCaptureItemOwner4A(physical), "belongings.Item_holder4A");
                    var holder = (raw < 6001 ? current.ReadPersonLookupReference(raw)
                        : current.ReadTransferPersonTableReference(0)) ?? throw Missing("belongings.holder_pointer");
                    int person = Need(holder.PhysicalSlot, "belongings.holder_receiver");
                    if (!Need(current.ReadPersonNativeValidity(person), "belongings.holder_validity")) continue;
                    var force = current.ReadPersonForce(person) ?? throw Missing("belongings.holder_Force");
                    if (force == player) result.Add(physical);
                }
                catch (InvalidDataException error) { candidateIssues.Add(error.Message); }
            }
            return result.ToArray();
        }

        // Gem rows have no inspect/command action. The original pointer list
        // carries resolved physical receivers; the adapted shared Gem reader
        // must use new GemReference(physical), without a second table lookup.
        private int[] BelongingsGemCandidates()
        {
            var result = new List<int>();
            for (int lookup = 0; lookup < 31; lookup++)
            {
                try
                {
                    var gem = current.ReadSkillGemReference(lookup) ?? throw Missing("belongings.Gem_table");
                    if (Need(current.ReadSkillGemValidity(gem), "belongings.Gem_validity"))
                        result.Add(Need(gem.PhysicalSlot, "belongings.Gem_receiver"));
                }
                catch (InvalidDataException error) { candidateIssues.Add(error.Message); }
            }
            return result.ToArray();
        }
    }
}
