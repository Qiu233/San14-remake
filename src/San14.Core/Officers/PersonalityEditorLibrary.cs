using System.Collections.Immutable;

namespace San14.Core;

/// <summary>A detached title definition. The second option parameter is retained
/// by the original exchange format even though its current application does not copy it.</summary>
public sealed record CreatedPersonality(int Id, string Name, int ConditionKind, uint ConditionValue,
    int OptionKind, uint OptionValue, int Effect1Code, int Effect1Value, int Effect2Code, int Effect2Value)
{
    public void Validate(bool requireComplete = false)
    {
        if (Id is < 256 or > 355 || Name is null || Name.Length > 5 || Name.Contains('\0') ||
            ConditionKind is < 0 or > 100 || OptionKind is < 0 or > 100 ||
            Effect1Code is < 0 or > 126 || Effect2Code is < 0 or > 126 ||
            Effect1Value is < -100 or > 2000 || Effect2Value is < -100 or > 2000 ||
            (requireComplete && (Name.Length == 0 || Effect1Code == 0)))
            throw new InvalidDataException("Created personality fields exceed their source representation.");
    }
}

public sealed record PersonalityEditorLibrary(ImmutableArray<CreatedPersonality> Created)
{
    public static PersonalityEditorLibrary Empty { get; } = new([]);
    [System.Text.Json.Serialization.JsonIgnore]
    public int? FirstFreeId => Enumerable.Range(256, 100).Cast<int?>()
        .FirstOrDefault(id => Created.All(value => value.Id != id));
    public CreatedPersonality? Find(int id) => Created.FirstOrDefault(value => value.Id == id);
    public PersonalityEditorLibrary Set(CreatedPersonality value)
    {
        value.Validate();
        var next = new PersonalityEditorLibrary(Created.Where(row => row.Id != value.Id)
            .Append(value).OrderBy(row => row.Id).ToImmutableArray());
        next.Validate(); return next;
    }
    public PersonalityEditorLibrary Delete(IEnumerable<int> ids)
    {
        var removed = ids.ToHashSet();
        return new(Created.Where(value => !removed.Contains(value.Id)).ToImmutableArray());
    }
    public void Validate()
    {
        if (Created.IsDefault || Created.Length > 100 || Created.Any(value => value is null) ||
            Created.Select(value => value.Id).Distinct().Count() != Created.Length)
            throw new InvalidDataException("Created personality slot identities are invalid.");
        foreach (var value in Created) value.Validate();
    }
}
