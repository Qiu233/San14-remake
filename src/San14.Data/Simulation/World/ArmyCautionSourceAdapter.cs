using San14.Core;

namespace San14.Data;

/// <summary>Projects owned caution facts into the operation-local source API.
/// Legacy receiver numbers do not identify caution targets in saved state.</summary>
internal static class ArmyCautionSourceAdapter
{
    internal static void Restore(SourceWorldContext current, ArmyCautionsState cautions)
    {
        for (int group = 0; group < 501; group++)
            current.SetRegistryTablePointer(RegistryKind.CautionTroops, group, group);
        for (int relation = 0; relation < 1001; relation++)
            current.SetRegistryTablePointer(RegistryKind.Table81B50, relation, relation);
        InstallEligibility(current, cautions);
    }

    internal static void InstallEligibility(SourceWorldContext current, ArmyCautionsState cautions) =>
        current.SetCautionEligibilityProjection(
            group => ArmyCautionRules.IsGroupRegisteredEligibility(cautions.GetGroupLabel(group)),
            relation => ArmyCautionRules.IsRelationRegisteredEligibility(cautions.GetRelation(relation)));
}
