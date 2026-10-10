namespace San14.Core;

internal static class ArmyFormationSaveValidation
{
    public static void Validate(GameState state)
    {
        foreach (var unit in state.Units.Values.Concat(state.ArmyLifecycle?.Receivers.Entries
            .Where(row => row.PendingImage is not null).Select(row => row.PendingImage!) ?? [])
            .Concat(state.ForceDefeats.Values.SelectMany(defeat => defeat.WithdrawnUnits)))
            if (unit.Formations is { } formations && (formations.SelectedLandFormationId is < 0 ||
                formations.CurrentLandFormationId is < 0 || formations.NavalFormationId is < 0))
                throw new InvalidDataException("Saved army formation identity is invalid.");
    }
}
