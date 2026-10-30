using System.Globalization;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record CurrentPersonTaskTarget(int Category, string Text);

/// <summary>The actual shared Person-list task columns, borrowed from one
/// current draft. Readers remain lazy; no complete officer card is resolved.</summary>
internal sealed class CurrentPersonTaskSource(GameState state, SourceWorldContext current, int physical,
    GameResourceRepository? repository, LoadedScenario? scenario, string? language)
{
    //The source's empty-string result differs from its null/not-applicable353.
    internal const int EmptyCaption = -1;
    private MessageDocument? messages;
    private IReadOnlyList<ForeignTradeCountryDefinition>? countries;
    private OfficerDetailTask? detailTask;
    private bool detailTaskRead;
    private sealed record OfficerDetailTask(int Identity, int Kind, NativeMapSeasonDate Start,
        NativeMapSeasonDate Due, bool ClampElapsed, Func<string?> Target);

    internal int? ReadCaptionId()
    {
        var army = DetailCommanderArmy();
        if (army is not null && DetailArmyCommandVisible(army))
        {
            int action = DetailArmyAction(army);
            return action is >= 1 and <= 17 ? action + 0x103 : EmptyCaption;
        }
        var task = InformationLinkedTask();
        return task is null ? null : task.Kind is >= 1 and <= 25 ? task.Kind + 0x127 : EmptyCaption;
    }
    internal string? ReadTaskName() => ReadCaptionId() is int id ? id == EmptyCaption ? null : Data(0, id) : Data(1, 0x353);

    internal string? ReadTaskTarget()
    {
        var army = DetailCommanderArmy();
        if (army is null) return InformationLinkedTask()?.Target();
        if (!DetailArmyCommandVisible(army)) return Data(1, 0x6d6);
        int at = DetailNeed(army.PhysicalSlot, "task_army_receiver");
        int raw = DetailNeed(current.ReadArmyLifecycleTargetFields(at)?.Identity3A, "task_army_target3A");
        if (raw == 255) return Data(1, 0x353);
        switch (DetailArmyAction(army))
        {
            case 2: case 4: case 11: return DetailTaskBase(BaseKind.City, raw < 52 ? raw : 0, false);
            case 3: case 5: case 12: return DetailTaskBase(BaseKind.Gate, raw < 11 ? raw : 0, false);
            case 6: case 9: case 10:
                var obj = current.ReadObjectTableReference(raw < 3001 ? raw : 0)
                    ?? throw Missing("task_target_object_lookup");
                int hex = DetailNeed(current.ReadObjectHexIndex(obj), "task_target_object_hex10");
                return DetailTaskHexArea(hex < 48400 ? hex : 0);
            case 7: case 17:
                var target = current.ReadArmyUnitTableReference(raw < 501 ? raw : 0)
                    ?? throw Missing("task_target_army_lookup");
                if (!DetailNeed(current.ReadArmyUnitNativeValidity(target), "task_target_army_validity")) return null;
                string commander = ReadPersonName(DetailNeed(current.ReadArmyUnitCommanderId(target), "task_target_army_commander"));
                return commander.Length == 0 ? null : commander + Data(1, 0x352);
            case 1: case 8: case 14: case 15: case 16:
                return DetailTaskHexArea(raw < 48400 ? raw : 0);
            default: return null;
        }
    }

    internal string? ReadTaskDuration()
    {
        //907EA0 asks22A470 of the linked command independently of the
        //commander-Army priority used by the two neighboring labels.
        var task = InformationLinkedTask();
        if (task is null) return null;
        if (task.Kind == 16) return Data(1, 0x353);
        var today = state.NativeMapSeason?.ThirdDate ?? throw Missing("task_World34_date");
        var due = task.Due;
        if (task.ClampElapsed)
        {
            //231AA0 initializes an invalid Return/MovePerson deadline
            //through the existing same-current2351C0 path implementation.
            var initialized = PersonReturnCommands.EnsureArrivalDate(state, current, task.Identity, today);
            due = initialized.ArrivalDate ?? throw Missing(initialized.UnresolvedOwner ?? "task_return_deadline");
        }
        int start = PersonReturnCommands.CalendarOrdinal(task.Start);
        int total = unchecked(PersonReturnCommands.CalendarOrdinal(due) - start);
        int elapsed = unchecked(PersonReturnCommands.CalendarOrdinal(today) - start);
        if (task.ClampElapsed) elapsed = Math.Max(0, elapsed); //22E270, unlike22E2C0.
        int remaining = Math.Max(0, unchecked(total - elapsed));
        return Data(1, 0x324).Replace("%d", remaining.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private ArmyUnitReference? DetailCommanderArmy()
    {
        var army = PersonArmyLocationSource.ReadCommanderArmy(current, physical)
            ?? throw Missing("task_commander_army_pointer");
        return DetailNeed(current.ReadArmyUnitNativeValidity(army), "task_commander_army_validity") ? army : null;
    }

    private bool DetailArmyCommandVisible(ArmyUnitReference army)
    {
        //3EB010 chooses its actual current CEnvironment field. It is
        //independent of the application's user-selection service.
        int selector = DetailNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "task_environment_selector");
        int restriction = DetailNeed(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x2cc : 0xd4),
            "task_environment_information_mode");
        if (restriction != 1) return true;
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw Missing("task_player_Force");
        var force = current.ReadArmyUnitForceReference(army) ?? throw Missing("task_army_Force");
        return new DiplomacyCommandSource(state, current).HasTreaty(
            DetailNeed(current.ReadForcePhysicalSlot(player), "task_player_Force_identity"),
            DetailNeed(current.ReadForcePhysicalSlot(force), "task_army_Force_identity"));
    }

    private byte DetailArmyAction(ArmyUnitReference army)
    {
        int at = DetailNeed(army.PhysicalSlot, "task_army_receiver");
        var lifecycle = state.ReadArmyLifecycle() ?? throw Missing("task_army_lifecycle");
        if ((uint)at >= lifecycle.Slots.Count || lifecycle.Slots[at].PhysicalSlot != at)
            throw Missing("task_army_source_slot");
        var receiver = lifecycle.Receivers.Find(lifecycle.Slots[at].ReceiverId)
            ?? throw Missing("task_army_receiver_owner");
        UnitState? image = receiver.ActiveUnitId is int unit ? state.Units.GetValueOrDefault(unit) : receiver.PendingImage;
        return DetailNeed(ArmyConfigurationSourceAdapter.ReadAction(state, image), "task_army_action37");
    }

    private OfficerDetailTask? DetailLinkedTask()
    {
        if (detailTaskRead) return detailTask;
        var linked = current.ReadPersonLinkedEntity(physical) ?? throw Missing("task_associated_pointer");
        if (linked.Identity is not int id) { detailTaskRead = true; return null; }
        var manager = current.ReadOwnedPersonCommandManager() ?? throw Missing("task_current_manager");
        OfficerDetailTask result;
        if (manager.Returns.TryGetValue(id, out var returning))
            result = new(id, returning.IsReassignment ? 1 : 15, returning.StartedOn, returning.ArriveOn, true,
                () => returning.RouteDestinations.Length == 0 ? null : DetailTaskDestination(returning.RouteDestinations[0]));
        else if (manager.Disappearances?.TryGetValue(id, out var absence) == true)
            result = new(id, 16, absence.StartedOn, absence.DueOn, false, () => null);
        else if (manager.Talents?.TryGetValue(id, out var talent) == true)
            result = new(id, talent.IsRecruitWait ? 22 : talent.Kind == TalentOrderKind.Search ? 3 : 2,
                talent.StartDate, talent.DueDate, false, () => talent.Kind == TalentOrderKind.Search
                    ? talent.TargetHexId is int hex ? DetailTaskHexArea(hex) : null
                    : talent.TargetOfficerId is int officer ? DetailTaskPerson(officer) : null);
        else if (manager.Diplomacies?.TryGetValue(id, out var diplomacy) == true)
            result = new(id, diplomacy.NativeKind, diplomacy.StartDate, diplomacy.DueDate, false,
                () => diplomacy.NativeKind == 11
                    ? diplomacy.TargetCityId is int city ? DetailTaskLocation(city) : null
                    : DetailTaskForce(diplomacy.TargetForceId));
        else if (manager.Strategies?.TryGetValue(id, out var strategic) == true)
            result = new(id, strategic.Kind, strategic.StartDate, strategic.DueDate, false,
                () => strategic.Target.Kind switch
                {
                    StrategicCommandTargetKind.City => DetailTaskLocation(strategic.Target.Id),
                    StrategicCommandTargetKind.Person => DetailTaskPerson(strategic.Target.Id),
                    StrategicCommandTargetKind.Force => DetailTaskForce(strategic.Target.Id),
                    StrategicCommandTargetKind.Area => DetailTaskArea(strategic.Target.Id, true),
                    _ => throw Missing("task_strategic_target_kind")
                });
        else if (manager.Independences?.TryGetValue(id, out var independence) == true)
            result = new(id, 23, independence.StartDate, independence.DueDate, false,
                () => DetailTaskLocation(independence.TargetCityId));
        else if (manager.Intelligences?.TryGetValue(id, out var intelligence) == true)
            result = new(id, intelligence.Kind == PersonnelIntelligenceKind.Away ? 21 : 25,
                intelligence.StartDate, intelligence.DueDate, false,
                () => intelligence.Kind == PersonnelIntelligenceKind.Away ? DetailTaskForce(intelligence.TargetId)
                    : DetailTaskLocation(intelligence.TargetId));
        else throw Missing("task_linked_receiver_kind");
        detailTask = result; detailTaskRead = true; return result;
    }

    private string? DetailTaskDestination(PersonnelReturnDestination destination)
        => DetailTaskBase(destination.Kind == PersonnelReturnBaseKind.City ? BaseKind.City : BaseKind.Gate,
            destination.BaseId - (destination.Kind == PersonnelReturnBaseKind.Gate ? 51 : 0), true);
    private string? DetailTaskLocation(int identity)
    {
        var location = BaseLookup.Find(identity, current);
        return DetailNeed(current.ReadBaseNativeValidity(location), "task_target_base_validity") ? BaseName(location) : null;
    }
    private string? DetailTaskBase(BaseKind kind, int raw, bool checkValidity)
    {
        var location = (kind == BaseKind.City ? current.ReadCityTableReference(raw) : current.ReadGateTableReference(raw))
            ?? throw Missing("task_target_base_lookup");
        if (checkValidity && !DetailNeed(current.ReadBaseNativeValidity(location), "task_target_base_validity")) return null;
        int at = DetailNeed(location.PhysicalSlot, "task_target_base_receiver");
        if (at == 0) return null;
        return BaseName(location);
    }
    private string? DetailTaskPerson(int raw)
    { string name = ReadPersonName(raw); return name.Length == 0 ? null : name; }
    private string? DetailTaskHexArea(int raw)
    {
        var hex = current.ReadHexTableReference(raw is >= 0 and < 48400 ? raw : 0)
            ?? throw Missing("task_target_hex_lookup");
        int area = DetailNeed(current.ReadHexAreaSlot(hex), "task_target_hex_area12");
        return DetailTaskArea(area < 501 ? area : 0, false);
    }
    private string? DetailTaskArea(int raw, bool checkValidity)
    {
        var area = current.ReadAreaTableReference(raw is >= 0 and < 501 ? raw : 0)
            ?? throw Missing("task_target_area_lookup");
        if (checkValidity && !DetailNeed(current.ReadAreaNativeValidity(area), "task_target_area_validity")) return null;
        int at = DetailNeed(area.PhysicalSlot, "task_target_area_receiver");
        return at == 0 ? null : state.Areas.GetValueOrDefault(at)?.Name ?? throw Missing("task_target_area_name");
    }
    private string? DetailTaskForce(int raw)
    {
        var force = current.ReadForceTableReference(raw is >= 0 and < 52 ? raw : 0)
            ?? throw Missing("task_target_Force_lookup");
        if (!DetailNeed(current.ReadForceNativeValidity(force), "task_target_Force_validity")) return null;
        int at = DetailNeed(current.ReadForcePhysicalSlot(force), "task_target_Force_identity");
        if (at is >= 46 and <= 50) return DetailTaskBase(BaseKind.City, at + 1, false);
        int ruler = at == 0 ? 5014 : DetailNeed(current.ReadForceRecord(at)?.RawRuler10, "task_target_Force_ruler");
        string? name = DetailTaskPerson(ruler);
        return name is null ? null : name + Data(1, 0x34f);
    }

    internal string? InformationTaskName()
    {
        var army = DetailCommanderArmy();
        if (army is null || !DetailArmyCommandVisible(army)) InformationPrepareCaravan();
        return ReadTaskName();
    }

    internal int? InformationTaskNameKey()
    {
        var army = DetailCommanderArmy();
        //Unlike35DE70 display,34BD60 does not fall back to a linked
        //command if the commander Army exists but its action is hidden.
        if (army is not null) return DetailArmyCommandVisible(army) ? DetailArmyAction(army) + 0x73b : null;
        return InformationLinkedTask()?.Kind;
    }

    internal string? InformationTaskTarget()
    {
        if (DetailCommanderArmy() is null) InformationPrepareCaravan();
        return ReadTaskTarget();
    }

    internal string? InformationTaskDuration()
    {
        var task = InformationLinkedTask();
        if (task is null) return null;
        return task.Kind == 16 ? Data(1, 0x353)
            : InformationTaskRemaining(task).ToString(CultureInfo.InvariantCulture);
    }

    internal int? InformationTaskDurationKey()
    {
        var task = InformationLinkedTask();
        //34BE50 still calls22A470 for kind16; only its display is a dash.
        return task is null ? null : InformationTaskRemaining(task);
    }

    private int InformationTaskRemaining(OfficerDetailTask task)
    {
        var today = state.NativeMapSeason?.ThirdDate ?? throw Missing("task_World34_date");
        var due = task.Due;
        if (task.ClampElapsed)
        {
            var initialized = PersonReturnCommands.EnsureArrivalDate(state, current, task.Identity, today);
            due = initialized.ArrivalDate ?? throw Missing(initialized.UnresolvedOwner ?? "task_return_deadline");
        }
        int start = PersonReturnCommands.CalendarOrdinal(task.Start);
        int total = unchecked(PersonReturnCommands.CalendarOrdinal(due) - start);
        int elapsed = unchecked(PersonReturnCommands.CalendarOrdinal(today) - start);
        if (task.ClampElapsed) elapsed = Math.Max(0, elapsed);
        return Math.Max(0, unchecked(total - elapsed));
    }

    private OfficerDetailTask? InformationLinkedTask()
    { InformationPrepareCaravan(); return DetailLinkedTask(); }

    private void InformationPrepareCaravan()
    {
        if (detailTaskRead) return;
        var linked = current.ReadPersonLinkedEntity(physical) ?? throw Missing("task_associated_pointer");
        if (linked.Identity is not int id) return;
        var manager = current.ReadOwnedPersonCommandManager() ?? throw Missing("task_current_manager");
        if (manager.Caravans?.GetValueOrDefault(id) is not { } caravan) return;
        detailTask = new(id, caravan.NativeKind, caravan.StartDate, caravan.DueDate, false,
            () => InformationCountryName(caravan.CountryId, false));
        detailTaskRead = true;
    }

    internal CurrentPersonTaskTarget InformationTaskTargetKey()
    {
        var army = DetailCommanderArmy();
        if (army is not null)
        {
            //342970 resolves this Person's Force;3664F0's display and
            //34BD60 resolve the Army's Force instead. Preserve both owners.
            if (!InformationPersonTargetVisible()) return new(6, "");
            int at = DetailNeed(army.PhysicalSlot, "task_sort_army_receiver");
            int action = DetailArmyAction(army);
            if (action == 13) return new(8, "");
            if (action is not (>= 1 and <= 12 or >= 14 and <= 17))
                throw Missing("task_sort_unwritten_army_category." + action);
            int raw = DetailNeed(current.ReadArmyLifecycleTargetFields(at)?.Identity3A, "task_sort_army_target3A");
            switch (action)
            {
                case 1: case 8: case 14: case 15: case 16:
                    return new(3, HexAreaName(raw < 48400 ? raw : 0));
                case 2: case 4: case 11:
                    return new(1, DetailTaskBase(BaseKind.City, raw < 52 ? raw : 0, false) ?? "");
                case 3: case 5: case 12:
                    return new(2, DetailTaskBase(BaseKind.Gate, raw < 11 ? raw : 0, false) ?? "");
                case 6: case 9: case 10:
                    var obj = current.ReadObjectTableReference(raw < 3001 ? raw : 0)
                        ?? throw Missing("task_sort_object_lookup");
                    int hex = DetailNeed(current.ReadObjectHexIndex(obj), "task_sort_object_hex");
                    return new(3, HexAreaName(hex < 48400 ? hex : 0));
                case 7: case 17:
                    var target = current.ReadArmyUnitTableReference(raw < 501 ? raw : 0)
                        ?? throw Missing("task_sort_army_lookup");
                    return new(5, ReadPersonName(DetailNeed(current.ReadArmyUnitCommanderId(target), "task_sort_target_commander")));
                default: throw Missing("task_sort_unwritten_army_category." + action);
            }
        }
        var task = InformationLinkedTask();
        if (task is null || task.Kind == 16) return new(8, "");
        var targetValue = InformationTaskTargetSource(task.Identity);
        int kind = task.Kind, tag = targetValue.Tag, id = targetValue.Id;
        switch (kind)
        {
            case 1: case 11: case 15: case 23: case 25:
                if (tag is not (3 or 4)) throw Missing("task_sort_base_target_tag");
                var location = (tag == 3 ? current.ReadCityTableReference(id is >= 0 and < 52 ? id : 0)
                    : current.ReadGateTableReference(id is >= 0 and < 11 ? id : 0))
                    ?? throw Missing("task_sort_base_lookup");
                if (!DetailNeed(current.ReadBaseNativeValidity(location), "task_sort_base_validity"))
                    throw Missing("task_sort_unwritten_base_category");
                return new(tag == 3 ? 1 : 2, BaseName(location));
            case 2: case 12: case 22: case 24:
                if (tag != 1) throw Missing("task_sort_person_target_tag");
                var personReference = current.ReadPersonLookupReference(id is >= 0 and < 6001 ? id : 0)
                    ?? throw Missing("task_sort_person_lookup");
                int p = DetailNeed(personReference.PhysicalSlot, "task_sort_person_receiver");
                if (!DetailNeed(current.ReadPersonNativeValidity(p), "task_sort_person_validity"))
                    throw Missing("task_sort_unwritten_person_category");
                return new(4, ReadPersonName(id));
            case 3:
                if (tag != 5) throw Missing("task_sort_hex_target_tag");
                var targetHex = current.ReadHexTableReference(id is >= 0 and < 48400 ? id : 0)
                    ?? throw Missing("task_sort_hex_lookup");
                if (targetHex.PhysicalSlot is null) throw Missing("task_sort_unwritten_hex_category");
                int areaId = DetailNeed(current.ReadHexAreaSlot(targetHex), "task_sort_hex_area");
                return InformationAreaTarget(areaId);
            case 4: case 5: case 6: case 7: case 8: case 9: case 10: case 19: case 21:
                if (tag != 6) throw Missing("task_sort_force_target_tag");
                var force = current.ReadForceTableReference(id is >= 0 and < 52 ? id : 0)
                    ?? throw Missing("task_sort_force_lookup");
                if (!DetailNeed(current.ReadForceNativeValidity(force), "task_sort_force_validity"))
                    throw Missing("task_sort_unwritten_force_category");
                return new(0, DetailTaskForce(id) ?? "");
            case 13: case 14: case 20:
                if (tag != 7) throw Missing("task_sort_area_target_tag");
                return InformationAreaTarget(id);
            case 17: case 18:
                if (tag != 8) throw Missing("task_sort_country_target_tag");
                return new(7, InformationCountryName(id, true)!);
            default: throw Missing("task_sort_unwritten_linked_category." + kind);
        }
    }

    private bool InformationPersonTargetVisible()
    {
        int selector = DetailNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "task_sort_environment_selector");
        int mode = DetailNeed(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x2cc : 0xd4),
            "task_sort_environment_information_mode");
        if (mode != 1) return true;
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("task_sort_player_force");
        var force = current.ReadPersonForce(physical) ?? throw Missing("task_sort_person_force");
        return new DiplomacyCommandSource(state, current).HasTreaty(
            DetailNeed(player.PhysicalSlot, "task_sort_player_force_receiver"),
            DetailNeed(force.PhysicalSlot, "task_sort_person_force_receiver"));
    }

    private CurrentPersonTaskTarget InformationAreaTarget(int id)
    {
        var area = current.ReadAreaTableReference(id is >= 0 and < 501 ? id : 0)
            ?? throw Missing("task_sort_area_lookup");
        if (!DetailNeed(current.ReadAreaNativeValidity(area), "task_sort_area_validity"))
            throw Missing("task_sort_unwritten_area_category");
        int at = DetailNeed(area.PhysicalSlot, "task_sort_area_receiver");
        return new(3, state.Areas.GetValueOrDefault(at)?.Name ?? throw Missing("task_sort_area_name"));
    }

    private string? InformationCountryName(int id, bool requireValid)
    {
        var country = current.ReadStaticTableReference(0x85a20, id is >= 0 and < 5 ? id : 0)
            ?? throw Missing("task_country_lookup");
        int at = DetailNeed(country.PhysicalSlot, "task_country_receiver");
        //CForeignData.v3 is the nonzero physical ordinal,234060.
        if (at == 0)
        {
            if (requireValid) throw Missing("task_sort_unwritten_country_category");
            return null;
        }
        return CountryName(at);
    }

    private (int Tag, int Id) InformationTaskTargetSource(int identity)
    {
        var manager = current.ReadOwnedPersonCommandManager() ?? throw Missing("task_current_manager");
        if (manager.Returns.GetValueOrDefault(identity) is { } returning)
        {
            if (returning.RouteDestinations.Length == 0) throw Missing("task_sort_unwritten_return_destination");
            var destination = returning.RouteDestinations[0];
            return destination.Kind == PersonnelReturnBaseKind.City ? (3, destination.BaseId) : (4, destination.BaseId - 51);
        }
        if (manager.Talents?.GetValueOrDefault(identity) is { } talent)
            return talent.Kind == TalentOrderKind.Search && !talent.IsRecruitWait
                ? (5, talent.TargetHexId ?? throw Missing("task_sort_search_target"))
                : (1, talent.TargetOfficerId ?? throw Missing("task_sort_talent_target"));
        if (manager.Diplomacies?.GetValueOrDefault(identity) is { } diplomacy)
            return diplomacy.NativeKind == 11 ? (3, diplomacy.TargetCityId ?? throw Missing("task_sort_diplomacy_city"))
                : (6, diplomacy.TargetForceId);
        if (manager.Strategies?.GetValueOrDefault(identity) is { } strategy)
            return (strategy.Target.Kind switch
            {
                StrategicCommandTargetKind.City => 3, StrategicCommandTargetKind.Person => 1,
                StrategicCommandTargetKind.Force => 6, StrategicCommandTargetKind.Area => 7,
                _ => throw Missing("task_sort_strategy_target_kind")
            }, strategy.Target.Id);
        if (manager.Independences?.GetValueOrDefault(identity) is { } independence) return (3, independence.TargetCityId);
        if (manager.Intelligences?.GetValueOrDefault(identity) is { } intelligence)
            return (intelligence.Kind == PersonnelIntelligenceKind.Away ? 6 : 3, intelligence.TargetId);
        if (manager.Caravans?.GetValueOrDefault(identity) is { } caravan) return (8, caravan.CountryId);
        throw Missing("task_sort_linked_target_owner");
    }

    internal CityDomesticPersonTaskRemaining? ReadRemainingCell()
    {
        var task = InformationLinkedTask();
        return task is null ? null : task.Kind == 16 ? new(true, null) : new(false, InformationTaskRemaining(task));
    }
    internal static bool BeforeTarget(CurrentPersonTaskTarget left, CurrentPersonTaskTarget right, bool reversed)
    {
        int a = left.Category, b = right.Category;
        if (!reversed) return a != b ? a < b : DeploymentResponsibilityNameOrder.Before(left.Text, right.Text);
        if (a == 7) a = -1;
        if (b == 7) b = -1;
        return a != b ? b < a : DeploymentResponsibilityNameOrder.Before(right.Text, left.Text);
    }
    private string Data(int section, int index)
    {
        if (repository is null || language is not ("TC" or "SC")) throw Missing("caption_resource_owner");
        messages ??= repository.LoadMessages(language, MessageKind.Data).Messages;
        return messages.Sections[section].Records[index].Text ?? throw Missing("caption_text");
    }
    private string ReadPersonName(int raw)
    {
        var reference = current.ReadPersonLookupReference(raw is >= 0 and < 6001 ? raw : 0) ?? throw Missing("name_person_lookup");
        int at = DetailNeed(reference.PhysicalSlot, "name_person_receiver");
        if (!DetailNeed(current.ReadPersonNativeValidity(at), "name_person_validity")) return "";
        int id = DetailNeed(current.ReadPersonIdentity(at), "name_person_identity");
        return state.Officers.GetValueOrDefault(id)?.Name ?? throw Missing("name_person_owner");
    }
    private string BaseName(BaseReference location)
    {
        int at = DetailNeed(location.PhysicalSlot, "location_base_receiver");
        return location.Kind == BaseKind.City ? state.Cities.GetValueOrDefault(at)?.Name ?? throw Missing("city_name")
            : state.Gates?.GetValueOrDefault(at + 51)?.Name ?? throw Missing("gate_name");
    }
    private string HexAreaName(int raw)
    {
        var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("target_Hex_lookup");
        int rawArea = DetailNeed(current.ReadHexAreaSlot(hex), "target_Hex_Area12");
        var area = current.ReadAreaTableReference(rawArea < 501 ? rawArea : 0) ?? throw Missing("target_Area_lookup");
        int at = DetailNeed(area.PhysicalSlot, "target_Area_receiver");
        if (at != 0) return state.Areas.GetValueOrDefault(at)?.Name ?? throw Missing("target_Area_name");
        var rows = scenario?.Snapshot.Areas ?? throw Missing("target_Area0_definition_owner");
        return rows.Count > 0 && rows[0].Slot == 0 ? rows[0].Name : throw Missing("target_Area0_definition");
    }
    private string CountryName(int at)
    {
        if (repository is null || scenario is null) throw Missing("country_resource_owner");
        countries ??= repository.LoadForeignTradeCountries(scenario.FixedDataSource.SourcePath);
        return countries[at].Name;
    }
    private static T DetailNeed<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("person-task.current." + field);
}
