namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveIntelligenceSelectionGuide(int message)
    {
        if (message is not (0x5C24 or 0x5C41 or 0x5A33 or 0x5C3F or 0x5D14 or 0x5C40))
            throw Failure("intelligence_selection_guide_root");
        _prefix = "intelligence.selection.guide";
        bool previous = _report; _report = true;
        try
        {
            // The normal message reader resolves the global section/index,
            // parses all control bytes and consumes the complete record.
            // These source callers supply no World, random or argument state.
            return ExecuteMessage(IntelligenceSelectionGuideArguments.Instance,
                _ => throw Failure("random_argument"), message, Get(message, true),
                _ => false, true, null, _ => false,
                (group, code) => (group, code) switch
                { ('C', 'J') => "^02", ('C', 'L') => "^05",
                    _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}") }, true).Text;
        }
        finally { _report = previous; }
    }

    private sealed class IntelligenceSelectionGuideArguments : INarrationCurrentContext
    {
        internal static readonly IntelligenceSelectionGuideArguments Instance = new();
        private static InvalidDataException Missing() => new("intelligence.selection.guide.argument");
        public int PersonValue(int argument, int field) => throw Missing();
        public string PersonText(int argument, int field) => throw Missing();
        public int Relationship(int field, int firstPersonId, int secondPersonId) => throw Missing();
        public string AreaText(int argument, int field) => throw Missing();
    }
}
