using San14.Core;

namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveNormalTurnEventResult(INarrationCurrentContext context, int root)
    {
        if (root is < 0x87D5 or > 0x87ED) throw Failure("normal_result_root");
        _prefix = "normal.event.result";
        bool previous = _report; _report = true;
        try
        {
            //The actual TC/SC templates have no reference, RNG, voice or sound
            //calls. They retain the shared ordinary message VM and field readers.
            return ExecuteMessage(context, _ => throw Failure("unexpected_random_argument"), root, Get(root),
                _ => false, true, null, id => id is >= 0x87D5 and <= 0x87ED,
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

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed class NormalResultMessageCurrent(ArmyCampaignRuleAdapter package, GameState state,
        SourceWorldContext current, INarrationCurrentContext inner, StoryItemMessageCurrent items,
        HistoryMessageOperands arguments) : INarrationCurrentContext
    {
        public int PersonValue(int a, int f) => inner.PersonValue(a, f);
        public string PersonText(int a, int f) => inner.PersonText(a, f);
        public int Relationship(int f, int a, int b) => inner.Relationship(f, a, b);
        public string AreaText(int a, int f) => inner.AreaText(a, f);
        public int IntegerValue(int a) => inner.IntegerValue(a);
        public string StringText(int a, int f) => inner.StringText(a, f);
        public int StringValue(int a, int f) => inner.StringValue(a, f);
        public int CityValue(int a, int f) => inner.CityValue(a, f);
        public string CityText(int a, int f) => inner.CityText(a, f);
        public int ItemValue(int a, int f) => items.ItemValue(a, f);
        public string ItemText(int a, int f) => items.ItemText(a, f);
        public int ForceValue(int a, int f) => inner.ForceValue(a, f);
        public string ForceText(int a, int f)
        {
            if (f != '2') return inner.ForceText(a, f);
            int raw = (a switch { 0 => arguments.Force0, 1 => arguments.Force1, _ => null })
                ?? throw NormalResultMissing("Force_argument");
            if (raw is < 1 or > 51) return string.Empty;
            var force = current.ReadForceTableReference(raw) ?? throw NormalResultMissing("Force_lookup");
            //P2 is22EE20's ruler/army fallback, distinct from P3/P4's
            //custom-kingdom-first display. Reuse the existing source helper.
            var history = new CampaignHistoryWriter(package._repository, package._scenario, package.Seed.Language);
            string name = history.FallbackName(state, current, force,
                (person, full) => full ? person.Name : person.CurrentSurname ?? throw NormalResultMissing("Force_ruler_surname"));
            return CityDomesticMessageFormatting.FormatCityNameField(name);
        }
    }
}
