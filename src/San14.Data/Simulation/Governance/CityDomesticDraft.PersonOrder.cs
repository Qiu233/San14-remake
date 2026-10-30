namespace San14.Data;

public sealed partial class CityDomesticDraft
{
    /// <summary>Orders every occurrence on a local copy. A missing leaf never
    /// publishes a partial order or changes the role's initial publication.</summary>
    public AreaAdministrationQuery<IReadOnlyList<int>> QueryPersonOrder(CityDomesticPersonSelection selection,
        IReadOnlyList<int> currentOrder, CityDomesticPersonSortColumn column, CityDomesticPersonSortDirection direction)
    {
        try
        {
            CheckPersonSelection(selection);
            if (!Enum.IsDefined(column) || !Enum.IsDefined(direction))
                throw CityDomesticPersonCellSource.Missing("sort_column");
            var rows = PersonOrder(selection, currentOrder).ToList();
            var source = new CityDomesticPersonCellSource(_state, _current, _definitions, _governorInputs, _scenario, _personResources, _language, _userSelectionServicePresent);
            bool reversed = direction == CityDomesticPersonSortDirection.Reversed;
            AreaAdministrationAutomaticOrder.StableSort(rows, Before);
            return AreaAdministrationQuery<IReadOnlyList<int>>.Known(rows.AsReadOnly());

            bool Before(int one, int two)
            {
                var a = selection.Candidates[one]; var b = selection.Candidates[two];
                int p = a.PhysicalSlot, q = b.PhysicalSlot;
                // Match the original payload only at the special-person
                // predicate's actual identity read. Early type3 gates stay lazy.
                bool Special(int physical) => source.Special(physical == p ? a : b);
                if (column == CityDomesticPersonSortColumn.PrimaryResponsibility)
                    return DeploymentPersonResponsibilitySource.BeforePrimary(_state, _current, p, q, Special, reversed);
                if (column is CityDomesticPersonSortColumn.SecondaryResponsibility or CityDomesticPersonSortColumn.TertiaryResponsibility)
                    return DeploymentPersonResponsibilitySource.BeforeSecondary(_state, _current, p, q,
                        column == CityDomesticPersonSortColumn.SecondaryResponsibility ? 1 : 2,
                        Special, Additional, _vectors.Count, _vectors.Entry, reversed);
                if (column == CityDomesticPersonSortColumn.Force) return source.BeforeForce(a, b, reversed);
                if (column is CityDomesticPersonSortColumn.Home or CityDomesticPersonSortColumn.CurrentLocation)
                    return reversed ? source.BeforeLocation(b, a, column == CityDomesticPersonSortColumn.CurrentLocation)
                        : source.BeforeLocation(a, b, column == CityDomesticPersonSortColumn.CurrentLocation);
                if (column == CityDomesticPersonSortColumn.TaskTarget)
                    return source.BeforeTaskTarget(a, b, reversed);
                if (column >= CityDomesticPersonSortColumn.District)
                {
                    var left = Key(a); var right = Key(b);
                    if (left.NotApplicable != right.NotApplicable) return !left.NotApplicable;
                    if (left.NotApplicable) return false;
                    if (left.Text is not null || right.Text is not null)
                    {
                        if (left.Text is null || right.Text is null) throw CityDomesticPersonCellSource.Missing("sort_key_variant");
                        return reversed ? PersonNameOrder.Compare(right.Text, left.Text) < 0
                            : PersonNameOrder.Compare(left.Text, right.Text) < 0;
                    }
                    return reversed ? right.Number < left.Number : left.Number < right.Number;
                }
                // Both generic directions call the original left getter, then
                // the original right getter; only their returned keys reverse.
                if (column == CityDomesticPersonSortColumn.Name)
                {
                    string left = source.Name(a, true), right = source.Name(b, true);
                    return reversed ? PersonNameOrder.Compare(right, left) < 0 : PersonNameOrder.Compare(left, right) < 0;
                }
                if (column == CityDomesticPersonSortColumn.Task)
                {
                    int? left = source.TaskKey(a); int? right = source.TaskKey(b);
                    if (left.HasValue != right.HasValue) return left.HasValue;
                    return left.HasValue && (reversed ? right!.Value < left.Value : left.Value < right!.Value);
                }
                int first = Scalar(a), second = Scalar(b);
                return reversed ? second < first : first < second;
            }
            CityDomesticPersonCellSource.SortValue Key(CityDomesticCandidate c)
            {
                CityDomesticPersonCellSource.SortValue Number(int value) => CityDomesticPersonCellSource.SortValue.Numeric(value);
                int Dimension(int dimension) { source.Identity(c); return Ability(c.PhysicalSlot, dimension); }
                return column switch
                {
                    CityDomesticPersonSortColumn.District => source.DistrictKey(c),
                    CityDomesticPersonSortColumn.ServiceStatus => Number(source.ServiceStatusKey(c)),
                    CityDomesticPersonSortColumn.Loyalty => source.LoyaltyKey(c),
                    CityDomesticPersonSortColumn.ServiceYears => source.ServiceYearsKey(c),
                    CityDomesticPersonSortColumn.Intelligence => Number(Dimension(2)),
                    CityDomesticPersonSortColumn.Politics => Number(Dimension(3)),
                    CityDomesticPersonSortColumn.Charm => Number(Dimension(4)),
                    CityDomesticPersonSortColumn.Health => Number(source.HealthKey(c)),
                    CityDomesticPersonSortColumn.ItemCount => source.ItemKey(c),
                    CityDomesticPersonSortColumn.Office => source.OfficeKey(c),
                    CityDomesticPersonSortColumn.CommandCapacity => source.CommandCapacityKey(c),
                    CityDomesticPersonSortColumn.OtherMetric => source.OtherMetricKey(c),
                    CityDomesticPersonSortColumn.Doctrine => source.DoctrineKey(c),
                    CityDomesticPersonSortColumn.Age => source.AgeKey(c),
                    CityDomesticPersonSortColumn.Sex => Number(source.SexKey(c)),
                    CityDomesticPersonSortColumn.Father => source.RelationKey(c, 0xB4),
                    CityDomesticPersonSortColumn.Mother => source.RelationKey(c, 0xB6),
                    CityDomesticPersonSortColumn.Partner => source.RelationKey(c, 0xBA),
                    CityDomesticPersonSortColumn.Sworn => source.RelationKey(c, 0xBC),
                    CityDomesticPersonSortColumn.TaskRemaining => source.TaskRemainingKey(c),
                    CityDomesticPersonSortColumn.Merit => source.MeritKey(c),
                    CityDomesticPersonSortColumn.CreditRankStage => source.RankStageKey(c),
                    CityDomesticPersonSortColumn.CommandBonus => Number(source.CreditBonus(c, 1)),
                    CityDomesticPersonSortColumn.AdditionalAreaCapacity => Number(source.AdditionalBonus(c)),
                    CityDomesticPersonSortColumn.CreditKind3Bonus => Number(source.CreditBonus(c, 3)),
                    CityDomesticPersonSortColumn.ResetCounter => source.ResetKey(c),
                    _ => throw CityDomesticPersonCellSource.Missing("sort_key")
                };
            }
            int Scalar(CityDomesticCandidate candidate)
            {
                int physical = candidate.PhysicalSlot;
                switch (column)
                {
                    case CityDomesticPersonSortColumn.Leadership:
                        source.Identity(candidate); return Ability(physical, 0);
                    case CityDomesticPersonSortColumn.Strength:
                        source.Identity(candidate); return Ability(physical, 1);
                    case CityDomesticPersonSortColumn.RolePersonality:
                        return source.Personality(candidate, selection.Duty) ? 1 : 0;
                    case CityDomesticPersonSortColumn.Returning: return source.ReturningKey(candidate);
                    case CityDomesticPersonSortColumn.AreaLocation: return source.AreaLocation(candidate, false) ? 1 : 0;
                    default: throw CityDomesticPersonCellSource.Missing("sort_scalar");
                }
            }
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<IReadOnlyList<int>>.Unknown(error.Message); }
    }
}
