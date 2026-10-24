using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : ICityOfficerMovementExecution
{
    public AreaAdministrationQuery<CityOfficerMovementDraft> OpenCityOfficerMovementDraft(GameState state,
        int cityId, OfficerMoveMode mode)
    {
        try
        {
            var source = CreateCityOfficerMovementSource(state, cityId, mode);
            var cards = new AreaAdministrationOfficerCardSource(state, source.Current, _definitions, _scenario,
                Seed.Language, _baseCapacityInputs, _repository);
            var cache = new Dictionary<int, AreaAdministrationQuery<AreaAdministrationOfficerCard>>();
            return AreaAdministrationQuery<CityOfficerMovementDraft>.Known(new(source.Page, ReadCard));

            AreaAdministrationQuery<AreaAdministrationOfficerCard> ReadCard(int id)
            {
                if (cache.TryGetValue(id, out var existing)) return existing;
                AreaAdministrationQuery<AreaAdministrationOfficerCard> result;
                try
                {
                    if (!source.People.Any(person => person.OfficerId == id))
                        throw CityOfficerMovementSource.Missing("card_candidate");
                    int physical = source.Current.ResolveOfficerSource(id)
                        ?? throw CityOfficerMovementSource.Missing("card_Person_pointer");
                    bool valid = CityOfficerMovementSource.K(source.Current.ReadPersonNativeValidity(physical),
                        "card_Person_validity");
                    //6F8F90/6F0540 initialize CUtilPInfo's context to0.
                    //Face, base/effective five abilities and traits share the
                    //same page current; no new capture is made per selection.
                    result = AreaAdministrationQuery<AreaAdministrationOfficerCard>.Known(
                        cards.ReadCity(0, physical, valid, 0));
                }
                catch (InvalidDataException error)
                { result = AreaAdministrationQuery<AreaAdministrationOfficerCard>.Unknown(error.Message); }
                cache.Add(id, result);
                return result;
            }
        }
        catch (InvalidDataException missing) { return AreaAdministrationQuery<CityOfficerMovementDraft>.Unknown(missing.Message); }
    }

    private CityOfficerMovementSource CreateCityOfficerMovementSource(GameState state, int cityId, OfficerMoveMode mode)
    {
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        PersonReturnCommands.BindCurrent(state, current);
        return new(state, current, cityId, mode);
    }

    CommandResult ICityOfficerMovementExecution.Move(GameState state, ForceState force,
        PrepareOfficersMoveOrder order, CommandContext context)
    {
        try
        {
            var source = CreateCityOfficerMovementSource(state, order.CityId, order.Mode);
            if (!source.CanOpen() || order.OfficerIds.Any(id => !source.People.Any(person => person.OfficerId == id)))
                return CommandResult.Reject("Officer is not a current movement candidate.", "officer_move.candidate");
            int targetId = order.Mode == OfficerMoveMode.Summon ? order.CityId : order.DestinationCityId
                ?? throw CityOfficerMovementSource.Missing("destination");
            if (order.Mode == OfficerMoveMode.Dispatch && !source.Destinations.Any(city => city.CityId == targetId))
                return CommandResult.Reject("Destination is not a current friendly base.", "officer_move.destination");
            if (source.Books - CommandBookAvailability.Reserved(state, source.DistrictId) < source.Cost)
                return CommandResult.Reject("District lacks uncommitted command books.", "officer_move.books");
            //1F0010 admits the ordinary ruler too. Cross-District relocation
            //belongs to the actual arrival's271320→26D2E0, not preparation.
            //The return manager already supplies that writer with its live
            //current, pool, report callbacks and actual arrival cut date.
            var working = state;
            //6CCE70 preserves the first occurrence of each actual home and
            //the selected member order within that home. A single command
            //transaction owns every group and the one final book debit.
            foreach (var group in order.OfficerIds.GroupBy(id => source.Person(id).SourceCityId))
            {
                var started = PersonReturnCommands.StartReassignment(working, source.Current,
                    group.ToArray(), targetId);
                working = started.State ?? throw CityOfficerMovementSource.Missing(started.UnresolvedOwner ?? "command1F1360");
            }
            var district = working.Districts[source.DistrictId];
            working = working with { Districts = new(working.Districts) { [district.Id] = district with
                { OrdersRemaining = Math.Clamp(unchecked(source.Books - source.Cost), 0, 200) } } };
            if (working.CampaignEventWorld?.CurrentUserIndex != -1)
                throw CityOfficerMovementSource.Missing("App_command_service31FD60");
            return CommandResult.Accept(working);
        }
        catch (InvalidDataException missing) { return CommandResult.RuleUnavailable("rules.city_officer_movement", missing.Message); }
    }
}

internal sealed class CityOfficerMovementSource
{
    internal readonly GameState State;
    internal readonly SourceWorldContext Current;
    internal readonly int CityId, DistrictId;
    internal readonly OfficerMoveMode Mode;
    internal readonly IReadOnlyList<CityOfficerMovementCandidate> People;
    internal readonly IReadOnlyList<CityOfficerMovementCity> Destinations;
    private readonly BaseReference city;
    private readonly Dictionary<(int, int), PersonReturnPathResult> routes = [];
    //6CD371/6CD3AE read the same immutable TC book byte1A49464=1,
    //once after all actual-home groups. No per-group book debit is added.
    internal static int ReadCost(GameState state) => state.NativeArmySimulation?.Parameters.Language is "TC" ? 1 : throw Missing("SC_command_cost");
    internal int Cost => ReadCost(State);
    internal int Books => State.Districts.GetValueOrDefault(DistrictId)?.OrdersRemaining ?? throw Missing("District14");

    internal CityOfficerMovementSource(GameState state, SourceWorldContext current, int cityId, OfficerMoveMode mode)
    {
        State = state; Current = current; CityId = cityId; Mode = mode;
        if (!Enum.IsDefined(mode) || !state.Cities.ContainsKey(cityId)) throw Missing("executing_City");
        city = BaseLookup.Find(cityId, current);
        if (!K(current.ReadBaseNativeValidity(city), "City_validity")) throw Missing("invalid_City");
        var district = current.ReadBaseDistrictReference(city) ?? throw Missing("District_pointer");
        if (!K(current.ReadDistrictNativeValidity(district), "District_validity")) throw Missing("invalid_District");
        DistrictId = K(district.PhysicalSlot, "District_identity");
        var force = Force(city);
        if (!K(current.ReadForceNativeValidity(force), "City_Force_validity")) throw Missing("invalid_City_Force");
        var people = new List<CityOfficerMovementCandidate>();
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100"))
        {
            int p = K(entry, "Person_pointer");
            if (K(current.ReadPersonStatus(p), "PersonE6") is not (>= 1 and <= 4)) continue;
            ushort home = K(current.ReadPersonFirstLocation(p), "PersonE2");
            if (mode == OfficerMoveMode.Dispatch) { if (home != cityId) continue; }
            else if ((current.ReadPersonForce(p) ?? throw Missing("Person_Force")) != force) continue;
            if (!K(current.ReadPersonNativeValidity(p), "Person_validity")) continue;
            ushort place = K(current.ReadPersonSecondLocation(p), "PersonE4");
            if (mode == OfficerMoveMode.Summon && home == cityId || place != home ||
                (K(current.ReadArmyReturnPersonFlags196(p), "Person15E") & 1) != 0) continue;
            int id = K(current.ReadPersonIdentity(p), "Person10");
            var person = state.Officers.GetValueOrDefault(id) ?? throw Missing("Person_owner");
            if (PlayableOfficerAvailability.IsCaptive(person) || PlayableDiplomacy.IsOfficerOnMission(state, id) ||
                state.PreparedOfficerMoves?.ContainsKey(id) == true || state.PreparedTalentOrders?.ContainsKey(id) == true ||
                state.PreparedDeployments.Values.Any(plan => plan.OfficerId == id)) continue;
            people.Add(new(id, person.Name, home, Name(home), null));
        }
        People = people.AsReadOnly();
        var destinations = new List<CityOfficerMovementCity>();
        if (mode == OfficerMoveMode.Dispatch)
            foreach (var target in current.ReadRegisteredBases() ?? throw Missing("Base80"))
            {
                var at = target ?? throw Missing("Base_pointer");
                if (!K(current.ReadBaseNativeValidity(at), "Base_validity") || Force(at) != force) continue;
                int location = K(current.ReadBaseObjectLink(at), "Base4E");
                if (location != cityId) destinations.Add(new(location, Name(location)));
            }
        Destinations = destinations.AsReadOnly();
    }
    internal CityOfficerMovementCandidate Person(int id) => People.FirstOrDefault(person => person.OfficerId == id) ?? throw Missing("selected_Person");
    internal bool CanOpen() => Books >= Cost && People.Count != 0 && (Mode == OfficerMoveMode.Summon || Destinations.Count != 0);
    internal CityOfficerMovementPage Page(IReadOnlyList<int> selected, int? destination)
    {
        int? cost = null, books = null, lastArrival = null;
        bool? confirm = null; string? missing = null;
        var rows = People.ToArray();
        int? target = Mode == OfficerMoveMode.Summon ? CityId : destination;
        try
        {
            cost = Cost; books = Books;
            bool targetKnown = target is int actual && (Mode == OfficerMoveMode.Summon || Destinations.Any(city => city.CityId == actual));
            if (targetKnown)
                for (int n = 0; n < rows.Length; n++)
                    if (selected.Contains(rows[n].OfficerId))
                    {
                        var key = (rows[n].SourceCityId, target!.Value);
                        if (!routes.TryGetValue(key, out var result)) routes[key] = result = PersonReturnPathSource.QueryReassignment(State, Current, key.Item1, key.Item2);
                        if (result.Days is not int days) throw Missing(result.UnresolvedOwner ?? "travel_days");
                        rows[n] = rows[n] with { TravelDays = days };
                        lastArrival = Math.Max(lastArrival ?? 0, days);
                    }
            confirm = CanOpen() && targetKnown && selected.Count != 0 && selected.Distinct().Count() == selected.Count &&
                selected.All(id => People.Any(person => person.OfficerId == id)) &&
                books - CommandBookAvailability.Reserved(State, DistrictId) >= cost;
        }
        catch (InvalidDataException error) { missing = error.Message; }
        return new(CityId, Mode, State.Cities[CityId].Name, target, Destinations, Array.AsReadOnly(rows),
            Array.AsReadOnly(selected.ToArray()), People.Count, cost, books, lastArrival, confirm, missing);
    }
    private ForceReference Force(BaseReference at)
    {
        int raw = K(Current.ReadBaseForceSlot(at), "Base_Force");
        var force = Current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("Force_pointer");
        return force;
    }
    private string Name(int id) => State.Cities.GetValueOrDefault(id)?.Name ?? State.Gates.GetValueOrDefault(id)?.Name ?? throw Missing("location_name");
    internal static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    internal static InvalidDataException Missing(string field) => new("city-officer-movement." + field);
}
