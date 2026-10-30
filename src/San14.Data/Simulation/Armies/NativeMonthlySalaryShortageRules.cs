namespace San14.Data;

/// <summary>Disposition buckets used only after the actual salary-shortage and
/// relationship gates. Special-person loss is handled by the caller.</summary>
internal readonly record struct NativeMonthlySalaryShortageRuleProfile(
    int FirstThreshold, int SecondThreshold, int ThirdThreshold, int FourthThreshold,
    int FirstBucket, int SecondBucket, int ThirdBucket, int FourthBucket, int LastBucket,
    int Multiplier)
{
    internal int LoyaltyLoss(int disposition)
    {
        int bucket = disposition < FirstThreshold ? FirstBucket
            : disposition < SecondThreshold ? SecondBucket
            : disposition < ThirdThreshold ? ThirdBucket
            : disposition < FourthThreshold ? FourthBucket
            : LastBucket;
        // The native subtraction consumes the low byte of the signed product.
        return unchecked((byte)(Multiplier * bucket));
    }
}

internal static class NativeMonthlySalaryShortageRules
{
    // Each locale was independently qualified from its own monthly body and
    // directly referenced constants. Matching values are not locale fallback.
    private static readonly NativeMonthlySalaryShortageRuleProfile Traditional =
        new(-14, -5, 6, 15, 8, 7, 6, 5, 4, 2);
    private static readonly NativeMonthlySalaryShortageRuleProfile Simplified =
        new(-14, -5, 6, 15, 8, 7, 6, 5, 4, 2);

    internal static NativeMonthlySalaryShortageRuleProfile ForLanguage(string language) => language switch
    {
        "TC" => Traditional,
        "SC" => Simplified,
        _ => throw new InvalidDataException("army.Turn_salary.unsupported_shortage_rule_language")
    };
}
