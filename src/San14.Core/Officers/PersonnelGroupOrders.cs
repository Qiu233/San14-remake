namespace San14.Core;

public sealed record SetPersonnelGroupsOrder(int ForceId, PersonnelGroupsState Groups) : GameOrder(ForceId)
{
    public IReadOnlyList<int?>? Destinations { get; init; }
}
public sealed record PersonnelGroupMoveSelection(int GroupIndex, int DestinationLocationId);
public sealed record PreparePersonnelGroupMoveOrder(int ForceId,
    IReadOnlyList<PersonnelGroupMoveSelection> Groups) : GameOrder(ForceId);

public interface IPersonnelGroupEditExecution
{
    CommandResult Edit(GameState state, ForceState force, SetPersonnelGroupsOrder order, CommandContext context);
}

public interface IPersonnelGroupMovementExecution
{
    CommandResult Move(GameState state, ForceState force, PreparePersonnelGroupMoveOrder order, CommandContext context);
}

public sealed class PersonnelGroupOrders(IOrderRules next, IPersonnelGroupEditExecution? editing,
    IPersonnelGroupMovementExecution? movement) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not (SetPersonnelGroupsOrder or PreparePersonnelGroupMoveOrder))
            return next.Apply(state, force, order, context);
        if (state.Phase != TurnPhase.Strategy || context.DelegatedDistrictId is not null)
            return CommandResult.Reject("Personnel groups require direct strategy control.", "personnel.groups.scope");
        if (order is SetPersonnelGroupsOrder edit)
        {
            if (!ValidGroups(edit.Groups))
                return CommandResult.Reject("The personnel group selection is invalid.", "personnel.groups.selection");
            if (edit.Destinations is null)
                return CommandResult.RuleUnavailable("personnel.groups.destinations", "Personnel group destination knowledge is unavailable.");
            if (edit.Destinations.Count != 10 || edit.Destinations.Any(id => id is < 1 or > 61))
                return CommandResult.Reject("The personnel group destinations are invalid.", "personnel.groups.destinations");
            return editing?.Edit(state, force, edit, context) ??
                CommandResult.RuleUnavailable("personnel.groups.edit_rules", "Personnel group editing rules are unavailable.");
        }
        var move = (PreparePersonnelGroupMoveOrder)order;
        if (move.Groups is not { Count: > 0 and <= 10 } ||
            move.Groups.Any(group => group is null || group.GroupIndex is < 0 or > 9 || group.DestinationLocationId is < 1 or > 61) ||
            move.Groups.Select(group => group.GroupIndex).Distinct().Count() != move.Groups.Count)
            return CommandResult.Reject("The personnel group destinations are invalid.", "personnel.groups.move_selection");
        return movement?.Move(state, force, move, context) ??
            CommandResult.RuleUnavailable("personnel.groups.move_rules", "Personnel group movement rules are unavailable.");
    }

    private static bool ValidGroups(PersonnelGroupsState? owner) => owner is not null && !owner.Groups.IsDefault &&
        owner.Groups.Length == 10 && owner.Groups.All(group => !group.IsDefault && group.Length <= 50 &&
            group.All(id => id is >= 1 and <= 6000));
}
