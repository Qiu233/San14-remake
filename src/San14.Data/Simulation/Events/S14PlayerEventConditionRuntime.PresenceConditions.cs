namespace San14.Data.Formats;

public sealed partial class S14PlayerEventConditionRuntime
{
    internal static bool IsStoryPresenceConditionCode(int code) => code is 70 or 71 or 76 or 283 or 297 or 376 or 440;

    //Only a read-only evaluation that actually consumes dynamic selectors
    //needs this invocation. It shares the already-bound current owner and
    //mature selector/dialogue producers, and captures no second world.
    private CampaignEventExecutionContext? _presenceConditionInvocation;
    private CampaignEventExecutionContext PresenceConditionInvocation
    {
        get
        {
            if (_presenceConditionInvocation is not null) return _presenceConditionInvocation;
            var inputs = (_queries as IS14StoryPresenceConditionQueries
                ?? throw StoryPresenceConditions.Missing("current_query_owner")).ReadStoryPresenceInvocationInputs();
            var invocation = new CampaignEventExecutionContext(inputs);
            invocation.BindStoryTargetCityReader(ReadStoryTargetCitySelection);
            return _presenceConditionInvocation = invocation;
        }
    }

    internal bool CompareStoryCitySelectionCount(IReadOnlyList<int> arguments)
    {
        if (arguments.Count != 8) throw StoryPresenceConditions.Missing("City_count_operands");
        return StoryPresenceConditions.Compare(_selectedCities.Count, arguments[0], arguments[1]);
    }

    internal bool EvaluateStoryPresenceCondition(S14ConditionContext context)
    {
        var arguments = context.Operation.RawArguments;
        if (arguments.Count != 8) throw StoryPresenceConditions.Missing("operands");
        switch (context.Operation.Code)
        {
            case 70:
                foreach (int selector in arguments.Take(5))
                    if (selector != 0 && !StoryPresenceConditions.PersonDisappearing(_queries, PresencePerson(selector))) return false;
                return true;
            case 71:
            {
                var first = PresencePerson(arguments[0]);
                var second = PresencePerson(arguments[1]);
                return StoryPresenceConditions.SameForce(_queries, first, second);
            }
            case 76:
                //3D2E50 clears all five actor pointers in a fresh condition
                //invocation. Selected Persons are an independent list.
                return StoryPresenceConditions.SpeakerCount(_queries, PresenceSpeakers(), arguments[0], arguments[1]);
            case 283:
                return StoryPresenceConditions.Endurance(_queries,
                    arguments[0] >= 0 ? ResolveBase(arguments[0]) : PresenceConditionInvocation.ResolveResultBase(arguments[0]),
                    arguments[1], arguments[2]);
            case 297:
            {
                var first = PresencePerson(arguments[0]);
                var second = PresencePerson(arguments[2]);
                return StoryPresenceConditions.Likes(_queries, first, second, arguments[1]);
            }
            case 376:
            {
                var item = StoryPresenceConditions.ValidItem(_queries, arguments[0]);
                return item is not null && StoryPresenceConditions.ItemOwner(_queries, item,
                    PresencePerson(arguments[1]), arguments[2]);
            }
            case 440: return CompareStoryCitySelectionCount(arguments);
            default: throw StoryPresenceConditions.Missing("leaf");
        }
    }

    private S14EventConditionPersonReference? PresencePerson(int selector)
    {
        if (_storyActorResolver is not null) return _storyActorResolver(selector);
        if (selector < 0) return PresenceConditionInvocation.ResolvePerson(selector, ReadMessageTargetForce);
        return RawPersonPointer(selector) ?? RawPersonPointer(0);
    }

    private IEnumerable<S14EventConditionPersonReference?> PresenceSpeakers()
    {
        for (int slot = -14; slot <= -10; slot++)
            yield return _storyActorResolver is not null ? _storyActorResolver(slot)
                : _presenceConditionInvocation?.Actors.GetValueOrDefault(slot);
    }
}
