using System.Globalization;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>Locale formatting for one published basic list. Source empty values
/// remain empty; unavailable current values use the normal unknown caption.</summary>
internal static class DeploymentPersonListContent
{
    internal static IReadOnlyList<DeploymentPersonListEntry> CreateTraits(PreviewText text,
        IReadOnlyList<DeploymentPersonListEntry> basic, DeploymentPersonTraitQuery query)
    {
        if(!query.Available)throw new InvalidDataException(query.UnresolvedInput ?? "Person traits are unavailable.");
        var byId=basic.ToDictionary(entry=>entry.OfficerId);
        return query.Rows!.Select(row=>
        {
            var original=byId[row.OfficerId];
            var cells=new List<DeploymentPersonCell> {original.Cells[0]};
            cells.AddRange(row.Traits.Select(value=>new DeploymentPersonCell(!value.Available ? text.Get("detail_unknown") :
                value.Value?.Name ?? "")));
            return new DeploymentPersonListEntry(row.OfficerId,original.Disabled,cells);
        }).ToArray();
    }

    internal static IReadOnlyList<DeploymentPersonListEntry> CreateTactics(PreviewText text,
        IReadOnlyList<DeploymentPersonListEntry> basic, DeploymentPersonTacticQuery query,
        Func<int,string?>? tacticName)
    {
        if(!query.Available)throw new InvalidDataException(query.UnresolvedInput ?? "Person tactics are unavailable.");
        var byId=basic.ToDictionary(entry=>entry.OfficerId);
        string unknown=text.Get("detail_unknown");
        return query.Rows!.Select(row=>
        {
            var original=byId[row.OfficerId];
            var cells=new List<DeploymentPersonCell> {original.Cells[0]};
            cells.AddRange(row.Tactics.Select(value=>new DeploymentPersonCell(!value.Available ? unknown :
                value.Value is int tactic ? tacticName?.Invoke(tactic) ?? unknown : "")));
            return new DeploymentPersonListEntry(row.OfficerId,original.Disabled,cells);
        }).ToArray();
    }

    internal static (IReadOnlyList<DeploymentPersonListEntry> Entries,IReadOnlyList<string> Captions) CreateLinkage(
        GameResourceRepository repository,PreviewText text,IReadOnlyList<DeploymentPersonListEntry> basic,
        DeploymentPersonLinkageQuery query)
    {
        if(!query.Available)throw new InvalidDataException(query.UnresolvedInput ?? "Person linkage is unavailable.");
        var byId=basic.ToDictionary(entry=>entry.OfficerId);
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[0];
        var captions=query.SelectedOfficerIds!.Select(id=>id is int person ? byId[person].Cells[0].Text : "").ToArray();
        var entries=query.Rows!.Select(row=>
        {
            var original=byId[row.OfficerId];
            var cells=new List<DeploymentPersonCell> {original.Cells[0]};
            cells.AddRange(row.Tiers.Select(value=>new DeploymentPersonCell(!value.Available ? text.Get("detail_unknown") :
                value.Value is >=1 and <=5 ? messages.Records[0x0818+value.Value.Value].Text
                    ?? throw new InvalidDataException("Missing linkage tier caption.") : "")));
            return new DeploymentPersonListEntry(row.OfficerId,original.Disabled,cells);
        }).ToArray();
        return (entries,captions);
    }

    internal static IReadOnlyList<DeploymentPersonListEntry> CreateFormations(GameResourceRepository repository,
        PreviewText text, IReadOnlyList<DeploymentPersonListEntry> basic, DeploymentPersonFormationQuery query)
    {
        if (!query.Available) throw new InvalidDataException(query.UnresolvedInput ?? "Person formations are unavailable.");
        var byId = basic.ToDictionary(entry => entry.OfficerId);
        string circle = repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1].Records[0x0338].Text
            ?? throw new InvalidDataException("Missing formation capability caption.");
        return query.Rows!.Select(row =>
        {
            var entry = byId[row.OfficerId];
            var capabilities = row.Capabilities.ToDictionary(cell => cell.ResourceFormationId);
            var cells = DeploymentPersonColumns.Formations.Select(Cell).ToArray();
            return new DeploymentPersonListEntry(row.OfficerId,entry.Disabled,cells);

            DeploymentPersonCell Cell(DeploymentPersonColumn column)
            {
                if(column.Id==0)return entry.Cells[0];
                var value=capabilities[DeploymentPersonColumns.FormationId(column.Id)].Capability;
                return new(value.Available ? value.Value==true ? circle : "" : text.Get("detail_unknown"));
            }
        }).ToArray();
    }

    internal static IReadOnlyList<DeploymentPersonListEntry> Create(GameResourceRepository repository,
        PreviewText text, GameState world, ScenarioCityPreview scenario, DeploymentPersonBasicListQuery query)
    {
        if (!query.Available) throw new InvalidDataException(query.UnresolvedInput ?? "Person list is unavailable.");
        var sections = repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections;
        string unknown = text.Get("detail_unknown");
        string Message(int section,int id) => sections[section].Records[id].Text ??
            throw new InvalidDataException("Missing person-list message.");
        string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? unknown;
        return query.Rows!.Select(row =>
        {
            var values = new Dictionary<int,string>
            {
                [0] = world.Officers.GetValueOrDefault(row.Candidate.OfficerId)?.Name ?? unknown,
                [22] = Number(row.CommandCapacity.Capacity),
                [8] = Number(row.Abilities?.Leadership), [9] = Number(row.Abilities?.Strength),
                [10] = Number(row.Abilities?.Intelligence), [11] = Number(row.Abilities?.Politics),
                [12] = Number(row.Abilities?.Charm),
                [39] = Responsibility(),
                [18] = row.HealthCode.Available ? row.HealthCode.Value is byte health ? Message(0,0x0123+health) : "" : unknown,
                [19] = row.IsCommandMember.Available ? row.IsCommandMember.Value == true ? Message(1,0x0338) : "" : unknown,
                [5] = row.ServiceStatus.Available ? row.ServiceStatus.Value is { } status
                    ? status.Hired ? Message(1,0x0859) : status.StatusCode is byte code ? Message(0,0x00F9+code) : "" : "" : unknown,
                [194] = CreditRank(),
                [210] = row.GovernorPriority.Available ? row.GovernorPriority.Value switch
                {
                    DeploymentPersonGovernorPriority.Marked => Message(1,0x0338),
                    DeploymentPersonGovernorPriority.NotApplicable => Message(1,0x0353),
                    _ => ""
                } : unknown
            };
            // An unresolved colour rule cannot be represented as a known
            // ordinary value. Keep the whole cell visibly unavailable.
            if (!row.CommandEmphasized.Available) values[22] = unknown;
            var cells = DeploymentPersonColumns.Basic.Select(column => new DeploymentPersonCell(values[column.Id],
                column.Id == 22 && row.CommandEmphasized.Value == true)).ToArray();
            return new DeploymentPersonListEntry(row.Candidate.OfficerId,
                row.Candidate.DisabledReasons != DeploymentPersonDisabledReason.None,cells);

            string CreditRank()
            {
                if (!row.CreditRankStage.Available) return unknown;
                if (row.CreditRankStage.Value is not byte stage || stage == 0) return Message(1,0x0353);
                return scenario.CreditRankParameters?.Ranks.ElementAtOrDefault(stage)?.Name ?? unknown;
            }
            string Responsibility()
            {
                if (!row.Responsibility.Available) return unknown;
                if (row.Responsibility.Value is not { } value) return "";
                return value.Kind switch
                {
                    DeploymentPersonResponsibilityKind.Empty => "",
                    DeploymentPersonResponsibilityKind.NotApplicable => Message(1,0x0353),
                    DeploymentPersonResponsibilityKind.AreaName => value.AreaId is int area
                        ? world.Areas.GetValueOrDefault(area)?.Name ?? unknown : unknown,
                    DeploymentPersonResponsibilityKind.Recruit => Message(1,0x0020),
                    DeploymentPersonResponsibilityKind.Train => Message(1,0x0021),
                    DeploymentPersonResponsibilityKind.Spy => Message(1,0x009C),
                    _ => unknown
                };
            }
        }).ToArray();
    }
}
