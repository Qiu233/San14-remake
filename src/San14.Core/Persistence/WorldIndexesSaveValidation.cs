namespace San14.Core;

internal static class WorldIndexesSaveValidation
{
    public static void Validate(WorldIndexesState indexes)
    {
        if (indexes.Registries is null || indexes.BaseIndexes is null || indexes.Armies is null || indexes.CautionGroupMembers is null)
            throw new InvalidDataException("Saved world membership indexes are incomplete.");
        foreach (var registry in indexes.Registries)
        {
            if (!Enum.IsDefined(registry.Key)) throw new InvalidDataException("Saved registry has an unknown entity kind.");
            foreach (var entity in registry.Value ?? []) ValidateEntity(entity);
        }
        foreach (var entity in indexes.Bases ?? []) ValidateEntity(entity);
        foreach (var location in indexes.BaseIndexes)
        {
            ValidateEntity(location.Base);
            ValidateIds(location.TerritoryHexIds); ValidateIds(location.OfficerIds);
        }
        foreach (var army in indexes.Armies)
        {
            if (army.Key != army.Value.UnitId || army.Key <= 0 || army.Value.TraitRelatedUnitIds is null)
                throw new InvalidDataException("Saved army relationship owner is invalid.");
            foreach (var related in army.Value.TraitRelatedUnitIds) ValidateIds(related);
        }
        foreach (var group in indexes.CautionGroupMembers)
        {
            if (group.Key < 0) throw new InvalidDataException("Saved army caution group is invalid.");
            ValidateIds(group.Value);
        }
        ValidateIds(indexes.ReturningPersonnelTaskOrder); ValidateIds(indexes.PendingUnitIds);
        ValidateIds(indexes.ChangedPendingUnitIds); ValidateIds(indexes.ChangedWorkingUnitIds);
    }

    private static void ValidateEntity(WorldEntityReference entity)
    {
        if (entity is null || !Enum.IsDefined(entity.Kind) || entity.Id is < 0 ||
            entity.Kind is WorldEntityKind.FallbackCity or WorldEntityKind.FallbackGate && entity.Id is not null)
            throw new InvalidDataException("Saved world index entity reference is invalid.");
    }
    private static void ValidateIds(IEnumerable<int?>? ids)
    {
        if (ids?.Any(id => id is < 0) == true) throw new InvalidDataException("Saved world index has a negative entity ID.");
    }
}
