using System.Globalization;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>Formats the requested typed page. Display text never becomes a
/// sorting key, candidate identity or command permission.</summary>
internal static class CityDomesticPersonListContent
{
    internal static IReadOnlyList<CityDomesticPersonEntry> Create(GameResourceRepository repository,
        PreviewText text, GameState world, CityDomesticPersonBasicPage page)
    {
        var f = new Formatting(repository, text, world);
        return page.Rows.Select(r => f.Entry(r.Occurrence, r.Candidate, r.Name, r.RowState,
            CityDomesticPersonColumns.For(page.Selection.Duty).Select(c => c.Id switch
            {
                0 => f.Name(r.Name), 8 => f.Number(r.Leadership), 9 => f.Number(r.Strength),
                121 or 122 => f.Mark(r.RolePersonality),
                35 => f.Responsibility(r.PrimaryResponsibility),
                208 => f.Responsibility(r.SecondaryResponsibility),
                209 => f.Responsibility(r.TertiaryResponsibility),
                34 => f.Returning(r.Returning), 36 => f.Mark(r.AreaLocation), 37 => f.Task(r.Task),
                _ => throw new InvalidDataException("Column is outside the City Basic preset.")
            }).ToArray(), includesName: true)).ToArray();
    }

    internal static IReadOnlyList<CityDomesticPersonEntry> Create(GameResourceRepository repository,
        PreviewText text, GameState world, CityDomesticPersonPage<CityDomesticPersonAffiliationRow> page)
    {
        var f = new Formatting(repository, text, world);
        return page.Rows.Select(r => f.Entry(r.Occurrence, r.Candidate, r.Name, r.RowState,
            new DeploymentPersonCell[]
            {f.Force(r.ForceName), new(f.PatternNumber(r.DistrictNumber, 0x0494)), new(f.Text(r.HomeName, nullIsNotApplicable: true)),
             new(f.Text(r.CurrentLocationName)), new(f.Status(r.ServiceStatus)), new(f.Responsibility(r.PrimaryResponsibility)),
             new(f.Number(r.Loyalty, nullIsNotApplicable: true)), new(f.Text(r.OfficeName, nullIsNotApplicable: true)),
             new(f.Number(r.OtherMetric, nullIsNotApplicable: true)), new(f.Number(r.ServiceYears, nullIsNotApplicable: true))})).ToArray();
    }

    internal static IReadOnlyList<CityDomesticPersonEntry> Create(GameResourceRepository repository,
        PreviewText text, GameState world, CityDomesticPersonPage<CityDomesticPersonAbilityRow> page)
    {
        var f = new Formatting(repository, text, world);
        return page.Rows.Select(r => f.Entry(r.Occurrence, r.Candidate, r.Name, r.RowState,
            [f.Status(r.ServiceStatus), f.Number(r.Leadership), f.Number(r.Strength), f.Number(r.Intelligence),
             f.Number(r.Politics), f.Number(r.Charm), f.Health(r.HealthCode), f.Number(r.ItemCount)])).ToArray();
    }

    internal static IReadOnlyList<CityDomesticPersonEntry> Create(GameResourceRepository repository,
        PreviewText text, GameState world, CityDomesticPersonPage<CityDomesticPersonMeritRow> page)
    {
        var f = new Formatting(repository, text, world);
        return page.Rows.Select(r => f.Entry(r.Occurrence, r.Candidate, r.Name, r.RowState,
            [f.Text(r.RankName, nullIsNotApplicable: true), f.Number(r.Merit, nullIsNotApplicable: true), f.PatternNumber(r.CommandBonus, 0x038C),
             f.PatternNumber(r.AdditionalAreaCapacity, 0x038C), f.PatternNumber(r.CreditKind3Bonus, 0x038C)])).ToArray();
    }

    internal static IReadOnlyList<CityDomesticPersonEntry> Create(GameResourceRepository repository,
        PreviewText text, GameState world, CityDomesticPersonPage<CityDomesticPersonTaskRow> page)
    {
        var f = new Formatting(repository, text, world);
        return page.Rows.Select(r => f.Entry(r.Occurrence, r.Candidate, r.Name, r.RowState,
            [f.Task(r.Task), f.Text(r.TargetName), f.Remaining(r.Remaining), f.Returning(r.Returning),
             f.Number(r.ResetValue)])).ToArray();
    }

    internal static IReadOnlyList<CityDomesticPersonEntry> Create(GameResourceRepository repository,
        PreviewText text, GameState world, CityDomesticPersonPage<CityDomesticPersonPersonalRow> page)
    {
        var f = new Formatting(repository, text, world);
        return page.Rows.Select(r => f.Entry(r.Occurrence, r.Candidate, r.Name, r.RowState,
            [f.Sex(r.SexCode), f.Number(r.AgeValue, nullIsNotApplicable: true), f.Text(r.DoctrineName, nullIsNotApplicable: true), f.Text(r.FatherName),
             f.Text(r.MotherName), f.Text(r.PartnerName), f.Text(r.SwornName)])).ToArray();
    }

    private sealed class Formatting(GameResourceRepository repository, PreviewText text, GameState world)
    {
        private readonly MessageDocument messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages;
        private readonly string unknown = text.Get("detail_unknown");
        private string Message(int id, int section = 1)
        {
            var records = messages.Sections[section].Records;
            return (uint)id < (uint)records.Count && records[id].Text is { } value ? value
                : throw new InvalidDataException("Missing City person cell caption.");
        }
        internal DeploymentPersonCell Force(AreaAdministrationQuery<CityDomesticPersonForceName?> field) => !field.Available
            ? new(unknown) : field.Value is { } value ? new(value.Text, FirstGlyphColour: value.MarkerColour.CpuPacked) : new("");
        internal string Name(AreaAdministrationQuery<string> field) => !field.Available ? unknown : field.Value ?? "";
        internal string Text(AreaAdministrationQuery<string?> field, bool nullIsNotApplicable = false) => !field.Available
            ? unknown : field.Value ?? (nullIsNotApplicable ? Message(0x0353) : "");
        internal string Number(AreaAdministrationQuery<int?> field, bool nullIsNotApplicable = false) => !field.Available
            ? unknown : field.Value?.ToString(CultureInfo.InvariantCulture) ?? (nullIsNotApplicable ? Message(0x0353) : "");
        internal string PatternNumber(AreaAdministrationQuery<int?> field, int message)
        {
            if (!field.Available) return unknown;
            if (field.Value is not int value) return "";
            string pattern = Message(message);
            int token = pattern.IndexOf("%d", StringComparison.Ordinal);
            if (token < 0 || pattern.Remove(token, 2).Contains('%'))
                throw new InvalidDataException("Unsupported City person integer format.");
            return pattern.Replace("%d", value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }
        internal string Mark(AreaAdministrationQuery<bool?> field) => !field.Available ? unknown : field.Value == true ? Message(0x0338) : "";
        internal string Returning(AreaAdministrationQuery<bool?> field) => !field.Available ? unknown : field.Value is bool value
            ? Message(value ? 0x0339 : 0x0338) : Message(0x0353);
        internal string Status(AreaAdministrationQuery<DeploymentPersonServiceStatus?> field) => !field.Available ? unknown
            : field.Value is not { } value ? "" : value.Hired ? Message(0x0859)
            : value.StatusCode is byte code ? Message(0x00F9 + code, 0) : "";
        internal string Health(AreaAdministrationQuery<int?> field) => !field.Available ? unknown
            : field.Value is >= 0 and <= 3 ? Message(0x0123 + field.Value.Value, 0) : "";
        internal string Sex(AreaAdministrationQuery<int?> field) => !field.Available ? unknown
            : field.Value is 0 or 1 ? Message(0x0495 + field.Value.Value) : field.Value is null ? "" : unknown;
        internal string Task(AreaAdministrationQuery<int?> field) => field.Available && field.Value is null ? "" : unknown;
        internal string Remaining(AreaAdministrationQuery<CityDomesticPersonTaskRemaining?> field) => !field.Available ? unknown
            : field.Value is not { } value ? "" : value.NotApplicable ? Message(0x0353)
            : value.Value?.ToString(CultureInfo.InvariantCulture) ?? unknown;
        internal string Responsibility(AreaAdministrationQuery<DeploymentPersonResponsibility?> field)
        {
            if (!field.Available) return unknown;
            if (field.Value is not { } value) return "";
            return value.Kind switch
            {
                DeploymentPersonResponsibilityKind.Empty => "",
                DeploymentPersonResponsibilityKind.NotApplicable => Message(0x0353),
                DeploymentPersonResponsibilityKind.AreaName => value.AreaId is int area ? world.Areas.GetValueOrDefault(area)?.Name ?? unknown : unknown,
                DeploymentPersonResponsibilityKind.Recruit => Message(0x0020),
                DeploymentPersonResponsibilityKind.Train => Message(0x0021),
                DeploymentPersonResponsibilityKind.Spy => Message(0x009C),
                _ => unknown
            };
        }
        internal CityDomesticPersonEntry Entry(int occurrence, CityDomesticCandidate candidate,
            AreaAdministrationQuery<string> name, AreaAdministrationQuery<int?> rowState, string[] cells, bool includesName = false) =>
            Entry(occurrence, candidate, name, rowState, cells.Select(value => new DeploymentPersonCell(value)).ToArray(), includesName);
        internal CityDomesticPersonEntry Entry(int occurrence, CityDomesticCandidate candidate,
            AreaAdministrationQuery<string> name, AreaAdministrationQuery<int?> rowState, DeploymentPersonCell[] cells, bool includesName = false)
        {
            bool? health = rowState.Available ? rowState.Value switch { 0 => false, 2 => true, _ => null } : null;
            IEnumerable<DeploymentPersonCell> values = includesName ? cells : new[] { new DeploymentPersonCell(Name(name)) }.Concat(cells);
            return new(occurrence, candidate, values.ToArray(), health);
        }
    }

    internal static CityDomesticPersonSortColumn SortColumn(int column) => column switch
    {
        0 => CityDomesticPersonSortColumn.Name, 1 => CityDomesticPersonSortColumn.Force,
        2 => CityDomesticPersonSortColumn.District, 3 => CityDomesticPersonSortColumn.Home,
        4 => CityDomesticPersonSortColumn.CurrentLocation, 5 => CityDomesticPersonSortColumn.ServiceStatus,
        6 => CityDomesticPersonSortColumn.Loyalty, 7 => CityDomesticPersonSortColumn.ServiceYears,
        8 => CityDomesticPersonSortColumn.Leadership, 9 => CityDomesticPersonSortColumn.Strength,
        10 => CityDomesticPersonSortColumn.Intelligence, 11 => CityDomesticPersonSortColumn.Politics,
        12 => CityDomesticPersonSortColumn.Charm, 18 => CityDomesticPersonSortColumn.Health,
        20 => CityDomesticPersonSortColumn.ItemCount, 21 => CityDomesticPersonSortColumn.Office,
        23 => CityDomesticPersonSortColumn.OtherMetric, 24 => CityDomesticPersonSortColumn.Doctrine,
        28 => CityDomesticPersonSortColumn.Age, 29 => CityDomesticPersonSortColumn.Sex,
        30 => CityDomesticPersonSortColumn.Father, 31 => CityDomesticPersonSortColumn.Mother,
        32 => CityDomesticPersonSortColumn.Partner, 33 => CityDomesticPersonSortColumn.Sworn,
        34 => CityDomesticPersonSortColumn.Returning, 35 => CityDomesticPersonSortColumn.PrimaryResponsibility,
        36 => CityDomesticPersonSortColumn.AreaLocation, 37 => CityDomesticPersonSortColumn.Task,
        86 => CityDomesticPersonSortColumn.TaskTarget, 88 => CityDomesticPersonSortColumn.TaskRemaining,
        121 or 122 => CityDomesticPersonSortColumn.RolePersonality, 193 => CityDomesticPersonSortColumn.Merit,
        194 => CityDomesticPersonSortColumn.CreditRankStage, 196 => CityDomesticPersonSortColumn.CommandBonus,
        197 => CityDomesticPersonSortColumn.AdditionalAreaCapacity, 198 => CityDomesticPersonSortColumn.CreditKind3Bonus,
        204 => CityDomesticPersonSortColumn.ResetCounter, 208 => CityDomesticPersonSortColumn.SecondaryResponsibility,
        209 => CityDomesticPersonSortColumn.TertiaryResponsibility,
        _ => throw new ArgumentOutOfRangeException(nameof(column))
    };
}
