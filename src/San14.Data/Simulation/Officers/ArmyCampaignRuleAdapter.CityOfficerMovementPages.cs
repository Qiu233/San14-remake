using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private AreaAdministrationQuery<CityOfficerMovementPersonSelection> PrepareCityOfficerMovementPersonSelection(
        CityOfficerMovementSource source, int playerForceId)
    {
        try
        {
            var reader = new OfficerInformationCurrent(this, source.State, playerForceId, rowPaletteEnabled: true, retainedCurrent: source.Current);
            reader.EnableMovementPalette(source.SummonTravelDays);
            var ids = source.People.Select(person => person.OfficerId).ToArray();
            var pages = reader.Pages(ids, ReadHistoryDataText(1, 0x32E),
                CityOfficerMovementPersonColumnsSource.Create(source.Mode, ReadHistoryDataText));
            var health = ids.ToDictionary(id => id, reader.MovementAlteredHealth);
            return AreaAdministrationQuery<CityOfficerMovementPersonSelection>.Known(new(pages, health, ids.Length));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<CityOfficerMovementPersonSelection>.Unknown(error.Message); }
    }

    private sealed partial class OfficerInformationCurrent
    {
        private bool commandBodyPalette;
        private Func<int, int>? movementTravelDays;
        private readonly Dictionary<int, IReadOnlyDictionary<int, PersonnelGroupPersonCell>> movementDevelopment = [];

        internal void EnableMovementPalette(Func<int,int> readTravelDays)
        { commandBodyPalette = true; movementTravelDays = readTravelDays; }
        internal AreaAdministrationQuery<bool> MovementAlteredHealth(int id)
        {
            try { return AreaAdministrationQuery<bool>.Known(Need(OfficerHealthSourceAdapter.ReadByte(current,
                ReadPerson(id).PhysicalSlot), "movement.row_health") is >= 1 and <= 3); }
            catch (InvalidDataException error) { return AreaAdministrationQuery<bool>.Unknown(error.Message); }
        }

        private PreparedCell MovementCell(Person person, int column)
        {
            if (column is 132 or 133 or 134 or 139)
            {
                if (!movementDevelopment.TryGetValue(person.Id, out var entries))
                    movementDevelopment.Add(person.Id, entries = rules.ReadPersonnelGroupDevelopmentPreviewCurrent(state, current,
                        person.PhysicalSlot).ToDictionary(cell => cell.ColumnId));
                var entry = entries[column];
                var cell = MakeCell(() => entry.UnresolvedOwner is null ? entry.Text ?? "" : throw new InvalidDataException(entry.UnresolvedOwner),
                    () => entry.SortUnresolvedOwner is null && (entry.SortNumber ?? entry.Number) is int value
                        ? new NumberKey(value) : throw new InvalidDataException(entry.SortUnresolvedOwner ?? Missing("movement.development_sort").Message),
                    statePalette: column == 139 ? () => BasicRowPalette(person, 9) : null);
                return cell with { Display = cell.Display with { SourceFormattedText = entry.SourceFormattedText,
                    PresetUnresolvedOwner = JoinMissing(cell.Display.PresetUnresolvedOwner, entry.PresentationUnresolvedOwner) } };
            }
            return column switch
            {
                39 => MakeCell(() => MovementResponsibility(person, 0), () => new MovementPrimaryKey(this, person)),
                208 or 209 => MakeCell(() => MovementResponsibility(person, column - 207),
                    () => new MovementSecondaryKey(this, person, column - 207)),
                102 => MakeCell(() => Number(MovementDays(person)),
                    () => new NumberKey(InformationNativeValid(person) ? MovementDays(person) : int.MaxValue),
                    statePalette: () => BasicRowPalette(person, 5)),
                210 => MakeCell(() => MovementPriority(person) ? Data(1, 0x338)
                        : InformationSpecialForce(person) || Cells.Special(person.Candidate) || (InformationFlags(person) & 0x4000) != 0
                            ? Data(1, 0x353) : "",
                    () => new NumberKey(MovementPriority(person) ? 1 : 0)),
                _ => throw Missing("movement.column." + column)
            };
        }
        private static string? JoinMissing(string? first, string? second) => second is null ? first : first is null ? second : first + ";" + second;
        private int MovementDays(Person person) => movementTravelDays?.Invoke(person.Id) ?? throw Missing("movement.prepared_days");
        private bool MovementPriority(Person person) =>
            (Need(rules._governorInputs.ReadCurrentPriority160(state, person.PhysicalSlot), "movement.priority160") & 1) != 0 || InformationStatus(person) is 1 or 2;
        private int MovementAssignmentCount(int physical) => PersonAssignmentPrecallInputs.ReadCurrentAreaAssignmentActiveCount(state, physical)
            ?? throw Missing("movement.assignment_count");
        private int MovementAssignmentEntry(int physical, int slot)
        {
            var parameters = state.NativeArmySimulation?.Parameters.Int32Values ?? throw Missing("movement.assignment_owner");
            return parameters.TryGetValue(PersonAssignmentPrecallInputs.PersonAreaEntryKey(physical, slot), out int value)
                ? value : throw Missing("movement.assignment_entry");
        }
        private int MovementAdditionalCount(int physical) => PersonAreaAppointmentSource.AdditionalCount(state, current, physical, rules.ReadApplicationGameplay()?.UserSelectionServicePresent);
        private bool MovementSpecial(int physical) => Cells.Special(new(checked((ushort)Need(current.ReadPersonIdentity(physical), "movement.Person_id")), physical));
        private string MovementResponsibility(Person person, int index)
        {
            var value = index == 0
                ? DeploymentPersonResponsibilityDisplaySource.Read(state, current, person.PhysicalSlot, MovementAssignmentCount, MovementAssignmentEntry)
                : DeploymentPersonResponsibilityDisplaySource.ReadSecondary(state, current, person.PhysicalSlot, index,
                    MovementAdditionalCount, MovementAssignmentCount, MovementAssignmentEntry);
            return value.Kind switch
            {
                DeploymentPersonResponsibilityKind.Empty => "",
                DeploymentPersonResponsibilityKind.NotApplicable => Data(1, 0x353),
                DeploymentPersonResponsibilityKind.AreaName => state.Areas.GetValueOrDefault(value.AreaId!.Value)?.Name ?? throw Missing("movement.Area_name"),
                DeploymentPersonResponsibilityKind.Recruit => Data(1, 0x20),
                DeploymentPersonResponsibilityKind.Train => Data(1, 0x21),
                DeploymentPersonResponsibilityKind.Spy => Data(1, 0x9C),
                _ => throw Missing("movement.responsibility_kind")
            };
        }
        private sealed record MovementPrimaryKey : SortKey
        {
            private readonly OfficerInformationCurrent reader;
            private readonly Lazy<DeploymentPersonResponsibilityKey> value;
            internal MovementPrimaryKey(OfficerInformationCurrent reader, Person person)
            { this.reader = reader; value = new(() => DeploymentPersonResponsibilitySource.Read(reader.state, reader.current,
                person.PhysicalSlot, () => reader.Cells.Special(person.Candidate))); }
            internal override bool Before(SortKey other, bool ascending)
            {
                if (!ascending) return ((MovementPrimaryKey)other).Before(this, true);
                var a = value.Value; var b = ((MovementPrimaryKey)other).value.Value;
                string Name(int? id) => id is int area ? reader.state.Areas.GetValueOrDefault(area)?.Name ?? throw Missing("movement.sort_Area_name") : "";
                return a.Weight == b.Weight ? DeploymentResponsibilityNameOrder.Before(Name(b.NamedArea), Name(a.NamedArea)) : a.Weight < b.Weight;
            }
        }
        private sealed record MovementSecondaryKey(OfficerInformationCurrent Reader, Person Person, int Index) : SortKey
        {
            internal override bool Before(SortKey other, bool ascending) => DeploymentPersonResponsibilitySource.BeforeSecondary(
                Reader.state, Reader.current, Person.PhysicalSlot, ((MovementSecondaryKey)other).Person.PhysicalSlot, Index,
                Reader.MovementSpecial, Reader.MovementAdditionalCount, Reader.MovementAssignmentCount, Reader.MovementAssignmentEntry,
                reversed: !ascending);
        }
    }
}
