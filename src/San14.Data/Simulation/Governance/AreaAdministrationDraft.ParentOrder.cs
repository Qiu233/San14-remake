namespace San14.Data;

public sealed partial class AreaAdministrationDraft
{
    private sealed record AreaOrderKey(int Tag, int Number = 0, string? Text = null);

    /// <summary>Orders exactly the caller's current complete held rows without changing the projection.</summary>
    public AreaAdministrationQuery<IReadOnlyList<int>> QueryAreaOrder(IReadOnlyList<int> currentAreaIds,
        AreaAdministrationSortColumn column, bool ascending)
    {
        ArgumentNullException.ThrowIfNull(currentAreaIds);
        try
        {
            EnsureOpen();
            if (!Enum.IsDefined(column)) throw Missing("order_column");
            var remaining = _held.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
            if (currentAreaIds.Count != _held.Length) throw Missing("order_complete_rows");
            foreach (int id in currentAreaIds)
            {
                if (!remaining.TryGetValue(id, out int count) || count == 0) throw Missing("order_held_rows");
                remaining[id] = count - 1;
            }
            var rows = currentAreaIds.ToArray();
            var keys = new Dictionary<int, AreaOrderKey>();
            foreach (int id in rows) if (!keys.ContainsKey(id)) keys[id] = ReadKey(id);
            // Source stable insertion/merge preserves the current row order for equal keys.
            for (int index = 1; index < rows.Length; index++)
            {
                int row = rows[index], at = index;
                while (at > 0 && Before(row, rows[at - 1])) { rows[at] = rows[at - 1]; at--; }
                rows[at] = row;
            }
            return AreaAdministrationQuery<IReadOnlyList<int>>.Known(Array.AsReadOnly(rows));

            AreaOrderKey ReadKey(int area)
            {
                if (column == AreaAdministrationSortColumn.Place)
                {
                    int raw = K(_current.ReadAreaHexIndex(new(area)), "order_place_hex");
                    var hex = _current.ReadHexTableReference(raw <= 48399 ? raw : 0) ?? throw Missing("order_place_hex_pointer");
                    int surface = K(_current.ReadHexSurfaceCode(hex), "order_place_surface");
                    return new(0, surface == 15 ? int.MaxValue : surface);
                }
                if (column == AreaAdministrationSortColumn.AreaName)
                {
                    if (area < 0 || area >= _areaDefinitions.Count || _areaDefinitions[area].Slot != area)
                        throw Missing("order_area_sort_key_owner");
                    return new(1, Text: _areaDefinitions[area].SecondaryText ?? throw Missing("order_area_sort_key"));
                }
                if (column == AreaAdministrationSortColumn.Officer)
                {
                    ushort raw = Person(area);
                    if (raw == 0) return new(1, Text: string.Empty);
                    int physical = PersonAreaAppointmentSource.Resolve(_current, raw);
                    int id = K(_current.ReadPersonIdentity(physical), "order_person_identity");
                    if (!_state.Officers.TryGetValue(id, out var person) || _current.ResolveOfficerSource(id) != physical)
                        throw Missing("order_person_owner");
                    return new(1, Text: person.CurrentNameSortKey ?? throw Missing("order_person_sort_key"));
                }
                if (column == AreaAdministrationSortColumn.Focus)
                { byte raw = Focus(area); return new(0, raw == 0 ? 4 : raw); }
                if (column == AreaAdministrationSortColumn.OwnedLand)
                {
                    if (!K(_current.ReadForceNativeValidity(ParentAreaForce(area)), "order_land_force_validity")) return new(0, -1);
                    var statistics = _state.RegionStatistics?.Entries.GetValueOrDefault(area) ?? throw Missing("order_land_statistics");
                    if (statistics.TerrainCellCount == 0) return new(0, -1);
                    if (!statistics.IsOwnedCountCurrent) throw Missing("order_land_current");
                    if (statistics.TerrainCellCount is < 0 or > ushort.MaxValue || statistics.OwnedCellCount is < 0 or > ushort.MaxValue)
                        throw Missing("order_land_word");
                    return new(0, statistics.OwnedCellCount * 1000 / statistics.TerrainCellCount);
                }
                if (column == AreaAdministrationSortColumn.Security)
                {
                    if (!_state.Areas.TryGetValue(area, out var securityRow)) throw Missing("order_area_owner");
                    if (securityRow.Security is not (>= 0 and <= byte.MaxValue)) throw Missing("order_security");
                    return new(0, securityRow.Security.Value);
                }
                var special = ParentSpecialForce(area);
                if (!special.Available) throw new InvalidDataException(special.UnresolvedInput);
                if (special.Value == true) return new(2, int.MaxValue);
                if (!_state.Areas.TryGetValue(area, out var row)) throw Missing("order_area_owner");
                int? progress = column switch
                {
                    AreaAdministrationSortColumn.CommerceLevel or AreaAdministrationSortColumn.CommerceExperience => row.Development?.Commerce,
                    AreaAdministrationSortColumn.AgricultureLevel or AreaAdministrationSortColumn.AgricultureExperience => row.Development?.Agriculture,
                    AreaAdministrationSortColumn.BarracksLevel or AreaAdministrationSortColumn.BarracksExperience => row.Development?.Barracks,
                    _ => null
                };
                if (progress is not (>= 0 and <= ushort.MaxValue)) throw Missing("order_progress");
                return new(0, column is AreaAdministrationSortColumn.CommerceLevel or AreaAdministrationSortColumn.AgricultureLevel or
                    AreaAdministrationSortColumn.BarracksLevel ? CeAreaIncomeMath.Level((ushort)progress.Value) : progress.Value);
            }
            bool Before(int left, int right)
            {
                if (left == right) return false;
                var a = keys[left]; var b = keys[right];
                if (column == AreaAdministrationSortColumn.OwnedLand)
                    return a.Number != b.Number ? ascending ? a.Number < b.Number : a.Number > b.Number :
                        ascending ? left > right : left < right;
                return ascending ? TypedBefore(a, b) : TypedBefore(b, a);
            }
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<IReadOnlyList<int>>.Unknown(unknown.Message); }
    }
    private static bool TypedBefore(AreaOrderKey left, AreaOrderKey right)
    {
        if (left.Tag == 2) return false;
        if (left.Tag == 0) return left.Number < right.Number;
        if (right.Tag == 2) return true;
        string a = left.Text!, b = right.Text!;
        for (int index = 0; index < 1024; index++)
        {
            char one = index < a.Length ? a[index] : '\0', two = index < b.Length ? b[index] : '\0';
            if (one != two) return one < two;
            if (one == '\0') break;
        }
        return false;
    }
}
