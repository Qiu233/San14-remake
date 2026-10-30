using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>An application-owned registered officer, independent of any
/// campaign's current Person. The original editor has 150 numbered slots.</summary>
public sealed record RegisteredOfficer(int Id)
{
    public string FamilyName { get; init; } = "";
    public string GivenName { get; init; } = "";
    public string CourtesyName { get; init; } = "";
    public string FamilyNameSortKey { get; init; } = "";
    public string GivenNameSortKey { get; init; } = "";
    public string CourtesyNameSortKey { get; init; } = "";
    public int PortraitId { get; init; }
    public byte Sex { get; init; }
    public ushort BirthYear { get; init; }
    public ushort DeathYear { get; init; }
    public ushort FamilyId { get; init; }
    public ushort FatherId { get; init; }
    public ushort MotherId { get; init; }
    public ushort SpouseId { get; init; }
    public ushort SwornBrotherId { get; init; }
    public ushort CompatibilityPersonId { get; init; }
    public byte Compatibility { get; init; }
    public ImmutableArray<int> LikePersonIds { get; init; }
    public ImmutableArray<int> DislikePersonIds { get; init; }
    public byte Leadership { get; init; }
    public byte Strength { get; init; }
    public byte Intelligence { get; init; }
    public byte Politics { get; init; }
    public byte Charm { get; init; }
    public ImmutableArray<int> TraitIds { get; init; }
    public ImmutableArray<int> FormationIds { get; init; }
    public ImmutableArray<int> TacticIds { get; init; }
    public byte DoctrineId { get; init; }
    public byte PolicyId { get; init; }
    public byte PolicyBaseCost { get; init; }
    public byte VoiceType { get; init; }
    public string Description { get; init; } = "";
    [JsonIgnore] public string Name => FamilyName + GivenName;

    public void Validate(bool requireName = true)
    {
        if (Id is < 3001 or > 3150 || FamilyName is null || GivenName is null || CourtesyName is null ||
            FamilyName.Length > 2 || GivenName.Length > 2 || CourtesyName.Length > 2 ||
            requireName && (FamilyName.Length == 0 || GivenName.Length == 0) ||
            FamilyName.Contains('\0') || GivenName.Contains('\0') || CourtesyName.Contains('\0') ||
            FamilyNameSortKey is null || GivenNameSortKey is null || CourtesyNameSortKey is null ||
            FamilyNameSortKey.Length > 6 || GivenNameSortKey.Length > 6 || CourtesyNameSortKey.Length > 6 ||
            PortraitId is < 0 or > ushort.MaxValue || Sex > 1 || BirthYear > 350 || DeathYear > 350 ||
            FatherId > 6000 || MotherId > 6000 || SpouseId > 6000 || SwornBrotherId > 6000 ||
            CompatibilityPersonId > 6000 || Compatibility > 149 ||
            Leadership > 100 || Strength > 100 || Intelligence > 100 || Politics > 100 || Charm > 100 ||
            DoctrineId > 10 || PolicyId > 120 || PolicyBaseCost > 10 || VoiceType > 16 ||
            Description is null || Description.Length > 500 || Description.Contains('\0'))
            throw new InvalidDataException("Registered officer fields are outside the editor's supported format.");
        Check(LikePersonIds, 8, 6000); Check(DislikePersonIds, 8, 6000);
        Check(TraitIds, 9, ushort.MaxValue); Check(TacticIds, 10, byte.MaxValue);
        if (FormationIds.IsDefault || FormationIds.Length > 21 || FormationIds.Any(id => id is < 0 or > 20) ||
            FormationIds.Distinct().Count() != FormationIds.Length)
            throw new InvalidDataException("Registered officer formation selection is invalid.");
        static void Check(ImmutableArray<int> values, int count, int maximum)
        {
            if (values.IsDefault || values.Length != count || values.Any(id => id < 0 || id > maximum))
                throw new InvalidDataException("Registered officer slot values are invalid.");
        }
    }
}

public sealed record RegisteredOfficerLibrary(ImmutableArray<RegisteredOfficer> Officers)
{
    public static RegisteredOfficerLibrary Empty => new([]);
    [JsonIgnore]
    public int? FirstFreeId => Enumerable.Range(3001, 150).Select(id => (int?)id)
        .FirstOrDefault(id => !Officers.Any(officer => officer.Id == id));
    public RegisteredOfficer? Find(int id) => Officers.FirstOrDefault(officer => officer.Id == id);
    public void Validate()
    {
        if (Officers.IsDefault || Officers.Length > 150 || Officers.Any(officer => officer is null) ||
            Officers.Select(officer => officer.Id).Distinct().Count() != Officers.Length)
            throw new InvalidDataException("Registered officer library is invalid.");
        foreach (var officer in Officers) officer.Validate(requireName: false);
    }
    public RegisteredOfficerLibrary Set(RegisteredOfficer officer)
    {
        officer.Validate();
        return new([.. Officers.Where(value => value.Id != officer.Id).Append(officer).OrderBy(value => value.Id)]);
    }
    /// <summary>Deletes the slot and all editor-library references atomically.
    /// Existing campaigns retain their own independent Person state.</summary>
    public RegisteredOfficerLibrary Delete(int id)
    {
        if (id is < 3001 or > 3150) throw new ArgumentOutOfRangeException(nameof(id));
        return new([.. Officers.Where(officer => officer.Id != id).Select(officer => officer with
        {
            FatherId = Clear(officer.FatherId), MotherId = Clear(officer.MotherId),
            SpouseId = Clear(officer.SpouseId), SwornBrotherId = Clear(officer.SwornBrotherId),
            CompatibilityPersonId = Clear(officer.CompatibilityPersonId),
            LikePersonIds = [.. officer.LikePersonIds.Select(value => value == id ? 0 : value)],
            DislikePersonIds = [.. officer.DislikePersonIds.Select(value => value == id ? 0 : value)]
        })]);
        ushort Clear(ushort value) => value == id ? (ushort)0 : value;
    }
}
