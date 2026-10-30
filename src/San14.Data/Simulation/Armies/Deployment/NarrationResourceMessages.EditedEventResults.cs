namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    //The actual383420 leaves used by the implemented result language, plus
    //the edited result-page hint and overflow row. TC/SC closure has no refs,
    //random, voice or sound functions; retain the ordinary message VM.
    private static readonly HashSet<int> EditedResultSummaryMessages =
    [
        0x28E3, 0x28E4, 0x87FC, 0x87FD, 0x87FF, 0x8800, 0x8801, 0x8802,
        0x8803, 0x8804, 0x8805, 0x8806, 0x8807, 0x8808, 0x8809, 0x880B,
        0x880C, 0x880E, 0x8810, 0x8811, 0x8812, 0x8844, 0x884A, 0x884B, 0x884C
    ];

    internal string ResolveEditedEventResultSummary(INarrationCurrentContext context, int root)
    {
        if (!EditedResultSummaryMessages.Contains(root)) throw Failure("edited_result_summary_root");
        _prefix = "edited.event.result.summary";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, _ => throw Failure("unexpected_random_argument"),
                root, Get(root), _ => false, true, null, EditedResultSummaryMessages.Contains,
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
