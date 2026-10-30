using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class EditedEventResultsCurrent
    {
        private WorldIndexContext ForceResultPool => current.OwnedIndexes ?? throw Missing("force_result_current_pool");

        private int ForceResult(int code, IReadOnlyList<int> arguments) => code switch
        {
            388 => Surrender(arguments),
            389 => ChangeLord(arguments),
            391 => ForceMorale(arguments),
            392 => Friendship(arguments),
            393 => Alliance(arguments),
            394 => BreakAlliance(arguments),
            399 => ExpandCity(arguments),
            _ => throw Missing("force_result_opcode")
        };

        private ForceReference EditedForce(int selector) => Selectors.Force(selector);
        private int EditedForcePerson(int selector) => Selectors.Person(selector);
        private int ForceSlot(ForceReference force) => Need(current.ReadForcePhysicalSlot(force), "force_result_Force60");
        private bool ForceValid(ForceReference force) => Need(current.ReadForceNativeValidity(force), "force_result_Force_validity");
        private IEnumerable<int> EditedForceArmies(ForceReference force)
        {
            int selected = ForceSlot(force);
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw Missing("force_result_Army60"))
            {
                int physical = Need(entry?.PhysicalSlot, "force_result_Army_payload");
                if (entry!.Kind != RegistryKind.Army) throw Missing("force_result_Army_kind");
                if (selected is >= 1 and <= 51 && ForceSlot(current.ReadArmyUnitForceReference(new(physical))) == selected)
                    yield return physical;
            }
        }

        private int ForceMorale(IReadOnlyList<int> a)
        {
            var force = EditedForce(a[0]);
            if (!ForceValid(force)) return 0;
            int operand = a[1], mode = a[2];
            // Preserve the source mutable operand: mode3 negates it for EACH
            // reached Base and then each Army, including its alternating sign.
            foreach (var entry in current.ReadRegisteredBases() ?? throw Missing("morale_Base80"))
            {
                var at = entry ?? throw Missing("morale_Base_payload");
                int selected = ForceSlot(force);
                if (selected is < 1 or > 51 || Need(current.ReadBaseForceSlot(at), "morale_Base_Force") != selected) continue;
                if (mode is not (1 or 2 or 3)) continue;
                int request = operand;
                if (mode != 1)
                {
                    if (mode == 3) operand = unchecked(-operand);
                    int cap = BasePostLoad.ReadMoraleUpper(force, current);
                    request = EditedEventResultExecution.Bound(unchecked(Need(current.ReadBaseMorale44(at), "morale_Base44") + operand), cap);
                }
                if (!BasePostLoad.WriteMoraleCurrent(at, request, current)) continue;
                byte value = Need(current.ReadBaseMorale44(at), "morale_Base44_after");
                int physical = Need(at.PhysicalSlot, "morale_Base_physical");
                if (at.Kind == BaseKind.City)
                {
                    var owner = State.Cities.GetValueOrDefault(physical) ?? throw Missing("morale_City_owner");
                    var runtime = owner.NativeReturnRuntime ?? throw Missing("morale_City_runtime");
                    State = State with { Cities = new(State.Cities) { [physical] = owner with { Morale = value,
                        NativeReturnRuntime = runtime with { Morale44 = value } } } };
                }
                else
                {
                    int id = physical + 51;
                    var owner = State.Gates.GetValueOrDefault(id) ?? throw Missing("morale_Gate_owner");
                    var runtime = owner.NativeCombatRuntime ?? throw Missing("morale_Gate_runtime");
                    State = State with { Gates = new(State.Gates) { [id] = owner with
                        { NativeCombatRuntime = runtime with { Morale44 = value } } } };
                    var simulation = State.NativeArmySimulation ?? throw Missing("morale_parameters");
                    State = State with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
                        { Int32Values = new(simulation.Parameters.Int32Values) { [ArmyMoraleSource.BaseMorale44Key(at)] = value } } } };
                }
            }
            foreach (int physical in EditedForceArmies(force))
            {
                if (mode is not (1 or 2 or 3)) continue;
                var receiver = new ArmyUnitReference(physical);
                int request = operand;
                if (mode != 1)
                {
                    if (mode == 3) operand = unchecked(-operand);
                    int cap = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(receiver), current);
                    int getter = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(receiver), current);
                    byte raw = Need(current.ReadArmyReturnResourceFields(receiver)?.Morale1A, "morale_Army1A");
                    request = EditedEventResultExecution.Bound(unchecked(Math.Min(raw, getter) + operand), cap);
                }
                byte after = ArmyMoraleSource.SetByte2419A0(request,
                    BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(receiver), current));
                current.WriteArmyReturnMorale(physical, after);
                State = rules.WriteEditedEventArmyMorale(State, physical, after);
            }
            return 1;
        }

        private int Friendship(IReadOnlyList<int> a)
        {
            var first = EditedForce(a[0]); var second = EditedForce(a[1]);
            if (!ForceValid(first) || !ForceValid(second) || first == second) return 0;
            int other = ForceSlot(second);
            int before = other < 52 ? rules.ReadEditedEventFriendship(State, current, first, other) : 0;
            State = rules.ApplyNormalTurnForceRelationCurrent(State, current, ForceResultPool, first, second, unchecked(a[2] - before));
            State = rules.SynchronizeDiplomacyProjection(State);
            return 1;
        }

        private int Surrender(IReadOnlyList<int> a)
        {
            if (State.EditedEventForceAbsorptionTail is { } resumed)
            {
                if (HasPendingForceAbsorptionCurrent(State))
                    State = rules.ResumeNormalTurnForceAbsorptionCurrent(State, current, ForceResultPool,
                        new(null, null, new(null, null, CampaignHistoryPlaybackControl.ReadEmperorReceptionChronicleSuppression(State))),
                        eventPlaybackPresent: true);
                if (HasPendingForceAbsorptionCurrent(State)) return 0;
                return CompleteSurrenderTail(resumed.RestoreRecordingGate);
            }
            var from = EditedForce(a[0]); var to = EditedForce(a[1]);
            if (!ForceValid(from) || !ForceValid(to) || from == to) return 0;
            if (rules.TalentForceIsPlayerCurrent(State, current, from)) return 0;
            var owner = State.CampaignEventEligibility ?? throw Missing("surrender_event_manager");
            current.ReportBlockerState = new(true, 1);
            State = State with { CampaignEventEligibility = owner with { HistoryRecordingSuppressed = true } };
            State = rules.ApplyNormalTurnForceAbsorptionCurrent(State, current, ForceResultPool, from, to,
                emperorInputs: new(null, null, new(null, null, CampaignHistoryPlaybackControl.ReadEmperorReceptionChronicleSuppression(State))),
                eventPlaybackPresent: true);
            if (HasPendingForceAbsorptionCurrent(State))
            {
                State = State with { EditedEventForceAbsorptionTail = new(ForceSlot(from), ForceSlot(to), true) };
                return 0; // The result evaluator keeps this actual leaf incomplete.
            }
            return CompleteSurrenderTail(restoreRecordingGate: true);
        }

        private int CompleteSurrenderTail(bool restoreRecordingGate)
        {
            var effects = new List<CampaignEffectRequest>();
            State = rules.RetireDiplomacyAreaObjects(State, current, ForceResultPool, effects);
            if (restoreRecordingGate)
            {
                State = State with { CampaignEventEligibility = (State.CampaignEventEligibility ?? throw Missing("surrender_event_manager_after"))
                    with { HistoryRecordingSuppressed = false } };
                current.ReportBlockerState = new(true, 0);
            }
            State = RefreshEditedEventRegionStatistics(State, current);
            State = AppendEditedForceResultEffects(State, effects);
            State = PublishNormalTurnCurrent(State, current, ForceResultPool) with { EditedEventForceAbsorptionTail = null };
            return 1;
        }

        private int Alliance(IReadOnlyList<int> a)
        {
            var first = EditedForce(a[0]); var second = EditedForce(a[1]);
            if (!ForceValid(first) || !ForceValid(second) || first == second) return 0;
            int own = ForceSlot(first), other = ForceSlot(second);
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(State, current) ?? throw Missing("alliance_player");
            int selected = ForceSlot(player);
            byte Flags(int id) => Need(State.NativeArmySimulation?.Forces.GetValueOrDefault(id)?.Flags12, "alliance_Force12");
            if ((selected == own && (Flags(other) & 2) != 0) || (selected == other && (Flags(own) & 2) != 0)) return 0;
            int old = ReadReportForceRelationByte(State, first, other, Missing) & 127;
            //A3=0 replaces; nonzero permits only a strict extension.
            bool writes = a[3] == 0 || a[2] > old;
            if (writes) State = WriteEditedEventAlliance(State, current, own, other, a[2]);
            State = rules.MaintainEditedEventAppointments(State, current);
            if (old == 0 && (ReadReportForceRelationByte(State, first, other, Missing) & 127) != 0)
            {
                _ = DAllianceDistrictObjectives(State, current, own, other);
                State = PersonAssignmentPrecallInputs.PublishCurrent(State, current);
                State = PublishEditedEventDistrictWindows(State, current);
            }
            State = rules.SynchronizeDiplomacyProjection(State);
            return 1;
        }

        private int BreakAlliance(IReadOnlyList<int> a)
        {
            var first = EditedForce(a[0]); var second = EditedForce(a[1]);
            if (!ForceValid(first) || !ForceValid(second) || first == second) return 0;
            if ((ReadReportForceRelationByte(State, first, ForceSlot(second), Missing) & 127) != 0)
            {
                var maintenance = new MonthlyCampaignMaintenanceSource(rules, State, current, ForceResultPool);
                State = maintenance.ApplyEditedEventAllianceEnd(first, second);
            }
            State = RefreshEditedEventRegionStatistics(State, current);
            State = rules.MaintainEditedEventAppointments(State, current);
            State = rules.SynchronizeDiplomacyProjection(State);
            State = PublishNormalTurnCurrent(State, current, ForceResultPool);
            return 1;
        }



        private int ExpandCity(IReadOnlyList<int> a)
        {
            var city = Selectors.City(a[0]);
            if (!Need(current.ReadBaseNativeValidity(city), "expansion_City_validity")) return 0;
            int before = Need(current.ReadCitySizeB8(city), "expansion_CityB8"), requested = a[1];
            if (requested <= before) return 0;
            int physical = Need(city.PhysicalSlot, "expansion_City_receiver");
            current.SetCitySizeB8(physical, (byte)Math.Clamp(requested, 0, 4));
            var owner = State.Cities.GetValueOrDefault(physical) ?? throw Missing("expansion_City_owner");
            State = State with { Cities = new(State.Cities) { [physical] = owner with { Size = (CitySize)(Math.Clamp(requested, 0, 4) - 1) } } };
            int raw = Need(current.ReadBaseForceSlot(city), "expansion_Force");
            var force = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw Missing("expansion_Force_pointer");
            if (ForceValid(force) && ForceSlot(force) != 0 && ForceSlot(force) is not (>= 46 and <= 51) && requested is 2 or 3 or 4)
                State = RulerCaptureReleaseSource.ApplyCurrentPopularityAdjustment(State, current, ForceSlot(force),
                    rules.ReadEditedEventExpansionParameter(State, requested, false));
            State = rules.ApplyEditedEventCityExpansionDevelopment(State, current, city, before, requested);
            byte tier = BasePostLoad.ReadFlag66(city, current);
            current.SetBasePostLoadFlag66(city, tier);
            State = State with { Cities = new(State.Cities) { [physical] = State.Cities[physical] with { DamageTier = tier } } };
            return 1;
        }
    }

    private GameState WriteEditedEventArmyMorale(GameState state, int physical, byte morale)
    {
        var image = NotificationArmy(state, physical);
        image = ArmyMoraleSourceAdapter.WithCurrent(image, morale);
        if (state.Units.ContainsKey(image.Id)) return state with { Units = new(state.Units) { [image.Id] = image } };
        var arena = state.ReadArmyLifecycle() ?? throw EditedForceMissing("morale_lifecycle");
        var slot = arena.ReceiverSlots()[physical].Receiver;
        if (slot.PendingImage?.Id != image.Id) throw EditedForceMissing("morale_pending_receiver");
        return state.WithArmyLifecycle(arena with { Receivers = arena.Receivers.With(slot with { PendingImage = image }) });
    }
    private int ReadEditedEventFriendship(GameState state, SourceWorldContext current, ForceReference force, int other)
    {
        int physical = EditedForceNeed(current.ReadForcePhysicalSlot(force), "friendship_Force");
        var values = physical == 0 ? state.NativeForceUnregisterCurrent?.PlaceholderRelations13 :
            state.NativeArmySimulation?.Forces.GetValueOrDefault(physical)?.NormalAftermath?.Relations13;
        return values is { Length: 52 } ? values[other] : throw EditedForceMissing("friendship_Relations13");
    }
    private static GameState WriteEditedEventAlliance(GameState state, SourceWorldContext current, int first, int second, int months)
    {
        foreach (var (self, other) in new[] { (first, second), (second, first) })
        {
            var receiver = current.ReadForceTableReference(self) ?? throw EditedForceMissing("alliance_Force_pointer");
            byte previous = ReadReportForceRelationByte(state, receiver, other, EditedForceMissing);
            state = AbsorptionWriteRelation(state, current, self, other, true,
                (byte)((previous & 128) | Math.Clamp(months, 0, 48)));
        }
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw EditedForceMissing("alliance_player");
        int playerId = EditedForceNeed(current.ReadForcePhysicalSlot(player), "alliance_player60");
        if (unchecked((uint)(months - 1)) > 2 && playerId != 0 && (playerId == first || playerId == second))
        {
            int other = playerId == first ? second : first;
            state = AbsorptionWriteRelation(state, current, playerId, other, true,
                (byte)(ReadReportForceRelationByte(state, player, other, EditedForceMissing) & 127));
        }
        return state;
    }
    private static GameState PublishEditedEventDistrictWindows(GameState state, SourceWorldContext current)
    {
        var owner = state.NativeForceUnregisterCurrent ?? throw EditedForceMissing("District_windows_owner");
        var windows = new Dictionary<int, byte?[]>(owner.DistrictWindows10);
        foreach (int slot in windows.Keys)
            windows[slot] = Enumerable.Range(0x10, 28).Select(at => current.ReadDistrictRawByte(slot, at)).ToArray();
        return state with { NativeForceUnregisterCurrent = owner with { DistrictWindows10 = windows } };
    }
    private GameState MaintainEditedEventAppointments(GameState state, SourceWorldContext current)
    {
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.City) ?? throw EditedForceMissing("appointments_City90"))
            MaintainDistrictAdministrationAppointmentsCurrent(current, new(BaseKind.City,
                EditedForceNeed(entry?.PhysicalSlot, "appointments_City_payload")));
        state = PersonAssignmentPrecallInputs.PublishCurrent(state, current);
        state = OwnedArmyReturnCurrentFields.PublishCurrent(state, current);
        return state;
    }
    private GameState ApplyEditedEventCityExpansionDevelopment(GameState state, SourceWorldContext current, BaseReference city, int before, int after)
        => ApplyCitySizeDevelopmentCurrent(state, current, city, before, after, allowShrink: false).State;
    private int ReadEditedEventExpansionParameter(GameState state, int size, bool levels)
    {
        string name = size switch { 2 => "middle", 3 => "large", 4 => "giant", _ => throw EditedForceMissing("expansion_size_parameter") };
        string key = "simulation.normal-turn.city-expansion." + name + (levels ? "-levels" : "-popularity");
        var bank = state.NativeArmySimulation?.Parameters ?? throw EditedForceMissing("expansion_parameters");
        if (bank.Float32Values.ContainsKey(key)) throw EditedForceMissing("expansion_parameter_type");
        if (bank.Int32Values.TryGetValue(key, out int value)) return value;
        if (Seed.Language == "SC")
        {
            //SC279CD0/2A0DF0 independently read level6/9/12 and popularity3/10/20.
            //These use the SC sources already consumed by normal City expansion.
            return levels ? size switch { 2 => 6, 3 => 9, _ => 12 } : size switch { 2 => 3, 3 => 10, _ => 20 };
        }
        if (Seed.Language != "TC") throw EditedForceMissing("expansion_locale_parameter");
        return levels ? size switch { 2 => 6, 3 => 9, _ => 12 } : size switch { 2 => 3, 3 => 10, _ => 20 };
    }
    private static GameState RefreshEditedEventRegionStatistics(GameState state, SourceWorldContext current)
    {
        //33E2E0 is an explicit mutation after absorption/break-alliance. Its
        //full Hex traversal is source gameplay work, never a query-time proof.
        var counts = new int[501];
        for (int slot = 0; slot < 48400; slot++)
        {
            var hex = current.ReadHexTableReference(slot) ?? throw EditedForceMissing("statistics_Hex");
            int raw = EditedForceNeed(current.ReadHexAreaSlot(hex), "statistics_Hex12");
            var area = current.ReadAreaTableReference(raw < 501 ? raw : 0) ?? throw EditedForceMissing("statistics_Area");
            if (!EditedForceNeed(current.ReadAreaNativeValidity(area), "statistics_Area_validity")) continue;
            int force = EditedForceNeed(current.ReadAreaCurrentForceSlot(area), "statistics_Area_Force");
            if (force is < 1 or > 51 || EditedForceNeed(current.ReadHexForceSlot(hex), "statistics_Hex14") != force) continue;
            int physical = EditedForceNeed(area.PhysicalSlot, "statistics_Area_receiver");
            counts[physical] = unchecked(counts[physical] + 1);
        }
        var saved = state.RegionStatistics ?? throw EditedForceMissing("statistics_owner");
        var entries = new Dictionary<int, AreaMapStatisticsEntry>(saved.Entries);
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Area) ?? throw EditedForceMissing("statistics_AreaB0"))
        {
            int physical = EditedForceNeed(entry?.PhysicalSlot, "statistics_Area_payload");
            var area = new AreaReference(physical);
            ushort count = AreaMapStatisticsInitialization.CountToNativeWord(counts[physical]);
            byte force = (byte)Math.Clamp((int)EditedForceNeed(current.ReadAreaCurrentForceSlot(area), "statistics_Area_Force_after"), 0, 51);
            current.WriteAreaRawField68(area, count); current.WriteAreaRawField78(area, force);
            var previous = entries.GetValueOrDefault(physical) ?? throw EditedForceMissing("statistics_Area_owner");
            entries[physical] = previous with { OwnedCellCount = count, ForceId = force == 0 ? null : force, IsOwnedCountCurrent = true };
        }
        return state with { RegionStatistics = saved with { Entries = entries } };
    }
    private static GameState AppendEditedForceResultEffects(GameState state, IReadOnlyList<CampaignEffectRequest> effects)
        => effects.Count == 0 ? state : state with { CampaignEffectRequests =
            (state.CampaignEffectRequests ?? throw EditedForceMissing("effect_queue")).Append(CampaignEffectSource.MonthlyMaintenance,
                state.NativeClock?.UpdateOrdinal ?? throw EditedForceMissing("effect_revision"), effects) };


    internal sealed partial class MonthlyCampaignMaintenanceSource
    {
        internal GameState ApplyEditedEventAllianceEnd(ForceReference first, ForceReference second)
        {
            bool changed = ForceArmies(first).Any(army => Need(current.ReadHexForceSlot(ArmyHex(army)), "editor_crossed_Hex14") == Physical(second)) ||
                ForceArmies(second).Any(army => Need(current.ReadHexForceSlot(ArmyHex(army)), "editor_crossed_Hex14") == Physical(first));
            EndAlliance(first, second);
            if (changed && ((ForceFlags(first) & 2) == 0 || (ForceFlags(second) & 2) == 0))
            {
                var selected = ForceArmies(first).Concat(ForceArmies(second)).ToArray();
                state = new NormalTurnTerritoryMaintenanceSource(package, state, current, pool).ApplyEditedEventLogistics(selected);
            }
            return state;
        }
    }
    private sealed partial class NormalTurnTerritoryMaintenanceSource
    {
        internal GameState ApplyEditedEventLogistics(IReadOnlyList<int> armies)
        {
            UpdateLogistics(armies);
            PublishPeople();
            if (effects.Count != 0) state = AppendEditedForceResultEffects(state, effects);
            return state;
        }
    }
    private static T EditedForceNeed<T>(T? value, string field) where T : struct => value ?? throw EditedForceMissing(field);
    private static InvalidDataException EditedForceMissing(string field) => new("edited-event.force-results." + field);
}
