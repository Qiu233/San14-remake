using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The reached3E73F0 personnel milestone, after the same current
    /// has counted its200 owned Persons. Only the actual pending application
    /// changes are published; workers never write the independent profile.</summary>
    internal GameState RefreshPersonnelMilestoneCurrent(GameState state, SourceWorldContext current,
        int first, int second, int third)
    {
        if (first != 1 || second != 1 || third != 0)
            throw new InvalidDataException("personnel.service.arguments");
        var modeReader = current.TransferModeStateReader;
        try
        {
            return PrepareApplicationServiceCurrent(state, current, service =>
            {
                TransferService.CompletePersonnelMilestone(current, service);
                return service;
            });
        }
        finally
        {
            //BindApplicationServiceCurrent borrows the real application mode
            //for this write. Do not leave an external reader on an owned
            //current that may subsequently prepare an ordinary return.
            current.TransferModeStateReader = modeReader;
        }
    }
}
