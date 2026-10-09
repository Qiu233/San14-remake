using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

internal sealed record ScenarioBrowserItem(string Label, string ResourceUri, EffectiveScenario Selection);
internal sealed record BrowserChoice(int Id, string Label);
internal sealed record ForceBrowserChoices(IReadOnlyList<BrowserChoice> Items, int InitialIndex);
internal sealed record BrowserPortrait(int AppearanceCode, string Name);
internal sealed record ForceBrowserView(int ForceId, IReadOnlyList<BrowserChoice> Cities,
    int PreferredCityId, IReadOnlyList<BrowserChoice> Officers, int PreferredOfficerId,
    BrowserPortrait? Portrait, string Description);

/// <summary>Captures independent values for the scenario and faction browser.
/// Controls retain selections and labels, never mutable campaign entities.</summary>
internal static class ScenarioBrowserViews
{
    public static IReadOnlyList<ScenarioBrowserItem> Scenarios(IEnumerable<S14Entry> documents,
        IEnumerable<EffectiveScenario> selections, string locale, PreviewText text)
    {
        var metadata = documents.ToDictionary(item => item.SourcePath, StringComparer.OrdinalIgnoreCase);
        return Array.AsReadOnly(selections.Where(item => item.Locale == locale).OrderBy(item => item.Number)
            .Select(selection =>
            {
                S14Entry entry = metadata[selection.SourcePath];
                string date = entry.StartYear is int year && entry.StartMonth is int month &&
                    entry.StartDay is int day ? text.DateInline(year, month, day) : "";
                return new ScenarioBrowserItem(text.ScenarioItem(selection.Number, date, entry.Title),
                    GameResourceId.File(entry.SourcePath).ToString(), selection);
            }).ToArray());
    }

    public static int FindScenario(IReadOnlyList<ScenarioBrowserItem> items, string resourceUri)
    {
        for (int index = 0; index < items.Count; index++)
            if (items[index].ResourceUri == resourceUri) return index;
        return -1;
    }

    public static int FindForce(IReadOnlyList<BrowserChoice> items, int forceId)
    {
        for (int index = 0; index < items.Count; index++)
            if (items[index].Id == forceId) return index;
        return -1;
    }

    public static ForceBrowserChoices Forces(GameState world, int scenarioNumber,
        int? restoredPlayerForceId, PreviewText text)
    {
        BrowserChoice[] choices = world.Forces.Values.Where(force => force.IsActive || force.Id == restoredPlayerForceId)
            .OrderBy(force => force.Id).Select(force => new BrowserChoice(force.Id,
                text.ForceItem(force.Id, force.Name, world.Cities.Values.Count(city => city.ForceId == force.Id)))).ToArray();
        const int yellowTurbanScenario = 1, pingyuanCity = 7;
        int preferred = scenarioNumber == yellowTurbanScenario && world.Cities.TryGetValue(pingyuanCity, out CityState? city) &&
            city.ForceId is int forceId ? FindForce(choices, forceId) : -1;
        return new(Array.AsReadOnly(choices), choices.Length == 0 ? -1 : preferred < 0 ? 0 : preferred);
    }

    public static ForceBrowserView Force(GameState world, ScenarioCityPreview snapshot,
        S14Entry selected, int forceId, PreviewText text)
    {
        if (!world.Forces.TryGetValue(forceId, out ForceState? force))
            throw new InvalidDataException($"Current force {forceId} is missing.");
        CityState[] cities = world.Cities.Values.Where(city => city.ForceId == forceId).OrderBy(city => city.Id).ToArray();
        OfficerState[] people = world.Officers.Values.Where(person => person.ForceId == forceId &&
            person.Presence is null or OfficerPresence.Present).OrderBy(person => person.Id).ToArray();
        DistrictState[] districts = world.Districts.Values.Where(district => district.ForceId == forceId)
            .OrderBy(district => district.Number).ThenBy(district => district.Id).ToArray();
        GateState[] gates = world.Gates.Values.Where(gate => gate.ForceId == forceId).OrderBy(gate => gate.Id).ToArray();
        OfficerState? ruler = force.RulerId is int rulerId ? world.Officers[rulerId] : null;
        CharacterPreviewRecord? portrait = ruler is null ? null : snapshot.Characters.FirstOrDefault(person =>
            person.CharacterId != 0 && person.CharacterId == ruler.Id);
        string date = text.DateLine(world.Date.Year, world.Date.Month, world.Date.Day);
        string districtRows = string.Join("\n", districts.Select(district =>
            $"{text.DistrictNumber(district.Number)}  {text.DistrictLeader}{text.Colon}" +
            OfficerName(world, district.LeaderId, text)));
        OfficerAbilities? baseAbilities = ruler?.BaseAbilities;
        string abilities = ruler is null ? "" :
            $"\n{text.BaseAbilities}{text.Colon}" +
            $"{text.Leadership} {Number(baseAbilities?.Leadership, text)} / {text.Strength} {Number(baseAbilities?.Strength, text)} / " +
            $"{text.Intelligence} {Number(baseAbilities?.Intelligence, text)} / {text.Politics} {Number(baseAbilities?.Politics, text)} / " +
            $"{text.Charm} {Number(baseAbilities?.Charm, text)}\n{text.BaseAbilitiesHint}";
        string rows = string.Join("\n\n", cities.Select(city =>
        {
            CityPreviewRecord? original = snapshot.Cities.FirstOrDefault(item => item.Slot == city.Id);
            string names = string.Join(text.ListSeparator, people.Where(person => person.LocationId == city.Id).Select(person => person.Name));
            string links = original is null ? text.Get("detail_unknown") : string.Join(text.ListSeparator,
                original.LinkedLocationSlots.Select(slot => LocationName(world, slot)));
            string rawLimits = original is null ? text.Get("detail_unknown") :
                $"{original.RawLimit0:N0} / {original.RawLimit1:N0} / {original.RawLimit2:N0}";
            return $"{city.Id:00} {city.Name}  {text.Governor} {OfficerName(world, city.GovernorId, text)}\n" +
                $"{DistrictName(world, city.DistrictId, text)}  {text.Size}{text.Colon}{CitySizeName(city.Size, text)}\n" +
                $"{text.CityHex}{text.Colon}{Number(city.HexId, text)}  {text.GroupCode}{text.Colon}{Number(original?.GroupCode, text)}\n" +
                $"{text.LinkedLocations}{text.Colon}{links}\n" +
                $"{text.Gold}{text.Colon}{city.Gold:N0}  {text.Supplies}{text.Colon}{city.Supplies:N0}  {text.Troops}{text.Colon}{city.Troops:N0}\n" +
                $"{text.Morale}{text.Colon}{Number(city.Morale, text)}  {text.Durability}{text.Colon}{Number(city.Durability, text, "N0")}\n" +
                $"{text.RawLimits}{text.Colon}{rawLimits}\n{text.OfficerList}{text.Colon}{names}";
        }));
        string gateRows = string.Join("\n", gates.Select(gate =>
            $"{gate.Name}  {DistrictName(world, gate.DistrictId, text)}  {text.Troops}{text.Colon}{gate.Troops:N0}"));
        int controlled = world.Territory.Values.Count(tile => tile.ForceId == forceId);
        int available = world.Territory.Values.Count(tile => tile.AreaId is not null);
        string land = world.Territory.Count == 0 ?
            $"{text.LandControlled}{text.Colon}{text.Get("detail_unknown")}  {text.LandRate}{text.Colon}{text.Get("detail_unknown")}\n" :
            $"{text.LandControlled}{text.Colon}{controlled:N0} / {available:N0}  " +
            $"{text.LandRate}{text.Colon}{(available == 0 ? text.Get("detail_unknown") : $"{100.0 * controlled / available:F1}%")}\n";
        string unresolved = snapshot.UnresolvedCharacters.Count == 0 && snapshot.UnresolvedPlacements.Count == 0 ? "" :
            text.UnresolvedReferences(snapshot.UnresolvedCharacters.Count, snapshot.UnresolvedPlacements.Count) + "\n";
        string description = $"{selected.Title}\n{date}\n{selected.Description}\n\n" +
            $"{text.SourceFile}{text.Colon}{selected.SourcePath}\n{text.SourceHash}{text.Colon}{selected.DecodedSha256}\n" +
            $"{text.DecodedBytes}{text.Colon}{selected.DecodedBytes:N0} {text.Bytes}\n\n" + unresolved +
            $"{text.Force} {forceId}{text.Colon}{force.Name}\n" +
            $"{text.CityRecords}{text.Colon}{cities.Length}  {text.Districts}{text.Colon}{districts.Length}  {text.Officer}{text.Colon}{people.Length}\n" +
            land + $"{text.Gold}{text.Colon}{cities.Sum(city => (long)city.Gold):N0}  {text.Supplies}{text.Colon}{cities.Sum(city => (long)city.Supplies):N0}  " +
            $"{text.Troops}{text.Colon}{cities.Sum(city => (long)city.Troops) + gates.Sum(gate => (long)gate.Troops):N0}\n" +
            $"{text.TroopsIncludesGates}\n{districtRows}{abilities}\n\n" + rows +
            (gates.Length == 0 ? "" : $"\n\n{text.Gates}\n{gateRows}");
        return new(forceId, Array.AsReadOnly(cities.Select(city => new BrowserChoice(city.Id, city.Name)).ToArray()),
            ruler?.LocationId ?? -1, Array.AsReadOnly(people.Select(person => new BrowserChoice(person.Id, person.Name)).ToArray()),
            ruler?.Id ?? -1, portrait is null ? null : new(portrait.AppearanceCode, ruler!.Name), description);
    }

    private static string Number(int? value, PreviewText text, string format = "") =>
        value is int number ? number.ToString(format) : text.Get("detail_unknown");
    private static string OfficerName(GameState world, int? officerId, PreviewText text)
    {
        if (officerId is not int id) return text.Get("detail_unknown");
        return world.Officers.TryGetValue(id, out OfficerState? officer) ? officer.Name :
            throw new InvalidDataException($"Current officer reference {id} is missing.");
    }

    private static string DistrictName(GameState world, int? districtId, PreviewText text)
    {
        if (districtId is not int id) return text.Get("detail_unknown");
        return world.Districts.TryGetValue(id, out DistrictState? district) ? text.DistrictNumber(district.Number) :
            throw new InvalidDataException($"Current district reference {id} is missing.");
    }

    private static string LocationName(GameState world, int id)
    {
        if (world.Cities.TryGetValue(id, out CityState? city)) return city.Name;
        if (world.Gates.TryGetValue(id, out GateState? gate)) return gate.Name;
        throw new InvalidDataException($"Current linked location reference {id} is missing.");
    }
    private static string CitySizeName(CitySize? size, PreviewText text) => size switch
    {
        CitySize.Small => text.CitySize(1), CitySize.Medium => text.CitySize(2),
        CitySize.Large => text.CitySize(3), CitySize.Giant => text.CitySize(4),
        null => text.Get("detail_unknown"),
        _ => throw new InvalidDataException($"Current city size {(int)size.Value} is invalid.")
    };
}
