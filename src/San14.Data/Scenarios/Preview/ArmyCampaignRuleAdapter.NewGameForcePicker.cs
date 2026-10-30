using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Prepare the complete authored Force selector over the actual
    /// clockless preview. Selection changes list order, never World3A/player.</summary>
    public NewGameForcePickerPreview CreateNewGameForcePicker(GameState state,
        NewGameForceSelectionPreview publication,int? selectedForceId)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(publication);
        var current=CaptureNewGameStaticVisualDefinitions(state);
        var world=current.ReadStaticCompletionWorld()
            ??throw new InvalidDataException("new-game.force-picker.static-world-owner");
        var reader=new ForceInformationCurrent(this,state,current,world);
        var pages=reader.Pages(publication.Rows.Select(row=>row.ForceId).ToArray());
        IReadOnlyList<NewGameForcePreview> ordered=publication.Rows;
        if(selectedForceId is int selected&&publication.Rows.Any(row=>row.ForceId==selected))
        {
            //800240 only orders a selected receiver before an unselected one.
            //Stable ordering retains every original occurrence and other tie.
            ordered=publication.Rows.OrderBy(row=>row.ForceId==selected?0:1).ToArray();
            var adopted=pages.AdoptDetailOrder(0,ordered.Select(row=>row.ForceId).ToArray(),selected,null);
            if(!adopted.Available)
                throw new InvalidDataException(adopted.UnresolvedInput??"new-game.force-picker.initial-order");
        }
        return new(publication.Rows,ordered,selectedForceId,null,publication.UnresolvedInputs,pages);
    }
}
