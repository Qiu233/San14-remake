namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveDuelPresentation(INarrationCurrentContext context, Func<int,int> draw, int root)
    {
        if (root is not (0xC8C9 or 0xC904 or 0x5F81)) throw Failure("duel_presentation_root");
        _prefix = "duel.presentation.text";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, draw, root, Get(root, true), _ => false, true, null,
                id => id is >=0xC8C9 and <=0xC913 or 0x5F81 || DuelPresentationSharedMessage(id), (group, code) => (group, code) switch
                { ('C','J') => "^02", ('C','L') => "^05", ('E','K') or ('E','M') => "",
                    _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}") }, true).Text;
        }
        finally { _report = previous; }
    }
    private static bool DuelArmySpeechRoot(int root) => root is 0xC935 or 0xC927 or 0xC92A or 0xC948 or 0xC4E7;
    private static bool DuelArmySpeechMessage(int id) => id is >=0xC927 and <=0xC951 or 0xC4E7 || DuelPresentationSharedMessage(id);
    //The reached small name/address grammar reuses the mature history
    //dialect's already-owned records. SC additionally reaches its0600 family.
    private static bool DuelPresentationSharedMessage(int id) =>
        id is >=0x0097 and <=0x0128 && HistoryMessages.Contains(id) || id is 0x013C or >=0x0600 and <=0x060A;
}
