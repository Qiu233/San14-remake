using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private const string NormalTurnTradeAdmissionParameterPrefix = "simulation.normal-turn.foreign-trade-admission.";
    //Both complete native admission bodies independently read these same
    //five file-backed integers. Keep one semantic table, no runtime EXE reads.
    private static readonly IReadOnlyDictionary<string, int> NormalTurnTradeAdmissionStaticRules =
        new Dictionary<string, int>
        {
            ["maximum-rank"] = 8, ["command-books"] = 1,
            ["gold"] = 1000, ["food"] = 10000, ["troops"] = 500
        };

    internal GameState InstallFreshNormalTurnTradeAdmissionParameters(GameState state)
    {
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0)
            throw FinalizationMissing("trade_fresh_boundary");
        var native = state.NativeArmySimulation ?? throw FinalizationMissing("trade_fresh_parameter_owner");
        var bank = native.Parameters;
        if (bank.Language != Seed.Language || bank.Language is not ("TC" or "SC") ||
            bank.RuleVersion != SimulationRuleIdentity.ForLocale(bank.Language))
            throw FinalizationMissing("trade_fresh_rule_profile");
        var values = new Dictionary<string, int>(bank.Int32Values);
        foreach (var pair in NormalTurnTradeAdmissionStaticRules)
        {
            string key = NormalTurnTradeAdmissionParameterPrefix + pair.Key;
            if (bank.Float32Values.ContainsKey(key)) throw FinalizationMissing("trade_integer_" + pair.Key);
            values.TryAdd(key, pair.Value);
        }
        return state with { NativeArmySimulation = native with { Parameters = bank with { Int32Values = values } } };
    }

    //2DF140/2DF7A0 refresh their real saved menu flags once at their Turn
    //positions. They do not issue commands or consume the application's RNG.
    private GameState RefreshNormalTurnCommandAvailabilityCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, bool monthly)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw FinalizationMissing("menu_player");
        var district = ForcePrimaryDistrictSource.Resolve(current, player); //32EF60 precedes the validity test.
        uint mask = monthly ? 0x09000000u : 0xB800u;
        if (!FinalizationNeed(current.ReadForceNativeValidity(player), "menu_player_validity"))
            return PublishNormalTurnMenuFlags(state, current, mask, 0);
        int force = FinalizationNeed(current.ReadForcePhysicalSlot(player), "menu_player_ordinal");
        int districtId = FinalizationNeed(district.PhysicalSlot, "menu_primary_District");
        if (monthly)
        {
            bool promotion = false;
            var people = FinalizationTemporary(current);
            try
            {
                foreach (int person in NormalTurnServingPeople(current, force, districtId, true))
                    if (CreditEligibleCurrent(state, current, person, true))
                        FinalizationAppend(current, people, new(RegistryKind.Person, person));
                promotion = FinalizationNeed(current.ReadTemporaryHexListCount(people), "promotion_count") != 0;
            }
            finally { FinalizationRelease(current, people); }
            bool research = false;
            if (FinalizationNeed(current.ReadDistrictNativeValidity(district), "research_District_validity"))
            {
                //711CE0 passes third1: bypass the strategy-host-only gate.
                var rules = CaptureDoctrineResearchRules(state, current, force, true) with { HostAvailable = true };
                for (int panel = 2; panel <= 79; panel++)
                {
                    if (panel is >= 74 and <= 79) continue;
                    if (DoctrineResearchSource.CanResearch(rules, rules.Baseline, panel) == 0) { research = true; break; }
                }
            }
            return PublishNormalTurnMenuFlags(state, current, mask,
                (promotion ? 0x01000000u : 0) | (research ? 0x08000000u : 0));
        }

        bool search;
        var areas = FinalizationTemporary(current);
        try
        {
            var selected = BeginAreaReportContent(state, current, pool).Select(areas);
            search = selected.First != 0 || selected.Second != 0;
        }
        finally { FinalizationRelease(current, areas); }
        state = PublishNormalTurnMenuFlags(state, current, 0x800, search ? 0x800u : 0);

        bool recruit = false;
        bool reward;
        var excluded = FinalizationTemporary(current);
        try
        {
            var commands = current.ReadOwnedPersonCommandManager() ?? throw FinalizationMissing("menu_command_manager");
            var talents = commands.Talents ?? throw FinalizationMissing("menu_Talent_owner");
            foreach (var entry in commands.DispatchOrder())
            {
                if (entry.Kind != PersonnelCommandKind.Talent) continue;
                var task = talents[entry.Id];
                if (!task.IsRecruitWait && task.Kind == TalentOrderKind.Recruit && task.SourceDistrictId == districtId)
                {
                    int target = task.TargetOfficerId ?? throw FinalizationMissing("recruit_task_target");
                    var pointer = current.ReadPersonLookupReference(target <= 6000 ? target : 6000)
                        ?? throw FinalizationMissing("recruit_target_lookup");
                    FinalizationAppend(current, excluded, new(RegistryKind.Person, pointer.PhysicalSlot));
                }
            }
            //2C4510 is the reward flag, and executes before the free-person scan.
            reward = false;
            foreach (int person in FinalizationRegistry(current, RegistryKind.Person))
            {
                if (!FinalizationNeed(current.ReadPersonNativeValidity(person), "reward_Person_validity")) continue;
                if ((current.ReadPersonForce(person) ?? throw FinalizationMissing("reward_Person_Force")) != player) continue;
                if (FinalizationNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, _governorInputs), "reward_E8") < 90 &&
                    IsRewardEligibleCurrent(state, current, person)) { reward = true; break; }
            }
            foreach (int person in FinalizationRegistry(current, RegistryKind.Person))
            {
                if (!FinalizationNeed(current.ReadPersonNativeValidity(person), "recruit_Person_validity") ||
                    FinalizationNeed(current.ReadPersonStatus(person), "recruit_E6") != 5 ||
                    !NormalTurnFreePersonRecruitable(state, current, person, force)) continue;
                if (current.ReadCurrentListValues(excluded).OfType<RegistryObjectReference>()
                    .Any(pointer => pointer.Kind == RegistryKind.Person && pointer.PhysicalSlot == person)) continue;
                recruit = true; break;
            }
        }
        finally { FinalizationRelease(current, excluded); }
        state = PublishNormalTurnMenuFlags(state, current, 0x3000,
            (recruit ? 0x1000u : 0) | (reward ? 0x2000u : 0));
        bool trade = FinalizationNeed(current.ReadDistrictNativeValidity(district), "trade_District_validity") &&
            NormalTurnForeignTradeAvailable(state, current, player, district, districtId);
        return PublishNormalTurnMenuFlags(state, current, 0x8000, trade ? 0x8000u : 0);
    }

    private bool NormalTurnFreePersonRecruitable(GameState state, SourceWorldContext current, int person, int force)
    {
        //The caller has already tested status5, so232460 is false and the
        //serving/captive branches of1EF770 are not reached.
        if (NormalTurnPersonAbsent(current, person)) return false;
        int rawDistrict = FinalizationNeed(current.ReadPersonDistrictSlot(person), "recruit_PersonE0");
        var district = current.ReadDistrictTableReference(rawDistrict < 52 ? rawDistrict : 0)
            ?? throw FinalizationMissing("recruit_person_District");
        int original = FinalizationNeed(current.ReadDistrictForceSlot(district), "recruit_original_Force");
        if (original is >= 46 and <= 51 || DuelAftermathSpecialPerson(current, person) ||
            (FinalizationNeed(current.ReadArmyReturnPersonFlags196(person), "recruit_15E") & 0x4000) != 0) return false;
        var hex = PersonReturnCommands.ResolvePersonPosition(state, current, person,
            state.NativeMapSeason?.ThirdDate ?? throw FinalizationMissing("recruit_date"));
        if (!hex.PhysicalSlot.HasValue || hex.PhysicalSlot >= 48400) return false;
        int area = FinalizationNeed(current.ReadHexAreaSlot(hex), "recruit_current_Hex_Area");
        var at = current.ReadAreaTableReference(area < 501 ? area : 0) ?? throw FinalizationMissing("recruit_current_Area");
        return FinalizationNeed(current.ReadAreaCurrentForceSlot(at), "recruit_current_Area_Force") == force;
    }

    private static bool NormalTurnPersonAbsent(SourceWorldContext current, int person)
    {
        int status = FinalizationNeed(current.ReadPersonStatus(person), "absent_E6");
        if (status is >= 1 and <= 4 &&
            PersonPositionRepair.DecodeFirstLocation(FinalizationNeed(current.ReadPersonFirstLocation(person), "absent_E2")) !=
            PersonPositionRepair.DecodeSecondLocation(FinalizationNeed(current.ReadPersonSecondLocation(person), "absent_E4"))) return true;
        var linked = current.ReadPersonLinkedEntity(person) ?? throw FinalizationMissing("absent_linked");
        if (!FinalizationNeed(current.ReadLinkedEntityNativeValidity(linked), "absent_linked_validity")) return false;
        var manager = current.ReadOwnedPersonCommandManager() ?? throw FinalizationMissing("absent_manager");
        var disappearances = manager.Disappearances ?? throw FinalizationMissing("absent_Disappear_owner");
        //232370 checks exactly v98==16 after the position check, not every
        //busy task. Other represented current command kinds are known false.
        int identity = FinalizationNeed(linked.Identity, "absent_associated_identity");
        if (disappearances.ContainsKey(identity)) return true;
        if (manager.DispatchOrder().Any(task => task.Id == identity)) return false;
        throw FinalizationMissing("absent_associated_kind");
    }

    //70EC60 followed by the caller's second6DFC80 allocation. Trade levels
    //are read only after actual geography, rank, books, treasury and executor
    //gates. A country that cannot append a pool node does not enable the menu.
    private bool NormalTurnForeignTradeAvailable(GameState state, SourceWorldContext current,
        ForceReference force, DistrictReference district, int districtId)
        => ReadForeignTradeAdmissionCurrent(state, current, force, district, districtId, readNotification: true).Notification;

    //70EC60 owns the ordered allowed/help pair. The normal-turn caller adds
    //a second target-list/technology test only when it requests notification.
    private (bool Allowed, int GameMessageId, bool Notification) ReadForeignTradeAdmissionCurrent(
        GameState state, SourceWorldContext current, ForceReference force, DistrictReference district,
        int districtId, bool readNotification)
    {
        if (ReadApplicationInformationRestriction(state) ?? throw FinalizationMissing("trade_application_selection")) return (false, 0x11171, false);
        int own = FinalizationNeed(current.ReadForcePhysicalSlot(force), "trade_Force_ordinal");
        var app = ReadApplicationGameplay();
        bool Eligible(int country) => PersonReturnPathSource.ReadForeignTradeEligibility(state, current, force, country, app);
        bool any = false;
        for (int raw = 0; raw < 5; raw++)
        {
            int country = FinalizationNeed((current.ReadStaticTableReference(0x85A20, raw)
                ?? throw FinalizationMissing("trade_country_table")).PhysicalSlot, "trade_country_ordinal");
            if (country != 0 && Eligible(country)) any = true;
        }
        if (!any) return (false, 0x5BDF, false);
        int rankRaw = FinalizationNeed(current.ReadMoraleWaveForceRank47(own), "trade_Force47");
        int rank = FinalizationNeed((current.ReadStaticTableReference(0x78490, rankRaw < 13 ? rankRaw : 0)
            ?? throw FinalizationMissing("trade_Rank_table")).PhysicalSlot, "trade_Rank_receiver");
        var ranks = _scenario.Snapshot.RankParameters?.Ranks ?? throw FinalizationMissing("trade_Rank_owner");
        if ((uint)rank >= (uint)ranks.Count) throw FinalizationMissing("trade_Rank_range");
        byte[] row = Convert.FromHexString(ranks[rank].RawBytesHex);
        if (row.Length <= 156) throw FinalizationMissing("trade_RankAC");
        if (row[156] > P("maximum-rank")) return (false, 0x5BE0, false);
        if (FinalizationNeed(current.ReadDistrictRawByte(districtId, 0x14), "trade_District14") < P("command-books")) return (false, 0x57C7, false);
        int ruler = FinalizationNeed((current.ReadPersonLookupReference(TalentRulerCurrent(current, force))
            ?? throw FinalizationMissing("trade_ruler_lookup")).PhysicalSlot, "trade_ruler_pointer");
        var home = BaseLookup.Find(FinalizationNeed(current.ReadPersonFirstLocation(ruler), "trade_rulerE2"), current);
        var treasury = BaseCurrencyAdditionSource.CurrencyCity(home, current);
        if (FinalizationNeed(current.ReadBaseResource(treasury, BaseResourceKind.Gold), "trade_Gold") < P("gold")) return (false, 0x5BE2, false);
        if (FinalizationNeed(current.ReadBaseResource(treasury, BaseResourceKind.Supplies), "trade_Food") < P("food")) return (false, 0x5BE3, false);
        if (FinalizationNeed(current.ReadBaseResource(home, BaseResourceKind.Troops), "trade_Troops") < P("troops")) return (false, 0x5BE4, false);
        bool executor = false;
        foreach (int person in NormalTurnServingPeople(current, own, districtId, false))
        {
            if (FinalizationNeed(OfficerHealthSourceAdapter.ReadByte(current, person), "trade_Person161") >= 2 ||
                (FinalizationNeed(current.ReadArmyReturnPersonFlags196(person), "trade_Person15E") & 1) != 0 ||
                NormalTurnPersonAbsent(current, person) || FinalizationNeed(current.ReadPersonStatus(person), "trade_PersonE6") == 1 ||
                DuelAftermathSpecialPerson(current, person) ||
                (FinalizationNeed(current.ReadArmyReturnPersonFlags196(person), "trade_Person15E4000") & 0x4000) != 0) continue;
            executor = true; break;
        }
        if (!executor) return (false, 0x5BE5, false);
        var manager = current.ReadOwnedPersonCommandManager() ?? throw FinalizationMissing("trade_command_manager");
        var caravans = manager.Caravans ?? throw FinalizationMissing("trade_caravan_owner");
        var first = FinalizationTemporary(current);
        bool hasCountries;
        try { Fill(first); hasCountries = FinalizationNeed(current.ReadTemporaryHexListCount(first), "trade_first_count") != 0; }
        finally { FinalizationRelease(current, first); }
        if (!hasCountries) return (false, 0x5BE6, false);
        if (!readNotification) return (true, 0x5BDE, false);
        var second = FinalizationTemporary(current);
        try
        {
            Fill(second);
            var progress = state.NativeArmySimulation?.Forces.GetValueOrDefault(own)?.ForeignTradeProgress
                ?? throw FinalizationMissing("trade_acquired_technology_owner");
            if (progress.AcquiredTechnologyLevels.Length != 5) throw FinalizationMissing("trade_technology_dimensions");
            var node = current.ReadHexListHead(second) ?? throw FinalizationMissing("trade_country_head");
            while (node.Identity is int cursor)
            {
                var pointer = current.ReadCurrentListPayload(cursor) as StaticTableReference ?? throw FinalizationMissing("trade_country_payload");
                int country = Math.Clamp(FinalizationNeed(pointer.PhysicalSlot, "trade_country_identity"), 0, 4);
                if (progress.AcquiredTechnologyLevels[country] < 4) return (true, 0x5BDE, true);
                node = current.ReadHexListNodeNext(node) ?? throw FinalizationMissing("trade_country_next");
            }
            return (true, 0x5BDE, false);
        }
        finally { FinalizationRelease(current, second); }

        void Fill(HexListHandle list)
        {
            current.ClearTemporaryHexList(list);
            for (int raw = 0; raw < 5; raw++)
            {
                var pointer = current.ReadStaticTableReference(0x85A20, raw) ?? throw FinalizationMissing("trade_country_table");
                int country = FinalizationNeed(pointer.PhysicalSlot, "trade_country_ordinal");
                if (country == 0 || !Eligible(country)) continue;
                if (caravans.Values.Any(task => task.SourceForceId == own && task.CountryId == country)) continue;
                if (FinalizationNeed(current.ReadRegistryLockEnabled(), "trade_append_lock")) current.EnterRegistryLock();
                try
                {
                    var node = current.AllocateHexListNode(list);
                    if (node.Identity is int cursor) current.WriteCurrentListPayload(cursor, pointer);
                }
                finally { if (FinalizationNeed(current.ReadRegistryLockEnabled(), "trade_append_unlock")) current.ExitRegistryLock(); }
            }
        }
        int P(string name)
        {
            var bank = state.NativeArmySimulation?.Parameters ?? throw FinalizationMissing("trade_parameters");
            if (bank.Language != Seed.Language || bank.Language is not ("TC" or "SC") ||
                bank.RuleVersion != SimulationRuleIdentity.ForLocale(bank.Language))
                throw FinalizationMissing("trade_rule_profile");
            string key = NormalTurnTradeAdmissionParameterPrefix + name;
            if (bank.Float32Values.ContainsKey(key)) throw FinalizationMissing("trade_integer_" + name);
            if (bank.Int32Values.TryGetValue(key, out int value)) return value;
            return NormalTurnTradeAdmissionStaticRules.TryGetValue(name, out int original) ? original
                : throw FinalizationMissing("trade_parameter_" + name);
        }
    }

    private static IEnumerable<int> NormalTurnServingPeople(SourceWorldContext current, int force, int district, bool wholeForce)
    {
        foreach (int person in FinalizationRegistry(current, RegistryKind.Person))
        {
            if (wholeForce ? FinalizationNeed(current.ReadPersonOldForceSlot(person), "serving_Force") != force :
                FinalizationNeed(current.ReadPersonDistrictSlot(person), "serving_District") != district) continue;
            if (FinalizationNeed(current.ReadPersonStatus(person), "serving_E6") is >= 1 and <= 4) yield return person;
        }
    }

    private static GameState PublishNormalTurnMenuFlags(GameState state, SourceWorldContext current, uint mask, uint set)
    {
        uint before = FinalizationNeed(current.ReadWorldTransferServiceFlags(), "menu_World_flags");
        uint after = (before & ~mask) | (set & mask);
        current.RawTransferServiceFlags16A8 = after;
        var native = state.NativeArmySimulation ?? throw FinalizationMissing("menu_saved_owner");
        return state with { NativeArmySimulation = native with { Parameters = native.Parameters with
            { Int32Values = new(native.Parameters.Int32Values) { [OwnedArmyReturnServiceInputs.Flags16A8Key] = unchecked((int)after) } } } };
    }
}
