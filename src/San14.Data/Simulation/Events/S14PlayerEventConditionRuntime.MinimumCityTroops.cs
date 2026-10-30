namespace San14.Data.Formats;

public sealed partial class S14PlayerEventConditionRuntime
{
    internal int KeepStoryCityWithFewestTroops()
    {
        if (_selectedCities.Count < 2) return 1; //A singleton does not call a comparator.
        var selected = _selectedCities[0];
        for (int index = 1; index < _selectedCities.Count; index++)
        {
            var candidate = _selectedCities[index];
            int candidateTroops = Troops(candidate), selectedTroops = Troops(selected);
            if (candidateTroops < selectedTroops || candidateTroops == selectedTroops &&
                Physical(candidate) < Physical(selected)) selected = candidate;
        }
        //3D1AE0 signed troops ascending, physical City identity ascending on
        //ties, then clear and append the first pointer. Exact ties keep first.
        _selectedCities.Clear();
        _selectedCities.Add(selected);
        return 1;

        int Physical(S14EventConditionCityPointer city) => Known(city.PhysicalSlot, "minimum-troops City identity");
        int Troops(S14EventConditionCityPointer city) => Known(_queries.ReadConditionBaseTroops(
            new(BaseKind.City, Physical(city))), "minimum-troops City raw3C");
    }
}
