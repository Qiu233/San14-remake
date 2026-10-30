namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveBaseCapturePeople(INarrationCurrentContext context, Func<int, int> draw, int root)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(draw);
        if (root is not (0x2A8A or 0x34AB)) throw Failure("base_capture_people_root");
        _prefix = "capture.people.text";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, draw, root, Get(root),
                id => id is 0x2A89 or 0x2A8B, true, null, BaseCapturePeopleMessages.Contains).Text;
        }
        finally { _report = previous; }
    }
    //Both source locales: ordinary count chooses one of these two real
    //Person/Integer leaves; the special count notice is a single literal.
    private static readonly HashSet<int> BaseCapturePeopleMessages = [0x2A89, 0x2A8A, 0x2A8B, 0x34AB];
}
