namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    // The common selector binds its actual Data1 noun to string argument0.
    // Reuse the normal game-message parser rather than joining its literals.
    internal string ResolvePersonSelectorHint(string noun)
    {
        ArgumentNullException.ThrowIfNull(noun);
        _prefix = "person-selector.hint";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(new PersonSelectorArguments(noun),
                _ => throw Failure("random_argument"), 0x5EFF, Get(0x5EFF, true),
                _ => false, true, null, _ => false).Text;
        }
        finally { _report = previous; }
    }

    private sealed class PersonSelectorArguments(string noun) : INarrationCurrentContext
    {
        private static InvalidDataException Missing() => new("person-selector.hint_argument");
        public string StringText(int argument, int field) => argument == 0 && field is 0 or '2' ? noun :
            argument == 0 && field == '3' ? string.Empty : throw Missing();
        public int StringValue(int argument, int field) => argument == 0 && field is 0 or '2' or '3'
            ? noun.Length : throw Missing();
        public int PersonValue(int argument, int field) => throw Missing();
        public string PersonText(int argument, int field) => throw Missing();
        public int Relationship(int field, int firstPersonId, int secondPersonId) => throw Missing();
        public string AreaText(int argument, int field) => throw Missing();
    }
}
