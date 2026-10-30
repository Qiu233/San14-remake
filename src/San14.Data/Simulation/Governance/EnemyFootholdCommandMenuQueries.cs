using San14.Core;

namespace San14.Data;

public enum EnemyFootholdCommandMenuCommand
{
    UnitTarget, Strategic, CityInformation, GateInformation, AreaInformation, Government
}

/// <summary>The constructor's structural admission and the updater's enabled
/// state are independent. An unknown business input does not hide known details.</summary>
public sealed record EnemyFootholdCommandMenuEntry(EnemyFootholdCommandMenuCommand Command,
    int Payload, int CaptionId, AreaAdministrationQuery<bool> SourcePresent,
    AreaAdministrationQuery<bool> SourceEnabled);

/// <summary>BaseId uses the public City/Gate identity, including Gate52..61.
/// AreaId is229FD0's actual target; ForceId is the current Base owner.</summary>
public sealed record EnemyFootholdCommandMenuProjection(BaseKind Kind, int BaseId,
    int AreaId, int ForceId, int HexId, IReadOnlyList<EnemyFootholdCommandMenuEntry> Entries);
