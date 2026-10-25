using San14.Core;

namespace San14.Data;

public static partial class PersonReturnCommands
{
    /// <summary>335CD0: complete the actual personnel command first, then
    /// enqueue its receiver for manager retirement. Its association remains
    /// live until that queue is consumed at the end of the current manager cut.</summary>
    internal static GameState CancelAssociatedCommandCurrent(GameState state, SourceWorldContext current,
        int commandId, NativeMapSeasonDate date)
    {
        var owner = CurrentCommandOwner(state, current);
        int[] members;
        int districtId;
        if (owner.Returns.TryGetValue(commandId, out var journey))
        { members = journey.MemberIds; districtId = journey.DistrictId; }
        else if (owner.Talents?.TryGetValue(commandId, out var talent) == true)
        { members = talent.MemberIds; districtId = talent.SourceDistrictId; }
        else if (owner.Disappearances?.TryGetValue(commandId, out var absence) == true)
        { members = absence.MemberIds; districtId = absence.DistrictId; }
        else if (owner.Diplomacies?.TryGetValue(commandId, out var diplomacy) == true)
        { members = diplomacy.MemberIds; districtId = diplomacy.SourceDistrictId; }
        else if (owner.Strategies?.TryGetValue(commandId, out var strategic) == true)
        {
            if (strategic.IsRetiring) return state;
            state = RefundStrategicMilitaryCurrent(state, current, strategic);
            members = strategic.MemberIds; districtId = strategic.SourceDistrictId;
        }
        else if (owner.Independences?.TryGetValue(commandId, out var independence) == true)
        { members = independence.MemberIds; districtId = independence.SourceDistrictId; }
        else throw Missing("cancel associated command virtual90 owner");

        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("cancel current player");
        var district = current.ReadDistrictTableReference(districtId <= 51 ? districtId : 0) ?? throw Missing("cancel source District");
        bool isPlayer = Get(current.ReadForceNativeValidity(player), "cancel player validity") &&
            ForcePrimaryDistrictSource.Resolve(current, player) == district;
        var returned = RescheduleEligibleMembers(state, current, members, isPlayer, date);
        owner = returned.Owner;
        owner = QueueRetirementCurrent(owner, current, commandId);
        return state with { PersonnelCommands = owner };
    }

    internal static PersonnelCommandTasks QueueRetirementCurrent(PersonnelCommandTasks owner,
        SourceWorldContext current, int commandId)
    {
        if (owner.Returns.TryGetValue(commandId, out var journey))
        {
            if (journey.IsRetiring) return owner;
            owner = owner with { Returns = new(owner.Returns) { [commandId] = journey with { IsRetiring = true } } };
        }
        else if (owner.Talents?.TryGetValue(commandId, out var talent) == true)
        {
            if (talent.IsRetiring) return owner;
            owner = owner with { Talents = new(owner.Talents) { [commandId] = talent with { IsRetiring = true } } };
        }
        else if (owner.Disappearances?.TryGetValue(commandId, out var absence) == true)
        {
            if (absence.IsRetiring) return owner;
            owner = owner with { Disappearances = new(owner.Disappearances) { [commandId] = absence with { IsRetiring = true } } };
        }
        else if (owner.Diplomacies?.TryGetValue(commandId, out var diplomacy) == true)
        {
            if (diplomacy.IsRetiring) return owner;
            owner = owner with { Diplomacies = new(owner.Diplomacies) { [commandId] = diplomacy with { IsRetiring = true } } };
        }
        else if (owner.Strategies?.TryGetValue(commandId, out var strategic) == true)
        {
            if (strategic.IsRetiring) return owner;
            owner = owner with { Strategies = new(owner.Strategies) { [commandId] = strategic with { IsRetiring = true } } };
        }
        else if (owner.Independences?.TryGetValue(commandId, out var independence) == true)
        {
            if (independence.IsRetiring) return owner;
            owner = owner with { Independences = new(owner.Independences) { [commandId] = independence with { IsRetiring = true } } };
        }
        else throw Missing("retiring personnel command owner");
        owner = owner with { PendingRetirements = [.. owner.PendingRetirements, commandId] };
        current.SetOwnedPersonCommandManager(owner);
        return owner;
    }
}
