namespace San14.Data;

public sealed partial class CityDomesticDraft
{
    private readonly CityDomesticPersonSelection?[] _personSelections = new CityDomesticPersonSelection?[2];

    public AreaAdministrationQuery<CityDomesticPersonSelection> QueryPersonSelection(CityDomesticDuty duty)
    {
        try
        {
            EnsureOpen(); CheckDuty(duty);
            if (_personSelections[(int)duty] is { } prior)
                return AreaAdministrationQuery<CityDomesticPersonSelection>.Known(prior);
            var query = QueryCandidates(duty);
            if (!query.Available) throw new InvalidDataException(query.UnresolvedInput);
            var selection = new CityDomesticPersonSelection(CityId, duty, query.Value!);
            _personSelections[(int)duty] = selection;
            return AreaAdministrationQuery<CityDomesticPersonSelection>.Known(selection);
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<CityDomesticPersonSelection>.Unknown(error.Message); }
    }

    public AreaAdministrationQuery<CityDomesticPersonBasicPage> QueryPersonBasicPage(
        CityDomesticPersonSelection selection, IReadOnlyList<int> order)
    {
        try
        {
            CheckPersonSelection(selection); var ordered = PersonOrder(selection, order);
            var source = new CityDomesticPersonCellSource(_state, _current, _definitions);
            var rows = new List<CityDomesticPersonBasicRow>(ordered.Length);
            foreach (int occurrence in ordered)
            {
                var candidate = selection.Candidates[occurrence]; int p = candidate.PhysicalSlot;
                rows.Add(new(occurrence, candidate,
                    PersonCell(() => source.Name(candidate)),
                    selection.Duty == CityDomesticDuty.Trainer ? PersonCell<int?>(() =>
                        { source.Identity(candidate); return Ability(p, 0); }) : AreaAdministrationQuery<int?>.Known(null),
                    PersonCell<int?>(() => { source.Identity(candidate); return Ability(p, 1); }),
                    PersonCell<bool?>(() => source.Personality(candidate, selection.Duty)),
                    PersonCell<DeploymentPersonResponsibility?>(() =>
                    {
                        source.Identity(candidate);
                        return DeploymentPersonResponsibilityDisplaySource.Read(_state, _current, p, _vectors.Count, _vectors.Entry);
                    }),
                    PersonCell<DeploymentPersonResponsibility?>(() =>
                    {
                        source.Identity(candidate);
                        return DeploymentPersonResponsibilityDisplaySource.ReadSecondary(_state, _current, p, 1,
                            Additional, _vectors.Count, _vectors.Entry);
                    }),
                    PersonCell<DeploymentPersonResponsibility?>(() =>
                    {
                        source.Identity(candidate);
                        return DeploymentPersonResponsibilityDisplaySource.ReadSecondary(_state, _current, p, 2,
                            Additional, _vectors.Count, _vectors.Entry);
                    }),
                    PersonCell(() => source.Returning(candidate)),
                    PersonCell<bool?>(() => source.AreaLocation(candidate, true)),
                    PersonCell(() => source.Task(candidate)),
                    PersonCell<int?>(() => source.RowState(candidate))));
            }
            return AreaAdministrationQuery<CityDomesticPersonBasicPage>.Known(new(selection, Revision,
                rows.AsReadOnly(), PersonCell<int?>(() => Preselected(selection, ordered))));
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<CityDomesticPersonBasicPage>.Unknown(error.Message); }
    }

    private int Additional(int physical) => PersonAreaAppointmentSource.AdditionalCount(_state, _current, physical);

    private int? Preselected(CityDomesticPersonSelection selection, IReadOnlyList<int> order)
    {
        ushort raw = RoleRaw(selection.Duty);
        if (raw == 0) return null;
        var pointer = raw <= 6000 ? _current.ReadPersonLookupReference(raw) : _current.ReadTransferPersonTableReference(0);
        int physical = K(pointer?.PhysicalSlot, "person_preselection_pointer");
        foreach (int occurrence in order)
            if (selection.Candidates[occurrence].PhysicalSlot == physical) return occurrence;
        return null;
    }

    private void CheckPersonSelection(CityDomesticPersonSelection selection)
    {
        EnsureOpen();
        if (selection is null || !Enum.IsDefined(selection.Duty) ||
            !ReferenceEquals(_personSelections[(int)selection.Duty], selection))
            throw CityDomesticPersonCellSource.Missing("selection_owner");
    }
    private static int[] PersonOrder(CityDomesticPersonSelection selection, IReadOnlyList<int> order)
    {
        if (order is null || order.Count != selection.Candidates.Count)
            throw CityDomesticPersonCellSource.Missing("complete_order");
        var result = new int[order.Count]; var seen = new bool[order.Count];
        for (int index = 0; index < order.Count; index++)
        {
            int occurrence = order[index];
            if ((uint)occurrence >= (uint)seen.Length || seen[occurrence])
                throw CityDomesticPersonCellSource.Missing("order_permutation");
            seen[occurrence] = true; result[index] = occurrence;
        }
        return result;
    }
    private static AreaAdministrationQuery<T> PersonCell<T>(Func<T> read)
    {
        try { return AreaAdministrationQuery<T>.Known(read()); }
        catch (InvalidDataException error) { return AreaAdministrationQuery<T>.Unknown(error.Message); }
    }

    private AreaAdministrationQuery<CityDomesticPersonPage<TRow>> PersonPage<TRow>(
        CityDomesticPersonSelection selection, IReadOnlyList<int> order,
        Func<CityDomesticPersonCellSource, CityDomesticCandidate, int, TRow> read)
    {
        try
        {
            CheckPersonSelection(selection); var ordered = PersonOrder(selection, order);
            var source = new CityDomesticPersonCellSource(_state, _current, _definitions, _governorInputs, _scenario, _personResources, _language);
            var rows = new List<TRow>(ordered.Length);
            foreach (int occurrence in ordered) rows.Add(read(source, selection.Candidates[occurrence], occurrence));
            return AreaAdministrationQuery<CityDomesticPersonPage<TRow>>.Known(new(selection, Revision,
                rows.AsReadOnly(), PersonCell<int?>(() => Preselected(selection, ordered))));
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<CityDomesticPersonPage<TRow>>.Unknown(error.Message); }
    }
    public AreaAdministrationQuery<CityDomesticPersonPage<CityDomesticPersonAffiliationRow>> QueryPersonAffiliationPage(
        CityDomesticPersonSelection selection, IReadOnlyList<int> order) => PersonPage(selection, order, (source, c, occurrence) =>
        {
            var name = PersonCell(() => source.Name(c));
            var force = PersonCell(() => source.ForceName(c));
            var district = PersonCell(() => source.DistrictNumber(c));
            var home = PersonCell(() => source.Home(c));
            var location = PersonCell(() => source.CurrentLocation(c));
            var status = PersonCell(() => source.ServiceStatus(c));
            var responsibility = PersonCell<DeploymentPersonResponsibility?>(() =>
            { source.Identity(c); return DeploymentPersonResponsibilityDisplaySource.Read(_state, _current, c.PhysicalSlot, _vectors.Count, _vectors.Entry); });
            var loyalty = PersonCell(() => source.Loyalty(c));
            var officeName = PersonCell(() => source.Office(c));
            var metric = PersonCell(() => source.OtherMetric(c));
            var years = PersonCell(() => source.ServiceYears(c, true));
            var paint = PersonCell<int?>(() => source.RowState(c));
            return new CityDomesticPersonAffiliationRow(occurrence, c, name, paint, force, district, home, location,
                status, responsibility, loyalty, officeName, metric, years);
        });
    public AreaAdministrationQuery<CityDomesticPersonPage<CityDomesticPersonAbilityRow>> QueryPersonAbilityPage(
        CityDomesticPersonSelection selection, IReadOnlyList<int> order) => PersonPage(selection, order, (source, c, occurrence) =>
        {
            var name = PersonCell(() => source.Name(c)); var status = PersonCell(() => source.ServiceStatus(c));
            AreaAdministrationQuery<int?> Dimension(int dimension) => PersonCell<int?>(() => { source.Identity(c); return Ability(c.PhysicalSlot, dimension); });
            var lead = Dimension(0); var strength = Dimension(1); var intelligence = Dimension(2);
            var politics = Dimension(3); var charm = Dimension(4);
            var health = PersonCell(() => source.Health(c)); var items = PersonCell(() => source.ItemCount(c));
            var paint = PersonCell<int?>(() => source.RowState(c));
            return new CityDomesticPersonAbilityRow(occurrence, c, name, paint, status, lead, strength, intelligence,
                politics, charm, health, items);
        });
    public AreaAdministrationQuery<CityDomesticPersonPage<CityDomesticPersonMeritRow>> QueryPersonMeritPage(
        CityDomesticPersonSelection selection, IReadOnlyList<int> order) => PersonPage(selection, order, (source, c, occurrence) =>
        {
            var name = PersonCell(() => source.Name(c)); var stage = PersonCell(() => source.RankName(c));
            var merit = PersonCell(() => source.Merit(c, true));
            var command = PersonCell(() => source.BonusDisplay(c, () => source.CreditBonus(c, 1)));
            var areas = PersonCell(() => source.BonusDisplay(c, () => source.AdditionalBonus(c, () => Additional(c.PhysicalSlot))));
            var credit3 = PersonCell(() => source.BonusDisplay(c, () => source.CreditBonus(c, 3)));
            var paint = PersonCell<int?>(() => source.RowState(c));
            return new CityDomesticPersonMeritRow(occurrence, c, name, paint, stage, merit, command, areas, credit3);
        });
    public AreaAdministrationQuery<CityDomesticPersonPage<CityDomesticPersonTaskRow>> QueryPersonTaskPage(
        CityDomesticPersonSelection selection, IReadOnlyList<int> order) => PersonPage(selection, order, (source, c, occurrence) =>
        {
            var name = PersonCell(() => source.Name(c)); var task = PersonCell(() => source.Task(c));
            var target = PersonCell(() => source.TaskTarget(c)); var remaining = PersonCell(() => source.TaskRemaining(c));
            var returning = PersonCell(() => source.Returning(c)); var reset = PersonCell(() => source.Reset(c));
            var paint = PersonCell<int?>(() => source.RowState(c));
            return new CityDomesticPersonTaskRow(occurrence, c, name, paint, task, target, remaining, returning, reset);
        });
    public AreaAdministrationQuery<CityDomesticPersonPage<CityDomesticPersonPersonalRow>> QueryPersonPersonalPage(
        CityDomesticPersonSelection selection, IReadOnlyList<int> order) => PersonPage(selection, order, (source, c, occurrence) =>
        {
            var name = PersonCell(() => source.Name(c)); var sex = PersonCell(() => source.Sex(c));
            var age = PersonCell(() => source.Age(c, true)); var doctrine = PersonCell(() => source.Doctrine(c));
            var father = PersonCell(() => source.Relation(c, 0xB4, false)); var mother = PersonCell(() => source.Relation(c, 0xB6, false));
            var partner = PersonCell(() => source.Relation(c, 0xBA, false)); var sworn = PersonCell(() => source.Relation(c, 0xBC, false));
            var paint = PersonCell<int?>(() => source.RowState(c));
            return new CityDomesticPersonPersonalRow(occurrence, c, name, paint, sex, age, doctrine, father, mother, partner, sworn);
        });
}
