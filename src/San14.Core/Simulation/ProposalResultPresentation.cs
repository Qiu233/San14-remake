namespace San14.Core;

public sealed record ProposalResultRow(string NameAtSource, string BeforeText, string AfterText,
    TalentOfficerPresentationContext? OfficerAtSource = null);
public sealed record ProposalOfficerDetailRequest(int OfficerId);
public sealed record ProposalResultTableRequest(int ForceId, IReadOnlyList<ProposalResultRow> Rows)
{
    internal ProposalResultTableRequest Copy() => this with { Rows = Rows.ToArray() };
    internal void Validate()
    {
        if (ForceId is < 1 or > 51 || Rows is null || Rows.Count > 12 || Rows.Any(row =>
            row is null || row.NameAtSource is null || row.BeforeText is null || row.AfterText is null ||
            row.OfficerAtSource is { } officer && (officer.OfficerId is < 0 or > 6000 || officer.NameAtSource is null)))
            throw new InvalidDataException("Proposal result table is incomplete or invalid.");
    }
}
