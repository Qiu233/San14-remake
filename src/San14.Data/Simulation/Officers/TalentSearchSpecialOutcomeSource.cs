using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private const string SpecialSearchDivisorKey = "simulation.talent-search.special-probability-divisor";

    // TC/SC immutable initialized data, independently extracted offline. A saved parameter wins;
    // older campaigns can lack these newly named immutable rules.
    private static readonly IReadOnlyDictionary<string, int> SpecialSearchDefaults =
        new Dictionary<string, int> { [SpecialSearchDivisorKey] = 10 };

    private static int ReadSpecialSearchParameter(GameState state, string key)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw SearchOutcomeMissing("special_parameters");
        if (bank.Language is not ("TC" or "SC") || bank.Float32Values.ContainsKey(key))
            throw SearchOutcomeMissing("special_current_parameter:" + key);
        if (bank.Int32Values.TryGetValue(key, out int value)) return value;
        //The reached charm modifier uses the same independently proved
        //initialized1 in each edition. This admits no other SC talent rule.
        return key == TalentSearchSource.Trait63ScaleKey ? 1 : SpecialSearchDefaults[key];
    }

    // 1EB130: reached only after the ordinary person/scenery/item/gold attempts
    // all miss. The caller tries5006 before5008 and keeps the first success.
    private bool TrySpecialSearchCurrent(GameState state, SourceWorldContext current,
        NativeMapSeasonDate date, PersonnelTalentTask task, int executor, AreaReference area,
        int personId, Func<TacticSourceSamplerWorld> readSampler)
    {
        int eventId;
        byte mask;
        if (personId == 5006) { eventId = 59; mask = 1; }
        else if (personId == 5008) { eventId = 60; mask = 2; }
        else return false;
        var special = current.ReadPersonLookupReference(personId) ?? throw SearchOutcomeMissing("special_Person_pointer");
        int physical = SearchNeed(special.PhysicalSlot, "special_Person_physical");
        ushort identity = SearchNeed(current.ReadPersonIdentity(physical), "special_Person10");
        if (unchecked((ushort)(identity - 5001)) >= 100) return false;
        var world = state.CampaignEventWorld ?? throw SearchOutcomeMissing("special_World");
        if ((SearchNeed(world.SpecialPersonEncounterFlags, "special_World1699") & mask) != 0) return false;
        var season = state.NativeMapSeason ?? throw SearchOutcomeMissing("special_calendar");
        static int Period(NativeMapSeasonDate d) => unchecked(d.Year * 36 + (d.Month - 1) * 3 + (d.Day - 1) / 10);
        int elapsed = unchecked(world.ElapsedPeriodOffset + (int)season.AdvancedPeriods + Period(date) - Period(season.ThirdDate));
        if (elapsed <= 35) return false;
        ushort appearanceYear = state.Officers.GetValueOrDefault(identity)?.AppearanceYear
            ?? throw SearchOutcomeMissing("special_Person_AA");
        if (appearanceYear > unchecked((short)date.Year)) return false;
        byte flags = SearchNeed(current.ReadAreaFlags38(area), "special_Area38");
        if ((flags & 5) != 0) return false;
        var history = state.CampaignEvents ?? throw SearchOutcomeMissing("special_event_history");
        if (history.Occurred[eventId])
        {
            var previous = history.OccurrenceDates[eventId] ?? throw SearchOutcomeMissing("special_event_date");
            static int Day(int year, int month, int day) => unchecked((unchecked((short)year) * 12 + unchecked((sbyte)month)) * 30 + unchecked((sbyte)day) - 31);
            if (Day(date.Year, date.Month, date.Day) <= Day(previous.Year, previous.Month, previous.Day) + 30)
                return false;
        }
        int areaPhysical = SearchNeed(area.PhysicalSlot, "special_Area_pointer");
        if (SearchNeed(state.Areas.GetValueOrDefault(areaPhysical)?.Security, "special_Area54") < 90) return false;
        var home = BaseLookup.Find(SearchNeed(current.ReadPersonFirstLocation(executor), "special_executor_E2"), current);
        int homeId = SearchNeed(current.ReadBaseObjectLink(home), "special_Home4E");
        bool needed = false;
        if (homeId is >= 1 and <= 61)
        {
            // 234920(mode0) preserves the actual Person registry order. It
            // tests current Home before status and adds no validity filter.
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw SearchOutcomeMissing("special_Person100"))
            {
                int person = SearchNeed(entry, "special_Home_Person");
                if (SearchNeed(current.ReadPersonFirstLocation(person), "special_Home_E2") != homeId ||
                    SearchNeed(current.ReadPersonStatus(person), "special_Home_E6") is not (>= 1 and <= 4)) continue;
                if (personId == 5006)
                    needed = SearchNeed(OfficerHealthSourceAdapter.ReadByte(current, person), "special_Health161") != 0;
                else
                    needed = SearchNeed(current.ReadConditionPersonAtTerminalYear(person,
                        p => state.Officers.GetValueOrDefault(SearchNeed(current.ReadPersonIdentity(p), "special_lifespan_ID"))?.Lifespan,
                        () => BattleRemakeCurrentSettings.ReadSelectorE58(state) is int selector
                            ? BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x1C4 : 0x2C) : null,
                        unchecked((short)date.Year)), "special_terminal_year");
                if (needed) break;
            }
        }
        if (!needed) return false;
        var force = current.ReadPersonForce(executor) ?? throw SearchOutcomeMissing("special_executor_Force");
        if (!TalentForceIsPlayerCurrent(state, current, force)) return false;
        int adjusted = 0;
        if (SearchNeed(current.ReadPersonNativeValidity(executor), "special_executor_validity"))
        {
            int id = SearchNeed(current.ReadPersonIdentity(executor), "special_executor_ID");
            int charm = SearchAbilities(current).ResolveDimension(state, id, 4,
                (f, effect) => _baseCapacityInputs.ReadPanelModifier(state, f, effect))
                ?? throw SearchOutcomeMissing("special_executor_Charm");
            int traits = TacticRemakeCurrentTraits.CommanderEffectSum(current, executor, 63)
                ?? throw SearchOutcomeMissing("special_executor_trait63");
            int scale = ReadSpecialSearchParameter(state, TalentSearchSource.Trait63ScaleKey);
            adjusted = unchecked(charm + unchecked(unchecked(traits * charm) * scale) / 100);
        }
        int divisor = ReadSpecialSearchParameter(state, SpecialSearchDivisorKey);
        if (divisor == 0) throw SearchOutcomeMissing("special_zero_probability_divisor");
        int probability = adjusted / divisor;
        // The pure sample still runs when the command's saved sample exists.
        // 1EB130 rereads the command itself; it neither borrows nor overwrites
        // the ordinary/fallback attempt's local sample.
        int sample = TacticEffectSourceSamplerMath.Sample(100, 10,
            SearchNeed(current.ReadPersonIdentity(executor), "special_sample_executor"), areaPhysical, 0, 0, readSampler());
        if (task.SavedSample != -1) sample = task.SavedSample;
        return sample < probability;
    }

    private GameState ApplySpecialSearchEncounterCurrent(GameState state, SourceWorldContext current,
        int executor, AreaReference area, int personId, List<CampaignEffectRequest> effects)
    {
        var world = state.CampaignEventWorld ?? throw SearchOutcomeMissing("special_success_World");
        byte flags = SearchNeed(world.SpecialPersonEncounterFlags, "special_success_World1699");
        byte after = (byte)(flags | (personId == 5006 ? 1 : 2));
        current.ConditionSpecialPersonEncounterFlags = after;
        state = state with { CampaignEventWorld = world with { SpecialPersonEncounterFlags = after } };
        effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue, 5, null, null, null, null) });
        // The parent binds the original lookup operand5006/5008, not the
        // current identity of its underlying pointer. No chronicle call follows.
        var text = FormatTalentReportCurrent(state, current, 0xC365, new(Person0: personId,
            Person1: SearchNeed(current.ReadPersonIdentity(executor), "special_success_executor"),
            Area0: SearchNeed(area.PhysicalSlot, "special_success_Area"),
            Base0: SearchNeed(current.ReadBaseObjectLink(SearchAreaCity(current, area)), "special_success_City4E")));
        effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.Announcement, null, null, null, null, text.Text) });
        return text.State;
    }
}
