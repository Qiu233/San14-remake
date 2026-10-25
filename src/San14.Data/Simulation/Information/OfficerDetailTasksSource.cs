using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class OfficerDetailsCurrentReader
    {
        private OfficerDetailTask? detailTask;
        private bool detailTaskRead;
        private sealed record OfficerDetailTask(int Identity, int Kind, NativeMapSeasonDate Start,
            NativeMapSeasonDate Due, bool ClampElapsed, Func<string?> Target);

        private string? ReadTaskName()
        {
            //35DE70 falls back to the associated personnel command when an
            //otherwise valid commander Army is hidden by the information rule.
            var army = DetailCommanderArmy();
            if (army is not null && DetailArmyCommandVisible(army))
            {
                byte action = DetailArmyAction(army);
                return action is >= 1 and <= 17 ? Data(0, action + 0x103) : null;
            }
            var task = DetailLinkedTask();
            return task is null ? Data(1, 0x353) : task.Kind is >= 1 and <= 25
                ? Data(0, task.Kind + 0x127) : null;
        }

        private string? ReadTaskTarget()
        {
            var army = DetailCommanderArmy();
            if (army is null) return DetailLinkedTask()?.Target();
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
                        ?? throw OfficerDetailMissing("task_target_object_lookup");
                    int hex = DetailNeed(current.ReadObjectHexIndex(obj), "task_target_object_hex10");
                    return DetailTaskHexArea(hex < 48400 ? hex : 0);
                case 7: case 17:
                    var target = current.ReadArmyUnitTableReference(raw < 501 ? raw : 0)
                        ?? throw OfficerDetailMissing("task_target_army_lookup");
                    if (!DetailNeed(current.ReadArmyUnitNativeValidity(target), "task_target_army_validity")) return null;
                    string commander = ReadPersonName(DetailNeed(current.ReadArmyUnitCommanderId(target), "task_target_army_commander"));
                    return commander.Length == 0 ? null : commander + Data(1, 0x352);
                case 1: case 8: case 14: case 15: case 16:
                    return DetailTaskHexArea(raw < 48400 ? raw : 0);
                default: return null;
            }
        }

        private string? ReadTaskDuration()
        {
            //907EA0 asks22A470 of the linked command independently of the
            //commander-Army priority used by the two neighboring labels.
            var task = DetailLinkedTask();
            if (task is null) return null;
            if (task.Kind == 16) return Data(1, 0x353);
            var today = state.NativeMapSeason?.ThirdDate ?? throw OfficerDetailMissing("task_World34_date");
            var due = task.Due;
            if (task.ClampElapsed)
            {
                //231AA0 initializes an invalid Return/MovePerson deadline
                //through the existing same-current2351C0 path implementation.
                var initialized = PersonReturnCommands.EnsureArrivalDate(state, current, task.Identity, today);
                due = initialized.ArrivalDate ?? throw OfficerDetailMissing(initialized.UnresolvedOwner ?? "task_return_deadline");
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
                ?? throw OfficerDetailMissing("task_commander_army_pointer");
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
                ?? throw OfficerDetailMissing("task_player_Force");
            var force = current.ReadArmyUnitForceReference(army) ?? throw OfficerDetailMissing("task_army_Force");
            return new DiplomacyCommandSource(state, current).HasTreaty(
                DetailNeed(current.ReadForcePhysicalSlot(player), "task_player_Force_identity"),
                DetailNeed(current.ReadForcePhysicalSlot(force), "task_army_Force_identity"));
        }

        private byte DetailArmyAction(ArmyUnitReference army)
        {
            int at = DetailNeed(army.PhysicalSlot, "task_army_receiver");
            var lifecycle = state.ReadArmyLifecycle() ?? throw OfficerDetailMissing("task_army_lifecycle");
            if ((uint)at >= lifecycle.Slots.Count || lifecycle.Slots[at].PhysicalSlot != at)
                throw OfficerDetailMissing("task_army_source_slot");
            var receiver = lifecycle.Receivers.Find(lifecycle.Slots[at].ReceiverId)
                ?? throw OfficerDetailMissing("task_army_receiver_owner");
            UnitState? image = receiver.ActiveUnitId is int unit ? state.Units.GetValueOrDefault(unit) : receiver.PendingImage;
            return DetailNeed(ArmyConfigurationSourceAdapter.ReadAction(state, image), "task_army_action37");
        }

        private OfficerDetailTask? DetailLinkedTask()
        {
            if (detailTaskRead) return detailTask;
            var linked = current.ReadPersonLinkedEntity(physical) ?? throw OfficerDetailMissing("task_associated_pointer");
            if (linked.Identity is not int id) { detailTaskRead = true; return null; }
            var manager = current.ReadOwnedPersonCommandManager() ?? throw OfficerDetailMissing("task_current_manager");
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
                        _ => throw OfficerDetailMissing("task_strategic_target_kind")
                    });
            else if (manager.Independences?.TryGetValue(id, out var independence) == true)
                result = new(id, 23, independence.StartDate, independence.DueDate, false,
                    () => DetailTaskLocation(independence.TargetCityId));
            else throw OfficerDetailMissing("task_linked_receiver_kind");
            detailTask = result; detailTaskRead = true; return result;
        }

        private string? DetailTaskDestination(PersonnelReturnDestination destination)
            => DetailTaskBase(destination.Kind == PersonnelReturnBaseKind.City ? BaseKind.City : BaseKind.Gate,
                destination.BaseId - (destination.Kind == PersonnelReturnBaseKind.Gate ? 51 : 0), true);
        private string? DetailTaskLocation(int identity)
        {
            var location = BaseLookup.Find(identity, current);
            return DetailNeed(current.ReadBaseNativeValidity(location), "task_target_base_validity") ? BaseEntity(location).Name : null;
        }
        private string? DetailTaskBase(BaseKind kind, int raw, bool checkValidity)
        {
            var location = (kind == BaseKind.City ? current.ReadCityTableReference(raw) : current.ReadGateTableReference(raw))
                ?? throw OfficerDetailMissing("task_target_base_lookup");
            if (checkValidity && !DetailNeed(current.ReadBaseNativeValidity(location), "task_target_base_validity")) return null;
            int at = DetailNeed(location.PhysicalSlot, "task_target_base_receiver");
            if (at == 0) return null;
            return BaseEntity(location).Name;
        }
        private string? DetailTaskPerson(int raw)
        { string name = ReadPersonName(raw); return name.Length == 0 ? null : name; }
        private string? DetailTaskHexArea(int raw)
        {
            var hex = current.ReadHexTableReference(raw is >= 0 and < 48400 ? raw : 0)
                ?? throw OfficerDetailMissing("task_target_hex_lookup");
            int area = DetailNeed(current.ReadHexAreaSlot(hex), "task_target_hex_area12");
            return DetailTaskArea(area < 501 ? area : 0, false);
        }
        private string? DetailTaskArea(int raw, bool checkValidity)
        {
            var area = current.ReadAreaTableReference(raw is >= 0 and < 501 ? raw : 0)
                ?? throw OfficerDetailMissing("task_target_area_lookup");
            if (checkValidity && !DetailNeed(current.ReadAreaNativeValidity(area), "task_target_area_validity")) return null;
            int at = DetailNeed(area.PhysicalSlot, "task_target_area_receiver");
            return at == 0 ? null : state.Areas.GetValueOrDefault(at)?.Name ?? throw OfficerDetailMissing("task_target_area_name");
        }
        private string? DetailTaskForce(int raw)
        {
            var force = current.ReadForceTableReference(raw is >= 0 and < 52 ? raw : 0)
                ?? throw OfficerDetailMissing("task_target_Force_lookup");
            if (!DetailNeed(current.ReadForceNativeValidity(force), "task_target_Force_validity")) return null;
            int at = DetailNeed(current.ReadForcePhysicalSlot(force), "task_target_Force_identity");
            if (at is >= 46 and <= 50) return DetailTaskBase(BaseKind.City, at + 1, false);
            int ruler = at == 0 ? 5014 : DetailNeed(current.ReadForceRecord(at)?.RawRuler10, "task_target_Force_ruler");
            string? name = DetailTaskPerson(ruler);
            return name is null ? null : name + Data(1, 0x34f);
        }
    }
}
