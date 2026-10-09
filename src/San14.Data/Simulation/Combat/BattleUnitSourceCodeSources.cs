using San14.Core;

namespace San14.Data;

public sealed record BattleUnitSourceProfile(string Language, string RuleVersion);

/// <summary>Project rule version. Instruction-window evidence is kept only in local offline research.</summary>
public static class BattleUnitSourceCodeSources
{
    public static BattleUnitSourceProfile ForLanguage(string language) =>
        new(language, SimulationRuleIdentity.ForLocale(language));
}
