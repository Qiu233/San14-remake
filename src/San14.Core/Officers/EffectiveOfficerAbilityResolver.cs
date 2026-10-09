namespace San14.Core;

/// <summary>
/// Current resolved context outside the base-stat and item catalogs. AppointmentBonus
/// must be the applicable current ruler-rank OR office bonus. The original office
/// encoded cache is populated from definition values, not an extra bonus source.
/// Null values are unresolved, never inferred as zero or 100.
/// </summary>
public sealed record OfficerAbilityContext(int? HealthPercent, int? SecondaryPercent,
    OfficerAbilities? DoctrineBonus, OfficerAbilities? AppointmentBonus,
    int? OrdinaryCap, int? CharmCap);

/// <summary>Resolves current modifiers without mutating state or substituting source defaults.</summary>
public interface IOfficerAbilityContext
{
    OfficerAbilityContext? Resolve(GameState state, int officerId);
}

/// <summary>Original item-rule identities are configurable here, outside the generic state schema.</summary>
public sealed record OfficerAbilityRules(int MaximumPerItem, int? CharmOverrideItemId,
    int CharmOverrideValue)
{
    public static OfficerAbilityRules Baseline { get; } = new(10, 123, 100);
}

/// <summary>Frozen initial illness parameters; runtime parameter loading may replace these values.</summary>
public sealed record IllnessPercentages(int State0, int State1, int State2, int State3)
{
    public static IllnessPercentages Baseline { get; } = new(100, 80, 50, 20);

    public int ForState(int state) => state switch
    {
        0 => State0, 1 => State1, 2 => State2, 3 => State3, _ => 0
    };
}

/// <summary>
/// Complete CE arithmetic for the ordinary full-modifier ability query. Context
/// initialization and modifier eligibility are supplied separately. This implements
/// IEffectiveOfficerAbilities so command-book and other consumers share one result.
/// </summary>
public sealed class EffectiveOfficerAbilityResolver : IEffectiveOfficerAbilities
{
    private readonly IOfficerAbilityContext context;
    private readonly OfficerAbilityRules rules;

    public EffectiveOfficerAbilityResolver(IOfficerAbilityContext context,
        OfficerAbilityRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        this.context = context;
        this.rules = rules ?? OfficerAbilityRules.Baseline;
        ArgumentOutOfRangeException.ThrowIfNegative(this.rules.MaximumPerItem);
        if (this.rules.CharmOverrideItemId < 0)
            throw new ArgumentOutOfRangeException(nameof(rules), "Charm override item ID is negative.");
    }

    public OfficerAbilities? Resolve(GameState state, int officerId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Officers is null || officerId < 0 ||
            !state.Officers.TryGetValue(officerId, out OfficerState? officer) || officer is null ||
            officer.Id != officerId || officer.BaseAbilities is not { } basic)
            return null;
        OfficerAbilityContext? current = context.Resolve(state, officerId);
        if (current is not { HealthPercent: int health, SecondaryPercent: int secondary,
            DoctrineBonus: { } doctrine, AppointmentBonus: { } appointment,
            OrdinaryCap: int ordinaryCap, CharmCap: int charmCap } || ordinaryCap < 0 || charmCap < 0 ||
            !ItemAbilityBonuses.TryCalculate(state, officerId, out OfficerAbilities? items, rules.MaximumPerItem))
            return null;

        // The original late charm branch enumerates the same current person-item
        // list but tests the item's identity, independently of its ordinary bonus.
        int? forcedCharm = null;
        if (rules.CharmOverrideItemId is int itemId && state.Items!.TryGetValue(itemId, out ItemState? item) &&
            item.IsPresent == true && item.Holder?.OfficerId == officerId)
            forcedCharm = rules.CharmOverrideValue;

        return new OfficerAbilities(
            Calculate(basic.Leadership, health, secondary, doctrine.Leadership, appointment.Leadership, items!.Leadership, ordinaryCap),
            Calculate(basic.Strength, health, secondary, doctrine.Strength, appointment.Strength, items.Strength, ordinaryCap),
            Calculate(basic.Intelligence, health, secondary, doctrine.Intelligence, appointment.Intelligence, items.Intelligence, ordinaryCap),
            Calculate(basic.Politics, health, secondary, doctrine.Politics, appointment.Politics, items.Politics, ordinaryCap),
            Calculate(basic.Charm, health, secondary, doctrine.Charm, appointment.Charm, items.Charm, charmCap, forcedCharm));
    }

    /// <summary>
    /// Signed 32-bit operations and two separate divisions toward zero, matching
    /// CE 22F160. Appointment has exactly one applicable rank/office contribution.
    /// A forced value is applied after every addition and before the final bound.
    /// </summary>
    public static int Calculate(int baseValue, int healthPercent, int secondaryPercent,
        int doctrineBonus, int appointmentBonus, int itemBonus, int cap, int? forcedValue = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cap);
        int value;
        unchecked
        {
            value = baseValue * healthPercent / 100;
            value = value * secondaryPercent / 100;
            value += doctrineBonus;
            value += appointmentBonus;
            value += itemBonus;
        }
        if (forcedValue is int forced) value = forced;
        // The native lower branch returns 1 immediately. Cap zero is valid input
        // and therefore differs from Math.Clamp or min(max(value, 1), cap).
        return value < 1 ? 1 : Math.Min(value, cap);
    }
}
