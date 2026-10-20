namespace San14.Data;

public sealed partial class SourceWorldContext
{
    // Only relation evaluation's temporary primary lists. Existing descriptor
    // identities, payload order, next descriptor and next cursor all survive.
    internal sealed record AreaPreviewCheckpoint(CurrentOrderedLists Lists,
        Dictionary<int, (bool Present, ushort? Person)> Areas,
        Dictionary<int, (bool Present, PersonAreaAssignmentVector? Vector)> Vectors,
        Dictionary<int, byte?>? CityDistricts);
    internal AreaPreviewCheckpoint CaptureAreaPreview(IReadOnlyList<int> areas, IEnumerable<int> people,
        bool captureCityDistricts = false) => new(
        _orderedLists.Copy(), areas.Distinct().ToDictionary(a => a, a =>
            (_assignmentArea36.ContainsKey(a), _assignmentArea36.GetValueOrDefault(a))),
        people.Distinct().ToDictionary(p => p, p =>
            (_assignmentPersonAreas.ContainsKey(p), _assignmentPersonAreas.GetValueOrDefault(p))),
        captureCityDistricts ? new(_cityDistricts) : null);
    internal void RestoreAreaPreview(AreaPreviewCheckpoint checkpoint)
    {
        _orderedLists = checkpoint.Lists;
        foreach (var (area, before) in checkpoint.Areas)
            if (before.Present) _assignmentArea36[area] = before.Person; else _assignmentArea36.Remove(area);
        foreach (var (person, before) in checkpoint.Vectors)
            if (before.Present) _assignmentPersonAreas[person] = before.Vector; else _assignmentPersonAreas.Remove(person);
        if (checkpoint.CityDistricts is { } districts)
        {
            _cityDistricts.Clear();
            foreach (var (city, district) in districts) _cityDistricts.Add(city, district);
        }
    }
}
