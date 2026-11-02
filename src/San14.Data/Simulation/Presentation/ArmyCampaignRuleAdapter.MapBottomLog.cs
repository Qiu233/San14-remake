using San14.Core;

namespace San14.Data;

public sealed record MapBottomLogEntry(int Slot, byte Category, int HexId, int PersonId,
    string PersonName, string Body, string ForceName, byte ForceColour);
public sealed record MapBottomLogPublication(IReadOnlyList<MapBottomLogEntry> Entries);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The ordinary log borrows the saved source-time ring. It never
    /// substitutes period reports or writes the history again on presentation.</summary>
    public AreaAdministrationQuery<MapBottomLogPublication> QueryMapBottomLog(GameState state)
    {
        if(state.CampaignHistory is not {} history)
            return AreaAdministrationQuery<MapBottomLogPublication>.Unknown("map.log.saved_ring");
        var rows=new List<MapBottomLogEntry>();
        // Oldest first, matching the retained CLogData list. The view chooses
        // its visible window; empty constructor slots are known absent.
        for(int ordinal=1;ordinal<=history.Records.Length;ordinal++)
        {
            int slot=(history.Cursor+ordinal)%history.Records.Length;
            var row=history.Records[slot];
            if((row.Flags&2)==0)continue;
            string body=HistoryWindowText(row.Text);
            if(body.Length==0)continue;
            rows.Add(new(slot,row.Category,row.HexId,row.PersonId,
                state.Officers.GetValueOrDefault(row.PersonId)?.Name??"",body,
                HistoryWindowText(row.ForceShortName),row.ColorIndex));
        }
        return AreaAdministrationQuery<MapBottomLogPublication>.Known(new(rows));
    }
}
