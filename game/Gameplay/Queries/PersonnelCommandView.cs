using San14.Core;

namespace San14.Client.Gameplay;

public sealed record PersonnelOfficerChoice(int Id, string Name, int? OfficeId, string? OfficeName, int? Loyalty);
public sealed record PersonnelItemChoice(int Id, string Name, int? HolderOfficerId, string? HolderName);
internal sealed record PersonnelItemSelection(int ItemId, int? RecipientId, bool Revoke);
internal sealed record PersonnelItemEvaluationView(bool Allowed, int? CommandBooks, int? Gold,
    int? LoyaltyBefore, int? LoyaltyAfter, string Status);

/// <summary>
/// One personnel panel publication. Keeps display values and logical identities,
/// never a borrowed world; commands are validated by the simulation when issued.
/// </summary>
public sealed class PersonnelCommandView
{
    public static PersonnelCommandView Empty { get; } = Create(null, null);

    public int? PlayerForceId { get; }
    public int? RulerId { get; }
    public bool CanIssue { get; }
    public bool InventoryKnown { get; }
    public int UnresolvedItemCount { get; }
    public IReadOnlyList<PersonnelOfficerChoice> Officers { get; }
    public IReadOnlyList<PersonnelItemChoice> Items { get; }
    public IReadOnlyList<NamedCommandChoice> Offices { get; }

    private PersonnelCommandView(int? playerForceId, int? rulerId, bool canIssue, bool inventoryKnown, int unresolvedItemCount,
        PersonnelOfficerChoice[] officers, PersonnelItemChoice[] items, NamedCommandChoice[] offices)
    {
        PlayerForceId = playerForceId;
        RulerId = rulerId;
        CanIssue = canIssue;
        InventoryKnown = inventoryKnown;
        UnresolvedItemCount = unresolvedItemCount;
        Officers = Array.AsReadOnly(officers);
        Items = Array.AsReadOnly(items);
        Offices = Array.AsReadOnly(offices);
    }

    public PersonnelOfficerChoice? FindOfficer(int id) => Officers.FirstOrDefault(value => value.Id == id);
    public PersonnelItemChoice? FindItem(int id) => Items.FirstOrDefault(value => value.Id == id);

    public static PersonnelCommandView Create(GameState? world, int? playerForceId)
    {
        bool inventoryKnown = world?.Items is not null;
        int unresolved = world?.Items?.Values.Count(value => value.Holder?.OfficerId is null || value.IsPresent is null) ?? 0;
        bool canIssue = world?.Phase == TurnPhase.Strategy && playerForceId is int force &&
            world.Forces.TryGetValue(force, out ForceState? owner) && owner.IsActive;
        if (world is null || playerForceId is not int player)
            return new(playerForceId, null, canIssue, inventoryKnown, unresolved, [], [], []);

        var officers = world.Officers.Values.Where(value => value.ForceId == player).OrderBy(value => value.Id)
            .Select(value => new PersonnelOfficerChoice(value.Id, value.Name, value.OfficeId,
                value.OfficeId is int office ? world.OfficerOffices.GetValueOrDefault(office)?.Name : null,
                value.Loyalty)).ToArray();
        var items = (world.Items?.Values ?? Enumerable.Empty<ItemState>()).Where(value =>
                value.IsPresent == true && value.Holder?.OfficerId is int holder && world.Officers.TryGetValue(holder, out OfficerState? officer) &&
                officer.ForceId == player).OrderBy(value => value.Id)
            .Select(value => new PersonnelItemChoice(value.Id, world.ItemDefinitions[value.DefinitionId].Name,
                value.Holder!.OfficerId, world.Officers[value.Holder.OfficerId!.Value].Name)).ToArray();
        var offices = world.OfficerOffices.Values.OrderBy(value => value.Id)
            .Select(value => new NamedCommandChoice(value.Id, value.Name)).ToArray();
        return new(playerForceId, world.Forces.GetValueOrDefault(player)?.RulerId, canIssue, inventoryKnown, unresolved, officers, items, offices);
    }
}
