using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    //188/180 are independent of the five214 targets. An absent entry is
    //unknown old-save knowledge; new(null) is the constructor's reset pointer.
    private readonly S14EventConditionCityPointer?[] _storyMoveCities = [new(null), new(null)];
    internal IReadOnlyList<S14EventConditionCityPointer?> CaptureStoryMoveCities()
        => Array.AsReadOnly(_storyMoveCities.ToArray());
    internal void RestoreStoryMoveCities(IReadOnlyList<S14EventConditionCityPointer?>? values)
    {
        if (values is not null && (values.Count != 2 || values.Any(city => city?.PhysicalSlot is < 0 or > 51)))
            throw new InvalidDataException("Story move-City pointer bank is invalid.");
        for (int index = 0; index < 2; index++) _storyMoveCities[index] = values?[index];
    }
    internal S14EventConditionCityPointer ReadStoryMoveCityPointer(int index)
    {
        if ((uint)index >= 2) throw new InvalidDataException("Story move-City pointer index is invalid.");
        return _storyMoveCities[index] ?? throw Missing("move-City pointer knowledge");
    }

    internal int ExecuteStoryCitySearch(int code, IReadOnlyList<int> arguments,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        if (arguments.Count != 8) throw Missing("City search arguments");
        if (code == 348)
        {
            //All five source pointer resolutions precede the search itself.
            var choices = arguments.Take(5).Select(selector => ResolveStoryCitySelector(selector)).ToArray();
            foreach (var city in choices)
                if (Known(Queries.ReadConditionBaseValidity(city), "empty search City validity") &&
                    Known(Queries.ReadConditionCityForceSlot(Known(city.PhysicalSlot, "empty search City pointer")), "empty search City Force") == 0)
                { _storyMoveCities[0] = new(city.PhysicalSlot); break; }
            return 1;
        }
        if (code is not (133 or 134 or 375)) throw Missing("City search opcode");
        S14EventConditionCityPointer? excluded = null;
        if (code == 134) excluded = ReadStoryMoveCityPointer(0);
        if (code == 375)
        {
            var force = ResolveForce(arguments[0], readTargetForce);
            if (force is null || !Known(Queries.ReadConditionForceValidity(force), "capital search Force validity")) return 0;
            if (arguments[6] == 1)
            {
                var ruler = PersonLookup(Known(Queries.ReadConditionForceRulerPersonId(force), "capital search ruler"));
                var home = Queries.ReadConditionPersonHomeBase(ruler) ?? throw Missing("capital search ruler Home");
                if (Known(Queries.ReadConditionBaseObjectId(home), "capital search Home identity") < 52)
                {
                    if (home.Kind != BaseKind.City) throw Missing("capital search Home City receiver");
                    excluded = new(home.PhysicalSlot);
                }
            }
        }
        var selected = SearchStoryMoveCity(arguments, code == 375, excluded, readTargetForce);
        bool valid = selected is not null && Known(Queries.ReadConditionBaseValidity(new(BaseKind.City, selected.PhysicalSlot)), "searched City validity");
        if (valid) _storyMoveCities[code == 134 ? 1 : 0] = selected;
        return code == 375 ? valid ? 1 : 0 : 1;
    }

    //3E0650: five explicit choices, then the Force's fixed City table.
    //No RNG, registry sort, route-distance substitute or reset-on-failure.
    private S14EventConditionCityPointer? SearchStoryMoveCity(IReadOnlyList<int> a, bool allowEmpty,
        S14EventConditionCityPointer? excluded, Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        var force = ResolveForce(a[0], readTargetForce);
        if (force is null || !Known(Queries.ReadConditionForceValidity(force), "search Force validity")) return null;
        for (int index = 1; index <= 5; index++)
        {
            var city = ResolveStoryCitySelector(a[index]);
            var pointer = new S14EventConditionCityPointer(city.PhysicalSlot);
            if (pointer == excluded || !Known(Queries.ReadConditionBaseValidity(city), "search explicit City validity")) continue;
            var owner = (Queries.ReadConditionBaseForce(city) ?? throw Missing("search explicit City Force pointer")).Value;
            if (owner == force || allowEmpty && Known(Queries.ReadConditionCityForceSlot(Known(city.PhysicalSlot, "search explicit City pointer")), "search explicit Force ordinal") == 0)
                return pointer;
        }
        S14EventConditionCityPointer? best = null;
        int bestTroops = -1;
        if (force.PhysicalSlot is < 1 or > 51) return null;
        for (int slot = 0; slot < 52; slot++)
        {
            var city = Queries.ReadConditionCityTableSlot(slot) ?? throw Missing("search City table pointer");
            int physical = Known(city.PhysicalSlot, "search City table receiver");
            if (Known(Queries.ReadConditionCityForceSlot(physical), "search City table Force") != force.PhysicalSlot || city == excluded) continue;
            int troops = Known(Queries.ReadConditionBaseTroops(new(BaseKind.City, physical)), "search City raw troops");
            if (troops > bestTroops || troops == bestTroops && best is not null &&
                Known(Queries.ReadConditionBaseValidity(new(BaseKind.City, best.PhysicalSlot)), "search prior City validity") &&
                physical < Known(best.PhysicalSlot, "search prior City ordinal"))
            { best = city; bestTroops = troops; }
        }
        return best;
    }
}
