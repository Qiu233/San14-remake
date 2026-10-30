namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    //TC and SC share this finite 275-record closure, including the ordinary
    //person-address families. Voice33/42/43 remains a real VM request.
    internal string ResolveIntelligenceReport(INarrationCurrentContext context, Func<int, int> drawApp32, int root)
    {
        if (root is not (0x3439 or 0x343E or 0x3443 or 0x3448 or 0x3449 or 0x344A or 0x344B or 0x344C or
            0x3453 or 0x3461 or 0x3466 or 0x3468 or 0x34C7 or 0x34CC or 0x5C2C or 0x5C32 or 0x5C46 or 0x5D0C or
            0x3409 or 0x3419))
            throw Failure("intelligence_report_root");
        _prefix = "intelligence.report.text";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, drawApp32, root, Get(root), _ => false, true, null,
                id => id is >= 0x0097 and <= 0x00A7 or >= 0x00B8 and <= 0x00D5 or >= 0x00D7 and <= 0x0101 or
                    0x0119 or >= 0x01AC and <= 0x01C0 or >= 0x01C2 and <= 0x01C4 or >= 0x01C7 and <= 0x01E5 or
                    >= 0x01E8 and <= 0x01F0 or >= 0x3409 and <= 0x3428 or >= 0x3439 and <= 0x344C or >= 0x3453 and <= 0x345B or
                    >= 0x3461 and <= 0x3466 or 0x3468 or >= 0x34C7 and <= 0x34CC or >= 0x0600 and <= 0x060A or
                    >= 0x5C2C and <= 0x5C3E or >= 0x5C46 and <= 0x5C4D or >= 0x5D0C and <= 0x5D13,
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
