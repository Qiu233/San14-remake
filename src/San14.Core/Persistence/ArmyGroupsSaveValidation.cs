namespace San14.Core;

internal static class ArmyGroupsSaveValidation
{
    public static void Validate(ArmyGroupsState? groups)
    {
        if (groups is null) return;
        if (groups.UnitAssignments is null || groups.Leaders is null)
            throw new InvalidDataException("Saved army groups are incomplete.");
        Officer(groups.ResetLeader);
        foreach (var assignment in groups.UnitAssignments)
            if (assignment.Key <= 0 || assignment.Value is null || assignment.Value.GroupId is <= 0 ||
                !assignment.Value.IsKnown && assignment.Value.GroupId is not null)
                throw new InvalidDataException("Saved army group assignment is invalid.");
        foreach (var leader in groups.Leaders)
        {
            if (leader.Key <= 0 || leader.Value is null)
                throw new InvalidDataException("Saved army group leader is invalid.");
            Officer(leader.Value.Officer);
        }
    }

    private static void Officer(ArmyGroupOfficerReference reference)
    {
        if (reference is null || !Enum.IsDefined(reference.Kind) ||
            (reference.Kind == ArmyGroupOfficerKind.Officer ? reference.OfficerId is null or < 0 : reference.OfficerId is not null))
            throw new InvalidDataException("Saved army group officer reference is invalid.");
    }
}
