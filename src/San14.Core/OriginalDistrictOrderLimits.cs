namespace San14.Core;

/// <summary>
/// Resolves final current abilities, including all active modifiers. Null means
/// unavailable. Implementations must not substitute base stats or mutate state.
/// </summary>
public interface IEffectiveOfficerAbilities
{
    OfficerAbilities? Resolve(GameState state, int officerId);
}

/// <summary>Original formula parameters, independent of the state format and custom rules.</summary>
public sealed record DistrictOrderLimitParameters(int Minimum, int Maximum);

/// <summary>
/// Verified CE command-book cap for a district. Initial stock, period recovery,
/// refunds and their timing are separate rules; calculating a cap changes no state.
/// </summary>
public static class OriginalDistrictOrderLimits
{
    // Frozen CE SC .data initial values at RVAs1A4C24C/1A4C244. Runtime
    // parameter loading is separate; these are not immutable machine constants.
    public static DistrictOrderLimitParameters BaselineParameters { get; } = new(50, 200);

    /// <summary>
    /// Takes the resolved final politics value. Explicit zero also represents the
    /// original consumer's verified invalid-person branch; unknown is not zero.
    /// </summary>
    public static int Calculate(int effectiveLeaderPolitics, DistrictOrderLimitParameters? parameters = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(effectiveLeaderPolitics);
        parameters ??= BaselineParameters;
        ArgumentOutOfRangeException.ThrowIfNegative(parameters.Minimum);
        if (parameters.Maximum < parameters.Minimum)
            throw new ArgumentOutOfRangeException(nameof(parameters), "Command-book maximum is smaller than the minimum.");
        return (int)Math.Clamp(2L * effectiveLeaderPolitics, (long)parameters.Minimum, parameters.Maximum);
    }

    /// <summary>
    /// Looks up this district's current leader. Missing legacy data stays unavailable;
    /// neither the force ruler, the officer role nor the base ability is a fallback.
    /// </summary>
    public static bool TryCalculate(GameState state, int districtId,
        IEffectiveOfficerAbilities abilities, out int limit, DistrictOrderLimitParameters? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(abilities);
        limit = default;
        if (!state.Districts.TryGetValue(districtId, out DistrictState? district) ||
            district.LeaderId is not int leaderId || !state.Officers.ContainsKey(leaderId))
            return false;
        OfficerAbilities? resolved = abilities.Resolve(state, leaderId);
        if (resolved is null || resolved.Politics < 0) return false;
        limit = Calculate(resolved.Politics, parameters);
        return true;
    }
}
