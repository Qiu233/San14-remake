using System.Collections.Immutable;

namespace San14.Core;

public enum ArmyGroupPurposeKind
{
    Withdraw = 0,
    InterceptCautionGroup = 1,
    HoldAtHex = 7,
    TransportToFoothold = 12,
    CaptureArea = 13,
    EnterCity = 17,
    EnterGate = 18
}

public enum ArmyGroupPurposeTargetKind { Foothold, CautionGroup, Hex, Area, MapObject, Retained }

/// <summary>Footholds use the shared City/Gate location identity; caution
/// groups use their group identity and Hex uses a physical map coordinate.</summary>
public sealed record ArmyGroupPurposeTarget(ArmyGroupPurposeTargetKind Kind, int Id);

public sealed record ArmyGroupPurpose(ArmyGroupPurposeKind Kind, ArmyGroupPurposeTarget Target)
{
    // Only this exact purpose/target pair is the actual reset value.
    // A withdrawal to a nonzero foothold is an ordinary active objective.
    public static ArmyGroupPurpose Empty { get; } =
        new(ArmyGroupPurposeKind.Withdraw, new(ArmyGroupPurposeTargetKind.Foothold, 0));
}

/// <summary>Independent current purpose and ordered relay plan. Null retains
/// unknown knowledge; an empty relay array is a produced empty plan. The plan
/// holds at most ten actual map Hex identities, preserving order and repeats.</summary>
public sealed record ArmyGroupObjectiveState(ArmyGroupPurpose? Purpose, ImmutableArray<int>? RelayHexIds)
{
    public static ArmyGroupObjectiveState Reset { get; } = new(ArmyGroupPurpose.Empty, ImmutableArray<int>.Empty);
}

public static class ArmyGroupObjectives
{
    public static bool IsPurposeValid(ArmyGroupPurpose? purpose, int hexCount)
    {
        if (purpose?.Target is not { } target) return false;
        //035990 writes a clamped source purpose and one of these actual
        //target families. Unhandled purposes do not write the selected free
        //receiver's previous target identity; Retained preserves that fact.
        int kind = (int)purpose.Kind;
        if (kind is < 0 or > ushort.MaxValue) return false;
        if (target.Kind == ArmyGroupPurposeTargetKind.Retained)
            return target.Id is >= 0 and <= ushort.MaxValue;
        return kind switch
        {
            1 or 19 => target.Kind == ArmyGroupPurposeTargetKind.CautionGroup && target.Id is >= 0 and <= 500,
            5 or 6 or 7 => target.Kind == ArmyGroupPurposeTargetKind.Hex && target.Id >= 0 && target.Id < hexCount,
            3 or 13 or 14 or 15 or 16 or 21 or 23 => target.Kind == ArmyGroupPurposeTargetKind.Area && target.Id is >= 0 and <= 500,
            4 or 20 => target.Kind == ArmyGroupPurposeTargetKind.MapObject && target.Id is >= 0 and <= 3000,
            0 or 2 or 8 or 9 or 10 or 11 or 12 or 22 => target.Kind == ArmyGroupPurposeTargetKind.Foothold && target.Id is >= 0 and <= 61,
            17 or 18 => target.Kind == ArmyGroupPurposeTargetKind.Foothold && target.Id is >= 0 and <= 61,
            _ => false
        };
    }
}
