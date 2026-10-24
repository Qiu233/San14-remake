namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Reuses the existing ordinary force-page publication. No world
    /// capture, registry traversal, mutation or simulation owner is required.
    /// Initial selection follows the caller's actual selected Force receiver;
    /// it is not replaced with the first row or an avatar's RulerForceId.</summary>
    public NewGameForcePickerPreview CreateNewGameForcePicker(
        NewGameForceSelectionPreview publication, int? selectedForceId)
    {
        ArgumentNullException.ThrowIfNull(publication);
        return new(publication.Rows, null, selectedForceId,
            "new-game.force-picker.native-column-sort-owner", publication.UnresolvedInputs);
    }
}
