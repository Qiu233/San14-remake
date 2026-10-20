namespace San14.Core;

internal static class ArmyEquippedTactics
{
    // Native slots preserve empty0 and repeated equipped identities. Legacy
    // provisional units retain their distinct catalogue-list interpretation.
    internal static bool Valid(GameState state, UnitState unit) => unit.TacticIds is not { } ids ||
        (unit.NativeArmyOrigin is null
            ? ids.Distinct().Count() == ids.Length && ids.All(state.Tactics.ContainsKey)
            : ids.Length <= 3 && ids.All(id => id is >= 0 and <= 200 && (id == 0 || state.Tactics.ContainsKey(id))));
}
