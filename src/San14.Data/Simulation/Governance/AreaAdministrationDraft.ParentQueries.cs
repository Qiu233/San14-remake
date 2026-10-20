using San14.Core;

namespace San14.Data;

public sealed partial class AreaAdministrationDraft
{
    /// <summary>Pure reads of the current projection, in the source held order.
    /// Does not open children or execute either source restore or vector edits.</summary>
    public AreaAdministrationQuery<IReadOnlyList<AreaAdministrationAreaRow>> QueryAreas()
    {
        try
        {
            EnsureOpen();
            var rows = new List<AreaAdministrationAreaRow>();
            for (int i = 0; i < _held.Length; i++)
            {
                int area = _held[i]; ushort person = Person(area); byte focus = Focus(area);
                _state.Areas.TryGetValue(area, out var row);
                var officer = AreaAdministrationQuery<int?>.Unknown("area-draft.parent_person_lookup");
                var officerName = AreaAdministrationQuery<string>.Unknown("area-draft.parent_officer_name");
                var presentationFocus = AreaAdministrationQuery<AreaDevelopmentFocus?>.Unknown("area-draft.parent_focus");
                try
                {
                    //3660D0 reads the name of a nonzero raw appointment without
                    //a native-person-validity gate. This differs from child UI.
                    if (person == 0)
                    { officer = AreaAdministrationQuery<int?>.Known(null); officerName = AreaAdministrationQuery<string>.Known(string.Empty); }
                    else
                    {
                        int physical = PersonAreaAppointmentSource.Resolve(_current, person);
                        int id = K(_current.ReadPersonIdentity(physical), "parent_person_identity");
                        officer = AreaAdministrationQuery<int?>.Known(id);
                        if (_state.Officers.TryGetValue(id, out var personRow)) officerName = AreaAdministrationQuery<string>.Known(personRow.Name);
                    }
                }
                catch (InvalidDataException unknown)
                {
                    officer = AreaAdministrationQuery<int?>.Unknown(unknown.Message);
                    officerName = AreaAdministrationQuery<string>.Unknown(unknown.Message);
                }
                //358530 indexes local focus text independently of Area36.
                try { presentationFocus = AreaAdministrationQuery<AreaDevelopmentFocus?>.Known(AreaAdministration.DecodeFocus(focus)); }
                catch (InvalidDataException unknown) { presentationFocus = AreaAdministrationQuery<AreaDevelopmentFocus?>.Unknown(unknown.Message); }
                var cityId = AreaAdministrationQuery<int?>.Unknown("area-draft.parent_city_pointer");
                var cityName = AreaAdministrationQuery<string>.Unknown("area-draft.parent_city_name");
                if (_current.ReadAreaCitySlot(new(area)) is byte rawCity &&
                    _current.ReadCityTableReference(rawCity <= 51 ? rawCity : 0)?.PhysicalSlot is int city)
                {
                    cityId = AreaAdministrationQuery<int?>.Known(city);
                    if (_state.Cities.TryGetValue(city, out var cityRow)) cityName = AreaAdministrationQuery<string>.Known(cityRow.Name);
                }
                var land = ParentLand(area);
                var special = ParentSpecialForce(area);
                rows.Add(new(area, ParentAreaName(area, row),
                    cityId, cityName, officer, presentationFocus, row?.NativeNormalAftermath?.Quantity52,
                    row?.Development?.Commerce, row?.Development?.Agriculture, row?.Development?.Barracks, row?.Security,
                    person != _parent.People[i] || focus != _parent.Focus[i])
                {
                    OfficerName = officerName,
                    LandCurrent = land.Current, LandMaximum = land.Maximum,
                    CommerceDisplay = ParentDevelopment(row?.Development?.Commerce, special),
                    AgricultureDisplay = ParentDevelopment(row?.Development?.Agriculture, special),
                    BarracksDisplay = ParentDevelopment(row?.Development?.Barracks, special)
                });
            }
            return AreaAdministrationQuery<IReadOnlyList<AreaAdministrationAreaRow>>.Known(rows.AsReadOnly());
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<IReadOnlyList<AreaAdministrationAreaRow>>.Unknown(unknown.Message); }
    }

    //7971C0's valid-person count, projected without its temporary UI list.
    //Read the entire held chain, including raw0 lookup and physical aliases.
    public AreaAdministrationQuery<bool?> QueryCanDismissAll()
    {
        try
        {
            EnsureOpen();
            if (_child is not null) throw Missing("child_still_open");
            int count = 0;
            foreach (int area in _held)
            {
                int person = PersonAreaAppointmentSource.Resolve(_current, Person(area));
                if (K(_current.ReadPersonNativeValidity(person), "parent_dismiss_person_validity")) count++;
            }
            return AreaAdministrationQuery<bool?>.Known(count > 0);
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<bool?>.Unknown(unknown.Message); }
    }

    public AreaAdministrationQuery<AreaAdministrationParentStatus> QueryParentStatus()
    {
        try
        {
            EnsureOpen(); int changed = CountChangedAreas();
            return AreaAdministrationQuery<AreaAdministrationParentStatus>.Known(new(_cityId, changed != 0, changed, ReadCommandBooks(changed)));
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<AreaAdministrationParentStatus>.Unknown(unknown.Message); }
    }

    public AreaAdministrationQuery<AreaAdministrationChildStatus> QueryChildStatus()
    {
        try
        {
            int area = Active(); int index = Array.IndexOf(_held, area); ushort person = Person(area); byte focus = Focus(area);
            AreaAdministrationQuery<bool?> accept;
            //Original footer5 is identity-different OR (valid current person AND
            //focus-different). It does not depend on unrelated parent changes.
            if (person != _child!.People[index]) accept = AreaAdministrationQuery<bool?>.Known(true);
            else if (focus == _child.Focus[index]) accept = AreaAdministrationQuery<bool?>.Known(false);
            else accept = CurrentPersonValid();
            return AreaAdministrationQuery<AreaAdministrationChildStatus>.Known(new(area, accept, CurrentPersonValid()));

            AreaAdministrationQuery<bool?> CurrentPersonValid()
            {
                try
                {
                    int physical = PersonAreaAppointmentSource.Resolve(_current, person);
                    return AreaAdministrationQuery<bool?>.Known(K(_current.ReadPersonNativeValidity(physical), "child_person_validity"));
                }
                catch (InvalidDataException unknown) { return AreaAdministrationQuery<bool?>.Unknown(unknown.Message); }
            }
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<AreaAdministrationChildStatus>.Unknown(unknown.Message); }
    }

    private int CountChangedAreas()
    {
        int changed = 0;
        for (int i = 0; i < _held.Length; i++)
            if (Person(_held[i]) != _parent.People[i] || Focus(_held[i]) != _parent.Focus[i]) changed++;
        return changed;
    }

    private AreaAdministrationCommandBooks ReadCommandBooks(int changed)
    {
        int? districtId = _state.Cities.GetValueOrDefault(_cityId)?.DistrictId;
        AreaAdministrationQuery<int?> current = Unknown("current_command_books");
        AreaAdministrationQuery<long?> reserved = AreaAdministrationQuery<long?>.Unknown("area-draft.command_book_district");
        AreaAdministrationQuery<long?> unreserved = AreaAdministrationQuery<long?>.Unknown("area-draft.current_command_books");
        if (districtId is int district && _state.Districts.TryGetValue(district, out var row))
        {
            if (row.OrdersRemaining is int books) current = AreaAdministrationQuery<int?>.Known(books);
            reserved = AreaAdministrationQuery<long?>.Known(CommandBookAvailability.Reserved(_state, district));
            if (current.Available) unreserved = AreaAdministrationQuery<long?>.Known(current.Value!.Value - reserved.Value!.Value);
        }
        int? cost = PersonAssignmentPrecallInputs.ReadAreaAppointmentCost(_state);
        AreaAdministrationQuery<int?> required = Unknown("command_cost");
        if (changed == 0) required = AreaAdministrationQuery<int?>.Known(0);
        else if (cost is >= 0)
        {
            int debit = unchecked(cost.Value * changed);
            if (debit >= 0) required = AreaAdministrationQuery<int?>.Known(debit);
        }
        var difference = required.Available ? AreaAdministrationQuery<int?>.Known(-required.Value!.Value) : Unknown("command_cost");
        return new(districtId, current, reserved, unreserved, required, difference)
        {
            //791C80's source header uses changed-row count, independently of
            //the formal command's saved cost or shared reserved budget.
            SourceHeaderAfter = current.Available ? AreaAdministrationQuery<int?>.Known(Math.Max(0, current.Value!.Value - changed)) : Unknown("current_command_books"),
            SourceHeaderDifference = -changed
        };
    }

    private AreaAdministrationQuery<string> ParentAreaName(int area, AreaState? row)
    {
        try
        {
            if (!K(_current.ReadAreaNativeValidity(new(area)), "parent_area_validity")) return AreaAdministrationQuery<string>.Known(string.Empty);
            return row is null ? AreaAdministrationQuery<string>.Unknown("area-draft.parent_area_name") : AreaAdministrationQuery<string>.Known(row.Name);
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<string>.Unknown(unknown.Message); }
    }

    private ForceReference ParentAreaForce(int area)
    {
        int raw = K(_current.ReadAreaCurrentForceSlot(new(area)), "parent_representative_force");
        return _current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("parent_force_pointer");
    }
    private AreaAdministrationQuery<bool?> ParentSpecialForce(int area)
    {
        try
        {
            int physical = K(_current.ReadForcePhysicalSlot(ParentAreaForce(area)), "parent_force_identity");
            return AreaAdministrationQuery<bool?>.Known(physical is >= 46 and <= 50);
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<bool?>.Unknown(unknown.Message); }
    }
    private static AreaAdministrationParentDevelopment ParentDevelopment(int? progress, AreaAdministrationQuery<bool?> special)
    {
        if (!special.Available) return new(null, null, null, special.UnresolvedInput);
        //The original six parent cells show Data1/0353 for special forces. This
        //is a known source presentation, not a missing current value.
        if (special.Value == true) return new(null, null, true, null);
        if (progress is not (>= 0 and <= ushort.MaxValue)) return new(null, null, false, "area-draft.parent_progress");
        //Parent experience columns use raw total progress, never within-level.
        return new(CeAreaIncomeMath.Level((ushort)progress.Value), progress, false, null);
    }
    private (AreaAdministrationQuery<int?> Current, AreaAdministrationQuery<int?> Maximum) ParentLand(int area)
    {
        try
        {
            if (!K(_current.ReadForceNativeValidity(ParentAreaForce(area)), "parent_land_force_validity"))
                return (AreaAdministrationQuery<int?>.Known(null), AreaAdministrationQuery<int?>.Known(null));
            var statistics = _state.RegionStatistics?.Entries.GetValueOrDefault(area);
            if (statistics is null) return (Unknown("land_statistics"), Unknown("land_statistics"));
            if (statistics.TerrainCellCount == 0)
                return (AreaAdministrationQuery<int?>.Known(null), AreaAdministrationQuery<int?>.Known(null));
            var maximum = AreaAdministrationQuery<int?>.Known(statistics.TerrainCellCount);
            return (statistics.IsOwnedCountCurrent ? AreaAdministrationQuery<int?>.Known(statistics.OwnedCellCount) : Unknown("land_owned_count_current"), maximum);
        }
        catch (InvalidDataException unknown)
        { return (AreaAdministrationQuery<int?>.Unknown(unknown.Message), AreaAdministrationQuery<int?>.Unknown(unknown.Message)); }
    }
}
