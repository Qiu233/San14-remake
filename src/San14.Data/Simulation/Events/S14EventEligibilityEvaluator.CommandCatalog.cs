namespace San14.Data.Formats;

public static partial class S14EventEligibilityEvaluator
{
    // The manual list uses precisely the ordinary scanner's source filters.
    // Evaluating each admitted row separately lets a UI expose an unknown row
    // without replacing its result or hiding other, fully evaluated events.
    internal static IReadOnlyList<S14EventRecord> ReadCommandCandidates(S14EventDocument document,
        IS14EventEligibilityState state, int userIndex)
    {
        short year = Known(state.ReadCurrentYear(), "current year");
        return document.Events.Where(row => PassesFilter(row, year, new(0, 0, userIndex), state)).ToArray();
    }
}
