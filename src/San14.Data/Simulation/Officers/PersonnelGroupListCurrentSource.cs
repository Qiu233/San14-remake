using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed class PersonnelGroupListCurrentReader(ArmyCampaignRuleAdapter owner, GameState state,
        SourceWorldContext current, int forceId, PersonnelGroupMovementRules rules,
        IReadOnlyList<PersonnelGroupDestination> destinations)
    {
        private readonly Dictionary<int, OfficerDetailsCurrentReader> readers = [];
        private readonly Dictionary<(int, int), PersonnelGroupPersonCell> cells = [];
        private readonly Dictionary<string, AreaAdministrationQuery<PersonnelGroupDestinationPage>> destinationPages = [];

        internal AreaAdministrationQuery<PersonnelGroupPersonPage> QueryPeople(IReadOnlyList<int> ids, int page,
            IReadOnlyDictionary<int, int> membership)
        {
            if (page is < 0 or > 9) return AreaAdministrationQuery<PersonnelGroupPersonPage>.Unknown("personnel.groups.person_page_index");
            var columns = PersonnelGroupListProfiles.Person[page];
            var rows = ids.Select(id => new PersonnelGroupPersonListRow(id, columns.Select(column =>
            {
                if (column == 185)
                {
                    int group = membership.GetValueOrDefault(id);
                    return new PersonnelGroupPersonCell(column, group == 0 ? "" : owner.ReadHistoryDataText(1, 0x73d)
                        .Replace("%d", group.ToString(CultureInfo.InvariantCulture)), group, group, null, false, null);
                }
                if (cells.TryGetValue((id, column), out var cached)) return cached;
                PersonnelGroupPersonCell result;
                try
                {
                    int physical = current.ResolveOfficerSource(id) ?? throw GroupListMissing("Person_pointer");
                    if (column is >= 40 and <= 65)
                        result = owner.ReadPersonnelGroupSkillCellCurrent(state, current, id, physical, column);
                    else
                    {
                        if (!readers.TryGetValue(id, out var reader))
                            readers[id] = reader = new(owner, state, current, forceId,
                                state.Officers.GetValueOrDefault(id) ?? throw GroupListMissing("Person_owner"), physical);
                        result = reader.ReadGroupListCell(column);
                    }
                }
                catch (InvalidDataException error) { result = new(column, null, null, null, null, false, error.Message); }
                return cells[(id, column)] = result;
            }).ToArray())).ToArray();
            return AreaAdministrationQuery<PersonnelGroupPersonPage>.Known(new(page, columns, rows));
        }

        internal int? PreselectedDestination(IReadOnlyList<int> selected)
        {
            int? home = null;
            foreach (int id in selected.SelectMany(rules.ReadGroupMembers))
            {
                int at = rules.Home(id);
                if (home is int previous && previous != at) return null;
                home = at;
            }
            return home is int destination && rules.DestinationLocationIds.Contains(destination) ? destination : null;
        }

        internal AreaAdministrationQuery<PersonnelGroupDestinationPage> QueryDestinations(IReadOnlyList<int> selected)
        {
            try
            {
            //6BC9D0 deduplicates the actual movable members' Homes. A cache
            //key describes this small selection only, never the whole World.
            var representatives = selected.SelectMany(rules.ReadGroupMembers).Where(rules.CanMoveOfficer)
                .GroupBy(rules.Home).Select(group => group.First()).ToArray();
            string key = string.Join(",", representatives.Select(rules.Home));
            if (destinationPages.TryGetValue(key, out var cached)) return cached;
            var columns = PersonnelGroupListProfiles.Destination;
            var rows = destinations.Select(destination => new PersonnelGroupDestinationListRow(destination.LocationId,
                columns.Select(column => DestinationCell(destination, column)).ToArray())).ToArray();
            return destinationPages[key] = AreaAdministrationQuery<PersonnelGroupDestinationPage>.Known(new(columns, rows));

            PersonnelGroupPersonCell DestinationCell(PersonnelGroupDestination row, int column)
            {
                try
                {
                    var at = BaseLookup.Find(checked((ushort)row.LocationId), current);
                    if (column == 0) return new(column, row.Name, null, null, row.Name, false, null);
                    if (column == 1)
                    {
                        int district = current.ReadBaseDistrictSlot(at) ?? throw GroupListMissing("destination_District");
                        var receiver = current.ReadDistrictTableReference(district < 52 ? district : 0)
                            ?? throw GroupListMissing("destination_District_pointer");
                        if (!(current.ReadDistrictNativeValidity(receiver) ?? throw GroupListMissing("destination_District_validity")))
                            return new(column, "", null, null, "", false, null);
                        int leader = current.ReadDistrictLeader(district) ?? throw GroupListMissing("destination_District_leader");
                        string name = state.Officers.GetValueOrDefault(leader)?.Name ?? throw GroupListMissing("destination_leader_name");
                        string text = "^02" + name + owner.ReadHistoryDataText(1, 0x350) + "^00";
                        //3469B0 compares the actual22ED50 text, not the
                        //District's display number or physical registry slot.
                        return new(column, text, null, null, text, false, null);
                    }
                    int value;
                    if (column == 2)
                    {
                        value = 0;
                        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw GroupListMissing("destination_Person_registry"))
                        {
                            int p = entry ?? throw GroupListMissing("destination_Person_pointer");
                            int home = current.ReadPersonFirstLocation(p) ?? throw GroupListMissing("destination_PersonE2");
                            if (home != row.LocationId) continue;
                            byte status = current.ReadPersonStatus(p) ?? throw GroupListMissing("destination_PersonE6");
                            if (status is >= 1 and <= 4) value++;
                        }
                    }
                    else if (column is 3 or 4 or 5)
                        value = current.ReadBaseResource(at, column == 3 ? BaseResourceKind.Troops : column == 4
                            ? BaseResourceKind.Gold : BaseResourceKind.Supplies) ?? throw GroupListMissing("destination_resource");
                    else if (column == 7)
                    {
                        value = 0;
                        foreach (int id in representatives)
                            if (rules.Home(id) != row.LocationId)
                                value = Math.Max(value, rules.ReadTravelDays(id, row.LocationId)
                                    ?? throw GroupListMissing("destination_route_days"));
                        value = unchecked((short)value); //the real selector's short cache
                    }
                    else throw GroupListMissing("destination_column");
                    return new(column, column == 7 && value == 0 ? "" : null, value, value, null, false, null);
                }
                catch (InvalidDataException error) { return new(column, null, null, null, null, false, error.Message); }
            }
            }
            catch (InvalidDataException error)
            { return AreaAdministrationQuery<PersonnelGroupDestinationPage>.Unknown(error.Message); }
        }
    }

    private sealed partial class OfficerDetailsCurrentReader
    {
        private IReadOnlyDictionary<int, PersonnelGroupPersonCell>? groupDevelopmentPreview;

        private PersonnelGroupPersonCell ReadGroupDevelopmentPreviewCell(int column)
        {
            // The four selector columns share one source preview evaluation.
            // This reader already belongs to one Person and retained page current.
            groupDevelopmentPreview ??= owner.ReadPersonnelGroupDevelopmentPreviewCurrent(state, current, physical)
                .ToDictionary(cell => cell.ColumnId);
            return groupDevelopmentPreview[column];
        }

        internal PersonnelGroupPersonCell ReadGroupListCell(int column)
        {
            PersonnelGroupPersonCell Text(Func<string?> read, Func<CityDomesticPersonCellSource.SortValue>? key = null)
            {
                string? text;
                try { text = read(); }
                catch (InvalidDataException error) { return new(column, null, null, null, null, false, error.Message); }
                var result = new PersonnelGroupPersonCell(column, text, null, null, null, text is null, null);
                if (key is null) return result with { SortUnresolvedOwner = "personnel.groups.person_sort." + column };
                try { var sort = key(); return result with { SortNumber = sort.Text is null ? sort.Number : null,
                    SortKey = sort.Text, SortGroup = sort.NotApplicable ? 1 : 0 }; }
                catch (InvalidDataException error) { return result with { SortUnresolvedOwner = error.Message }; }
            }
            PersonnelGroupPersonCell Number(Func<int?> read, Func<CityDomesticPersonCellSource.SortValue>? key = null,
                Func<int, string?>? format = null)
            {
                int? value;
                try { value = read(); }
                catch (InvalidDataException error) { return new(column, null, null, null, null, false, error.Message); }
                var result = new PersonnelGroupPersonCell(column, value is int n ? format?.Invoke(n) : null,
                    value, value, null, value is null, null);
                if (key is null) return result;
                try { var sort = key(); return result with { SortNumber = sort.Text is null ? sort.Number : null,
                    SortKey = sort.Text, SortGroup = sort.NotApplicable ? 1 : 0 }; }
                catch (InvalidDataException error) { return result with { SortUnresolvedOwner = error.Message }; }
            }
            CityDomesticPersonCellSource.SortValue Numeric(int value) => CityDomesticPersonCellSource.SortValue.Numeric(value);
            int Ability(int dimension) => new TacticRemakeCurrentAbilities(current, owner._scenario, owner.Seed.Language,
                SimulationRuleIdentity.ForLocale(owner.Seed.Language)).ResolveDimension(state, person.Id, dimension,
                    (force, effect) => owner._baseCapacityInputs.ReadPanelModifier(state, force, effect))
                ?? throw GroupListMissing("effective_ability");
            string Mark(bool yes) => Data(1, yes ? 0x338 : 0x339);
            int Credit(int kind) => kind == 2 ? PersonAreaAppointmentSource.AdditionalCount(state, current, physical, owner.ReadApplicationGameplay()?.UserSelectionServicePresent)
                : OfficerCommandCapacity.ResolveCreditKindBonus(state, current, physical, kind, owner.ReadApplicationGameplay()?.UserSelectionServicePresent).Bonus
                    ?? throw GroupListMissing("credit_kind_" + kind);
            string? Responsibility(int index)
            {
                int Count(int p) => PersonAssignmentPrecallInputs.ReadCurrentAreaAssignmentActiveCount(state, p)
                    ?? throw GroupListMissing("responsibility_count");
                int Entry(int p, int slot)
                {
                    var values = state.NativeArmySimulation?.Parameters.Int32Values ?? throw GroupListMissing("responsibility_owner");
                    if (!values.TryGetValue(PersonAssignmentPrecallInputs.PersonAreaEntryKey(p, slot), out int value))
                        throw GroupListMissing("responsibility_entry");
                    return value;
                }
                var responsibility = index == 0 ? DeploymentPersonResponsibilityDisplaySource.Read(state, current, physical, Count, Entry)
                    : DeploymentPersonResponsibilityDisplaySource.ReadSecondary(state, current, physical, index,
                        p => PersonAreaAppointmentSource.AdditionalCount(state, current, p, owner.ReadApplicationGameplay()?.UserSelectionServicePresent), Count, Entry);
                return responsibility.Kind switch
                {
                    DeploymentPersonResponsibilityKind.Empty => "",
                    DeploymentPersonResponsibilityKind.NotApplicable => null,
                    DeploymentPersonResponsibilityKind.AreaName => state.Areas.GetValueOrDefault(responsibility.AreaId!.Value)?.Name
                        ?? throw GroupListMissing("responsibility_Area_name"),
                    DeploymentPersonResponsibilityKind.Recruit => Data(1, 0x20),
                    DeploymentPersonResponsibilityKind.Train => Data(1, 0x21),
                    DeploymentPersonResponsibilityKind.Spy => Data(1, 0x9c),
                    _ => throw GroupListMissing("responsibility_kind")
                };
            }
            CityDomesticPersonCellSource.SortValue TaskKey() =>
                PersonTasks.InformationTaskNameKey() is int value ? Numeric(value) : CityDomesticPersonCellSource.SortValue.Empty;
            PersonnelGroupPersonCell RemainingCell()
            {
                try
                {
                    var remaining = PersonTasks.ReadRemainingCell();
                    if (remaining is null) return new(column, "", null, int.MaxValue, null, true, null) { SortGroup = 1 };
                    if (remaining.NotApplicable) return new(column, Data(1, 0x353), null, null, null, false, null)
                        { SortUnresolvedOwner = "personnel.groups.task16_remaining_sort" };
                    return Number(() => remaining.Value, format: days => days.ToString(CultureInfo.InvariantCulture));
                }
                catch (InvalidDataException error) { return new(column, null, null, null, null, false, error.Message); }
            }
            PersonnelGroupPersonCell LocationCell(bool actual)
            {
                var cell = Text(() => actual ? ReadPlace(true)?.Name : Cells.Home(candidate));
                try { return cell with { LocationOrder = Cells.LocationOrderInput(candidate, actual), SortUnresolvedOwner = null }; }
                catch (InvalidDataException error) { return cell with { SortUnresolvedOwner = error.Message }; }
            }
            return column switch
            {
                0 => Text(() => Cells.Name(candidate), () => CityDomesticPersonCellSource.SortValue.String(Cells.Name(candidate, true))),
                1 => Text(() => Cells.ForceName(candidate)?.Text),
                2 => Number(() => Cells.DistrictNumber(candidate), () => Cells.DistrictKey(candidate),
                    value => Data(1, 0x494).Replace("%d", value.ToString(CultureInfo.InvariantCulture))),
                3 => LocationCell(false),
                4 => LocationCell(true),
                5 => Text(() => (Flags & 0x4000) != 0 ? Data(1, 0x859) : Status < 10 ? Data(0, 0xf9 + Status) : "",
                    () => Numeric(Cells.ServiceStatusKey(candidate))),
                6 => Number(() => Cells.Loyalty(candidate), () => Cells.LoyaltyKey(candidate)),
                7 => Number(() => Cells.ServiceYears(candidate, true), () => Cells.ServiceYearsKey(candidate)),
                >= 8 and <= 12 => Number(() => Ability(column - 8)),
                18 => Text(() => Cells.Health(candidate) is int health ? Data(0, 0x123 + health) : "", () => Numeric(Cells.HealthKey(candidate))),
                20 => Number(ReadItemCount),
                21 => Text(() => Cells.Office(candidate), () => Cells.OfficeKey(candidate)),
                22 => Text(() => "", () => CityDomesticPersonCellSource.SortValue.Empty), //reserved source column
                23 => Number(() => Cells.OtherMetric(candidate), () => Cells.OtherMetricKey(candidate)),
                34 => Text(() => Mark((Flags & 1) == 0), () => Numeric(Cells.ReturningKey(candidate))),
                35 => Text(() => Responsibility(0)),
                36 => Text(() => Cells.Doctrine(candidate), () => Cells.DoctrineKey(candidate)),
                37 => Text(ReadTaskName, TaskKey),
                86 => Text(ReadTaskTarget),
                88 => RemainingCell(),
                132 or 133 or 134 or 139 => ReadGroupDevelopmentPreviewCell(column),
                193 => Number(ReadMerit, () => Cells.MeritKey(candidate)),
                194 => Text(ReadMeritRank, () => Cells.RankStageKey(candidate)),
                196 or 197 or 198 => Number(() => Credit(column == 196 ? 1 : column == 197 ? 2 : 3),
                    format: value => value > 0 ? Data(1, 0x38c).Replace("%d", value.ToString(CultureInfo.InvariantCulture)) : ""),
                204 => Number(() => Cells.Reset(candidate), () => Cells.ResetKey(candidate), value => value.ToString(CultureInfo.InvariantCulture)),
                208 => Text(() => Responsibility(1)),
                209 => Text(() => Responsibility(2)),
                _ => new(column, null, null, null, null, false, "personnel.groups.person_column." + column)
            };
        }
    }

    private static InvalidDataException GroupListMissing(string field) => new("personnel.groups.list." + field);
}
