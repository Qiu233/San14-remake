namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    //1A1470 rebuilds this already-saved warning on view creation. There are
    //no formatter arguments or presentation randoms in that resource label.
    internal string ResolveNormalTurnFoodWarningLabel()
    {
        _prefix = "normal-turn.food-warning.label";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(InformationExplanationArguments.Instance,
                _ => throw Failure("unexpected_random"), 0x5EA7, Get(0x5EA7, true), _ => false, true, null,
                escapeText: (group, code) => (group, code) switch
                {
                    ('C', 'J') => "^02", ('C', 'L') => "^05", ('E', 'K') or ('E', 'M') => string.Empty,
                    _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
                }, allowOpaqueEscapes: true).Text;
        }
        finally { _report = previous; }
    }
}
