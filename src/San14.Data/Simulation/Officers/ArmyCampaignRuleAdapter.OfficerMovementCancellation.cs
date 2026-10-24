using San14.Core;

namespace San14.Data;

public sealed record OfficerMovementCancellationBase(int Id, PersonnelReturnBaseKind Kind, string Name);
public sealed record OfficerMovementCancellation(int TaskId, IReadOnlyList<int> MemberIds,
    IReadOnlyList<string> MemberNames, AreaAdministrationQuery<OfficerMovementCancellationBase> Source,
    AreaAdministrationQuery<OfficerMovementCancellationBase> Destination,
    AreaAdministrationQuery<int> CurrentHex, bool CancellationRequested, bool CanCancel, string? Reason);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One read-only page of actual saved reassignment tasks, in the
    /// manager's dispatch order. Cancellation remains a formal game command.</summary>
    public AreaAdministrationQuery<IReadOnlyList<OfficerMovementCancellation>> QueryOfficerMovementCancellations(
        GameState state, int forceId, int? delegatedDistrictId = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            if (!state.Forces.TryGetValue(forceId, out var force) || !force.IsActive)
                throw MovementCancellationMissing("Force");
            if (delegatedDistrictId is int delegated &&
                (!state.Districts.TryGetValue(delegated, out var delegatedDistrict) || delegatedDistrict.ForceId != forceId))
                throw MovementCancellationMissing("delegated_District");
            if (state.PersonnelCommands is null) throw MovementCancellationMissing("command_owner");
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var owner = current.ReadOwnedPersonCommandManager() ?? throw MovementCancellationMissing("current_command_owner");
            var basesByHex = new Dictionary<int, List<OfficerMovementCancellationBase>>();
            foreach (var city in state.Cities.Values)
                if (city.HexId is int hex) AddBase(hex, new(city.Id, PersonnelReturnBaseKind.City, city.Name));
            foreach (var gate in state.Gates.Values)
                if (gate.HexId is int hex) AddBase(hex, new(gate.Id, PersonnelReturnBaseKind.Gate, gate.Name));
            var rows = new List<OfficerMovementCancellation>();
            foreach (var task in owner.InDispatchOrder())
            {
                if (!task.IsReassignment) continue;
                var district = state.Districts.GetValueOrDefault(task.DistrictId);
                bool ownDistrict = district?.ForceId == forceId;
                bool hasOwnMember = task.MemberIds.Any(id => state.Officers.GetValueOrDefault(id)?.ForceId == forceId);
                if (!ownDistrict && !hasOwnMember) continue;
                var ids = Array.AsReadOnly(task.MemberIds.ToArray());
                var names = Array.AsReadOnly(task.MemberIds.Select(id => state.Officers.GetValueOrDefault(id)?.Name
                    ?? throw MovementCancellationMissing("member_" + id)).ToArray());
                string? reason = task.CancellationRequested ? "officer_move.cancellation_requested"
                    : task.IsRetiring ? "officer_move.task_retiring"
                    : task.MemberIds.Length == 0 ? "officer_move.empty_task"
                    : !ownDistrict || (delegatedDistrictId is int selected
                        ? selected != task.DistrictId : district!.Number != 1)
                        ? "officer_move.district_scope"
                    : task.MemberIds.Any(id => state.Officers.GetValueOrDefault(id)?.ForceId != forceId)
                        ? "officer_move.officer_force" : null;
                var source = basesByHex.TryGetValue(task.StartHex, out var matches) && matches.Count == 1
                    ? AreaAdministrationQuery<OfficerMovementCancellationBase>.Known(matches[0])
                    : AreaAdministrationQuery<OfficerMovementCancellationBase>.Unknown(
                        "officer_move.cancel.source_Base_at_start_Hex");
                var destination = ReadBase(task.Destination);
                rows.Add(new(task.Id, ids, names, source, destination, Position(task),
                    task.CancellationRequested, reason is null, reason));
            }
            return AreaAdministrationQuery<IReadOnlyList<OfficerMovementCancellation>>.Known(rows.AsReadOnly());

            void AddBase(int hex, OfficerMovementCancellationBase value)
            {
                if (!basesByHex.TryGetValue(hex, out var list)) basesByHex.Add(hex, list = []);
                list.Add(value);
            }

            AreaAdministrationQuery<OfficerMovementCancellationBase> ReadBase(PersonnelReturnDestination? destination)
            {
                if (destination is null)
                    return AreaAdministrationQuery<OfficerMovementCancellationBase>.Unknown("officer_move.cancel.destination");
                string? name = destination.Kind == PersonnelReturnBaseKind.City
                    ? state.Cities.GetValueOrDefault(destination.BaseId)?.Name
                    : destination.Kind == PersonnelReturnBaseKind.Gate
                        ? state.Gates.GetValueOrDefault(destination.BaseId)?.Name : null;
                return name is null
                    ? AreaAdministrationQuery<OfficerMovementCancellationBase>.Unknown("officer_move.cancel.destination_Base")
                    : AreaAdministrationQuery<OfficerMovementCancellationBase>.Known(
                        new(destination.BaseId, destination.Kind, name));
            }

            AreaAdministrationQuery<int> Position(PersonnelReturnTask task)
            {
                try
                {
                    // D8 can initialize an absent deadline. A display query
                    // must leave that write to the real lifecycle consumer.
                    if (!MovementCancellationDateValid(task.ArriveOn))
                        throw MovementCancellationMissing("saved_arrival_date");
                    if (task.StartHex is < 0 or >= 48400) throw MovementCancellationMissing("saved_start_Hex");
                    var date = state.NativeMapSeason?.ThirdDate ?? throw MovementCancellationMissing("current_date");
                    int hex = PersonReturnCommands.ResolveJourneyPosition(state, current, task.Id, date).PhysicalSlot
                        ?? throw MovementCancellationMissing("current_Hex_pointer");
                    return AreaAdministrationQuery<int>.Known(hex);
                }
                catch (InvalidDataException error) { return AreaAdministrationQuery<int>.Unknown(error.Message); }
            }
        }
        catch (InvalidDataException error)
        {
            return AreaAdministrationQuery<IReadOnlyList<OfficerMovementCancellation>>.Unknown(error.Message);
        }
    }

    private static bool MovementCancellationDateValid(NativeMapSeasonDate date) =>
        !(date.Year == 0 && date.Month == 1 && date.Day == 1 && date.Hour == 0) &&
        unchecked((ushort)date.Year) < 352 && unchecked((byte)(date.Month - 1)) < 13 &&
        unchecked((byte)(date.Day - 1)) < 31 && unchecked((byte)date.Hour) < 25;
    private static InvalidDataException MovementCancellationMissing(string field) => new("officer_move.cancel." + field);
}
