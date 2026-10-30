namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveAdvantageInformationRequirement(string province, int cities)
    {
        _prefix = "information.advantage.requirement";
        //3251 has the native INTEGER0 supplied by30DDF0 as well as STRING0.
        //Use the existing finite integer parser hook; do not enable report/world selectors.
        var parser = new Parser(ReadRecord(0x3251), _prefix, allowOpaqueEscapes: true,
            readInteger: argument => argument == 0 ? cities : throw Failure("integer_argument"));
        var program = parser.ReadProgram();
        parser.RequireEnd();
        return ExecuteMessage(new AdvantageInformationRequirementArguments(province, cities),
            _ => throw Failure("random_argument"), 0x3251, program, _ => false, true, () => province, _ => false,
            (group, code) => (group, code) switch
            { ('C', 'J') => "^02", ('C', 'L') => "^05", _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}") }, true).Text;
    }
    private sealed class AdvantageInformationRequirementArguments(string province, int cities) : INarrationCurrentContext
    {
        private static InvalidDataException Missing() => new("information.advantage.requirement_argument");
        public int PersonValue(int argument, int field) => throw Missing();
        public string PersonText(int argument, int field) => throw Missing();
        public int Relationship(int field, int firstPersonId, int secondPersonId) => throw Missing();
        public string AreaText(int argument, int field) => throw Missing();
        public int IntegerValue(int argument) => argument == 0 ? cities : throw Missing();
        public string StringText(int argument, int field) => argument == 0 && field is 0 or '2'
            ? CityDomesticMessageFormatting.FormatAbilityField(province) : argument == 0 && field == '3' ? "" : throw Missing();
    }
}
