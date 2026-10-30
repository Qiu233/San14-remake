using San14.Core;

namespace San14.Data;

/// <summary>Eligible source-time text plus the independent fixed-resource
/// reading used by the shared kingdom list. Description is not sortable.</summary>
public sealed record KingdomNameSelectionRow(int Id, string Name, string Description, string NameSortKey);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Projects the already eligible pending choices without rerunning
    /// eligibility, selecting a name or changing a resumed continuation.</summary>
    public IReadOnlyList<KingdomNameSelectionRow> PrepareKingdomNameSelection(
        IReadOnlyList<CampaignEndingKingdomChoice> choices)
    {
        var definitions = ReadEmpireNameResourcesCurrent().CustomNames.Definitions;
        var rows = new KingdomNameSelectionRow[choices.Count];
        for (int index = 0; index < rows.Length; index++)
        {
            var choice = choices[index];
            if ((uint)choice.Id >= (uint)definitions.Count || definitions[choice.Id].PhysicalSlot != choice.Id)
                throw new InvalidDataException("kingdom.selection.definition_receiver");
            //The ordinary list getter3498E0 stores type1 and receiver+18,
            //independent of display35E9E0's receiver+10 and prose35A5D0's+26.
            rows[index] = new(choice.Id, choice.Name, choice.Description, definitions[choice.Id].Text18);
        }
        return rows;
    }
}
