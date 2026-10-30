using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    internal void RefreshResultInputs(CampaignEventExecutionInputs currentInputs) => inputs = currentInputs;

    // Results share the presentation invocation's selected cast. These are
    // saved identities, not the actors currently visible on screen.
    internal void RestoreResultActors(IReadOnlyList<int?>? actorIds)
    {
        if (actorIds is null) return;
        for (int index = 0; index < actorIds.Count; index++)
            if (actorIds[index] is int id && ReadPersonPointer(id) is { } person)
                _actors[-10 - index] = person;
    }

    internal S14EventConditionForceReference ResolveResultForce(int selector,
        Func<int, S14EventConditionForceReference?> readTargetForce) =>
        ResolveForce(selector, readTargetForce) ?? throw Missing("result Force0 pointer");

    internal BaseReference ResolveResultBase(int selector) => ResolveBaseSelector(selector);
}
