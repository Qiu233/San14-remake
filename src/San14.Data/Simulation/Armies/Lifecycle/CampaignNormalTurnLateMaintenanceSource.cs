using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The caller owns all action-table guards, notably action52's monthStart.
    //These leaves borrow the already completed Turn's current and node pool.
    internal GameState ApplyNormalTurnLateMaintenanceCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int action)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw LateMaintenanceMissing("shared_pool");
        return action switch
        {
            49 => SynchronizeNormalTurnAdvisorHealthCurrent(state, current),
            50 => SettleNormalTurnMeritCurrent(state, current),
            51 => ActivateNormalTurnResearchCurrent(state, current),
            52 => RefreshPersonnelHireTemplatesCurrent(state, current),
            53 => ReclaimPersonnelHireReleasesCurrent(state, current, pool),
            54 => ClearNormalTurnPersonFlagsCurrent(state, current),
            55 => RefreshNormalTurnDistrictBooksCurrent(state, current),
            56 => PrepareCoalitionPreferenceCurrent(state, current),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }

    private GameState SynchronizeNormalTurnAdvisorHealthCurrent(GameState state, SourceWorldContext current)
    {
        uint flags = LateNeed(current.ReadWorldTransferServiceFlags(), "World16A8");
        if ((flags & 0x100) != 0) return state; //333EF0 is a World bit, not a tutorial-service query.
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw LateMaintenanceMissing("player_Force");
        if (!LateNeed(current.ReadForceNativeValidity(player), "player_Force_validity")) return state;
        int force = LateNeed(current.ReadForcePhysicalSlot(player), "player_Force60");
        int raw = LateNeed(current.ReadAssignmentForceAdvisor52(force), "player_Force52");
        int person = LateLookup(current, raw);
        //The source directly reads161 on this lookup; it does not substitute
        //Force10's ruler or add a second Person-validity gate.
        bool sick = LateNeed(OfficerHealthSourceAdapter.ReadByte(current, person), "advisor_Health161") != 0;
        uint after = flags & ~0x100000u;
        if (sick) after |= 0x100000;
        current.RawTransferServiceFlags16A8 = after;
        var simulation = state.NativeArmySimulation ?? throw LateMaintenanceMissing("World_flags_owner");
        if (!simulation.Parameters.Int32Values.ContainsKey(OwnedArmyReturnServiceInputs.Flags16A8Key))
            throw LateMaintenanceMissing("World_flags_saved_owner");
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with {
            Int32Values = new(simulation.Parameters.Int32Values) { [OwnedArmyReturnServiceInputs.Flags16A8Key] = unchecked((int)after) } } } };
    }

    private GameState ClearNormalTurnPersonFlagsCurrent(GameState state, SourceWorldContext current)
    {
        var people = new Dictionary<int, OfficerState>(state.Officers);
        foreach (int physical in LateRegistry(current, RegistryKind.Person))
        {
            ushort after = (ushort)(LateNeed(current.ReadArmyReturnPersonFlags196(physical), "clear_Person15E") & 0x7FFC);
            current.WriteArmyReturnPersonFlags196(physical, after);
            int id = LateNeed(current.ReadPersonIdentity(physical), "clear_Person10");
            var person = people.GetValueOrDefault(id) ?? throw LateMaintenanceMissing("clear_Person_owner");
            people[id] = person with { Behavior = OfficerBehaviorSourceAdapter.WithSourceFlags(person.Behavior, after) };
        }
        return state with { Officers = people };
    }

    private GameState SettleNormalTurnMeritCurrent(GameState state, SourceWorldContext current)
    {
        var promoted = new List<int>();
        //2DB630 settles accumulated merit only after its actual associated
        //command elapsed and Army action gates. Stage0 is not rewritten.
        foreach (int physical in LateRegistry(current, RegistryKind.Person))
        {
            var force = current.ReadPersonForce(physical) ?? throw LateMaintenanceMissing("merit_Force");
            if (!LateNeed(current.ReadForceNativeValidity(force), "merit_Force_validity")) continue;
            if (LateNeed(current.ReadPersonStatus(physical), "merit_PersonE6") == 1 || DuelAftermathSpecialPerson(current, physical) ||
                (LateNeed(current.ReadArmyReturnPersonFlags196(physical), "merit_Person15E") & 0x4000) != 0) continue;
            int id = LateNeed(current.ReadPersonIdentity(physical), "merit_Person10");
            var person = state.Officers.GetValueOrDefault(id) ?? throw LateMaintenanceMissing("merit_Person_owner");
            var merit = person.MeritState ?? throw LateMaintenanceMissing("merit_fields");
            int stage = LateNeed(OfficerMeritSourceAdapter.EncodeStage(merit), "merit_stage1BC");
            if (stage == 0) continue;
            var linked = current.ReadPersonLinkedEntity(physical) ?? throw LateMaintenanceMissing("merit_associated");
            if (LateNeed(current.ReadLinkedEntityNativeValidity(linked), "merit_associated_validity") &&
                LateAssociatedElapsedCurrent(state, current, linked) != 0) continue;
            var army = PersonArmyLocationSource.ReadCommanderArmy(current, physical) ?? throw LateMaintenanceMissing("merit_Army");
            if (LateNeed(current.ReadArmyUnitNativeValidity(army), "merit_Army_validity"))
            {
                var image = NotificationArmy(state, LateNeed(army.PhysicalSlot, "merit_Army_pointer"));
                if (LateNeed(ArmyConfigurationSourceAdapter.ReadAction(state, image), "merit_Army37") != 0) continue;
            }
            if (LateNeed(ReadApplicationInformationRestriction(state), "merit_application_mode"))
            {
                Write(person, BoundMerit(CreditRanks()[2].SignedThreshold), 0, 2, 0);
                continue;
            }
            int pending = LateNeed(OfficerMeritSourceAdapter.EncodePendingMerit(merit), "merit_pending1C0") / 10;
            if (LateNeed(current.ReadPersonStatus(physical), "merit_captive_status") == 6)
                pending = unchecked(LateMeritParameter(state, "captive-percent", 50) * pending) / 100;
            int after = BoundMerit(unchecked(LateNeed(OfficerMeritSourceAdapter.EncodeMerit(merit), "merit_signed1BE") + pending));
            int? reset = merit.ResetCode;
            if (stage == 1 && after >= CreditRanks()[2].SignedThreshold)
            {
                stage = 2; reset = 0;
                if (TalentForceIsPlayerCurrent(state, current, current.ReadPersonForce(physical) ?? throw LateMaintenanceMissing("merit_promoted_Force"))) promoted.Add(id);
            }
            Write(person, after, 0, stage, reset);
        }
        //2F5EE0 independently visits all remaining stage1 people, including
        //those whose accumulated-merit settlement was skipped above.
        foreach (int physical in LateRegistry(current, RegistryKind.Person))
        {
            if (LateNeed(current.ReadPersonStatus(physical), "stage1_PersonE6") == 1 || DuelAftermathSpecialPerson(current, physical) ||
                (LateNeed(current.ReadArmyReturnPersonFlags196(physical), "stage1_Person15E") & 0x4000) != 0) continue;
            int id = LateNeed(current.ReadPersonIdentity(physical), "stage1_Person10");
            var person = state.Officers.GetValueOrDefault(id) ?? throw LateMaintenanceMissing("stage1_Person_owner");
            var merit = person.MeritState ?? throw LateMaintenanceMissing("stage1_merit_fields");
            if (LateNeed(OfficerMeritSourceAdapter.EncodeStage(merit), "stage1_stage1BC") != 1) continue;
            if (LateNeed(ReadApplicationInformationRestriction(state), "stage1_application_mode"))
            { Write(person, BoundMerit(CreditRanks()[2].SignedThreshold), 0, 2, 0); continue; }
            int reset = unchecked((byte)(LateNeed(OfficerMeritSourceAdapter.EncodeResetCode(merit), "stage1_reset1C4") + 1));
            int before = LateNeed(OfficerMeritSourceAdapter.EncodeMerit(merit), "stage1_merit1BE");
            if (reset >= LateMeritParameter(state, "stage1-periods", 36))
            {
                Write(person, Math.Max(before, CreditRanks()[2].SignedThreshold), merit.PendingMerit, 2, 0);
                if (TalentForceIsPlayerCurrent(state, current, current.ReadPersonForce(physical) ?? throw LateMaintenanceMissing("stage1_promoted_Force"))) promoted.Add(id);
            }
            else Write(person, before, merit.PendingMerit, 1, reset);
        }
        //334D80 always consumes its local list; World bit8 suppresses text,
        //not settlement.319500's strict identity tie gives this total order.
        if (promoted.Count != 0 && (LateNeed(current.ReadWorldTransferServiceFlags(), "promotion_World16A8") & 0x100) == 0)
        {
            promoted = promoted.OrderByDescending(id => LateNeed(state.Officers[id].MeritState?.Merit, "promotion_sort_merit"))
                .ThenBy(id => LateNeed(current.ReadPersonStatus(LateNeed(current.ResolveOfficerSource(id), "promotion_sort_pointer")), "promotion_sort_E6"))
                .ThenBy(id => id).ToList();
            var text = FormatNormalTurnReport(state, current, 0x5D21 + Math.Min(promoted.Count - 1, 3),
                new(Integers: [promoted.Count], Person2: promoted[0], Person3: promoted.Count >= 2 ? promoted[1] : null,
                    Person4: promoted.Count >= 3 ? promoted[2] : null));
            state = text.State;
            var pending = state.CampaignEffectRequests ?? throw LateMaintenanceMissing("promotion_outbox");
            state = state with { CampaignEffectRequests = pending.Append(CampaignEffectSource.MonthlyMaintenance,
                state.NativeClock?.UpdateOrdinal ?? throw LateMaintenanceMissing("promotion_revision"),
                [new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.NarrationText, null, null, text.Text) }]) };
        }
        return state;

        int BoundMerit(int value)
        {
            int selector = LateNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "merit_bound_selector");
            int setting = LateNeed(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x4C4 : 0x128), "merit_lower_setting");
            int lower = CreditRanks()[setting == 1 ? 2 : 1].SignedThreshold;
            return unchecked((short)(value < lower ? lower : Math.Min(value, CreditRanks()[6].SignedThreshold)));
        }
        void Write(OfficerState person, int merit, int? pending, int stage, int? reset)
        {
            var before = person.MeritState ?? throw LateMaintenanceMissing("write_merit_owner");
            state = state with { Officers = new(state.Officers) { [person.Id] = person with {
                MeritState = new(merit, pending, stage, before.FirstSelectionCount, before.SecondSelectionCount, reset) } } };
        }
    }

    private GameState ActivateNormalTurnResearchCurrent(GameState state, SourceWorldContext current)
    {
        if (LateNeed(ReadApplicationInformationRestriction(state), "research_application_mode")) return state;
        for (int slot = 0; slot < 52; slot++)
        {
            var reference = current.ReadForceTableReference(slot) ?? throw LateMaintenanceMissing("research_Force_pointer");
            if (!LateNeed(current.ReadForceNativeValidity(reference), "research_Force_validity")) continue;
            int forceId = LateNeed(current.ReadForcePhysicalSlot(reference), "research_Force60");
            if (forceId == 0 || forceId == 51 || forceId is >= 46 and <= 50) continue;
            var force = state.Forces.GetValueOrDefault(forceId) ?? throw LateMaintenanceMissing("research_Force_owner");
            var policy = force.PolicyEffects ?? throw LateMaintenanceMissing("research_current_panels");
            var suppressed = policy.SuppressedEffectIds ?? throw LateMaintenanceMissing("research_pending");
            bool facilityCapacity = suppressed.Contains(62);
            state = state with { Forces = new(state.Forces) { [forceId] = force with { PolicyEffects = new(policy.ActiveEffectIds, []) } } };
            if (!facilityCapacity) continue;
            _baseCapacityInputs.BindCurrent(state, current);
            foreach (var obj in current.ReadRegisteredObjectReferences() ?? throw LateMaintenanceMissing("research_Object_registry"))
            {
                if (obj is null) throw LateMaintenanceMissing("research_Object_receiver");
                if (LateNeed(current.ReadObjectForceSlot13(obj), "research_Object13") != forceId) continue;
                if (!LateNeed(current.ReadObjectNativeValidity(obj), "research_Object_validity")) continue;
                var fields = current.ReadPhysicalObject(LateNeed(obj.PhysicalSlot, "research_Object_pointer")) ?? throw LateMaintenanceMissing("research_Object_record");
                var definition = current.ReadObjectDefinitionReference(fields.RawField12 < 31 ? fields.RawField12 : 0) ?? throw LateMaintenanceMissing("research_Object_definition");
                if (!LateNeed(current.ReadObjectDefinitionValidity(definition), "research_definition_validity") ||
                    LateNeed(current.ReadObjectDefinitionRaw2C(definition), "research_definition_category") != 2) continue;
                int basis = LateNeed(current.ReadObjectDefinitionBaseDurability(definition), "research_definition_HP");
                if (basis == 0) throw LateMaintenanceMissing("research_zero_HP_divisor");
                int capacity = LateNeed(FacilityRepairCapacity.ReadCapacity(state, current, obj, _baseCapacityInputs), "research_current_capacity");
                int hp = LateNeed(current.ReadObjectRawField14(obj), "research_Object14");
                int after = Math.Min(unchecked(hp * capacity) / basis, capacity);
                current.WriteObjectPostLoadField14(obj, after);
                state = PublishCurrentObjectDurability(state, current, obj, LateNeed(current.ReadObjectRawField14(obj), "research_stored_HP"));
            }
        }
        return state;
    }

    private GameState RefreshNormalTurnDistrictBooksCurrent(GameState state, SourceWorldContext current)
    {
        var query = QueryDistrictCommandBudgets(state, current);
        if (!query.Available) throw LateMaintenanceMissing(query.UnresolvedInput ?? "District_books_quote");
        foreach (var quote in query.Districts!)
        {
            var district = state.Districts.GetValueOrDefault(quote.DistrictId) ?? throw LateMaintenanceMissing("District_owner");
            var native = state.NativeForceUnregisterCurrent ?? throw LateMaintenanceMissing("District_window_owner");
            var window = native.DistrictWindows10.GetValueOrDefault(quote.DistrictId)?.ToArray() ?? throw LateMaintenanceMissing("District_window");
            if (window.Length < 5) throw LateMaintenanceMissing("District_window_format");
            byte books = (byte)Math.Clamp(quote.Books, 0, 200);
            current.SetDistrictRawByte(quote.DistrictId, 0x14, books); window[4] = books;
            state = state with { Districts = new(state.Districts) { [quote.DistrictId] = district with { OrdersRemaining = books } },
                NativeForceUnregisterCurrent = native with { DistrictWindows10 = new(native.DistrictWindows10) { [quote.DistrictId] = window } } };
            if (LateNeed(ReadApplicationInformationRestriction(state), "books_application_mode") &&
                BaseCaptureRewardSource.ReadPlayerRulerDistrict(current,
                    current.ReadDistrictTableReference(quote.DistrictId) ?? throw LateMaintenanceMissing("books_District_pointer"), LateMaintenanceMissing))
                state = ApplicationCommandAccountingSource.Grant(state, current,
                    LateNeed(quote.AccountingGrantAmount, "books_accounting_grant_amount"),
                    ReadApplicationGameplay()?.UserSelectionServicePresent);
        }
        return state;
    }

    private static IEnumerable<int> LateRegistry(SourceWorldContext current, RegistryKind kind)
    {
        foreach (var entry in current.ReadRegistryReferences(kind) ?? throw LateMaintenanceMissing("registry_" + kind))
        {
            if (entry is null || entry.Kind != kind) throw LateMaintenanceMissing("registry_payload_" + kind);
            yield return LateNeed(entry.PhysicalSlot, "registry_receiver_" + kind);
        }
    }
    private static int LateLookup(SourceWorldContext current, int raw) => LateNeed((raw <= 6000
        ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
    private static int LateAssociatedElapsedCurrent(GameState state, SourceWorldContext current, PersonLinkedEntityReference linked)
    {
        var commands = current.ReadOwnedPersonCommandManager() ?? throw LateMaintenanceMissing("associated_manager");
        int id = LateNeed(linked.Identity, "associated_receiver");
        //Both strategic command classes retain22E2C0 as their real vB0.
        NativeMapSeasonDate? start = commands.Strategies?.GetValueOrDefault(id)?.StartDate
            ?? commands.Independences?.GetValueOrDefault(id)?.StartDate;
        if (start is null) return CreditAssociatedElapsedCurrent(state, current, linked);
        return unchecked(PersonReturnCommands.CalendarOrdinal(state.NativeMapSeason?.ThirdDate ?? throw LateMaintenanceMissing("associated_World34")) -
            PersonReturnCommands.CalendarOrdinal(start));
    }
    private static int LateMeritParameter(GameState state, string name, int tcValue)
    {
        var parameters = state.NativeArmySimulation?.Parameters ?? throw LateMaintenanceMissing("merit_parameters");
        string key = "simulation.normal-turn.merit." + name;
        if (parameters.Float32Values.ContainsKey(key)) throw LateMaintenanceMissing("integer_parameter_" + name);
        if (parameters.Int32Values.TryGetValue(key, out int saved)) return saved;
        if (parameters.Language == "TC") return tcValue; //File-backed1A4AFE8=50/1A4AFF4=36.
        if (parameters.Language == "SC") return name switch
        {
            // Independently sourced SC signed32 rules. The first is read by
            // the captive pending-merit consumer; the second by the stage1
            // elapsed-period promotion consumer. Never infer one from TC.
            "captive-percent" => 50, //SC1A4C018, read at2DB694.
            "stage1-periods" => 36, //SC1A4C024, read at2F5F13.
            _ => throw LateMaintenanceMissing("locale_parameter_" + name)
        };
        throw LateMaintenanceMissing("locale_parameter_" + name);
    }
    private static T LateNeed<T>(T? value, string name) where T : struct => value ?? throw LateMaintenanceMissing(name);
    private static InvalidDataException LateMaintenanceMissing(string name) => new("normal-turn.late-maintenance." + name);
}
