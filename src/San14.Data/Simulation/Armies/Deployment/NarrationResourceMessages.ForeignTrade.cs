namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    //Actual TC/SC raw21 closure of the CCaravanReturnState reports. Source
    //message bytecode remains in the installed resource, not this program.
    internal string ResolveForeignTradeReport(INarrationCurrentContext context, Func<int, int> drawApp32, int root)
    {
        if (root is not (0x32BE or 0x32C3 or 0x32C8 or 0x32CB or 0x32CE or 0x32D2 or 0x32D6 or
            0x32DB or 0x32DF or 0x32E0 or 0x32E1 or 0x32E2 or 0x32E3 or 0x32E7 or 0x32F8 or 0x32F9 or 0x32FE or
            0x32FF or 0x3300 or 0x3301 or 0x3302 or 0x3303 or 0x3304 or 0x3308 or 0x330C or 0x3310 or 0x3314 or
            0x3366 or 0x3371 or 0x5BEE) && root is not (>= 0x32AB and <= 0x32BD))
            throw Failure("foreign_trade_report_root");
        _prefix = "foreign-trade.report.text";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, drawApp32, root, Get(root), _ => false, true, null,
                id => id is >= 0x32AB and <= 0x32E7 or >= 0x32F8 and <= 0x3317 or >= 0x3366 and <= 0x337B or
                    >= 0x5BEE and <= 0x5BF2 or >= 0x0097 and <= 0x0128 or >= 0x01AC and <= 0x01F0,
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
