using San14.Data.Formats;

namespace San14.Data;

public sealed partial class RegisteredOfficerNewGameDraft
{
    private IReadOnlyList<S14ForceNameDefinition>? kingdomDefinitions;
    public IReadOnlyList<RegisteredOfficerNewGameChoice> Ranks => (Scenario.Snapshot.RankParameters ?? throw Missing("rank_catalog"))
        .Ranks.Where(rank => rank.Id is >= 1 and <= 9).Select(rank => new RegisteredOfficerNewGameChoice(rank.Id, rank.Name)).ToArray();
    public IReadOnlyList<RegisteredOfficerNewGameChoice> Kingdoms => (kingdomDefinitions ?? throw Missing("kingdom_catalog"))
        .Where(row => row.NativeValidity && (row.Field8E & 1) != 0 &&
            !Scenario.Snapshot.Forces.Any(force => force.RawKingdomName48 == row.PhysicalSlot) &&
            !Selection.NewForces.Any(force => force.KingdomId == row.PhysicalSlot))
        .Select(row => new RegisteredOfficerNewGameChoice(row.PhysicalSlot, row.ShortName)).ToArray();

    public void LoadChoices(GameResourceRepository repository) => kingdomDefinitions =
        repository.LoadForceNameDefinitions(Scenario.FixedDataSource.SourcePath).Catalog.Definitions;

    public string KingdomText(int id) => id == 0 ? "" : (kingdomDefinitions ?? throw Missing("kingdom_catalog"))
        .FirstOrDefault(row => row.PhysicalSlot == id && row.NativeValidity)?.ShortName ?? throw Missing("kingdom_name");

    public bool CanChooseForceCity(int index, int cityId, bool capital)
    {
        var city = Cities.FirstOrDefault(city => city.Slot == cityId && cityId is >= 1 and <= 51);
        if (city is null || city.DistrictSlot != 0 || Selection.NewForces.Where((_, at) => at != index)
            .Any(force => force.CityId == cityId || force.AdditionalCityIds.Contains(cityId))) return false;
        if (capital) return true;
        if ((uint)index >= (uint)Selection.NewForces.Length) return false;
        return cityId != Selection.NewForces[index].CityId && AdjacentCities(Selection.NewForces[index].CityId).Contains(cityId);
    }

    //22F430 follows ten Base neighbors, crossing at most two non-City
    //receivers. A City terminates that branch; this is not graph reachability.
    private HashSet<int> AdjacentCities(int capital)
    {
        var result = new HashSet<int>();
        Walk(capital, 0);
        return result;
        void Walk(int location, int intermediate)
        {
            IReadOnlyList<byte>? links = location <= 51 ? Cities.FirstOrDefault(city => city.Slot == location)?.RawNeighborSlots58 :
                Scenario.Snapshot.Gates.FirstOrDefault(gate => gate.LocationSlot == location)?.RawNeighborSlots58;
            if (links is not { Count: 10 }) throw Missing("ten_base_neighbors");
            foreach (int next in links)
            {
                if (next is >= 1 and <= 51) result.Add(next);
                else if (next is >= 52 and <= 61 && intermediate < 2) Walk(next, intermediate + 1);
            }
        }
    }

    public void UpdateForce(int index, int rulerOfficerId, int cityId)
    {
        var before = Selection.NewForces[index]; RequireAdopted([rulerOfficerId]);
        if (!IsAdult(rulerOfficerId) || IsRuler(rulerOfficerId) && rulerOfficerId != before.RulerOfficerId ||
            !CanChooseForceCity(index, cityId, true)) throw Missing("force_ruler_or_capital");
        var adjacent = AdjacentCities(cityId);
        var retained = before.AdditionalCityIds.Append(before.CityId).Where(id => id != cityId && adjacent.Contains(id)).Distinct().ToArray();
        var detached = before.AdditionalCityIds.Append(before.CityId).Where(id => id != cityId && !retained.Contains(id)).ToArray();
        var after = before with { CityId = cityId, RulerOfficerId = rulerOfficerId,
            OfficerIds = [rulerOfficerId], AdditionalCityIds = [.. retained] };
        var places = Selection.Placements.Where(entry => entry.OfficerId != before.RulerOfficerId && entry.OfficerId != rulerOfficerId &&
            !detached.Contains(entry.CityId)).ToArray();
        ReplaceForce(index, after);
        Selection = Selection with { Placements = [.. places, new(rulerOfficerId, cityId, true)] };
    }

    public void SetForceCities(int index, IEnumerable<int> cities)
    {
        var ids = cities.ToArray();
        if (ids.Distinct().Count() != ids.Length || ids.Any(id => !CanChooseForceCity(index, id, false))) throw Missing("force_territory");
        var detached = Selection.NewForces[index].AdditionalCityIds.Where(id => !ids.Contains(id)).ToArray();
        ReplaceForce(index, Selection.NewForces[index] with { AdditionalCityIds = [.. ids] });
        Selection = Selection with { Placements = [.. Selection.Placements.Where(entry => !detached.Contains(entry.CityId))] };
    }

    public bool ForceCapitalChangeNeedsConfirmation(int index, int cityId)
    {
        var before = Selection.NewForces[index]; var neighbors = AdjacentCities(cityId);
        return Selection.Placements.Any(entry => entry.CityId != cityId &&
            before.AdditionalCityIds.Append(before.CityId).Contains(entry.CityId) && !neighbors.Contains(entry.CityId));
    }

    public bool ForceCitiesChangeNeedsConfirmation(int index, IEnumerable<int> cities)
    {
        var retained = cities.ToHashSet(); var before = Selection.NewForces[index];
        return Selection.Placements.Any(entry => before.AdditionalCityIds.Contains(entry.CityId) && !retained.Contains(entry.CityId));
    }

    public void SetForceColour(int index, int colour)
    {
        if (colour is < 50 or > 59) throw Missing("force_colour");
        var before = Selection.NewForces[index];
        int other = Array.FindIndex(Selection.NewForces.ToArray(), force => force.ColourId == colour);
        if (other >= 0 && other != index) ReplaceForce(other, Selection.NewForces[other] with { ColourId = before.ColourId });
        ReplaceForce(index, before with { ColourId = colour });
    }

    public void SetForceRank(int index, int rank)
    {
        if (!Ranks.Any(value => value.Id == rank)) throw Missing("force_rank");
        var before = Selection.NewForces[index];
        ReplaceForce(index, before with { RankId = rank, KingdomId = rank == 1 ? before.KingdomId : 0 });
    }

    public void SetForceKingdom(int index, int kingdom)
    {
        var before = Selection.NewForces[index];
        if (before.RankId != 1 || !Kingdoms.Any(value => value.Id == kingdom) && before.KingdomId != kingdom)
            throw Missing("force_kingdom");
        ReplaceForce(index, before with { KingdomId = kingdom });
    }

    public void RemoveForce(int index)
    {
        var before = Selection.NewForces[index];
        var locations = before.AdditionalCityIds.Append(before.CityId).ToArray();
        Selection = Selection with { NewForces = Selection.NewForces.RemoveAt(index),
            Placements = [.. Selection.Placements.Where(entry => !locations.Contains(entry.CityId))] };
    }

    private void ReplaceForce(int index, RegisteredOfficerNewGameForce value) =>
        Selection = Selection with { NewForces = Selection.NewForces.SetItem(index, value) };
}
