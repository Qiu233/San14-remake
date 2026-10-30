using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    // City and Base selectors are separate original domains. In particular,
    // City -4 is the Emperor's receiving City; Base -4 is a selected pointer.
    internal BaseReference ResolveStoryResourceBase(int code, int selector)
    {
        if (code is not (273 or 274 or 277 or 278)) return ResolveResultBase(selector);
        return ResolveStoryCitySelector(selector);
    }

    // Ordinary City3D9270 is also used by city expansion and explicit result
    // registration. Keep its current/Emperor/reset pointer domain in one place.
    internal BaseReference ResolveStoryCitySelector(int selector)
    {
        if (selector == -1) return ResolveResultBase(-1);
        S14EventConditionCityPointer city;
        if (selector == -4)
        {
            var emperor = PersonLookup(Known(Queries.ReadConditionEmperorPersonId(), "resource emperor identity"));
            int physical = 0;
            if (Known(Queries.ReadConditionPersonValidity(emperor), "resource emperor validity"))
            {
                var home = Queries.ReadConditionPersonLocationBase(emperor) ?? throw Missing("resource emperor whereabouts");
                if (home.Kind == BaseKind.City && home.PhysicalSlot is int location) physical = location;
            }
            city = Queries.ReadConditionCityTableSlot(physical < 52 ? physical : 0)
                ?? throw Missing("resource emperor City pointer");
        }
        else if (selector is >= -9 and <= -5)
            city = ReadStoryTargetCityPointer(selector);
        else if (selector is -3 or -2)
        {
            city = ReadStoryMoveCityPointer(selector == -3 ? 0 : 1);
        }
        else if (selector < 0) throw Missing("resource City selector");
        else city = Queries.ReadConditionCityTableSlot(selector < 52 ? selector : 0)
            ?? throw Missing("resource City pointer");
        if (city.PhysicalSlot is null)
            city = Queries.ReadConditionCityTableSlot(0) ?? throw Missing("resource City0 pointer");
        return new(BaseKind.City, city.PhysicalSlot);
    }
}
