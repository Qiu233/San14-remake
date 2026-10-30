namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveIncomeReportAdvice(INarrationCurrentContext context,
        Func<int, int> drawApp32, int root)
    {
        if (root is not (0x4FF2 or 0x4FF9 or 0x5000)) throw Failure("income_report_root");
        _prefix = "income-report.strategist-text";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, drawApp32, root, Get(root), _ => false, true, null,
                id => id is >= 0x4FF2 and <= 0x5006 or >= 0x0097 and <= 0x0128,
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
