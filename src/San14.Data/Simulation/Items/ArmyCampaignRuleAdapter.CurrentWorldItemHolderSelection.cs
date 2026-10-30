using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<CurrentWorldItemHolderSelection> QueryCurrentWorldItemHolderSelection(GameState state)
    {
        try
        {
            RequireCurrentWorldItemEditProfile(state);
            var current=CaptureCurrentWorldItemEdit(state);
            var people=ReadCurrentWorldItemEditOfficers(state,current);
            int force=CurrentWorldItemEditNeed(OwnedCurrentPlayerForce.ReadCurrentReference(state,current)?.PhysicalSlot,
                "holder_selection.current_player");
            var reader=new OfficerInformationCurrent(this,state,force,rowPaletteEnabled:false,retainedCurrent:current);
            var pages=reader.Pages(people.Select(row=>row.Id).ToArray(),ReadHistoryDataText(1,0x32E),
                CurrentWorldItemSelectionColumnsSource.Holder(ReadHistoryDataText));
            string guide=new NarrationResourceMessages(_repository.LoadMessages(Seed.Language,Formats.MessageKind.Game).Messages)
                .ResolvePersonSelectorHint(ReadHistoryDataText(1,0x1FD));
            return AreaAdministrationQuery<CurrentWorldItemHolderSelection>.Known(new(pages,0x32E,guide));
        }
        catch(InvalidDataException error)
        { return AreaAdministrationQuery<CurrentWorldItemHolderSelection>.Unknown(error.Message); }
    }
}
