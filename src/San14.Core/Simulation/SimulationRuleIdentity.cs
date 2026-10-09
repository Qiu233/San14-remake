namespace San14.Core;

/// <summary>The version of this project's simulation rules, independent of installed game binaries.</summary>
public static class SimulationRuleIdentity
{
    public const string Version = "san14-remake.simulation-rules/v1";

    public static string ForLocale(string locale) => locale is "SC" or "TC"
        ? Version
        : throw new ArgumentOutOfRangeException(nameof(locale), "Only SC and TC rules are supported.");
}
