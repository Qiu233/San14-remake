using System.Collections.Immutable;
using San14.Core;
using San14.Data;

public partial class Main
{
    private InformationDetailSelectionContext PrepareDetailSelection(ArmyCampaignRuleAdapter rules,
        GameState world, InformationListKind kind, int selected, IReadOnlyList<int>? provided) =>
        new(rules.QueryInformationDetailSelection(world, kind, provided ?? [selected], _session?.PlayerForceId), CurrentListScrollSpeed);

    private static IReadOnlyList<int> DetailSelectionIds(InformationList publication, InformationListKind kind) =>
        publication.Rows.Where(row => row.Detail?.Kind == kind).Select(row => row.Detail!.Id).ToArray();

    private static InformationList ApplyOfficerDetailReturn(InformationList publication, OfficerDetailSelectionReturn returned)
    {
        // Slots belong to the caller's retained rows. Reusing their actual
        // objects preserves duplicate identities and parent SourceIndex maps.
        var rows = publication.Rows.GroupBy(row => row.Detail?.Id)
            .ToDictionary(group => group.Key ?? throw new InvalidOperationException("A Person row lacks its detail identity."),
                group => new Queue<InformationRow>(group));
        return publication with { Rows = returned.OrderedIds.Select(id => rows[id].Dequeue()).ToImmutableArray() };
    }

}
