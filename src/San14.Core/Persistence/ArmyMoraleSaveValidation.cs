namespace San14.Core;

internal static class ArmyMoraleSaveValidation
{
    public static void Validate(GameState state)
    {
        foreach (var unit in state.Units.Values.Concat(state.ArmyLifecycle?.Receivers.Entries
            .Where(row => row.PendingImage is not null).Select(row => row.PendingImage!) ?? [])
            .Concat(state.ForceDefeats.Values.SelectMany(defeat => defeat.WithdrawnUnits)))
            if (unit.MoraleState is null || unit.MoraleState.CurrentValue is < 0)
                throw new InvalidDataException("Saved army morale is missing or invalid.");
    }
}
