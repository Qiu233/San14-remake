using San14.Core;

namespace San14.Client.Gameplay;

public sealed partial class GameplaySession
{
    // This does not commit the session simulation. The caller must publish the
    // accepted result back into the exact Main-owned period-menu checkpoint.
    internal CommandResult ApplyBorrowedOfficerPriority(GameState owned, SetOfficerPriorityOrder order)
    {
        if (_authority is null) throw new InvalidOperationException("The campaign has not started.");
        if (_tutorialOrderAdmission?.Invoke(order) == false)
            return CommandResult.Reject("The current lesson step does not permit this command.", "tutorial.command_not_allowed");
        return OfficerPriorityOrders.ApplyPresentation(owned, order,
            Rules.OrdinaryDeploymentRules as IOfficerPriorityRules, new CommandContext(LocalActor), _authority);
    }
}
