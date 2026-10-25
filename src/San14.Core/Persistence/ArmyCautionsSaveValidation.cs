namespace San14.Core;

internal static class ArmyCautionsSaveValidation
{
    public static void Validate(ArmyCautionsState? cautions)
    {
        if (cautions is null) return;
        if (cautions.Relations is null || cautions.GroupLabels is null)
            throw new InvalidDataException("Saved army cautions are incomplete.");
        foreach (var row in cautions.Relations)
        {
            if (row.Key < 0 || row.Value is not { Unit: not null, Base: not null } relation ||
                !Enum.IsDefined(relation.Unit.Kind) ||
                (relation.Unit.Kind == ArmyCautionUnitKind.Unit ? relation.Unit.UnitId is null or <= 0 : relation.Unit.UnitId is not null) ||
                relation.Base.MapObjectId is <= 0 || !relation.Base.IsKnown && relation.Base.MapObjectId is not null)
                throw new InvalidDataException("Saved army caution relation is invalid.");
        }
        foreach (var row in cautions.GroupLabels)
            if (row.Key < 0 || row.Value is null || row.Value.Label is <= 0 || !row.Value.IsKnown && row.Value.Label is not null)
                throw new InvalidDataException("Saved army caution group label is invalid.");
        if (cautions.GroupDistrictIds is not null)
            foreach (var row in cautions.GroupDistrictIds)
                if (row.Key is < 0 or > 500 || row.Value is < 0 or > 51)
                    throw new InvalidDataException("Saved army caution group District is invalid.");
    }
}
