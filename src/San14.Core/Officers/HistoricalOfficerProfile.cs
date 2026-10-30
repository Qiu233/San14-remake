using System.Collections.Immutable;

namespace San14.Core;

/// <summary>Only records differing from their original resource values are
/// retained. Original comparison and editor eligibility belong to the catalogue.</summary>
public sealed record HistoricalOfficerProfile(ImmutableArray<HistoricalOfficerEdit> Overrides)
{
    public static HistoricalOfficerProfile Empty => new([]);
    public HistoricalOfficerEdit? Find(int id) => Overrides.FirstOrDefault(officer => officer.Id == id);

    public void Validate()
    {
        if (Overrides.IsDefault || Overrides.Length > 1088 || Overrides.Any(officer => officer is null) ||
            Overrides.Select(officer => officer.Id).Distinct().Count() != Overrides.Length)
            throw new InvalidDataException("Historical officer profile is invalid.");
        foreach (var officer in Overrides) officer.Validate();
    }

    public HistoricalOfficerProfile Set(HistoricalOfficerEdit officer)
    {
        ArgumentNullException.ThrowIfNull(officer);
        officer.Validate();
        return new([.. Overrides.Where(value => value.Id != officer.Id).Append(officer).OrderBy(value => value.Id)]);
    }

    /// <summary>Restores this officer's original values by removing its override.
    /// The historical person and other officers' relationship identities remain.</summary>
    public HistoricalOfficerProfile Remove(int id)
    {
        if (!HistoricalOfficerEdit.IsSupportedId(id)) throw new ArgumentOutOfRangeException(nameof(id));
        return new([.. Overrides.Where(officer => officer.Id != id)]);
    }
}
