using San14.Core;

namespace San14.Data;

internal sealed record TalentTravelReceipt(int StartHexId, int EndHexId, int TravelDays);

/// <summary>Talent initializers use the shared mode8 path and2318F0, with
/// their actual actor. The enclosing operation owns the current and pool.</summary>
internal static class TalentTravelSource
{
    internal static HexReference ResolvePosition(GameState state, SourceWorldContext current,
        PersonnelTalentTask task, NativeMapSeasonDate date)
        => ResolvePosition(state, current, task.StartHexId, task.EndHexId, task.SourceDistrictId,
            task.StartDate, task.DueDate, date);

    internal static HexReference ResolvePosition(GameState state, SourceWorldContext current,
        int startHexId, int endHexId, int sourceDistrictId, NativeMapSeasonDate startDate,
        NativeMapSeasonDate dueDate, NativeMapSeasonDate date)
    {
        var output = current.CreateNearbyTemporary();
        try
        {
            var start = Hex(startHexId);
            var end = Hex(endHexId);
            if (start.PhysicalSlot == end.PhysicalSlot) return start;
            int districtSlot = sourceDistrictId < 52 && sourceDistrictId >= 0 ? sourceDistrictId : 0;
            var district = current.ReadDistrictTableReference(districtSlot)
                ?? throw Missing("position_District_pointer");
            //22B5F0 passes null Force when the District is invalid. When it
            //is valid,22CA30 reads that District's current Force, not a
            //person's current Home or the mission's opening Force snapshot.
            ForceReference force;
            if (!K(current.ReadDistrictNativeValidity(district), "position_District_validity")) force = new(null);
            else
            {
                int raw = K(current.ReadDistrictForceId(K(district.PhysicalSlot, "position_District_physical")),
                    "position_District10");
                force = current.ReadForceTableReference(raw <= 51 ? raw : 0)
                    ?? throw Missing("position_Force_pointer");
            }
            var path = PersonReturnPathSource.Fill(state, current, start, end, force, output);
            if (path.Days is null) throw Missing(path.UnresolvedOwner ?? "position_path2318F0");
            int total = unchecked(PersonReturnCommands.CalendarOrdinal(dueDate) -
                PersonReturnCommands.CalendarOrdinal(startDate));
            if (total <= 0) return end;
            int count = K(current.ReadTemporaryHexListCount(output), "position_path_count");
            int elapsed = unchecked(PersonReturnCommands.CalendarOrdinal(date) -
                PersonReturnCommands.CalendarOrdinal(startDate));
            int desired = unchecked(elapsed * count) / total;
            int index = 0;
            var at = current.ReadHexListHead(output) ?? throw Missing("position_path_head");
            while (at.Identity.HasValue)
            {
                if (desired <= index) return current.ReadHexListNodePayload(at)
                    ?? throw Missing("position_path_Hex");
                index++;
                at = current.ReadHexListNodeNext(at) ?? throw Missing("position_path_next");
            }
            return end;
        }
        finally { current.ClearNearbyTemporary(output, true); }

        HexReference Hex(int raw) => current.ReadHexTableReference(raw is >= 0 and < 48400 ? raw : 0)
            ?? throw Missing("position_Hex_pointer");
    }

    internal static TalentTravelReceipt Resolve(GameState state, SourceWorldContext current,
        TalentOrderKind kind, int actorPhysical, BaseReference sourceBase,
        AreaReference? searchArea, int? recruitPhysical, ForceReference force,
        NativeMapSeasonDate sourceDate)
    {
        HexReference start, end, durationEnd;
        if (kind == TalentOrderKind.Search)
        {
            var area = searchArea ?? throw Missing("Search_Area");
            byte rawCity = K(current.ReadAreaCitySlot(area), "Search_Area35");
            var city = current.ReadCityTableReference(rawCity < 52 ? rawCity : 0)
                ?? throw Missing("Search_Area_City_pointer");
            start = Hex(current.ReadBaseCurrentHexIndex(sourceBase));
            end = K(current.ReadBaseNativeValidity(city), "Search_Area_City_validity")
                ? Hex(current.ReadBaseCurrentHexIndex(city))
                : Hex(K(current.ReadAreaHexIndex(area), "Search_Area3A"));
            //Only the following distance query reaches the City22D640
            //fallback even for an invalid Area-city. Do not read it before
            //the stored-endpoint validity branch above.
            durationEnd = Hex(current.ReadBaseCurrentHexIndex(city));
        }
        else if (kind == TalentOrderKind.Recruit)
        {
            start = Hex(current.ReadBaseCurrentHexIndex(sourceBase));
            int target = recruitPhysical ?? throw Missing("Employ_target_Person");
            end = PersonReturnCommands.ResolvePersonPosition(state, current, target, sourceDate);
            durationEnd = end;
        }
        else throw Missing("command_kind");

        //1F6BD0 uses2ACD10's count-1, whereas1F5F70's231670 clamps
        //that distance to at least1. Keep the distinct source operations.
        var output = current.CreateNearbyTemporary();
        try
        {
            if (start.PhysicalSlot is int first && durationEnd.PhysicalSlot is int last)
                foreach (int at in PersonReturnPathSource.Search(current, first, last))
                    _ = current.AppendTemporaryHexReference(output,
                        current.ReadHexTableReference(at) ?? throw Missing("path_Hex_pointer"));
            int count = K(current.ReadTemporaryHexListCount(output), "path_output_count");
            int distance = unchecked(count - 1);
            if (kind == TalentOrderKind.Recruit) distance = Math.Max(1, distance);
            force = current.ReadPersonForce(actorPhysical) ?? throw Missing("duration_actor_Force");
            int days = PersonReturnPathSource.CalculateTalentTravelDays(state, current,
                distance, actorPhysical, force);
            return new(K(start.PhysicalSlot, "start_Hex_physical"),
                Math.Clamp(K(end.PhysicalSlot, "end_Hex_physical"), 0, 48399), days);
        }
        finally { current.ClearNearbyTemporary(output, true); }

        HexReference Hex(int raw) => current.ReadHexTableReference(raw < 48400 && raw >= 0 ? raw : 0)
            ?? throw Missing("Hex_pointer");
    }

    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("talent.travel." + field);
}
