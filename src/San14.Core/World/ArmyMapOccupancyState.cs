namespace San14.Core;

public enum ArmyMapDefaultCellKnowledge { Unknown, Empty }
public enum ArmyMapCellKind { Unknown, Empty, Binding, UnsupportedTargetNegative }
public enum ArmyOccupancyUnitKind { Unknown, Vacant, Unit }

/// <summary>A map marker names a reusable occupancy binding, independently of
/// its current army. Unsupported targets have a known absent army marker but
/// cannot be resolved by a field selector.</summary>
public sealed record ArmyMapCell(ArmyMapCellKind Kind, int? BindingId = null)
{
    public static ArmyMapCell Unknown { get; } = new(ArmyMapCellKind.Unknown);
    public static ArmyMapCell Empty { get; } = new(ArmyMapCellKind.Empty);
    public static ArmyMapCell UnsupportedTargetNegative { get; } = new(ArmyMapCellKind.UnsupportedTargetNegative);
    public static ArmyMapCell ForBinding(int bindingId) => bindingId > 0
        ? new(ArmyMapCellKind.Binding, bindingId) : throw new ArgumentOutOfRangeException(nameof(bindingId));
}

/// <summary>The current logical army of a reusable binding. A vacant binding
/// can remain marked on the map and later be assigned another army.</summary>
public sealed record ArmyOccupancyUnitReference(ArmyOccupancyUnitKind Kind, int? UnitId = null)
{
    public static ArmyOccupancyUnitReference Unknown { get; } = new(ArmyOccupancyUnitKind.Unknown);
    public static ArmyOccupancyUnitReference Vacant { get; } = new(ArmyOccupancyUnitKind.Vacant);
    public static ArmyOccupancyUnitReference ForUnit(int unitId) => unitId >= 0
        ? new(ArmyOccupancyUnitKind.Unit, unitId) : throw new ArgumentOutOfRangeException(nameof(unitId));
}

/// <summary>An independently written map index. Multiple cells may refer to
/// one binding, and neither position nor army validity regenerates its cells.</summary>
public sealed record ArmyMapOccupancyState
{
    public ArmyMapDefaultCellKnowledge DefaultCellKnowledge { get; init; }
    public Dictionary<int, ArmyMapCell> Cells { get; init; } = [];
    public Dictionary<int, ArmyOccupancyUnitReference> Bindings { get; init; } = [];

    public ArmyMapCell ReadCell(int hexId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hexId);
        return Cells.GetValueOrDefault(hexId) ?? (DefaultCellKnowledge switch
        {
            ArmyMapDefaultCellKnowledge.Unknown => ArmyMapCell.Unknown,
            ArmyMapDefaultCellKnowledge.Empty => ArmyMapCell.Empty,
            _ => throw new InvalidDataException("Map cell default knowledge is invalid.")
        });
    }

    public ArmyOccupancyUnitReference GetBinding(int bindingId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bindingId);
        return Bindings.GetValueOrDefault(bindingId) ?? ArmyOccupancyUnitReference.Unknown;
    }

    /// <summary>Recognized marker presence does not test the current army's
    /// validity, exclusion, active projection or position.</summary>
    public bool? HasArmyMarker(int hexId) => ReadCell(hexId).Kind switch
    {
        ArmyMapCellKind.Unknown => null,
        ArmyMapCellKind.Binding => true,
        ArmyMapCellKind.Empty or ArmyMapCellKind.UnsupportedTargetNegative => false,
        _ => throw new InvalidDataException("Map cell kind is invalid.")
    };

    public IEnumerable<int> ReferencedUnitIds() => Bindings.Values
        .Where(target => target is { Kind: ArmyOccupancyUnitKind.Unit, UnitId: not null })
        .Select(target => target.UnitId!.Value);

    public ArmyMapOccupancyState Copy() => this with
    {
        Cells = new(Cells),
        Bindings = new(Bindings)
    };
}

public sealed partial record GameState
{
    public ArmyMapOccupancyState? ArmyMapOccupancy { get; init; }
}
