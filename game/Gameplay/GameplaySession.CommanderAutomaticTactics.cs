using San14.Core;

namespace San14.Client.Gameplay;

public sealed partial class GameplaySession
{
    internal CommandResult ApplyBorrowedCommanderAutomaticTactics(GameState owner,
        ToggleCommanderAutomaticTacticsOrder order, ICommanderAutomaticTacticsCommandRules rules)
    {
        if (_authority is null) throw new InvalidOperationException("The campaign has not started.");
        if (_tutorialOrderAdmission?.Invoke(order) == false)
            return CommandResult.Reject("The current lesson step does not permit this command.", "tutorial.command_not_allowed");
        return CommanderAutomaticTacticsCommandApplication.Apply(owner, order, rules,
            new CommandContext(LocalActor), _authority);
    }
}
