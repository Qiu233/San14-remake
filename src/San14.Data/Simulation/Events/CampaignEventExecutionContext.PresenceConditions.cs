using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    internal int ExecuteStoryPresenceCondition(int code, IReadOnlyList<int> arguments,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        if (arguments.Count != 8) throw Missing("presence condition operands");
        bool result;
        switch (code)
        {
            case 70:
                //Only the first five nonzero operands are consumed. A failed
                //receiver stops before resolving the following operand.
                foreach (int selector in arguments.Take(5))
                    if (selector != 0 && !StoryPresenceConditions.PersonDisappearing(Queries, ResolvePerson(selector, readTargetForce)))
                        return 0;
                return 1;
            case 71:
            {
                var first = ResolvePerson(arguments[0], readTargetForce);
                var second = ResolvePerson(arguments[1], readTargetForce);
                result = StoryPresenceConditions.SameForce(Queries, first, second);
                break;
            }
            case 76:
                result = StoryPresenceConditions.SpeakerCount(Queries,
                    Enumerable.Range(-14, 5).Select(slot => _actors.GetValueOrDefault(slot)), arguments[0], arguments[1]);
                break;
            case 283:
                result = StoryPresenceConditions.Endurance(Queries, ResolveBaseSelector(arguments[0]), arguments[1], arguments[2]);
                break;
            case 297:
            {
                var first = ResolvePerson(arguments[0], readTargetForce);
                var second = ResolvePerson(arguments[2], readTargetForce);
                result = StoryPresenceConditions.Likes(Queries, first, second, arguments[1]);
                break;
            }
            case 376:
            {
                var item = StoryPresenceConditions.ValidItem(Queries, arguments[0]);
                //An invalid Item returns before the Person selector, including
                //its lazy dialogue producer, is consumed.
                if (item is null) return 0;
                result = StoryPresenceConditions.ItemOwner(Queries, item,
                    ResolvePerson(arguments[1], readTargetForce), arguments[2]);
                break;
            }
            default: throw Missing("presence condition leaf");
        }
        return result ? 1 : 0;
    }
}
