using San14.Core;

namespace San14.Data;

public static partial class PersonReturnCommands
{
    /// <summary>Complete the actual personnel command, retaining deferred
    /// retirement for existing manager cuts. Source335CD0 callers additionally
    /// consume its virtual110(...,1) with RetireAssociatedCommandImmediatelyCurrent.</summary>
    internal static GameState CancelAssociatedCommandCurrent(GameState state, SourceWorldContext current,
        int commandId, NativeMapSeasonDate date, ArmyCampaignRuleAdapter? package = null)
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
        else if (owner.Caravans?.TryGetValue(commandId, out var caravan) == true)
        {
            if (caravan.IsRetiring) return state;
            if (package is null) throw Missing("cancel caravan execution owner");
            return package.CancelForeignTradeAssociatedCurrent(state, current, caravan, date);
        }
        else if (owner.Intelligences?.TryGetValue(commandId, out var intelligence) == true)
        { if (intelligence.IsRetiring) return state; members = intelligence.MemberIds; districtId = intelligence.SourceDistrictId; }
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
        else if (owner.Caravans?.TryGetValue(commandId, out var caravan) == true)
        {
            if (caravan.IsRetiring) return owner;
            owner = owner with { Caravans = new(owner.Caravans) { [commandId] = caravan with { IsRetiring = true } } };
        }
        else if (owner.Intelligences?.TryGetValue(commandId, out var intelligence) == true)
        {
            if (intelligence.IsRetiring) return owner;
            owner = owner with { Intelligences = new(owner.Intelligences) { [commandId] = intelligence with { IsRetiring = true } } };
        }
        else throw Missing("retiring personnel command owner");
        owner = owner with { PendingRetirements = [.. owner.PendingRetirements, commandId] };
        current.SetOwnedPersonCommandManager(owner);
        return owner;
    }

    //228B50 ->32AC90 ->1F2450(param3=1): release this receiver now,
    //before22AC00 is queried again. Other manager cuts retain deferred order.
    internal static GameState RetireAssociatedCommandImmediatelyCurrent(GameState state,
        SourceWorldContext current, int commandId)
    {
        var owner = CurrentCommandOwner(state, current);
        var entry = owner.DispatchOrder().FirstOrDefault(entry => entry.Id == commandId)
            ?? throw Missing("immediate retirement dispatch receiver");
        owner = entry.Kind switch
        {
            PersonnelCommandKind.Return => owner with { Returns = Remove(owner.Returns) },
            PersonnelCommandKind.Disappear => owner with { Disappearances = Remove(owner.Disappearances!) },
            PersonnelCommandKind.Talent => owner with { Talents = Remove(owner.Talents!) },
            PersonnelCommandKind.Diplomacy => owner with { Diplomacies = Remove(owner.Diplomacies!) },
            PersonnelCommandKind.Strategic => owner with { Strategies = Remove(owner.Strategies!) },
            PersonnelCommandKind.Independence => owner with { Independences = Remove(owner.Independences!) },
            PersonnelCommandKind.Caravan => owner with { Caravans = Remove(owner.Caravans!) },
            PersonnelCommandKind.Intelligence => owner with { Intelligences = Remove(owner.Intelligences!) },
            _ => throw Missing("immediate retirement command kind")
        };
        var associated = current.OwnedIndexes?.AssociatedTaskHandle ?? throw Missing("immediate retirement association index");
        current.RemovePersonReturnAssociated(new(associated.Identity), commandId);
        current.SetLinkedEntityRawState58(commandId, -1);
        current.SetLinkedEntityMemberIds(commandId, []);
        owner = owner with { PendingRetirements = owner.PendingRetirements.Where(id => id != commandId).ToArray(),
            AvailableDispatchSlots = [entry.Slot, .. owner.AvailableDispatchSlots] };
        state = state with { PersonnelCommands = owner };
        BindCurrent(state, current);
        return state;

        Dictionary<int, T> Remove<T>(Dictionary<int, T> source)
        { var result = new Dictionary<int, T>(source); result.Remove(commandId); return result; }
    }
}
