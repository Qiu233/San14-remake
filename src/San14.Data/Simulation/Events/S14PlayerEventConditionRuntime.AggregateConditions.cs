namespace San14.Data.Formats;

public sealed partial class S14PlayerEventConditionRuntime
{
    internal static bool IsStoryAggregateConditionCode(int code) => code is 31 or 43 or 80 or 432 or 433;

    internal StoryAggregateConditionResult EvaluateStoryAggregateCondition(S14ConditionContext context)
    {
        var arguments = context.Operation.RawArguments;
        if (arguments.Count != 8) throw new InvalidDataException("Aggregate condition operands are incomplete.");
        int code = context.Operation.Code;
        if (code == 80)
            return new(Compare(_selectedPersons.Count, arguments[0], arguments[1]));
        if (code is not (31 or 43 or 432 or 433))
            throw new InvalidDataException("This leaf is not an aggregate story condition.");

        //The source rejects these operands before touching the world table or
        //the manager. An invalid comparison must not invent a kind write.
        if (code is 432 or 433 &&
            (arguments[0] < 1 || arguments[0] > (code == 432 ? 17 : 6) ||
             arguments[1] is < 1 or > 3 || arguments[3] is < 1 or > 3 ||
             arguments[1] == arguments[3]))
            return new(false);

        ForceReference? force = null;
        if (code is 31 or 43)
        {
            var resolved = ResolveRulerForce(arguments[0]);
            force = new(resolved?.PhysicalSlot);
        }
        return (_queries as IS14StoryAggregateConditionQueries
            ?? throw new InvalidDataException("Current aggregate condition owner is unknown."))
            .EvaluateStoryAggregateConditionCurrent(code, arguments, force);
    }
}
