namespace San14.Core;

public enum ArmyCautionUnitKind { Unknown, None, Unit, Expired }

/// <summary>A caution's logical army target. An expired legacy target has no
/// identity that can bind to a subsequently created army.</summary>
public sealed record ArmyCautionUnitReference(ArmyCautionUnitKind Kind, int? UnitId = null)
{
    public static ArmyCautionUnitReference Unknown { get; } = new(ArmyCautionUnitKind.Unknown);
    public static ArmyCautionUnitReference None { get; } = new(ArmyCautionUnitKind.None);
    public static ArmyCautionUnitReference Expired { get; } = new(ArmyCautionUnitKind.Expired);
}

/// <summary>The resource map-object identity associated with the target base.
/// Data resolves a current City/Gate receiver through its actual object binding
/// when needed; this identity is not an ordinal City/Gate table position.</summary>
public sealed record ArmyCautionBaseReference(bool IsKnown, int? MapObjectId)
{
    public static ArmyCautionBaseReference Unknown { get; } = new(false, null);
    public static ArmyCautionBaseReference None { get; } = new(true, null);
}

public sealed record ArmyCautionRelation(ArmyCautionUnitReference Unit, ArmyCautionBaseReference Base)
{
    public static ArmyCautionRelation Empty { get; } = new(ArmyCautionUnitReference.None, ArmyCautionBaseReference.None);
}

/// <summary>A positive label allocated within a caller's selected group list.
/// This label is not a global identity or a member count.</summary>
public sealed record ArmyCautionGroupLabel(bool IsKnown, int? Label)
{
    public static ArmyCautionGroupLabel Empty { get; } = new(true, null);
    public static ArmyCautionGroupLabel Unknown { get; } = new(false, null);
}

/// <summary>Caution relations and group labels. Registry and member ordering
/// belong to WorldIndexesState. Omitted rows have the normal inactive default;
/// unknown and nondefault inactive rows are stored explicitly.</summary>
public sealed record ArmyCautionsState
{
    public Dictionary<int, ArmyCautionRelation> Relations { get; init; } = [];
    public Dictionary<int, ArmyCautionGroupLabel> GroupLabels { get; init; } = [];

    public ArmyCautionRelation GetRelation(int cautionId) => Relations.GetValueOrDefault(cautionId) ?? ArmyCautionRelation.Empty;
    public ArmyCautionGroupLabel GetGroupLabel(int groupId) => GroupLabels.GetValueOrDefault(groupId) ?? ArmyCautionGroupLabel.Empty;

    public IEnumerable<int> ReferencedUnitIds() => Relations.Values
        .Where(relation => relation.Unit is { Kind: ArmyCautionUnitKind.Unit, UnitId: not null })
        .Select(relation => relation.Unit.UnitId!.Value);

    public ArmyCautionsState Copy() => this with
    {
        Relations = new(Relations),
        GroupLabels = new(GroupLabels)
    };
}
