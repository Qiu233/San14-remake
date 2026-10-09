using San14.Core;

namespace San14.Data;

/// <summary>Provenance for project rule definitions; original resource hashes remain separate.</summary>
public static class SimulationRuleSources
{
    public static SourceProvenance Provenance(string resourceUri, string locale) =>
        new(resourceUri, 0, "")
        {
            ResourceUri = resourceUri,
            RuleVersion = SimulationRuleIdentity.ForLocale(locale)
        };
}
