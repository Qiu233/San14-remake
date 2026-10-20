using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Only source-time operands belonging to the real report formatter.
/// Null means that an operand was not supplied; it is never replaced by a
/// scenario snapshot or guessed from the eventual notification.</summary>
internal sealed record HistoryMessageOperands(ArmyUnitReference? Army0 = null, ArmyUnitReference? Army1 = null,
    ushort? Base0 = null, string? String0 = null, string? String1 = null, string? String2 = null,
    int Person0 = 0, int Person1 = 0, int? Force0 = null, int? Area0 = null, int[]? Integers = null);
internal sealed record HistoryTextResult(GameState State, string Text);

public sealed partial class ArmyCampaignRuleAdapter
{
    internal string ReadHistoryDataText(int section, int id)
    {
        var data = _repository.LoadMessages(Seed.Language, MessageKind.Data).Messages;
        if ((uint)section >= (uint)data.Sections.Count || (uint)id >= (uint)data.Sections[section].Records.Count)
            throw new InvalidDataException("campaign-history.data_message");
        return data.Sections[section].Records[id].Text ?? throw new InvalidDataException("campaign-history.data_text");
    }

    //30AEE0's1421461A8 is msgdata table0, bounded by0x1809. It is
    //not another resource family or an inferred name from the command enum.
    internal string ReadHistoryChoiceName(GameState state, int choice) => choice is 1 or 2
        ? ReadHistoryDataText(0, 0x118 + choice) : CampaignHistoryWriter.Supplemental(state)
            ?? throw new InvalidDataException("campaign-history.current_empty_text");

    internal string ReadHistoryArmyName(GameState state, SourceWorldContext current, ArmyUnitReference army)
    {
        if (!HistoryKnown(current.ReadArmyUnitNativeValidity(army), "Army_name_validity"))
            return CampaignHistoryWriter.Supplemental(state) ?? throw HistoryMissing("current_empty_text");
        ushort commander = HistoryKnown(current.ReadArmyUnitCommanderId(army), "Army_name_Commander12");
        var person = (commander <= 6000 ? current.ReadPersonLookupReference(commander) : current.ReadTransferPersonTableReference(0))
            ?? throw HistoryMissing("Army_name_Person_lookup");
        if (person.PhysicalSlot is not int physical) return CampaignHistoryWriter.Supplemental(state)
            ?? throw HistoryMissing("current_empty_text");
        if (!HistoryKnown(current.ReadPersonNativeValidity(physical), "Army_name_Person_validity"))
            return CampaignHistoryWriter.Supplemental(state) ?? throw HistoryMissing("current_empty_text");
        int id = HistoryKnown(current.ReadPersonIdentity(physical), "Army_name_Person_identity");
        if (current.ResolveOfficerSource(id) != physical || !state.Officers.TryGetValue(id, out var owner))
            throw HistoryMissing("Army_name_current_Person");
        string name = owner.Name ?? throw HistoryMissing("Army_name_current_Name");
        //22ECB0 calls309450(name,0): complete current full name then
        //Data1/0352; the outer Army n2 field adds its own ^02/^00.
        return HeadHistory(name) + HeadHistory(ReadHistoryDataText(1, 0x352));
    }

    internal HistoryTextResult FormatHistoryReport(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands)
    {
        var latest = state;
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        var people = new DeploymentNarrationCurrentPeople(state, current, abilities,
            operands.Person0, operands.Person1, area: operands.Area0,
            panelReader: (force, effect) => _baseCapacityInputs.ReadPanelModifier(latest, force, effect),
            cityArgument: operands.Base0, appearanceRepository: _repository,
            appearanceScenario: _scenario, appearanceLanguage: Seed.Language, forceArgument: operands.Force0);
        var context = new ReportCurrent(this, () => latest, current, people, operands);
        int Draw(int bound)
        {
            if (bound < 2) return 0;
            var saved = latest.CampaignAppRandom32 ?? throw HistoryMissing("App32_draw_state");
            var random = new SharedSourceRandom32(saved.State);
            int value = random.Next(bound);
            latest = latest with { CampaignAppRandom32 = new(random.State) };
            return value;
        }
        var vm = new NarrationResourceMessages(_repository.LoadMessages(Seed.Language, MessageKind.Game).Messages);
        string text = vm.ResolveHistoryReport(context, Draw, root);
        return new(latest, text);
    }

    private static string HeadHistory(string text) => text.IndexOf('\0') is int n && n >= 0 ? text[..n] : text;
    private static T HistoryKnown<T>(T? value, string field) where T : struct => value ?? throw HistoryMissing(field);
    private static InvalidDataException HistoryMissing(string field) => new("campaign-history.text." + field);

    private sealed class ReportCurrent(ArmyCampaignRuleAdapter package, Func<GameState> latest,
        SourceWorldContext current, INarrationCurrentContext people, HistoryMessageOperands operands) : INarrationCurrentContext
    {
        public int PersonValue(int a, int f) => people.PersonValue(a, f);
        public string PersonText(int a, int f) => people.PersonText(a, f);
        public int Relationship(int f, int a, int b) => people.Relationship(f, a, b);
        public int Personality(int a, Func<int> key) => people.Personality(a, key);
        public string AreaText(int a, int f)
        {
            if (a != 0 || f != '3') throw HistoryMissing("Area_field");
            int raw = operands.Area0 ?? throw HistoryMissing("Area_argument");
            if (raw is < 1 or > 500) return "";
            var selected = current.ReadAreaTableReference(raw) ?? throw HistoryMissing("Area_lookup");
            if (!HistoryKnown(current.ReadAreaNativeValidity(selected), "Area_validity")) return "";
            int physical = HistoryKnown(selected.PhysicalSlot, "Area_physical");
            var name = latest().Areas.GetValueOrDefault(physical)?.Name ?? throw HistoryMissing("Area_current_name");
            return CityDomesticMessageFormatting.FormatCityNameField(name);
        }
        public string CityText(int a, int f) => people.CityText(a, f);
        public int IntegerValue(int a) => operands.Integers is { } values && (uint)a < (uint)values.Length
            ? values[a] : throw HistoryMissing("Integer_argument");
        public string ForceText(int a, int f)
        {
            if (f == '4') return people.ForceText(a, f);
            if (a != 0 || f != '3') throw HistoryMissing("Force_field");
            int raw = operands.Force0 ?? throw HistoryMissing("Force_argument");
            if (raw is < 1 or > 51) return "";
            var value = TrapCounterReportForceNameSource.Capture(latest(), current, checked((byte)raw));
            string name = value.Form switch
            {
                TrapCounterForceNameForm.Empty => "",
                TrapCounterForceNameForm.CustomKingdom => Custom(value.CustomKingdomIndex!.Value) +
                    HeadHistory(package.ReadHistoryDataText(1, 0x34F)),
                TrapCounterForceNameForm.PersonWithArmySuffix => HeadHistory(value.PersonNameOperandAtSource
                    ?? throw HistoryMissing("Force_name")) + HeadHistory(package.ReadHistoryDataText(1, 0x34F)),
                TrapCounterForceNameForm.PersonWithoutArmySuffix => HeadHistory(value.PersonNameOperandAtSource
                    ?? throw HistoryMissing("Force_name")),
                _ => throw HistoryMissing("Force_name_form")
            };
            return CityDomesticMessageFormatting.FormatCityNameField(name);
        }
        private string Custom(byte id)
        {
            var defs = package._repository.LoadForceNameDefinitions(package._scenario.FixedDataSource.SourcePath).Catalog.Definitions;
            if ((uint)id >= (uint)defs.Count) throw HistoryMissing("Force_name_catalog");
            return HeadHistory(defs[id].ShortName);
        }
        private ArmyUnitReference Army(int a) => (a == 0 ? operands.Army0 : a == 1 ? operands.Army1 : null)
            ?? throw HistoryMissing("Army_argument");
        public int ArmyValue(int a, int f)
        {
            var arg = Army(a);
            if (f == 0) return HistoryKnown(arg.PhysicalSlot, "Army_argument_identity");
            if (f != '2') throw HistoryMissing("Army_field_" + f.ToString("X"));
            int raw = HistoryKnown(arg.PhysicalSlot, "Army_argument_identity");
            if (raw is < 1 or > 500) return 0;
            var looked = current.ReadArmyUnitTableReference(raw) ?? throw HistoryMissing("Army_argument_lookup");
            _ = HistoryKnown(current.ReadArmyUnitNativeValidity(looked), "Army_argument_validity");
            //30E350 only calls22ECB0 when a display buffer was supplied.
            return 0;
        }
        public string ArmyText(int a, int f)
        {
            var arg = Army(a);
            int raw = HistoryKnown(arg.PhysicalSlot, "Army_argument_identity");
            if (f != '2') throw HistoryMissing("Army_field_" + f.ToString("X"));
            if (raw is < 1 or > 500) return "";
            var looked = current.ReadArmyUnitTableReference(raw) ?? throw HistoryMissing("Army_argument_lookup");
            if (!HistoryKnown(current.ReadArmyUnitNativeValidity(looked), "Army_argument_validity")) return "";
            return CityDomesticMessageFormatting.FormatPersonNameField(package.ReadHistoryArmyName(latest(), current, looked));
        }
        private string String(int a) => (a switch { 0 => operands.String0, 1 => operands.String1, 2 => operands.String2,
            _ => null }) ?? throw HistoryMissing("String_argument");
        public string StringText(int a, int f)
        {
            string value = HeadHistory(String(a));
            if (f is not (0 or '2' or '3')) throw HistoryMissing("String_field");
            return f == '3' ? "" : CityDomesticMessageFormatting.FormatAbilityField(value);
        }
        public int StringValue(int a, int f)
        {
            string value = HeadHistory(String(a));
            if (f is not (0 or '2' or '3')) throw HistoryMissing("String_field");
            return f == '3' ? value.Length : CityDomesticMessageFormatting.FormatAbilityField(value).Length;
        }
    }
}
