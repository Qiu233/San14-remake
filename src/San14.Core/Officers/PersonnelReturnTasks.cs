namespace San14.Core;

public enum PersonnelReturnBaseKind { City, Gate }

/// <summary>A city or gate in the simulation, resolved before scheduling a return.</summary>
public sealed record PersonnelReturnDestination(PersonnelReturnBaseKind Kind, int BaseId);

/// <summary>A personnel journey. Task identity is stable; dispatch slots determine
/// the compatible processing order and may be reused after a completed journey.</summary>
public sealed record PersonnelReturnTask(int Id, int? DispatchSlot, int[] MemberIds,
    PersonnelReturnDestination[] RouteDestinations, PersonnelReturnDestination? Destination,
    byte DistrictId, int StartHex, bool Started, bool NeedsRefresh, bool IsPlayer,
    bool IsRetiring, bool IsAssociated, NativeMapSeasonDate StartedOn, NativeMapSeasonDate ArriveOn);

public sealed record PersonnelReturnTasks(int NextTaskId, int[] AvailableDispatchSlots,
    Dictionary<int, PersonnelReturnTask> Tasks, int[] PendingRetirements)
{
    public const int CompatibleDispatchCapacity = 1852;

    public static PersonnelReturnTasks Create(int initialCapacity = CompatibleDispatchCapacity) => new(0,
        Enumerable.Range(0, initialCapacity).Reverse().ToArray(), [], []);

    public IEnumerable<PersonnelReturnTask> InDispatchOrder() => Tasks.Values
        .Where(task => task.DispatchSlot.HasValue).OrderBy(task => task.DispatchSlot);

    public bool IsOfficerReturning(int officerId) => Tasks.Values.Any(task =>
        task.IsAssociated && !task.IsRetiring && task.MemberIds.Any(id => id == officerId));
}

/// <summary>Checks serialized task shape at save/load boundaries.</summary>
public static class PersonnelReturnSaveValidation
{
    public static void Validate(PersonnelReturnTasks? owner)
    {
        if (owner is null) return;
        if (owner.NextTaskId < 0 || owner.Tasks is null || owner.AvailableDispatchSlots is null || owner.PendingRetirements is null)
            throw new InvalidDataException("Personnel return tasks are incomplete.");
        var liveSlots = new HashSet<int>();
        foreach (var pair in owner.Tasks)
        {
            var task = pair.Value;
            if (task is null || pair.Key != task.Id || task.Id < 0 || task.Id >= owner.NextTaskId ||
                task.MemberIds is null || task.RouteDestinations is null ||
                task.MemberIds.Any(id => id < 0) || task.StartedOn is null || task.ArriveOn is null ||
                task.DispatchSlot.HasValue && task.Destination is null ||
                task.StartHex < 0 ||
                task.Destination is { } destination && (!Enum.IsDefined(destination.Kind) || destination.BaseId <= 0) ||
                task.RouteDestinations.Any(route => route is null || !Enum.IsDefined(route.Kind) || route.BaseId <= 0) ||
                task.DispatchSlot is int slot && (slot < 0 || !liveSlots.Add(slot)))
                throw new InvalidDataException("Invalid personnel return task.");
        }
        foreach (int slot in owner.AvailableDispatchSlots)
            if (slot < 0 || !liveSlots.Add(slot))
                throw new InvalidDataException("Duplicate personnel return dispatch slot.");
        if (owner.PendingRetirements.Any(id => !owner.Tasks.TryGetValue(id, out var task) || !task.IsRetiring) ||
            owner.PendingRetirements.Distinct().Count() != owner.PendingRetirements.Length)
            throw new InvalidDataException("Invalid personnel return retirement order.");
    }
}
