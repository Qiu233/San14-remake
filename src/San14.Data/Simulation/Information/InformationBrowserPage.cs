using System.Collections.Immutable;

namespace San14.Data;

public enum InformationTab { OwnForce, AllForces, Data }
public enum InformationListKind
{
    Cities = 5, Gates = 6, Forces = 7, Districts = 8, Areas = 10,
    Facilities = 13, Officers = 14, Formations = 15, Tactics = 16, Items = 17,
    Ranks = 19, Offices = 20, Doctrines = 21, Policies = 22, Traits = 26,
    Armies = 27, Advantages = 39, ForeignTechnologies = 41, Scenery = 42,
    CreditRanks = 43, DoctrinePanels = 46,
    //These two application states have no dispatcher data-type number.
    Chronology = 100, Destiny = 101, GemBooks = 102
}

public sealed record InformationMenuEntry(int Node, InformationListKind Kind, int CaptionMessageId,
    string Caption, bool Visible, bool? Enabled, string? UnresolvedOwner);
public sealed record InformationMenuProjection(InformationTab? InitialTab,
    ImmutableArray<bool?> TabEnabled, ImmutableArray<InformationMenuEntry> Entries, string? UnresolvedOwner);
public sealed record InformationColumn(string Key, int? CaptionMessageId, string Caption);
/// <summary>A known empty text/value is separate from a missing current input.
/// SortText retains a source name key independently from its displayed label.</summary>
public sealed record InformationCell(string? Text = null, long? Number = null, string? SortText = null,
    string? UnresolvedOwner = null);
public sealed record InformationDetailIdentity(InformationListKind Kind, int Id);
public sealed record InformationRow(int LogicalType, int Id, string Name,
    ImmutableDictionary<string, InformationCell> Cells, InformationDetailIdentity? Detail = null);
public sealed record InformationList(string Title, ImmutableArray<InformationColumn> Columns,
    ImmutableArray<InformationRow> Rows, string? UnresolvedOwner)
{
    public bool Available => UnresolvedOwner is null;
    public string? BodyText { get; init; }
    public InformationDestinyPage? DestinySummary { get; init; }
    /// <summary>Display-only sorting of the already captured page. Equal
    /// source keys retain the input order; no current query runs here.</summary>
    public InformationList Sort(string column, bool ascending)
    {
        int Compare(InformationRow left, InformationRow right)
        {
            left.Cells.TryGetValue(column, out var a); right.Cells.TryGetValue(column, out var b);
            bool unknownA = a is null || a.UnresolvedOwner is not null;
            bool unknownB = b is null || b.UnresolvedOwner is not null;
            if (unknownA != unknownB) return unknownA ? 1 : -1;
            if (unknownA) return 0;
            int value = a!.Number is long first && b!.Number is long second ? first.CompareTo(second)
                : column == "name" && left.LogicalType == (int)InformationListKind.Officers
                    ? PersonNameOrder.Compare(a.SortText ?? a.Text ?? "", b!.SortText ?? b.Text ?? "")
                    : string.Compare(a.SortText ?? a.Text, b!.SortText ?? b.Text, StringComparison.Ordinal);
            return ascending ? value : -value;
        }
        return this with { Rows = Rows.OrderBy(row => row, Comparer<InformationRow>.Create(Compare)).ToImmutableArray() };
    }
}

/// <summary>The two separate lists owned by CInfoBelongingsState. Changing
/// sections or selecting an item is a display draft, never an item transfer.</summary>
public sealed record InformationBelongingsSection(int Index, int CaptionMessageId,
    string Caption, InformationList List);
public sealed record InformationBelongingsPage(string Title,
    ImmutableArray<InformationBelongingsSection> Sections, string? UnresolvedOwner);

/// <summary>One immutable world publication with one lazy current capture and
/// per-list caches. Menu refresh and row hover do not scan the world again.</summary>
public sealed class InformationBrowserPage
{
    private readonly IInformationBrowserSource source;
    internal InformationBrowserPage(IInformationBrowserSource source) => this.source = source;
    public InformationMenuProjection Menu(InformationTab tab) => source.Menu(tab);
    public InformationList OpenList(InformationTab tab, int node) => source.OpenList(tab, node);
    public InformationList Query(InformationListKind kind, bool ownForceOnly) => source.Query(kind, ownForceOnly);
    public InformationBelongingsPage QueryBelongings() => source.QueryBelongings();
    public InformationHistoryPage QueryHistory() => source.QueryHistory();
}

internal interface IInformationBrowserSource
{
    InformationMenuProjection Menu(InformationTab tab);
    InformationList OpenList(InformationTab tab, int node);
    InformationList Query(InformationListKind kind, bool ownForceOnly);
    InformationBelongingsPage QueryBelongings();
    InformationHistoryPage QueryHistory();
}
