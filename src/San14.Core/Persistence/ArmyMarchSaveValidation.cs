namespace San14.Core;

internal static class ArmyMarchSaveValidation
{
    public static void Validate(GameState state)
    {
        foreach (var unit in state.Units.Values.Concat(state.NativeArmyLifecycle?.Slots
            .Where(slot => slot.PendingImage is not null).Select(slot => slot.PendingImage!) ?? []))
        {
            if (!Enum.IsDefined(unit.PositionKnowledge) || unit.March?.MovementCredit is < 0 ||
                unit.PositionKnowledge == ArmyPositionKnowledge.Conflicting && state.ResourceState is null)
                throw new InvalidDataException("Saved army position or movement credit is invalid.");
            Destination(unit.March?.Target); Destination(unit.March?.NextStep);
        }
    }
    private static void Destination(ArmyMarchDestination? value)
    {
        if (value is null) return;
        if (!Enum.IsDefined(value.Kind) || value.Kind == ArmyMarchDestinationKind.None && value.HexId is not null ||
            value.Kind == ArmyMarchDestinationKind.Hex && value.HexId is not (>= 0))
            throw new InvalidDataException("Saved army march destination is invalid.");
    }
}
