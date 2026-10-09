namespace San14.Core;

internal static class CombatSettlementSaveValidation
{
    public static void Validate(CombatSettlementQueue? queue)
    {
        if (queue is null) return;
        if (queue.Requests is null || queue.Requests.Count > CombatSettlementRules.PendingSettlementLimit)
            throw new InvalidDataException("Saved combat settlements exceed the supported admission limit.");
        foreach (var request in queue.Requests)
        {
            switch (request)
            {
                case UnitDefeatRequest unit when unit.VictimUnitId > 0 && unit.WinnerUnitId is null or > 0:
                    if (unit.WinnerBase is { } target) ValidateBase(target);
                    break;
                case BaseCaptureRequest capture when capture.WinnerUnitId is null or > 0:
                    ValidateBase(capture.Target);
                    break;
                case ExpiredCombatSettlement:
                    break;
                default:
                    throw new InvalidDataException("Saved combat settlement has an invalid entity reference.");
            }
        }
    }

    private static void ValidateBase(SettlementBase target)
    {
        if (target is null || !Enum.IsDefined(target.Kind) || target.Id <= 0)
            throw new InvalidDataException("Saved combat settlement has an invalid base reference.");
    }
}
