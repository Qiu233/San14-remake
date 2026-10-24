using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>7C65D0 over an already assembled source ETA list. etaPath
    /// has already omitted the final point of each segment. This does not
    /// search, remove an endpoint again, group rows, or modify world/selection.</summary>
    public IReadOnlyList<DeploymentDraftMarchRow> QueryDeploymentDraftEta(GameState state,
        IReadOnlyList<DeploymentDraftRow> rows, IReadOnlyList<int> etaPath) =>
        ReadDeploymentDraftEta(state, rows, etaPath, () => CaptureDeploymentDraftMarchCurrent(state));

    private IReadOnlyList<DeploymentDraftMarchRow> ReadDeploymentDraftEta(GameState state,
        IReadOnlyList<DeploymentDraftRow> rows, IReadOnlyList<int> etaPath, Func<SourceWorldContext> readCurrent)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(etaPath);
        // The source short-list leaf runs before constructing a preview or
        // reading its commander, formation, location or current rule owners.
        if (etaPath.Count < 2) return rows.Select(row => new DeploymentDraftMarchRow(row.Request.OfficerId, 0, null)).ToArray();
        if (rows.Count == 0) return Array.Empty<DeploymentDraftMarchRow>();
        SourceWorldContext current;
        try
        {
            if (etaPath.Any(hex => hex is < 0 or >= 48400)) throw DraftMarchMissing("eta_path_identity");
            current = readCurrent();
        }
        catch (InvalidDataException error)
        { return rows.Select(row => new DeploymentDraftMarchRow(row.Request.OfficerId, null, error.Message)).ToArray(); }
        var result = new List<DeploymentDraftMarchRow>(rows.Count);
        foreach (var row in rows)
        {
            int? days = null; string? missing = null;
            try
            {
                var input = ReadDeploymentDraftMarchInputs(state, current, row);
                days = DeploymentDraftMarchSource.Eta(state, current,
                    new DeploymentDraftMarchSource.Motion(state, current, input), etaPath);
            }
            catch (InvalidDataException error) { missing = error.Message; }
            result.Add(new(row.Request.OfficerId, days, missing));
        }
        return result.AsReadOnly();
    }
}
