namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveChronologyInformationNarrative(int message, string first, string second, string third)
    {
        _prefix = "information.chronology.narrative";
        // The real chronology supplies three string registers. Existing report
        // parsing/evaluation resolves them through the typed bounded context,
        // while Get() uses its separate _reportPrograms cache.
        bool previous = _report;
        _report = true;
        try
        {
            return ExecuteMessage(new ChronologyInformationArguments([first, second, third]),
                _ => throw Failure("random_argument"), message, Get(message, true), _ => false, true, null, _ => false,
                (group, code) => (group, code) switch
                { ('C', 'J') => "^02", ('C', 'L') => "^05", _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}") }, true).Text;
        }
        finally { _report = previous; }
    }
    private sealed class ChronologyInformationArguments(string[] labels) : INarrationCurrentContext
    {
        private static InvalidDataException Missing() => new("information.chronology.narrative_argument");
        public int PersonValue(int argument, int field) => throw Missing();
        public string PersonText(int argument, int field) => throw Missing();
        public int Relationship(int field, int firstPersonId, int secondPersonId) => throw Missing();
        public string AreaText(int argument, int field) => throw Missing();
        public string StringText(int argument, int field)
        {
            if ((uint)argument >= (uint)labels.Length || field is not (0 or '2' or '3')) throw Missing();
            return field == '3' ? "" : CityDomesticMessageFormatting.FormatAbilityField(labels[argument]);
        }
    }
}
