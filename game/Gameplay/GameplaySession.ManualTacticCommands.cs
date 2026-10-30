using San14.Core;

namespace San14.Client.Gameplay;

public sealed partial class GameplaySession
{
    public CommandResult IssueManualTacticCommand(GameState expectedOwner,
        IssueManualTacticInterventionOrder order, IManualTacticCommandRules rules,
        bool? progressAllowsReplacement)
    {
        if (_authority is null) throw new InvalidOperationException("The campaign has not started.");
        if (_tutorialOrderAdmission?.Invoke(order) == false)
            return CommandResult.Reject("The current lesson step does not permit this command.", "tutorial.command_not_allowed");
        var before = World;
        var prepared = _prepareTutorialWorld?.Invoke(before);
        var result = _simulation.IssueManualTacticCommand(expectedOwner, order, rules,
            new CommandContext(LocalActor), _authority, progressAllowsReplacement, prepared);
        if (result.Accepted) _adoptTutorialWorld?.Invoke(before, World);
        return result;
    }

    internal CommandResult ApplyBorrowedManualTacticCommand(GameState owner,
        IssueManualTacticInterventionOrder order, IManualTacticCommandRules rules,
        bool? progressAllowsReplacement)
    {
        if (_authority is null) throw new InvalidOperationException("The campaign has not started.");
        if (_tutorialOrderAdmission?.Invoke(order) == false)
            return CommandResult.Reject("The current lesson step does not permit this command.", "tutorial.command_not_allowed");
        return ManualTacticCommandApplication.Apply(owner, order, rules,
            new CommandContext(LocalActor), _authority, progressAllowsReplacement);
    }
}
