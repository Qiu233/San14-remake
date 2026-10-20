using San14.Core;

namespace San14.Data;

/// <summary>An unsubmitted UI composition row. The request owns actions and
/// waypoints; naval formation remains independent of the selected land formation.</summary>
public sealed record DeploymentDraftRow(DeploymentUnitRequest Request, int NavalFormationId);

/// <summary>Unknown reached input rejects the entire replacement. This query
/// neither reserves resources nor changes the saved world.</summary>
public sealed record DeploymentDraftQuery(IReadOnlyList<DeploymentDraftRow>? Rows,
    int? FocusedOfficerId, string? UnresolvedInput)
{
    public bool Available => Rows is not null && UnresolvedInput is null;
}
