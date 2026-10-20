namespace San14.Data;

/// <summary>Paired CE development/security arithmetic. Inputs are resolved current
/// results; missing writable fallback globals never become numeric defaults.</summary>
internal static class AreaAdministrationPreviewMath
{
    // The current Charm byte domain and exponent2 have identical binary64
    // square results in both CE power dispatches. Keep the later roundings.
    internal static int Occupation(int charm, int trait, int taskPercent, int advantage,
        bool difficultOpponent, bool alternateBonus, int? advancedPercent)
    {
        double numerator = (double)charm * charm, denominator = 100d * 100d;
        double ratio = numerator / denominator;
        double basis = 8d * ratio;
        basis += unchecked(trait * 1);
        double scaled = (double)taskPercent * basis;
        int value = Math.Max(1, (int)(scaled / 100d));
        unchecked
        {
            value += advantage;
            if (difficultOpponent) value += Math.Min(24, 130 * value / 100);
            if (alternateBonus) value += Math.Min(25, 100 * value / 100);
            if (advancedPercent is int percent) value = percent * value / 100;
        }
        return Math.Min(25, value);
    }

    internal static int OccupationWithCityCenter(int value, bool cityCenter) =>
        Math.Min(25, unchecked(value + (cityCenter ? 5 : 0)));

    // Paired static rule data, retained in private preflight/identity.json.
    private const int SpecialtyBonus = 10, AwayDivisor = 2;
    private const int CloseRelationPercent = -5, OtherRelationPercent = 5;
    private const int DevelopmentCoefficient = 300, ProgressDivisor = 5, ProgressBase = 30;
    private const int ForcePercent = 120, ForceBonus = 2;
    private const int SecurityCoefficient = 3, SecurityBase = 30;

    internal static int Development(int effective, int taskPercent, bool away, int doctrine,
        int kind, ushort progress, int level, int completedBonus, int closeRelations,
        int otherRelations, bool forceBonus, int ownTrait, int cityTrait, int policy, int? advancedPercent)
    {
        int basis = DevelopmentBasis(effective, taskPercent, away, doctrine, kind, progress,
            level, completedBonus, closeRelations, otherRelations, forceBonus);
        return DevelopmentModifiers(basis, ownTrait, cityTrait, policy, advancedPercent);
    }

    internal static int DevelopmentBasis(int effective, int taskPercent, bool away, int doctrine,
        int kind, ushort progress, int level, int completedBonus, int closeRelations,
        int otherRelations, bool forceBonus)
    {
        unchecked
        {
            int ability = effective * taskPercent / 100;
            if (away) ability /= AwayDivisor;
            bool specialty = kind switch { 1 => doctrine is 1 or 5, 2 => doctrine is 4 or 6, _ => doctrine is 2 or 3 };
            int basis = (closeRelations * CloseRelationPercent + 100 + otherRelations * OtherRelationPercent) * ability / 100;
            basis += level == 5 ? completedBonus : 0;
            if (specialty) basis += SpecialtyBonus;
            basis = (basis * DevelopmentCoefficient / 100) * level / (ProgressBase + progress / ProgressDivisor);
            basis = basis * (forceBonus ? ForcePercent : 100) / 100 + (forceBonus ? ForceBonus : 0);
            return basis;
        }
    }

    internal static int DevelopmentModifiers(int basis, int ownTrait, int cityTrait, int policy, int? advancedPercent)
    {
        unchecked
        {
            int policyTerm = basis * policy / 100;
            if (policy != 0) policyTerm = Math.Max(1, policyTerm);
            int traitTerm = (ownTrait + cityTrait) * basis / 100;
            if (cityTrait > 0) traitTerm = Math.Max(1, traitTerm);
            else if (cityTrait < 0) traitTerm = Math.Min(-1, traitTerm);
            int result = Math.Max(1, basis + policyTerm + traitTerm);
            return advancedPercent is int percent ? result * percent / 100 : result;
        }
    }

    internal static int Security(int effective, int taskPercent, bool away, byte security,
        int closeRelations, int otherRelations, int ownTrait, int cityTrait, int policy, int? advancedPercent)
    {
        unchecked
        {
            int ability = effective * taskPercent / 100;
            if (away) ability /= AwayDivisor;
            int basis = (closeRelations * CloseRelationPercent + 100 + otherRelations * OtherRelationPercent) * ability / 100;
            // Actual CDQ/IDIV after signed32 IMUL, not a widened final product.
            basis = basis * SecurityCoefficient / (security + SecurityBase);
            int policyTerm = policy != 0 ? Math.Max(1, basis * policy / 100) : 0;
            int mixed = ownTrait + cityTrait;
            int traitTerm = mixed > 0 ? Math.Max(1, mixed * basis / 100) : mixed < 0 ? Math.Min(-1, mixed * basis / 100) : 0;
            int result = Math.Max(1, basis + policyTerm + traitTerm);
            return advancedPercent is int percent ? result * percent / 100 : result;
        }
    }
}
