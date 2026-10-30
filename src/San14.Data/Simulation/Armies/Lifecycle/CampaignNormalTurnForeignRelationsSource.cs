using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private const string NormalForeignParameterPrefix = "simulation.foreign-relations.";
    //TC and SC independently resolve all thirty caller-read file-backed
    //parameters to these same values. The normal algorithms are also paired
    //over their complete bodies. Runtime uses this semantic table or a saved
    //typed override, never the original executable or its execution cache.
    private static readonly IReadOnlyDictionary<string, int> NormalForeignStaticRules = new Dictionary<string, int>
    {
        ["first-war-start-year"] = 194, ["first-war-start-month"] = 1,
        ["first-war-end-year"] = 197, ["first-war-end-month"] = 11,
        ["first-peace-start-year"] = 198, ["first-peace-start-month"] = 1,
        ["first-peace-end-year"] = 198, ["first-peace-end-month"] = 12,
        ["second-war-start-year"] = 217, ["second-war-start-month"] = 1,
        ["second-war-end-year"] = 217, ["second-war-end-month"] = 11,
        ["second-peace-start-year"] = 218, ["second-peace-start-month"] = 1,
        ["second-peace-end-year"] = 218, ["second-peace-end-month"] = 12,
        ["cycle-years"] = 10, ["war-chance-threshold"] = 20,
        ["third-fourth-war-year"] = 1, ["third-fourth-war-month"] = 6, ["third-fourth-peace-year"] = 3,
        ["third-second-war-year"] = 4, ["third-second-war-month"] = 2, ["third-second-peace-year"] = 5,
        ["third-first-war-year"] = 6, ["third-first-war-month"] = 2, ["third-first-peace-year"] = 7,
        ["second-first-war-year"] = 8, ["second-first-war-month"] = 5, ["second-first-peace-year"] = 9
    };

    //The independent five CForeignData objects are not Force46..50. Their
    //five current relation bytes are serialized by2383F0/203060 in section36.
    internal GameState InstallFreshNormalTurnForeignRelations(GameState state)
    {
        if (state.ForeignCountryRelations is not null || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 })
            throw NormalForeignMissing("fresh_owner_already_used");
        var table = _scenario.Snapshot.WorldParameters?.SourceBoundary?.StateTables.SingleOrDefault(row => row.Section == 36)
            ?? throw NormalForeignMissing("fresh_section36");
        if (table.RawCount != 5 || table.RecordBytes != 5 || table.RawBytes.Length != 25)
            throw NormalForeignMissing("fresh_section36_profile");
        var names = _repository.LoadForeignCountryNames(_scenario.FixedDataSource.SourcePath);
        if (names.Source.Sha256 != _scenario.FixedDataSource.Sha256 || names.Names.Count != 5)
            throw NormalForeignMissing("fresh_name_definitions");
        var countries = ImmutableDictionary.CreateBuilder<int, CampaignForeignCountryRelations>();
        for (int country = 0; country < 5; country++)
        {
            int start = country * 5;
            var relations = Enumerable.Range(0, 5).ToImmutableDictionary(peer => peer, peer => table.RawBytes[start + peer]);
            countries.Add(country, new(country, names.Names[country], relations));
        }
        state = state with { ForeignCountryRelations = new(countries.ToImmutable()) };
        var native = state.NativeArmySimulation ?? throw NormalForeignMissing("fresh_parameter_owner");
        if (native.Parameters.Language != Seed.Language || native.Parameters.Language is not ("TC" or "SC") ||
            native.Parameters.RuleVersion != SimulationRuleIdentity.ForLocale(native.Parameters.Language))
            throw NormalForeignMissing("fresh_parameter_source");
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        foreach (var pair in NormalForeignStaticRules) values.TryAdd(NormalForeignParameterPrefix + pair.Key, pair.Value);
        return state with { NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = values } } };
    }

    //Normal Turn action13:429FA0. One current and pool are borrowed throughout;
    //neither the mutable App RNG nor the list pool is reset or exported here.
    internal GameState ApplyNormalTurnSpecialForceRelationsCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw NormalForeignMissing("shared_current_pool");
        if (ReadApplicationInformationRestriction(state) is not bool restricted)
            throw NormalForeignMissing("Application_user_selection_service");
        if (restricted) return state;
        var application = ReadApplicationGameplay() ?? throw NormalForeignMissing("Application_context");
        if (application.TutorialServicePresent is not bool tutorialPresent)
            throw NormalForeignMissing("tutorial_service_presence");
        if (tutorialPresent)
        {
            bool active = application.TutorialActive ?? throw NormalForeignMissing("tutorial_active");
            if (active)
            {
                if (application.SpecialModeServicePresent is not bool specialPresent)
                    throw NormalForeignMissing("special_service_presence");
                if (specialPresent) throw NormalForeignMissing("special_service_foreign_relations_suppression_bit20");
            }
        }
        var date = state.NativeMapSeason?.ThirdDate ?? throw NormalForeignMissing("World34_date");
        int year = date.Year, month = date.Month;
        int now = unchecked((month + year * 12) * 30);
        var latest = state;
        var effects = new List<CampaignEffectRequest>();
        Window("first-war", 2);
        Window("first-peace", 1);
        Window("second-war", 2);
        Window("second-peace", 1);

        //All three source settings getters are reached, in this order, even
        //when the first one is false. Their E58 branches use the real415 owner.
        int selector = Setting(BattleRemakeCurrentSettings.ReadSelectorE58(latest), "selector_E58");
        bool historicalEvents = Setting(BattleRemakeCurrentSettings.ReadInt32(latest,
            selector == 0 ? 0x14C : 0x3C), "historical_events_setting") == 1;
        int second = selector == 0
            ? Setting(BattleRemakeCurrentSettings.ReadInt32(latest, 500), "foreign_events_setting") == 1 ? 1 : 0
            : unchecked((sbyte)Setting(BattleRemakeCurrentSettings.ReadByte(latest, 0x79), "foreign_events_setting"));
        bool third = Setting(BattleRemakeCurrentSettings.ReadInt32(latest,
            selector == 0 ? 0x2FC : 0xDC), "foreign_conflict_setting") == 1;
        Cycle(3, 4, "third-fourth");
        Cycle(3, 2, "third-second");
        Cycle(3, 1, "third-first");
        Cycle(2, 1, "second-first");
        if (effects.Count != 0)
        {
            var pending = latest.CampaignEffectRequests ?? throw NormalForeignMissing("pending_owner");
            long revision = latest.NativeClock?.UpdateOrdinal ?? throw NormalForeignMissing("source_revision");
            latest = latest with { CampaignEffectRequests = pending.Append(CampaignEffectSource.MonthlyMaintenance, revision, effects) };
        }
        return latest;

        int P(string key) => ReadNormalForeignParameter(latest, key);
        void Window(string key, byte relation)
        {
            int start = unchecked((P(key + "-start-month") + P(key + "-start-year") * 12) * 30 - 30);
            int end = unchecked((P(key + "-end-month") + P(key + "-end-year") * 12) * 30);
            if (start < now && now <= end) Change(3, 4, relation);
        }
        int Remainder()
        {
            int divisor = P("cycle-years");
            if (divisor == 0 || year == int.MinValue && divisor == -1) throw NormalForeignMissing("cycle_year_divisor");
            return year % divisor;
        }
        void Cycle(int first, int other, string key)
        {
            if (historicalEvents && second != 0 && third &&
                Remainder() == P(key + "-war-year") && month == P(key + "-war-month"))
            {
                int troops = 0;
                //The actual80 sequence is consumed without validity, Force
                //or uniqueness filtering; every Base's current3C contributes.
                foreach (var entry in current.ReadRegisteredBases() ?? throw NormalForeignMissing("Base80_registry"))
                {
                    var target = entry ?? throw NormalForeignMissing("Base80_pointer");
                    troops = unchecked(troops + Setting(current.ReadBaseResource(target, BaseResourceKind.Troops), "Base3C"));
                }
                var sampler = ReadArmyDestructionSampler(latest, current) ?? throw NormalForeignMissing("sampler_World450_45C");
                int sample = TacticEffectSourceSamplerMath.Sample(100, 1, troops, year, 0, 0, sampler);
                //The source samples before testing either stored relation.
                if (sample <= P("war-chance-threshold")) Change(first, other, 2);
            }
            if (Remainder() == P(key + "-peace-year")) Change(first, other, 1);
        }
        CampaignForeignCountryRelations Country(int id) => latest.ForeignCountryRelations?.Countries.GetValueOrDefault(id)
            ?? throw NormalForeignMissing("country_" + id);
        byte Relation(int id, int other) => Country(id).Relations.TryGetValue(other, out byte value)
            ? value : throw NormalForeignMissing("country_relationship");
        void Change(int first, int other, byte value)
        {
            byte a = Relation(first, other);
            //Native && is lazy; the second byte is not read on a failed gate.
            if (value == 2 ? a == 2 : a != 2) return;
            byte b = Relation(other, first);
            if (value == 2 ? b == 2 : b != 2) return;
            var owner = latest.ForeignCountryRelations ?? throw NormalForeignMissing("country_owner");
            var one = Country(first);
            var two = Country(other);
            latest = latest with { ForeignCountryRelations = owner with { Countries = owner.Countries
                .SetItem(first, one with { Relations = one.Relations.SetItem(other, value) })
                .SetItem(other, two with { Relations = two.Relations.SetItem(first, value) }) } };
            Report(first, other, value);
        }
        void Report(int first, int other, byte relation)
        {
            //430CA0:32EF60/primary-District validity is the report gate, not
            //Force validity. The relationship stores above always precede it.
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(latest, current)
                ?? throw NormalForeignMissing("report_player_Force");
            var district = ForcePrimaryDistrictSource.Resolve(current, player);
            if (!Setting(current.ReadDistrictNativeValidity(district), "report_primary_District_validity")) return;
            string secondName = Country(other).Name, firstName = Country(first).Name;
            int root = relation == 2 ? 0x5EAA : 0x5EAC;
            ushort ruler = Setting(current.ReadMoraleWaveRuler22EAC0(player), "report_player_ruler");
            string intro = Format(root, new(Person0: 5012, Person1: ruler, String0: secondName, String1: firstName));
            string main = Format(root + 1, new(String0: secondName, String1: firstName));
            int day = (ReadArmyDestructionSampler(latest, current) ?? throw NormalForeignMissing("report_Day450")).Day450;
            latest = WriteHistory(latest, current, new(1, Setting(district.PhysicalSlot, "report_District"), 0,
                day, main, intro, 5012, MarkBit0: 1, MarkBit4: 0), effects);
            effects.Add(new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.ConditionalSpeakerText,
                null, 5012, intro) });
        }
        string Format(int root, HistoryMessageOperands operands)
        {
            var result = FormatNormalTurnReport(latest, current, root, operands, value => latest = value);
            latest = result.State;
            return result.Text;
        }
        T Setting<T>(T? value, string field) where T : struct => value ?? throw NormalForeignMissing(field);
    }

    private int ReadNormalForeignParameter(GameState state, string name)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw NormalForeignMissing("parameter_owner");
        string key = NormalForeignParameterPrefix + name;
        if (bank.Language != Seed.Language || bank.Language is not ("TC" or "SC") ||
            bank.RuleVersion != SimulationRuleIdentity.ForLocale(bank.Language) || bank.Float32Values.ContainsKey(key))
            throw NormalForeignMissing(key);
        return bank.Int32Values.TryGetValue(key, out int value) ? value :
            NormalForeignStaticRules.TryGetValue(name, out int initial) ? initial : throw NormalForeignMissing(key);
    }
    private static InvalidDataException NormalForeignMissing(string owner) => new("normal.turn.foreign-relations." + owner);
}
