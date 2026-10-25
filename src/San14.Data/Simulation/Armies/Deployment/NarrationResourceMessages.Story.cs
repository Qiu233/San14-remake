namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    //The event resource supplies the message identity. References remain in
    //the normal Game message bank; this evaluates resource expressions only.
    internal string ResolveStoryText(INarrationCurrentContext context, Func<int, int> draw, int root)
    {
        _prefix = "story.message";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, draw, root, Get(root, true), _ => false, true, null,
                escapeText: (group, code) => (group, code) switch
                {
                    ('C', 'J') => "^02", ('C', 'L') => "^05",
                    ('E', 'K') or ('E', 'M') => string.Empty,
                    _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
                }, allowOpaqueEscapes: true).Text;
        }
        finally { _report = previous; }
    }
}
