using San14.Core;

namespace San14.Data.Formats;

public sealed partial class S14PlayerEventConditionRuntime
{
    internal S14EventConditionPersonReference? ReadCommandTextPerson(int selector)
        => RawPersonPointer(selector) ?? RawPersonPointer(0);
    internal S14EventConditionCityPointer ReadCommandTextCity(int selector) => ResolveCityPointer(selector);
    internal BaseReference ReadCommandTextBase(int selector) => ResolveBase(selector);
    internal S14EventConditionForceReference? ReadCommandTextForce(int selector) => ResolveRulerForce(selector);
}
