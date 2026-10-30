namespace San14.Data;

/// <summary>Immutable monthly salary rules. The two locales have independent
/// positive source qualification; saved offices, current traits and policy
/// inputs remain the actual domain owners.</summary>
internal readonly record struct NativeMonthlySalaryRuleProfile(int OfficeMultiplier,
    int SpecialPersonPay, int TraitMultiplier, int PolicyMultiplier, int CaptivePay);

internal static class NativeMonthlySalaryRules
{
    //TC's existing2B4DE0/2DD2F0 rules and SC's independently located
    //2B4C20/2DD210 rules. Equal values are not a cross-locale fallback.
    private static readonly NativeMonthlySalaryRuleProfile Traditional = new(10, 10, 1, 1, 50);
    private static readonly NativeMonthlySalaryRuleProfile Simplified = new(10, 10, 1, 1, 50);

    internal static NativeMonthlySalaryRuleProfile ForLanguage(string language) => language switch
    {
        "TC" => Traditional,
        "SC" => Simplified,
        _ => throw new InvalidDataException("army.Turn_salary.unsupported_rule_language")
    };
}
