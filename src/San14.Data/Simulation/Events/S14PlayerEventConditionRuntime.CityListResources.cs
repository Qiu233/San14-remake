namespace San14.Data.Formats;

public sealed partial class S14PlayerEventConditionRuntime
{
    // A read-only borrow of the existing invocation owner. The saved cursor
    // already captures/restores its order; no final World scan recreates it.
    internal IReadOnlyList<S14EventConditionCityPointer> ReadStoryCitySelections() => _selectedCities;
}
