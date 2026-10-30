namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    //2E0FC0 supplies only Person0. The ordinary root selects that person's
    //capture speech; the special root is the common departure announcement.
    internal string ResolveDuelCapture(INarrationCurrentContext context, Func<int, int> draw, int root)
    {
        if (root is not (0x2A8C or 0x34AA)) throw Failure("duel_capture_root");
        _prefix = "duel.capture.text";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, draw, root, Get(root, true), _ => false, true, null,
                DuelCaptureMessage, (group, code) => (group, code) switch
                {
                    ('C', 'J') => "^02", ('C', 'L') => "^05", ('E', 'K') or ('E', 'M') => "",
                    _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
                }, true).Text;
        }
        finally { _report = previous; }
    }

    //Complete installed TC/SC reference closure of2A8C and34AA.2A96 is an
    //unreferenced adjacent record. SC additionally reaches its0573 family.
    private static bool DuelCaptureMessage(int id) =>
        id is >= 0x0097 and <= 0x00A7 or >= 0x0573 and <= 0x0576 or
            >= 0x2A8D and <= 0x2A95 or >= 0x2A97 and <= 0x2AA9;
}
