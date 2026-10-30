namespace San14.Data.Formats;

public sealed partial class S14PlayerEventConditionRuntime
{
    private Func<int, S14EventConditionCityPointer>? _storyCityResolver;

    internal void BindStoryCityResolver(Func<int, S14EventConditionCityPointer> resolveCity)
        => _storyCityResolver = resolveCity;

    // Outer null means unknown; a pointer with null PhysicalSlot is the
    // constructor's known reset pointer. Selection only writes reached slots.
    private readonly S14EventConditionCityPointer?[] _storyTargetCities =
        [new(null), new(null), new(null), new(null), new(null)];

    internal IReadOnlyList<S14EventConditionCityPointer?> CaptureStoryTargetCities()
        => Array.AsReadOnly(_storyTargetCities.ToArray());

    internal void RestoreStoryTargetCities(IReadOnlyList<S14EventConditionCityPointer?>? cities)
    {
        if (cities is not null && (cities.Count != 5 || cities.Any(city => city?.PhysicalSlot is < 0 or > 51)))
            throw new InvalidDataException("Story selected City pointer bank is invalid.");
        for (int index = 0; index < 5; index++) _storyTargetCities[index] = cities?[index];
    }

    internal S14EventConditionCityPointer ReadStoryTargetCitySelection(int selector)
    {
        if ((uint)(selector + 9) >= 5) throw new InvalidDataException("Story selected City selector is out of range.");
        return _storyTargetCities[selector + 9]
            ?? throw new InvalidDataException("Story selected City pointer knowledge is unknown.");
    }

    internal int ExecuteStoryCityList(int code, IReadOnlyList<int> arguments,
        Func<int, S14EventConditionCityPointer> resolveCity,
        Func<int, S14EventConditionPersonReference> resolvePerson,
        Func<int, S14EventConditionForceReference?> resolveForce)
    {
        if (arguments.Count != 8) throw new InvalidDataException("Story City-list operands are incomplete.");
        switch (code)
        {
            case 203:
                //Clearing the candidate list does not reset selected targets.
                _selectedCities.Clear();
                return 1;
            case 204:
                foreach (int selector in arguments)
                {
                    var city = resolveCity(selector);
                    if (BaseValid(new(BaseKind.City, city.PhysicalSlot)) && !_selectedCities.Contains(city))
                        _selectedCities.Add(city);
                }
                return 1;
            case 205:
            {
                var force = resolveForce(arguments[0]);
                if (force is null || !Known(_queries.ReadConditionForceValidity(force), "City-list Force validity")) return 1;
                //2243A0 walks the actual City pointer table, including slot0,
                //rather than the registered-City chain. It adds every match.
                if (force.PhysicalSlot is < 1 or > 51) return 1;
                for (int slot = 0; slot < 52; slot++)
                {
                    var city = _queries.ReadConditionCityTableSlot(slot)
                        ?? throw new InvalidDataException("City-list table pointer is unknown.");
                    int physical = Known(city.PhysicalSlot, "City-list table receiver");
                    if (Known(_queries.ReadConditionCityForceSlot(physical), "City-list table Force getter") == force.PhysicalSlot)
                        _selectedCities.Add(city);
                }
                return 1;
            }
            case 206:
            {
                var person = resolvePerson(arguments[0]);
                if (!Known(_queries.ReadConditionPersonValidity(person), "City-list Home Person validity")) return 1;
                var home = _queries.ReadConditionPersonHomeBase(person)
                    ?? throw new InvalidDataException("City-list current Person Home is unknown.");
                if (!BaseValid(home) || Known(_queries.ReadConditionBaseObjectId(home), "City-list Home object identity") >= 52) return 1;
                if (home.Kind != BaseKind.City) throw new InvalidDataException("City-list Home identity is not a City receiver.");
                var city = new S14EventConditionCityPointer(home.PhysicalSlot);
                if (!_selectedCities.Contains(city)) _selectedCities.Add(city);
                return 1;
            }
            case 207:
            {
                var city = resolveCity(arguments[0]);
                if (BaseValid(new(BaseKind.City, city.PhysicalSlot)))
                    _selectedCities.RemoveAll(candidate => candidate == city);
                return 1;
            }
            case 208:
            {
                var force = resolveForce(arguments[0]);
                if (force is not null && Known(_queries.ReadConditionForceValidity(force), "City-list narrowed Force validity"))
                    _selectedCities.RemoveAll(city =>
                        (_queries.ReadConditionBaseForce(new(BaseKind.City, city.PhysicalSlot))
                            ?? throw new InvalidDataException("City-list current City Force pointer is unknown.")).Value != force);
                //An invalid target retains the list, but still returns its
                //current nonemptiness. Filtering is stable, including repeats.
                return _selectedCities.Count != 0 ? 1 : 0;
            }
            case 214:
                SelectStoryCitiesByTroops(arguments);
                return 1;
            default: throw new InvalidDataException($"Unsupported Story City-list instruction {code}.");
        }
    }

    private void SelectStoryCitiesByTroops(IReadOnlyList<int> arguments)
    {
        //3D2130 moves list nodes in place; strict insertion preserves equal
        //keys. A singleton does not read a comparator field.
        for (int index = 1; index < _selectedCities.Count; index++)
        {
            var city = _selectedCities[index];
            int prior = index - 1;
            while (prior >= 0 && CityTroopsKey(_selectedCities[prior]) < CityTroopsKey(city))
            { _selectedCities[prior + 1] = _selectedCities[prior]; prior--; }
            _selectedCities[prior + 1] = city;
        }
        int candidate = 0;
        for (int index = 0; index < 5; index++)
        {
            int role = arguments[index];
            if ((uint)(role - 1) >= 5) continue;
            if (candidate == _selectedCities.Count) return;
            _storyTargetCities[role - 1] = _selectedCities[candidate++];
        }
    }

    private uint CityTroopsKey(S14EventConditionCityPointer city)
    {
        int physical = Known(city.PhysicalSlot, "City-list sort receiver");
        int troops = Known(_queries.ReadConditionBaseTroops(new(BaseKind.City, physical)), "City-list current raw troops");
        return unchecked(((uint)troops << 8) | ((uint)(51 - physical) & 0xFF));
    }
}
