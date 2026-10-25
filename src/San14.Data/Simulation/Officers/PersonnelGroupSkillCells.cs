using System.Text;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The formation, current tactic-slot and first-five personality
    /// columns over the page's existing current. No per-cell capture or bind.</summary>
    internal PersonnelGroupPersonCell ReadPersonnelGroupSkillCellCurrent(GameState state,
        SourceWorldContext current, int officerId, int physical, int columnId)
    {
        string? text = null, sortKey = null;
        int? number = null, sortNumber = null;
        bool notApplicable = false;
        try
        {
            if (columnId is >= 40 and <= 50)
            {
                int formation = columnId == 50 ? 14 : columnId - 39;
                uint mask = current.ReadPersonFormationMask164(physical) ?? throw GroupSkillMissing("formation_mask");
                //232480 only consumes the Person identity for a set bit14.
                int identity = formation == 14 && (mask & (1u << 14)) != 0
                    ? current.ReadPersonIdentity(physical) ?? throw GroupSkillMissing("formation_Person10") : officerId;
                bool allowed = DeploymentFormationEligibility.Allows(mask, formation, identity);
                number = allowed ? 1 : 0;
                sortNumber = allowed ? 0 : int.MaxValue;
                notApplicable = !allowed;
                //35C4E0 and its other formation wrappers use Data0338 only
                //for capability. The other branch is the empty source string.
                text = allowed ? ReadHistoryDataText(1, 0x338) : "";
            }
            else if (columnId is >= 51 and <= 60)
            {
                int identity = current.ReadPersonIdentity(physical) ?? throw GroupSkillMissing("tactic_Person10");
                var tactic = DeploymentTacticSelectionSource.ReadCurrentSlot(current, identity, physical, columnId - 51);
                bool valid = current.ReadSkillTacticValidity(tactic) ?? throw GroupSkillMissing("tactic_validity");
                notApplicable = !valid;
                if (!valid) { text = ""; sortNumber = int.MaxValue; }
                else
                {
                    int id = current.ReadSkillTacticPhysicalSlot(tactic) ?? throw GroupSkillMissing("tactic_receiver");
                    number = sortNumber = id; //351F10 ->22E1F0 actual receiver ordinal.
                    text = state.Tactics.GetValueOrDefault(id)?.Name ?? throw GroupSkillMissing("tactic_name");
                }
            }
            else if (columnId is >= 61 and <= 65)
            {
                //35FD40..360000 call230730 with the fixed slot0..4.
                //They do not compact2307D0's nine-slot temporary list.
                var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, physical, columnId - 61)
                    ?? throw GroupSkillMissing("trait_current_slot");
                bool valid = current.ReadSkillPersonalityValidity(trait) ?? throw GroupSkillMissing("trait_validity");
                notApplicable = !valid;
                if (!valid) text = "";
                else
                {
                    number = current.ReadPersonalityPhysicalSlot(trait) ?? throw GroupSkillMissing("trait_receiver");
                    text = current.ReadPersonalityName(trait);
                    //354A00's two source comparison directions use the raw
                    //sort text, falling back to name only when both are empty.
                    //An empty key is a known value, not an identity sort key.
                    var key = new StringBuilder(16);
                    bool terminated = false;
                    for (int index = 0; index < 16; index++)
                    {
                        ushort unit = current.ReadPersonalityWord(trait, 0x1C + index * 2)
                            ?? throw GroupSkillMissing("trait_sort_key");
                        if (unit == 0) { terminated = true; break; }
                        key.Append((char)unit);
                    }
                    if (!terminated) throw GroupSkillMissing("trait_sort_key_terminator");
                    sortKey = key.ToString();
                }
            }
            else throw GroupSkillMissing("column");
            return new(columnId, text, number, sortNumber, sortKey, notApplicable, null)
            {
                SortGroup = notApplicable ? 2 : columnId >= 61 && string.IsNullOrEmpty(sortKey) ? 1 : 0
            };
        }
        catch (InvalidDataException error)
        {
            //A missing comparison key retains the already known display/name.
            return new(columnId, text, number, sortNumber, sortKey, notApplicable,
                text is null ? error.Message : null)
            { SortUnresolvedOwner = text is null ? null : error.Message };
        }
    }

    private static InvalidDataException GroupSkillMissing(string owner)
        => new("personnel.groups.skill_cell." + owner);
}
