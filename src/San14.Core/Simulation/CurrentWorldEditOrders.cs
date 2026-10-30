namespace San14.Core;

public enum CurrentWorldEditKind { City = 3, Force = 4, Area = 5, Item = 6 }

/// <summary>Author is the actual local player's Force. Targets may belong to
/// another Force; these operations require the separate host edit capability.</summary>
public abstract record CurrentWorldEditOrder(int ForceId) : GameOrder(ForceId);

public sealed record CurrentWorldAreaEditValues(int? Commerce = null, int? Agriculture = null,
    int? Barracks = null, int? Population = null, int? Security = null);
public sealed record EditCurrentWorldAreaOrder(int ForceId, int AreaId,
    CurrentWorldAreaEditValues Values) : CurrentWorldEditOrder(ForceId);

public sealed record CurrentWorldCityEditValues(int? Size = null, int? Gold = null,
    int? Food = null, int? Troops = null, int? Durability = null, int? Morale = null,
    int? CounterTactic = null);
public sealed record EditCurrentWorldCityOrder(int ForceId, int CityId,
    CurrentWorldCityEditValues Values) : CurrentWorldEditOrder(ForceId);

public sealed record CurrentWorldForceDoctrinePointsEdit(int DoctrineId, int Points);
public sealed record CurrentWorldDistrictMilitaryInformationEdit(int DistrictId, int Value);
public sealed record CurrentWorldForceRelationshipEdit(int ForceId,
    int? Friendship = null, int? AllianceMonths = null)
{
    public int? FriendshipLevel { get; init; }
}
public sealed record CurrentWorldForceEditValues(int? RankId = null, int? NationalTitleId = null,
    int? DoctrineExperience = null, int? CivilReputation = null, int? MilitaryDistrictId = null,
    int? MilitaryInformation = null)
{
    public IReadOnlyList<CurrentWorldForceDoctrinePointsEdit> DoctrinePoints { get; init; } = [];
    public IReadOnlyList<CurrentWorldForceRelationshipEdit> Relationships { get; init; } = [];
    public IReadOnlyList<CurrentWorldDistrictMilitaryInformationEdit> DistrictMilitaryInformation { get; init; } = [];
}
public sealed record EditCurrentWorldForceOrder(int ForceId, int TargetForceId,
    CurrentWorldForceEditValues Values) : CurrentWorldEditOrder(ForceId);

/// <summary>Separate application capability; normal computer-command
/// authorities never acquire editing permission by controlling a Force.</summary>
public interface ICurrentWorldEditAuthority : IOrderAuthority
{
    bool CanEditCurrentWorld(CommandContext context, int forceId);
}

public sealed class LocalPlayerWorldEditAuthority(IOrderAuthority commands,
    int playerForceId, string actorId) : ICurrentWorldEditAuthority
{
    public bool CanControl(CommandContext context, int forceId) => commands.CanControl(context, forceId);
    public bool CanEditCurrentWorld(CommandContext context, int forceId) => forceId == playerForceId &&
        context.ActorId == actorId && context.DelegatedDistrictId is null && commands.CanControl(context, forceId);
}

public interface ICurrentWorldEditExecution
{
    CommandResult ApplyCurrentWorldEdit(GameState state, ForceState actorForce,
        CurrentWorldEditOrder order, CommandContext context);
}

public sealed class CurrentWorldEditOrders(IOrderRules otherCommands,
    ICurrentWorldEditExecution? execution) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not CurrentWorldEditOrder edit) return otherCommands.Apply(state, force, order, context);
        if (context.DelegatedDistrictId is not null)
            return CommandResult.Reject("World editing requires the local player's direct scope.", "world_edit.scope");
        if (execution is null)
            return CommandResult.RuleUnavailable("rules.current_world_edit", "Current-world editing is unavailable.");
        return execution.ApplyCurrentWorldEdit(state, force, edit, context);
    }
}
