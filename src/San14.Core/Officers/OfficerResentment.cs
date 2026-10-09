namespace San14.Core;

/// <summary>A remembered grievance and its strength are independent knowledge.
/// Zero is a known target identity, not an absent observation.</summary>
public sealed record OfficerResentment
{
    public int? TargetOfficerId { get; }
    public int? Intensity { get; }
    public OfficerResentment(int? targetOfficerId, int? intensity)
    {
        if (targetOfficerId is < 0 || intensity is < 0)
            throw new InvalidDataException("Officer resentment identities and strength must be nonnegative.");
        TargetOfficerId = targetOfficerId; Intensity = intensity;
    }
}

/// <summary>Observed disposition used by allegiance loss and the diplomatic
/// consequence of releasing a ruler. It is independent of current loyalty.
/// The sign is meaningful; no character-attribute label is inferred.</summary>
public sealed record OfficerAllegianceDisposition(int Value);

public sealed partial record OfficerState
{
    public OfficerResentment? Resentment { get; init; }
    public OfficerAllegianceDisposition? AllegianceDisposition { get; init; }
}
