namespace San14.Data;

public sealed record DeploymentRadarMaximums(int Mobility, int FieldAttack,
    int SiegeTroopAttack, int WallAttack, int Defense);

/// <summary>Display rules for the audited CE deployment summary and large
/// chart. Palette identities are resolved by the existing UI palette source.
/// LargeComparisonMinimumPalette colors min(current, comparison), whereas
/// the summary layers each display their complete named statistics group.</summary>
public sealed record DeploymentRadarPresentation(string ProfileId, string Language,
    DeploymentRadarMaximums Maximums,
    int SummaryWithModifiersPalette, int SummaryWithoutModifiersPalette,
    int LargeCurrentPalette, int LargeComparisonMinimumPalette)
{
    // Both current locale profiles were inspected independently. The large
    // chart's final page assignment overrides its reversed constructor colors.
    // See docs/DEPLOYMENT_RADAR_PRESENTATION_PROFILE_SOURCE_20261018.md.
    public static DeploymentRadarPresentation ForLocale(string locale) => locale switch
    {
        "SC" => new("ce-sc-deployment-radar-20261018", "SC",
            new(50, 1500, 1500, 150, 1500), 15, 14, 15, 14),
        "TC" => new("ce-tc-deployment-radar-20261018", "TC",
            new(50, 1500, 1500, 150, 1500), 15, 14, 15, 14),
        _ => throw new NotSupportedException($"Deployment radar locale {locale} is not supported.")
    };
}
