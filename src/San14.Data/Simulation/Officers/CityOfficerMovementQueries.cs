using San14.Core;

namespace San14.Data;

public sealed record CityOfficerMovementDestinationColumn(int Id, int Width, int CaptionId, bool InitialAscending);
public sealed record CityOfficerMovementDestinationCell(int ColumnId, string? Text, string? UnresolvedInput)
{
    internal string? TextKey { get; init; }
    internal int? NumberKey { get; init; }
    public bool CanSort => UnresolvedInput is null && (TextKey is not null || NumberKey.HasValue);
}
public sealed record CityOfficerMovementCity(int CityId, string Name)
{
    internal Lazy<IReadOnlyList<CityOfficerMovementDestinationCell>>? PreparedCells { get; init; }
    public IReadOnlyList<CityOfficerMovementDestinationCell> Cells => PreparedCells?.Value ?? [];
    public static IReadOnlyList<CityOfficerMovementDestinationColumn> Columns { get; } = Array.AsReadOnly(
        new CityOfficerMovementDestinationColumn[] {
            new(0,132,0x205,true), new(1,216,0x1FE,true), new(2,76,0x218,false),
            new(3,104,0x20E,false), new(4,104,0x20C,false), new(5,118,0x20D,false), new(6,62,0x26C,true) });

    internal static int CompareCells(CityOfficerMovementDestinationCell left,CityOfficerMovementDestinationCell right,int columnId)
        =>columnId is 0 or 1?PersonNameOrder.Compare(left.TextKey!,right.TextKey!):left.NumberKey!.Value.CompareTo(right.NumberKey!.Value);
    public static AreaAdministrationQuery<IReadOnlyList<CityOfficerMovementCity>> Sort(
        IReadOnlyList<CityOfficerMovementCity> cities, int columnId, bool ascending)
    {
        ArgumentNullException.ThrowIfNull(cities);
        if (!Columns.Any(column => column.Id == columnId))
            return AreaAdministrationQuery<IReadOnlyList<CityOfficerMovementCity>>.Unknown("city-officer-movement.destination_column");
        var cells = new Dictionary<int, CityOfficerMovementDestinationCell>();
        foreach (var city in cities)
        {
            var cell = city.Cells.FirstOrDefault(cell => cell.ColumnId == columnId);
            if (cell?.CanSort != true)
                return AreaAdministrationQuery<IReadOnlyList<CityOfficerMovementCity>>.Unknown(
                    cell?.UnresolvedInput ?? "city-officer-movement.destination_sort_key");
            cells.Add(city.CityId, cell);
        }
        var comparer = Comparer<CityOfficerMovementCity>.Create((left, right) =>
        {
            var a = cells[ascending ? left.CityId : right.CityId];
            var b = cells[ascending ? right.CityId : left.CityId];
            return CompareCells(a,b,columnId);
        });
        // Stable in both directions over the current display order.
        return AreaAdministrationQuery<IReadOnlyList<CityOfficerMovementCity>>.Known(
            Array.AsReadOnly(cities.OrderBy(city => city, comparer).ToArray()));
    }
}
public sealed record CityOfficerMovementCandidate(int OfficerId, string Name, int SourceCityId,
    string SourceCityName, int? TravelDays);
public sealed record CityOfficerMovementPage(int CityId, OfficerMoveMode Mode, string CityName,
    int? DestinationCityId, IReadOnlyList<CityOfficerMovementCity> Destinations,
    IReadOnlyList<CityOfficerMovementCandidate> Candidates, IReadOnlyList<int> SelectedOfficerIds,
    int MaximumOfficers, int? RequiredBooks, int? AvailableBooks, int? TravelDays,
    bool? CanConfirm, string? UnresolvedInput)
{
    public CityOfficerMovementMapQuery? DestinationMap { get; init; }
}

public sealed record CityOfficerMovementPersonSelection(OfficerInformationPageSet Pages,
    IReadOnlyDictionary<int, AreaAdministrationQuery<bool>> AlteredHealth, int MaximumOfficers);

public sealed class CityOfficerMovementDraft : IDisposable
{
    private readonly Func<IReadOnlyList<int>, int?, CityOfficerMovementPage> read;
    private readonly Func<int, AreaAdministrationQuery<AreaAdministrationOfficerCard>>? readCard;
    private readonly Func<int, AreaAdministrationQuery<CityOfficerMovementPersonSelection>>? readPersons;
    private readonly Func<DiplomacyTargetSelectionPage>? readDestinations;
    private int[] selected = [];
    private int? destination;
    private bool closed;
    internal CityOfficerMovementDraft(Func<IReadOnlyList<int>, int?, CityOfficerMovementPage> read,
        Func<int, AreaAdministrationQuery<AreaAdministrationOfficerCard>>? readCard = null,
        Func<int, AreaAdministrationQuery<CityOfficerMovementPersonSelection>>? readPersons = null,
        Func<DiplomacyTargetSelectionPage>? readDestinations=null)
    { this.read = read; this.readCard = readCard; this.readPersons = readPersons;this.readDestinations=readDestinations; }
    public AreaAdministrationQuery<DiplomacyTargetSelectionPage> QueryDestinations()
    {
        if(closed||readDestinations is null)return AreaAdministrationQuery<DiplomacyTargetSelectionPage>.Unknown("city-officer-movement.destination_selection");
        try{return AreaAdministrationQuery<DiplomacyTargetSelectionPage>.Known(readDestinations());}
        catch(InvalidDataException error){return AreaAdministrationQuery<DiplomacyTargetSelectionPage>.Unknown(error.Message);}
    }
    public AreaAdministrationQuery<CityOfficerMovementPersonSelection> QueryPersons(int playerForceId) =>
        closed ? AreaAdministrationQuery<CityOfficerMovementPersonSelection>.Unknown("city-officer-movement.closed")
        : readPersons?.Invoke(playerForceId) ?? AreaAdministrationQuery<CityOfficerMovementPersonSelection>.Unknown("city-officer-movement.person_columns");
    public AreaAdministrationQuery<CityOfficerMovementPage> QueryPage() => closed
        ? AreaAdministrationQuery<CityOfficerMovementPage>.Unknown("city-officer-movement.closed")
        : AreaAdministrationQuery<CityOfficerMovementPage>.Known(read(selected, destination));
    public AreaAdministrationQuery<AreaAdministrationOfficerCard> QueryOfficerCard(int? officerId = null)
    {
        if (closed) return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Unknown("city-officer-movement.closed");
        int id = officerId ?? selected.FirstOrDefault();
        if (readCard is null)
            return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Unknown("city-officer-movement.card_selection");
        return readCard(id);
    }
    public AreaAdministrationDraftResult SelectOfficers(IReadOnlyList<int> ids)
    {
        if (closed) return new(false, "city-officer-movement.closed");
        var page = read([], destination);
        if (ids is null || ids.Count > page.MaximumOfficers || ids.Distinct().Count() != ids.Count ||
            ids.Any(id => !page.Candidates.Any(person => person.OfficerId == id))) return new(false, "city-officer-movement.selection");
        selected = ids.ToArray(); return new(true);
    }
    public AreaAdministrationDraftResult SelectDestination(int id)
    {
        if (closed) return new(false, "city-officer-movement.closed");
        var page = read([], destination);
        if (page.Mode != OfficerMoveMode.Dispatch || !page.Destinations.Any(city => city.CityId == id))
            return new(false, "city-officer-movement.destination");
        destination = id; return new(true);
    }
    public AreaAdministrationQuery<PrepareOfficersMoveOrder> BuildOrder(int forceId)
    {
        var result = QueryPage(); var page = result.Value;
        if (!result.Available || page?.CanConfirm != true)
            return AreaAdministrationQuery<PrepareOfficersMoveOrder>.Unknown(result.UnresolvedInput ?? page?.UnresolvedInput ?? "city-officer-movement.cannot_confirm");
        return AreaAdministrationQuery<PrepareOfficersMoveOrder>.Known(new(forceId, page.CityId, page.DestinationCityId,
            Array.AsReadOnly(selected.ToArray()), page.Mode));
    }
    public void Abandon() => closed = true;
    public void Dispose() => Abandon();
}
