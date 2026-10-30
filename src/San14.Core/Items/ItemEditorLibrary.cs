using System.Collections.Immutable;

namespace San14.Core;

/// <summary>The seven independent values editable by the title item editor.
/// Holder, transfer restrictions and discovery state belong to a new game.</summary>
public sealed record ItemEditorValues(int Kind, int AbilityKind, int AbilityValue,
    int EffectKind, int EffectValue, int Worth, int ImageIndex)
{
    public void Validate()
    {
        if (Kind is < 0 or > 26 || AbilityKind is < 0 or > 5 || AbilityValue is < 0 or > 10 ||
            EffectKind is < 0 or > 2 || EffectValue is < 0 or > 10 || Worth is < 0 or > 50 ||
            ImageIndex is < 0 or > 456)
            throw new InvalidDataException("Title item values exceed their original field ranges.");
    }
}

public sealed record HistoricalItemEdit(int Id, ItemEditorValues Values);
public sealed record CreatedItem(int Id, string Name, string Description, ItemEditorValues Values)
{
    public void Validate(bool requireName = false)
    {
        if (Id is < 152 or > 201 || Name is null || Description is null ||
            Name.Length > 6 || Description.Length > 60 || Name.Contains('\0') || Description.Contains('\0') ||
            (requireName && Name.Length == 0))
            throw new InvalidDataException("Created item identity or text exceeds its original slot.");
        Values.Validate();
    }
}

/// <summary>A detached, versioned title library. Adoption is deliberately not
/// stored here: the original clears it for every fresh game.</summary>
public sealed record ItemEditorLibrary(ImmutableArray<HistoricalItemEdit> Historical,
    ImmutableArray<CreatedItem> Created)
{
    public static ItemEditorLibrary Empty { get; } = new([], []);
    [System.Text.Json.Serialization.JsonIgnore]
    public int? FirstFreeCreatedId => Enumerable.Range(152, 50)
        .Cast<int?>().FirstOrDefault(id => Created.All(item => item.Id != id));
    public HistoricalItemEdit? FindHistorical(int id) => Historical.FirstOrDefault(item => item.Id == id);
    public CreatedItem? FindCreated(int id) => Created.FirstOrDefault(item => item.Id == id);
    public ItemEditorLibrary SetHistorical(HistoricalItemEdit item)
    {
        var next = this with { Historical = Historical.Where(value => value.Id != item.Id)
            .Append(item).OrderBy(value => value.Id).ToImmutableArray() };
        next.Validate(); return next;
    }
    public ItemEditorLibrary RestoreHistorical(IEnumerable<int> ids)
    {
        var remove = ids.ToHashSet();
        return this with { Historical = Historical.Where(item => !remove.Contains(item.Id)).ToImmutableArray() };
    }
    public ItemEditorLibrary SetCreated(CreatedItem item)
    {
        item.Validate();
        var next = this with { Created = Created.Where(value => value.Id != item.Id)
            .Append(item).OrderBy(value => value.Id).ToImmutableArray() };
        next.Validate(); return next;
    }
    public ItemEditorLibrary DeleteCreated(IEnumerable<int> ids)
    {
        var remove = ids.ToHashSet();
        // Original 21C4D0 compacts the remaining title records after deleting
        // the whole selected set. Running campaigns own independent copies.
        return this with { Created = Created.Where(item => !remove.Contains(item.Id)).OrderBy(item => item.Id)
            .Select((item, index) => item with { Id = 152 + index }).ToImmutableArray() };
    }
    public void Validate()
    {
        if (Historical.IsDefault || Created.IsDefault || Historical.Length > 151 || Created.Length > 50 ||
            Historical.Any(item => item is null || item.Id is < 1 or > 151 || item.Values is null) ||
            Created.Any(item => item is null || item.Values is null) ||
            Historical.Select(item => item.Id).Distinct().Count() != Historical.Length ||
            Created.Select(item => item.Id).Distinct().Count() != Created.Length)
            throw new InvalidDataException("Title item archive identities are invalid.");
        foreach (var item in Historical) item.Values.Validate();
        foreach (var item in Created) item.Validate();
    }
}
