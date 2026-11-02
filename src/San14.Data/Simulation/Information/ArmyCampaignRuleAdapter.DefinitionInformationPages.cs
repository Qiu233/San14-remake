using System.Collections.Immutable;
using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<DefinitionInformationPageSet> PrepareAllDefinitionInformationPages(
        GameState state, int playerForceId, InformationListKind kind, bool ownOnly = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        try { return AreaAdministrationQuery<DefinitionInformationPageSet>.Known(new DefinitionInformationCurrent(this, state, playerForceId, ownOnly).Pages(kind)); }
        catch (InvalidDataException error) { return AreaAdministrationQuery<DefinitionInformationPageSet>.Unknown(error.Message); }
    }

    // The startup force page supplies its actual ordered pointer-list
    // projection. Do not run the ordinary own/all candidate filter again:
    // multiple qualifying Provinces can intentionally supply the same entry.
    public AreaAdministrationQuery<DefinitionInformationPageSet> PrepareNewGameGeographicAdvantageInformationPages(
        GameState state,NewGameForcePreview preview)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(preview);
        if(preview.ForceValid is not true||preview.GeographicAdvantages is not {} rows)
            return AreaAdministrationQuery<DefinitionInformationPageSet>.Unknown("new-game.force.geographic-list");
        try
        {
            return AreaAdministrationQuery<DefinitionInformationPageSet>.Known(
                new DefinitionInformationCurrent(this,state,preview.ForceId,
                    newGamePreview:ArmyOperationContext.IsNewGamePreview(state))
                    .Pages(InformationListKind.Advantages,rows.Select(row=>row.AdvantageId).ToArray()));
        }
        catch(InvalidDataException error)
        {return AreaAdministrationQuery<DefinitionInformationPageSet>.Unknown(error.Message);}
    }

    private sealed partial class DefinitionInformationCurrent
    {
        private readonly ArmyCampaignRuleAdapter rules;
        private readonly GameState state;
        private readonly int playerForceId;
        private readonly bool ownOnly;
        private readonly bool newGamePreview;
        private readonly Lazy<SourceWorldContext> captured;
        private SourceWorldContext current => captured.Value;
        private readonly Dictionary<(InformationListKind Kind, int Id, int Column), PreparedCell> prepared = [];
        private readonly HashSet<string> candidateIssues = new(StringComparer.Ordinal);
        internal DefinitionInformationCurrent(ArmyCampaignRuleAdapter rules, GameState state, int playerForceId,
            bool ownOnly = false,bool newGamePreview = false)
        {
            this.rules = rules; this.state = state; this.playerForceId = playerForceId; this.ownOnly = ownOnly;
            this.newGamePreview=newGamePreview;
            // The actual startup dialog borrows normal-loaded Person/Base
            // inputs; there are no saved Army indexes or lifecycle to bind.
            captured = new(() => newGamePreview ? rules.CapturePersonPresentationCurrent(state)
                : new OfficerInformationCurrent(rules,state,playerForceId).current);
        }
        internal DefinitionInformationPageSet Pages(InformationListKind kind,int[]? suppliedIds=null,bool selectInitial=true)
        {
            var profile = Profile(kind);
            int[] ids = suppliedIds ?? Candidates(kind);
            var groups = profile.Groups.Select((group,index) => new DefinitionInformationGroup(index,
                group.CaptionId, Data(1,group.CaptionId), group.ColumnIds.Select(id =>
                {
                    var row = profile.Rows[id];
                    int format = (row.Flags & 1) != 0 ? 3 : 4;
                    if ((row.Flags & 8) != 0) format = 4;
                    if ((row.Flags & 2) != 0) format = 5;
                    return new DefinitionInformationColumn(id,row.CaptionId,Data(1,row.CaptionId),row.Width,
                        format,(row.Flags & 0x20000000) == 0,true,row.Visible,row.SortMode != 2);
                }).ToImmutableArray())).ToImmutableArray();
            var pages = new DefinitionInformationPageSet(Data(1,profile.TitleCaptionId),groups,ids,
                (id,column) => Cell(kind,id,column).Display,
                (id,column) => Cell(kind,id,column).SortUnresolvedOwner,
                (left,right,column,ascending) => (Cell(kind,left,column).Key ?? throw Missing("sort_key"))
                    .Before(Cell(kind,right,column).Key ?? throw Missing("sort_key"),ascending))
            {
                Kind=kind, OfficersCaption=Data(1,0x211),
                ReadCanBrowseOfficers=id => profile.HasOfficerAction && ids.Contains(id),
                CandidateUnresolvedOwner=candidateIssues.Count==0 ? null : string.Join(";",candidateIssues)
            };
            if (selectInitial && ids.Length!=0) pages.SelectSourceIndex(0);
            return pages;
        }
        private static DefinitionInformationProfile Profile(InformationListKind kind) => kind switch
        {
            InformationListKind.GemBooks => BelongingsInformationColumnsSource.Gems,
            InformationListKind.Items => ItemInformationColumnsSource.Profile,
            InformationListKind.Scenery => SceneryInformationColumnsSource.Profile,
            InformationListKind.Advantages => AdvantageInformationColumnsSource.Profile,
            InformationListKind.Traits => TraitInformationColumnsSource.Profile,
            InformationListKind.Tactics => TacticInformationColumnsSource.Profile,
            InformationListKind.Ranks => RankInformationColumnsSource.Profile,
            InformationListKind.Offices => OfficeInformationColumnsSource.Profile,
            InformationListKind.Doctrines => DoctrineInformationColumnsSource.Profile,
            InformationListKind.Policies => PolicyInformationColumnsSource.Profile,
            InformationListKind.Facilities => FacilityInformationColumnsSource.Profile,
            InformationListKind.ForeignTechnologies => ForeignTechnologyInformationColumnsSource.Profile,
            InformationListKind.CreditRanks => CreditRankInformationColumnsSource.Profile,
            InformationListKind.DoctrinePanels => DoctrinePanelInformationColumnsSource.Profile,
            _ => throw Missing("profile."+kind)
        };
        private int[] Candidates(InformationListKind kind) => kind switch
        {
            InformationListKind.Items => ItemCandidates(),
            InformationListKind.Scenery => SceneryCandidates(),
            InformationListKind.Advantages => AdvantageCandidates(ownOnly),
            InformationListKind.Traits => TraitCandidates(),
            InformationListKind.Tactics => TacticCandidates(),
            InformationListKind.Ranks => RankCandidates(),
            InformationListKind.Offices => OfficeCandidates(),
            InformationListKind.Doctrines => DoctrineCandidates(),
            InformationListKind.Policies => PolicyCandidates(),
            InformationListKind.Facilities => FacilityCandidates(),
            InformationListKind.ForeignTechnologies => ForeignTechnologyCandidates(),
            InformationListKind.CreditRanks => CreditRankCandidates(),
            InformationListKind.DoctrinePanels => DoctrinePanelCandidates(),
            _ => throw Missing("candidates."+kind)
        };
        private PreparedCell Cell(InformationListKind kind,int id,int column)
        {
            if (prepared.TryGetValue((kind,id,column),out var saved)) return saved;
            try
            {
                saved=kind switch
                {
                    InformationListKind.GemBooks => BelongingsGemCell(id,column),
                    InformationListKind.Items => ItemCell(id,column),
                    InformationListKind.Scenery => SceneryCell(id,column),
                    InformationListKind.Advantages => AdvantageCell(id,column),
                    InformationListKind.Traits => TraitCell(id,column),
                    InformationListKind.Tactics => TacticCell(id,column),
                    InformationListKind.Ranks => RankCell(id,column),
                    InformationListKind.Offices => OfficeCell(id,column),
                    InformationListKind.Doctrines => DoctrineCell(id,column),
                    InformationListKind.Policies => PolicyCell(id,column),
                    InformationListKind.Facilities => FacilityCell(id,column),
                    InformationListKind.ForeignTechnologies => ForeignTechnologyCell(id,column),
                    InformationListKind.CreditRanks => CreditRankCell(id,column),
                    InformationListKind.DoctrinePanels => DoctrinePanelCell(id,column),
                    _ => throw Missing("column."+kind+"."+column)
                };
            }
            catch (InvalidDataException error) { saved=new(new(null,null,error.Message),null,error.Message); }
            return prepared[(kind,id,column)]=saved;
        }
        private PreparedCell MakeCell(Func<string?> display,Func<SortKey>? sort=null)
        {
            string? text=null,missing=null,sortMissing=null; SortKey? key=null;
            try { text=display() ?? ""; } catch (InvalidDataException error) { missing=error.Message; }
            if (sort is not null) try { key=sort(); } catch (InvalidDataException error) { sortMissing=error.Message; }
            return new(new(text,null,missing),key,sortMissing);
        }
        private string Data(int section,int id) => rules.ReadHistoryDataText(section,id);
        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
        internal static T Need<T>(T? value,string field) where T:struct => value ?? throw Missing(field);
        internal static InvalidDataException Missing(string field) => new("definition-information."+field);
        private sealed record PreparedCell(DefinitionInformationCell Display,SortKey? Key,string? SortUnresolvedOwner);
        private abstract record SortKey { internal abstract bool Before(SortKey other,bool ascending); }
        private sealed record NumberKey(long Value,bool Empty=false):SortKey
        {
            internal override bool Before(SortKey other,bool ascending)
            {
                var right=(NumberKey)other;
                if(Empty!=right.Empty)return !Empty;
                return !Empty && (ascending ? Value<right.Value : Value>right.Value);
            }
        }
        // Ordinary mode0 compares tagged keys in the requested direction;
        // unlike mode1, it does not pin the empty tag after ordinary names.
        private sealed record ModeZeroNameKey(string Value,bool Empty=false):SortKey
        {
            internal override bool Before(SortKey other,bool ascending)
            {
                var right=(ModeZeroNameKey)other;
                if(Empty!=right.Empty)return ascending?!Empty:Empty;
                return !Empty&&(ascending?string.CompareOrdinal(Value,right.Value)<0:string.CompareOrdinal(right.Value,Value)<0);
            }
        }
        private sealed record NameKey(string Value,bool Empty=false):SortKey
        {
            internal override bool Before(SortKey other,bool ascending)
            {
                var right=(NameKey)other;
                if(Empty!=right.Empty)return !Empty;
                return !Empty && (ascending ? string.CompareOrdinal(Value,right.Value)<0 : string.CompareOrdinal(right.Value,Value)<0);
            }
        }
    }
}
