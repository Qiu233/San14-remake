namespace San14.Core;

public enum PersonnelReturnBaseKind { City, Gate }

/// <summary>A city or gate in the simulation, resolved before scheduling a return.</summary>
public sealed record PersonnelReturnDestination(PersonnelReturnBaseKind Kind, int BaseId);

/// <summary>A personnel journey. Task identity is stable; dispatch slots determine
/// the compatible processing order and may be reused after a completed journey.</summary>
public sealed record PersonnelReturnTask(int Id, int? DispatchSlot, int[] MemberIds,
    PersonnelReturnDestination[] RouteDestinations, PersonnelReturnDestination? Destination,
    byte DistrictId, int StartHex, bool Started, bool NeedsRefresh, bool IsPlayer,
    bool IsRetiring, bool IsAssociated, NativeMapSeasonDate StartedOn, NativeMapSeasonDate ArriveOn)
{
    /// <summary>True for an explicit personnel reassignment (native kind1);
    /// false for the existing return-to-home journey (native kind15).</summary>
    public bool IsReassignment { get; init; }
    /// <summary>A requested cancellation is consumed by the movement task's
    /// next morning/evening lifecycle cut, preserving its actual return position.</summary>
    public bool CancellationRequested { get; init; }
}

public enum PersonnelCommandKind { Return, Disappear, Talent, Diplomacy, Strategic, Independence, Caravan, Intelligence }
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
    public Dictionary<int, PersonnelTalentTask>? Talents { get; init; }
    public Dictionary<int, PersonnelDiplomacyTask>? Diplomacies { get; init; }
    public Dictionary<int, PersonnelStrategicTask>? Strategies { get; init; }
    public Dictionary<int, PersonnelIndependenceTask>? Independences { get; init; }
    public Dictionary<int, PersonnelCaravanTask>? Caravans { get; init; }
    public Dictionary<int, PersonnelIntelligenceTask>? Intelligences { get; init; }
    public const int CompatibleDispatchCapacity = 1852;

    public static PersonnelCommandTasks Create(int initialCapacity = CompatibleDispatchCapacity) => new(0,
        Enumerable.Range(0, initialCapacity).Reverse().ToArray(), [], []) { Disappearances = [], Talents = [], Diplomacies = [], Strategies = [], Independences = [], Caravans = [], Intelligences = [] };

    public IEnumerable<PersonnelReturnTask> InDispatchOrder() => Returns.Values
        .Where(task => task.DispatchSlot.HasValue).OrderBy(task => task.DispatchSlot);

    public IEnumerable<PersonnelCommandDispatch> DispatchOrder() => Returns.Values
        .Where(task => task.DispatchSlot.HasValue).Select(task => new PersonnelCommandDispatch(task.Id, task.DispatchSlot!.Value, PersonnelCommandKind.Return))
        .Concat((Disappearances?.Values.AsEnumerable() ?? []).Select(task => new PersonnelCommandDispatch(task.Id, task.DispatchSlot, PersonnelCommandKind.Disappear)))
        .Concat((Talents?.Values.AsEnumerable() ?? []).Select(task => new PersonnelCommandDispatch(task.Id, task.DispatchSlot, PersonnelCommandKind.Talent)))
        .Concat((Diplomacies?.Values.AsEnumerable() ?? []).Select(task => new PersonnelCommandDispatch(task.Id, task.DispatchSlot, PersonnelCommandKind.Diplomacy)))
        .Concat((Strategies?.Values.AsEnumerable() ?? []).Select(task => new PersonnelCommandDispatch(task.Id, task.DispatchSlot, PersonnelCommandKind.Strategic)))
        .Concat((Independences?.Values.AsEnumerable() ?? []).Select(task => new PersonnelCommandDispatch(task.Id, task.DispatchSlot, PersonnelCommandKind.Independence)))
        .Concat((Caravans?.Values.AsEnumerable() ?? []).Select(task => new PersonnelCommandDispatch(task.Id, task.DispatchSlot, PersonnelCommandKind.Caravan)))
        .Concat((Intelligences?.Values.AsEnumerable() ?? []).Select(task => new PersonnelCommandDispatch(task.Id, task.DispatchSlot, PersonnelCommandKind.Intelligence)))
        .OrderBy(task => task.Slot);

    public bool IsOfficerBusy(int officerId) => Returns.Values.Any(task =>
        task.IsAssociated && !task.IsRetiring && task.MemberIds.Any(id => id == officerId)) ||
        Disappearances?.Values.Any(task => task.IsAssociated && !task.IsRetiring && task.MemberIds.Contains(officerId)) == true ||
        Talents?.Values.Any(task => task.IsAssociated && !task.IsRetiring && task.MemberIds.Contains(officerId)) == true ||
        Diplomacies?.Values.Any(task => task.IsAssociated && !task.IsRetiring && task.MemberIds.Contains(officerId)) == true ||
        Strategies?.Values.Any(task => task.IsAssociated && !task.IsRetiring && task.MemberIds.Contains(officerId)) == true ||
        Independences?.Values.Any(task => task.IsAssociated && !task.IsRetiring && task.MemberIds.Contains(officerId)) == true ||
        Caravans?.Values.Any(task => task.IsAssociated && !task.IsRetiring && task.MemberIds.Contains(officerId)) == true ||
        Intelligences?.Values.Any(task => task.IsAssociated && !task.IsRetiring && task.MemberIds.Contains(officerId)) == true;
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
                task.Home is null || !Enum.IsDefined(task.Home.Kind) || task.Home.BaseId < 0 ||
                task.Home.BaseId == 0 && task.Home.Kind != PersonnelReturnBaseKind.City ||
                task.PreviousPosition is null || !Enum.IsDefined(task.PreviousPosition.Kind) || task.PreviousPosition.Kind == OfficerPositionKind.Unresolved ||
                task.StationHex is < 0 or >= 48400 || task.StartedOn is null || task.DueOn is null ||
                task.DispatchSlot is < 0 or >= PersonnelCommandTasks.CompatibleDispatchCapacity || !liveSlots.Add(task.DispatchSlot))
                throw new InvalidDataException("Invalid personnel disappearance task.");
        }
        foreach (var pair in owner.Talents ?? [])
        {
            var task = pair.Value;
            if (task is null || pair.Key != task.Id || task.Id < 0 || task.Id >= owner.NextTaskId ||
                owner.Returns.ContainsKey(task.Id) || owner.Disappearances?.ContainsKey(task.Id) == true ||
                task.MemberIds is null || task.MemberIds.Any(id => id < 0) || !Enum.IsDefined(task.Kind) ||
                task.StartDate is null || task.DueDate is null ||
                task.StartHexId is < 0 or >= 48400 || task.EndHexId is < 0 or >= 48400 ||
                task.TargetHexId is < 0 or >= 48400 || task.SavedSample is < -1 or > 99 ||
                (task.Kind == TalentOrderKind.Search ? task.RecruitMode != 0 : task.RecruitMode is not (0 or 10 or 30)) ||
                task.IsRecruitWait && (task.Kind != TalentOrderKind.Recruit || task.StartHexId != task.EndHexId ||
                    task.RecruitMode <= 0 || task.TargetOfficerId is null || task.TargetOriginalHomeLocationId is null) ||
                task.DispatchSlot is < 0 or >= PersonnelCommandTasks.CompatibleDispatchCapacity || !liveSlots.Add(task.DispatchSlot))
                throw new InvalidDataException("Invalid personnel talent task.");
        }
        foreach (var pair in owner.Diplomacies ?? [])
        {
            var task = pair.Value;
            if (task is null || pair.Key != task.Id || task.Id < 0 || task.Id >= owner.NextTaskId ||
                owner.Returns.ContainsKey(task.Id) || owner.Disappearances?.ContainsKey(task.Id) == true ||
                owner.Talents?.ContainsKey(task.Id) == true || task.MemberIds is null ||
                task.MemberIds.Any(id => id < 0) || !Enum.IsDefined(task.Kind) ||
                task.StartDate is null || task.DueDate is null || task.InitialOffer is null ||
                task.InitialOffer.Gold < 0 || task.InitialOffer.Supplies < 0 ||
                task.StartHexId is < 0 or >= 48400 || task.EndHexId is < 0 or >= 48400 ||
                task.DispatchSlot is < 0 or >= PersonnelCommandTasks.CompatibleDispatchCapacity || !liveSlots.Add(task.DispatchSlot))
                throw new InvalidDataException("Invalid personnel diplomacy task.");
        }
        foreach (var pair in owner.Strategies ?? [])
        {
            var task = pair.Value;
            if (task is null || pair.Key != task.Id || task.Id < 0 || task.Id >= owner.NextTaskId ||
                owner.Returns.ContainsKey(task.Id) || owner.Disappearances?.ContainsKey(task.Id) == true ||
                owner.Talents?.ContainsKey(task.Id) == true || owner.Diplomacies?.ContainsKey(task.Id) == true ||
                task.MemberIds is null || task.MemberIds.Any(id => id < 0) ||
                task.Kind is not (10 or 11 or 12 or 13 or 14 or 20 or 24) || task.Target is null ||
                !Enum.IsDefined(task.Target.Kind) || task.Target.Id < 0 ||
                task.StartDate is null || task.DueDate is null ||
                task.StartHexId is < 0 or >= 48400 || task.EndHexId is < 0 or >= 48400 ||
                task.SavedResult < -1 || task.MilitaryIntelligence < 0 ||
                task.DispatchSlot is < 0 or >= PersonnelCommandTasks.CompatibleDispatchCapacity || !liveSlots.Add(task.DispatchSlot))
                throw new InvalidDataException("Invalid personnel strategic task.");
        }
        foreach (var pair in owner.Independences ?? [])
        {
            var task = pair.Value;
            if (task is null || pair.Key != task.Id || task.Id < 0 || task.Id >= owner.NextTaskId ||
                owner.Returns.ContainsKey(task.Id) || owner.Disappearances?.ContainsKey(task.Id) == true ||
                owner.Talents?.ContainsKey(task.Id) == true || owner.Diplomacies?.ContainsKey(task.Id) == true ||
                owner.Strategies?.ContainsKey(task.Id) == true || task.MemberIds is null ||
                task.MemberIds.Any(id => id < 0) || task.TargetCityId is < 0 or > 61 ||
                task.CapturedGovernorId is < 0 or > 6000 || task.StartDate is null || task.DueDate is null ||
                task.StartHexId is < 0 or >= 48400 || task.EndHexId is < 0 or >= 48400 ||
                task.SavedResult < -1 || task.MilitaryIntelligence < 0 ||
                task.DispatchSlot is < 0 or >= PersonnelCommandTasks.CompatibleDispatchCapacity || !liveSlots.Add(task.DispatchSlot))
                throw new InvalidDataException("Invalid personnel independence task.");
        }
        foreach (var pair in owner.Caravans ?? [])
        {
            var task = pair.Value;
            if (task is null || pair.Key != task.Id || task.Id < 0 || task.Id >= owner.NextTaskId ||
                owner.Returns.ContainsKey(task.Id) || owner.Disappearances?.ContainsKey(task.Id) == true ||
                owner.Talents?.ContainsKey(task.Id) == true || owner.Diplomacies?.ContainsKey(task.Id) == true ||
                owner.Strategies?.ContainsKey(task.Id) == true || owner.Independences?.ContainsKey(task.Id) == true ||
                task.MemberIds is null || task.MemberIds.Any(id => id is < 0 or > 6000) ||
                task.NativeKind is not (17 or 18) || task.CountryId is < 1 or > 4 ||
                task.SourceForceId is < 0 or > 51 || task.SourceDistrictId is < 0 or > 51 ||
                task.SourceLocationId is < 0 or > ushort.MaxValue || task.HomeLocationId is < 0 or > ushort.MaxValue ||
                task.StartHexId is < 0 or > 48400 || task.EndHexId is < 0 or > 48400 ||
                task.StartDate is null || task.DueDate is null || task.OfferedItemId is < 0 or > 201 ||
                task.GoldOffer < 0 || task.SuppliesOffer < 0 || task.TroopOffer < 0 ||
                task.RewardStage > 4 || task.ReturnDisposition > 2 || task.SavedResult < -1 ||
                task.DispatchSlot is < 0 or >= PersonnelCommandTasks.CompatibleDispatchCapacity || !liveSlots.Add(task.DispatchSlot) ||
                task.PendingReport is { } report && (report.ItemId is < 0 or > 201 || report.GemId is < 0 or > 30 ||
                    report.WeaponId is < 0 or > 8))
                throw new InvalidDataException("Invalid personnel caravan task.");
        }
        foreach (var pair in owner.Intelligences ?? [])
        {
            var task = pair.Value;
            if (task is null || pair.Key != task.Id || task.Id < 0 || task.Id >= owner.NextTaskId ||
                owner.Returns.ContainsKey(task.Id) || owner.Disappearances?.ContainsKey(task.Id) == true ||
                owner.Talents?.ContainsKey(task.Id) == true || owner.Diplomacies?.ContainsKey(task.Id) == true ||
                owner.Strategies?.ContainsKey(task.Id) == true || owner.Independences?.ContainsKey(task.Id) == true ||
                owner.Caravans?.ContainsKey(task.Id) == true || task.MemberIds is null ||
                task.MemberIds.Any(id => id is < 0 or > 6000) || !Enum.IsDefined(task.Kind) ||
                task.ForceId is < 0 or > 51 || task.SourceLocationId is < 0 or > ushort.MaxValue ||
                task.SourceDistrictId is < 0 or > 51 || task.TargetOriginalForceId is < 0 or > 51 ||
                (task.Kind == PersonnelIntelligenceKind.Away
                    ? task.TargetId is < 0 or > 51 || task.DurationOrLevel is not (5 or 10 or 15)
                    : task.TargetId is < 1 or > 51 || task.DurationOrLevel is not (10 or 20 or 30)) ||
                task.StartHexId is < 0 or > 48400 || task.EndHexId is < 0 or > 48400 ||
                task.StartDate is null || task.DueDate is null || task.SavedResult < -1 ||
                task.DispatchSlot is < 0 or >= PersonnelCommandTasks.CompatibleDispatchCapacity || !liveSlots.Add(task.DispatchSlot))
                throw new InvalidDataException("Invalid personnel intelligence task.");
        }
        if (owner.PendingRetirements.Any(id => !(owner.Returns.TryGetValue(id, out var task) && task.IsRetiring ||
                owner.Disappearances?.TryGetValue(id, out var disappearance) == true && disappearance.IsRetiring ||
                owner.Talents?.TryGetValue(id, out var talent) == true && talent.IsRetiring ||
                owner.Diplomacies?.TryGetValue(id, out var diplomacy) == true && diplomacy.IsRetiring ||
                owner.Strategies?.TryGetValue(id, out var strategic) == true && strategic.IsRetiring ||
                owner.Independences?.TryGetValue(id, out var independence) == true && independence.IsRetiring ||
                owner.Caravans?.TryGetValue(id, out var caravan) == true && caravan.IsRetiring ||
                owner.Intelligences?.TryGetValue(id, out var intelligence) == true && intelligence.IsRetiring)) ||
            owner.PendingRetirements.Distinct().Count() != owner.PendingRetirements.Length)
            throw new InvalidDataException("Invalid personnel return retirement order.");
    }
}
