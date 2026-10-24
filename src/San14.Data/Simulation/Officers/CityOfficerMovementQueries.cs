using San14.Core;

namespace San14.Data;

public sealed record CityOfficerMovementCity(int CityId, string Name);
public sealed record CityOfficerMovementCandidate(int OfficerId, string Name, int SourceCityId,
    string SourceCityName, int? TravelDays);
public sealed record CityOfficerMovementPage(int CityId, OfficerMoveMode Mode, string CityName,
    int? DestinationCityId, IReadOnlyList<CityOfficerMovementCity> Destinations,
    IReadOnlyList<CityOfficerMovementCandidate> Candidates, IReadOnlyList<int> SelectedOfficerIds,
    int MaximumOfficers, int? RequiredBooks, int? AvailableBooks, int? TravelDays,
    bool? CanConfirm, string? UnresolvedInput);

public sealed class CityOfficerMovementDraft : IDisposable
{
    private readonly Func<IReadOnlyList<int>, int?, CityOfficerMovementPage> read;
    private readonly Func<int, AreaAdministrationQuery<AreaAdministrationOfficerCard>>? readCard;
    private int[] selected = [];
    private int? destination;
    private bool closed;
    internal CityOfficerMovementDraft(Func<IReadOnlyList<int>, int?, CityOfficerMovementPage> read,
        Func<int, AreaAdministrationQuery<AreaAdministrationOfficerCard>>? readCard = null)
    { this.read = read; this.readCard = readCard; }
    public AreaAdministrationQuery<CityOfficerMovementPage> QueryPage() => closed
        ? AreaAdministrationQuery<CityOfficerMovementPage>.Unknown("city-officer-movement.closed")
        : AreaAdministrationQuery<CityOfficerMovementPage>.Known(read(selected, destination));
    public AreaAdministrationQuery<AreaAdministrationOfficerCard> QueryOfficerCard(int? officerId = null)
    {
        if (closed) return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Unknown("city-officer-movement.closed");
        int id = officerId ?? selected.FirstOrDefault();
        if (id <= 0 || readCard is null)
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
