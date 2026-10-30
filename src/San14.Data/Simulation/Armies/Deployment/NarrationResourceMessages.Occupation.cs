namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    //Finite installed TC/SC resource closures. Runtime evaluates normal
    //resources through the shared parser, never cached execution output.
    private static readonly HashSet<int> OccupationReportMessages =
    [
        0x0097, 0x0098, 0x0099, 0x009A, 0x009B, 0x009C, 0x009D, 0x009E, 0x009F, 0x00A0,
        0x00A1, 0x00A2, 0x00A3, 0x00A4, 0x00A5, 0x00A6, 0x00A7, 0x00A8, 0x00A9, 0x00AA,
        0x00AB, 0x00AC, 0x00AD, 0x00AE, 0x00AF, 0x00B0, 0x00B1, 0x00B2, 0x00B3, 0x00B4,
        0x00B5, 0x00B6, 0x00B7, 0x349E, 0x349F, 0x5365, 0x5366, 0x5367, 0x5368, 0x5369,
        0x536A, 0x536B, 0x536C, 0x536D, 0x536E, 0x536F, 0x5370, 0x5371, 0x5372, 0x5F6B,
        0x5F6C,
    ];
    private static readonly HashSet<int> OccupationArmySpeechMessages =
    [
        0x0098, 0x0099, 0x009A, 0x009B, 0x009C, 0x009D, 0x009F, 0x00A0, 0x00A1, 0x00A2,
        0x00A3, 0x00A4, 0x00A5, 0x00A6, 0x00A7, 0x00AB, 0x00AC, 0x00AD, 0x00B1, 0x00B2,
        0x00B3, 0x00B4, 0x00B5, 0x00B6, 0x00B7, 0xC4EA, 0xC4EB, 0xC4EC, 0xC4ED, 0xC4EE,
        0xC4EF, 0xC4F0, 0xC4F1, 0xC4F2, 0xC4F3,
    ];
    private static bool OccupationArmySpeechRoot(int root) => root == 0xC4EA;
    private static bool OccupationArmySpeechMessage(int id) => OccupationArmySpeechMessages.Contains(id);

    internal string ResolveOccupationReport(INarrationCurrentContext context, Func<int, int> draw, int root)
    {
        if (root is not (0x5365 or 0x536C or 0x349E or 0x349F or 0x5F6B or 0x5F6C))
            throw new InvalidDataException("occupation.text.root");
        _prefix = "occupation.text";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, draw, root, Get(root, true), _ => false, true, null,
                OccupationReportMessages.Contains, (group, code) => (group, code) switch
                {
                    ('C', 'J') => "^02", ('C', 'L') => "^05",
                    ('E', 'K') or ('E', 'M') => string.Empty,
                    _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
                }, true).Text;
        }
        finally { _report = previous; }
    }
}
