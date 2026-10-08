namespace San14.Core;

/// <summary>
/// The original item contribution to the five abilities. This is one additive term,
/// independent of illness, rank, office, doctrine panels and the final ability caps.
/// </summary>
public static class OriginalItemAbilityBonuses
{
    /// <summary>
    /// Requires a complete known inventory. Only present items held by this officer
    /// contribute. Each nonnegative parameter is capped before taking the maximum;
    /// multiple items never stack in this original rule. An unresolved record that
    /// could contribute makes the query unavailable, rather than supplying zero.
    /// </summary>
    public static bool TryCalculate(GameState state, int officerId,
        out OfficerAbilities? result, int maximumPerItem = 10)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumPerItem);
        result = null;
        if (state.Items is null || state.ItemDefinitions is null || state.Officers is null ||
            officerId < 0 || !state.Officers.TryGetValue(officerId, out OfficerState? officer) ||
            officer is null || officer.Id != officerId)
            return false;

        int leadership = 0, strength = 0, intelligence = 0, politics = 0, charm = 0;
        foreach ((int id, ItemState item) in state.Items)
        {
            if (item is null || id != item.Id || id < 0 ||
                !state.ItemDefinitions.TryGetValue(item.DefinitionId, out ItemDefinition? definition) ||
                definition is null || definition.Id != item.DefinitionId ||
                (item.Holder?.OfficerId is int holder && (holder < 0 ||
                    !state.Officers.TryGetValue(holder, out OfficerState? owner) || owner is null || owner.Id != holder)))
                return false;
            if (item.IsPresent == false) continue;
            if (item.Holder is null) return false;
            if (item.Holder.OfficerId != officerId) continue;
            if (item.IsPresent is null || definition.AbilityBonus is not { } bonus ||
                bonus.Leadership < 0 || bonus.Strength < 0 || bonus.Intelligence < 0 ||
                bonus.Politics < 0 || bonus.Charm < 0)
                return false;

            leadership = Math.Max(leadership, Math.Min(bonus.Leadership, maximumPerItem));
            strength = Math.Max(strength, Math.Min(bonus.Strength, maximumPerItem));
            intelligence = Math.Max(intelligence, Math.Min(bonus.Intelligence, maximumPerItem));
            politics = Math.Max(politics, Math.Min(bonus.Politics, maximumPerItem));
            charm = Math.Max(charm, Math.Min(bonus.Charm, maximumPerItem));
        }
        result = new OfficerAbilities(leadership, strength, intelligence, politics, charm);
        return true;
    }
}
