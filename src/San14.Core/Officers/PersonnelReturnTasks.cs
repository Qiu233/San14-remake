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

public enum PersonnelCommandKind { Return, Disappear }
public sealed record PersonnelCommandDispatch(int Id, int Slot, PersonnelCommandKind Kind);

/// <summary>A temporary absence at one fixed area. Its prior place is restored
/// on expiry unless the person has joined a force and needs a return journey.</summary>
public sealed record PersonnelDisappearanceTask(int Id, int DispatchSlot, int[] MemberIds,
    PersonnelReturnDestination Home, byte DistrictId, int StationHex, OfficerPosition PreviousPosition,
    NativeMapSeasonDate StartedOn, NativeMapSeasonDate DueOn, bool Started, bool NeedsRefresh, bool IsPlayer,
    bool CancellationRequested, bool IsRetiring, bool IsAssociated);

/// <summary>One shared identity, dispatch allocator and deferred retirement
/// queue for both personnel command kinds. Null disappearance knowledge is
/// retained when loading saves produced before that task kind was owned.</summary>
public sealed record PersonnelCommandTasks(int NextTaskId, int[] AvailableDispatchSlots,
    Dictionary<int, PersonnelReturnTask> Returns, int[] PendingRetirements)
{
    public Dictionary<int, PersonnelDisappearanceTask>? Disappearances { get; init; }
    public const int CompatibleDispatchCapacity = 1852;

    public static PersonnelCommandTasks Create(int initialCapacity = CompatibleDispatchCapacity) => new(0,
        Enumerable.Range(0, initialCapacity).Reverse().ToArray(), [], []) { Disappearances = [] };

    public IEnumerable<PersonnelReturnTask> InDispatchOrder() => Returns.Values
        .Where(task => task.DispatchSlot.HasValue).OrderBy(task => task.DispatchSlot);

    public IEnumerable<PersonnelCommandDispatch> DispatchOrder() => Returns.Values
        .Where(task => task.DispatchSlot.HasValue).Select(task => new PersonnelCommandDispatch(task.Id, task.DispatchSlot!.Value, PersonnelCommandKind.Return))
        .Concat((Disappearances?.Values.AsEnumerable() ?? []).Select(task => new PersonnelCommandDispatch(task.Id, task.DispatchSlot, PersonnelCommandKind.Disappear)))
        .OrderBy(task => task.Slot);

    public bool IsOfficerBusy(int officerId) => Returns.Values.Any(task =>
        task.IsAssociated && !task.IsRetiring && task.MemberIds.Any(id => id == officerId)) ||
        Disappearances?.Values.Any(task => task.IsAssociated && !task.IsRetiring && task.MemberIds.Contains(officerId)) == true;
}

/// <summary>Checks serialized task shape at save/load boundaries.</summary>
public static class PersonnelCommandSaveValidation
{
    public static void Validate(PersonnelCommandTasks? owner)
    {
        if (owner is null) return;
        if (owner.NextTaskId < 0 || owner.Returns is null || owner.AvailableDispatchSlots is null || owner.PendingRetirements is null)
            throw new InvalidDataException("Personnel return tasks are incomplete.");
        var liveSlots = new HashSet<int>();
        foreach (var pair in owner.Returns)
        {
            var task = pair.Value;
            if (task is null || pair.Key != task.Id || task.Id < 0 || task.Id >= owner.NextTaskId ||
                task.MemberIds is null || task.RouteDestinations is null ||
                task.MemberIds.Any(id => id < 0) || task.StartedOn is null || task.ArriveOn is null ||
                task.DispatchSlot.HasValue && task.Destination is null ||
                task.StartHex < 0 ||
                task.Destination is { } destination && (!Enum.IsDefined(destination.Kind) || destination.BaseId <= 0) ||
                task.RouteDestinations.Any(route => route is null || !Enum.IsDefined(route.Kind) || route.BaseId <= 0) ||
                task.DispatchSlot is int slot && (slot is < 0 or >= PersonnelCommandTasks.CompatibleDispatchCapacity || !liveSlots.Add(slot)))
                throw new InvalidDataException("Invalid personnel return task.");
        }
        foreach (int slot in owner.AvailableDispatchSlots)
            if (slot is < 0 or >= PersonnelCommandTasks.CompatibleDispatchCapacity || !liveSlots.Add(slot))
                throw new InvalidDataException("Duplicate personnel return dispatch slot.");
        foreach (var pair in owner.Disappearances ?? [])
        {
            var task = pair.Value;
            if (task is null || pair.Key != task.Id || task.Id < 0 || task.Id >= owner.NextTaskId || owner.Returns.ContainsKey(task.Id) ||
                task.MemberIds is null || task.MemberIds.Any(id => id < 0) ||
                task.Home is null || !Enum.IsDefined(task.Home.Kind) || task.Home.BaseId <= 0 ||
                task.PreviousPosition is null || !Enum.IsDefined(task.PreviousPosition.Kind) || task.PreviousPosition.Kind == OfficerPositionKind.Unresolved ||
                task.StationHex is < 0 or >= 48400 || task.StartedOn is null || task.DueOn is null ||
                task.DispatchSlot is < 0 or >= PersonnelCommandTasks.CompatibleDispatchCapacity || !liveSlots.Add(task.DispatchSlot))
                throw new InvalidDataException("Invalid personnel disappearance task.");
        }
        if (owner.PendingRetirements.Any(id => !(owner.Returns.TryGetValue(id, out var task) && task.IsRetiring ||
                owner.Disappearances?.TryGetValue(id, out var disappearance) == true && disappearance.IsRetiring)) ||
            owner.PendingRetirements.Distinct().Count() != owner.PendingRetirements.Length)
            throw new InvalidDataException("Invalid personnel return retirement order.");
    }
}
