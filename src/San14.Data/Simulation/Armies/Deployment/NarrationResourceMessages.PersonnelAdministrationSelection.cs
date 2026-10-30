namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    internal string ResolvePersonnelAdministrationSelectionGuide(int message)
    {
        if (message is not (0x5884 or 0x5C4E or 0x5C55))
            throw Failure("personnel_selection_guide_root");
        _prefix = "personnel.selection.guide";
        bool previous = _report; _report = true;
        try
        {
            //These actual caller guides supply no current-world operands or
            //random stream. Parse the complete normal message program.
            return ExecuteMessage(PersonnelAdministrationGuideArguments.Instance,
                _ => throw Failure("random_argument"), message, Get(message, true),
                _ => false, true, null, _ => false).Text;
        }
        finally { _report = previous; }
    }

    private sealed class PersonnelAdministrationGuideArguments : INarrationCurrentContext
    {
        internal static readonly PersonnelAdministrationGuideArguments Instance = new();
        private static InvalidDataException Missing() => new("personnel.selection.guide.argument");
        public int PersonValue(int argument, int field) => throw Missing();
        public string PersonText(int argument, int field) => throw Missing();
        public int Relationship(int field, int firstPersonId, int secondPersonId) => throw Missing();
        public string AreaText(int argument, int field) => throw Missing();
    }
}
