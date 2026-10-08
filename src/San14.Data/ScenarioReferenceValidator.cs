using San14.Data.Formats;

namespace San14.Data;

public sealed record UnresolvedCharacterReference(string EntityKind, int EntitySlot,
    int CharacterSlot);

public sealed record UnresolvedCharacterPlacement(string EntityKind, int EntitySlot,
    int CharacterId, int RecordSlot, string RawBytesHex);

public sealed record ScenarioReferenceReport(
    IReadOnlyList<UnresolvedCharacterReference> UnresolvedCharacters)
{
    public IReadOnlyList<UnresolvedCharacterPlacement> UnresolvedPlacements { get; init; } = [];
}

/// <summary>
/// Checks references through explicit character IDs rather than record positions.
/// Unknown IDs and references to template placements remain explicit in the report.
/// </summary>
public static class ScenarioReferenceValidator
{
    public static ScenarioReferenceReport Validate(S14Document scenario, string locale)
    {
        IReadOnlyList<S14CharacterIdentity> identities =
            S14ScenarioTables.ReadCharacterIdentities(scenario, locale);
        IReadOnlyList<S14CharacterPlacement> placements =
            S14ScenarioTables.ReadCharacterPlacements(scenario, locale);
        IReadOnlyList<S14ScenarioCity> cities =
            S14ScenarioTables.ReadScenarioCities(scenario);
        IReadOnlyList<S14ScenarioGate> gates =
            S14ScenarioTables.ReadScenarioGates(scenario);
        IReadOnlyList<S14ScenarioForce> forces =
            S14ScenarioTables.ReadScenarioForces(scenario);
        IReadOnlyList<S14ScenarioDistrict> districts =
            S14ScenarioTables.ReadScenarioDistricts(scenario);
        // This reader also validates each region's faction ID.
        _ = S14ScenarioTables.ReadScenarioAreas(scenario);
        var unresolved = new List<UnresolvedCharacterReference>();
        var unresolvedPlacements = new List<UnresolvedCharacterPlacement>();
        Dictionary<int, int> characterRows = identities.Where(identity => identity.CharacterId != 0)
            .ToDictionary(identity => (int)identity.CharacterId, identity => identity.Slot);

        int ForceOf(int districtSlot, string entityKind, int entitySlot)
        {
            if (districtSlot < 0 || districtSlot >= districts.Count)
                throw new InvalidDataException(
                    $"S14 {entityKind} {entitySlot} has invalid district {districtSlot}.");
            return districts[districtSlot].ForceSlot;
        }

        bool TryPlacement(string entityKind, int entitySlot, int characterId,
            out S14CharacterPlacement placement)
        {
            placement = null!;
            if (characterId == 0) return false;
            if (!characterRows.TryGetValue(characterId, out int recordSlot))
            {
                unresolved.Add(new UnresolvedCharacterReference(entityKind, entitySlot,
                    characterId));
                return false;
            }
            placement = placements[recordSlot];
            if (placement.StatusCode == 0 || placement.CitySlot == 0)
            {
                unresolvedPlacements.Add(new UnresolvedCharacterPlacement(entityKind,
                    entitySlot, characterId, recordSlot, placement.RawBytesHex));
                return false;
            }
            return true;
        }

        foreach (S14CharacterPlacement character in placements)
        {
            _ = ForceOf(character.DistrictSlot, "character", character.Slot);
            if (character.CitySlot > 61 || character.SecondaryCitySlot > 61)
                throw new InvalidDataException(
                    $"S14 character {character.Slot} has unsupported static location " +
                    $"{character.CitySlot}/{character.SecondaryCitySlot}: {character.RawBytesHex}.");
        }

        foreach (S14ScenarioCity city in cities)
        {
            int cityForce = ForceOf(city.DistrictSlot, "city", city.Slot);
            if (!TryPlacement("city", city.Slot, city.OfficerSlot,
                out S14CharacterPlacement governor)) continue;
            if (governor.CitySlot != city.Slot ||
                ForceOf(governor.DistrictSlot, "character", governor.Slot) != cityForce)
                throw new InvalidDataException(
                    $"S14 city {city.Slot} governor {governor.Slot} location or faction differs: " +
                    $"{governor.RawBytesHex}.");
        }

        foreach (S14ScenarioGate gate in gates)
        {
            int gateForce = ForceOf(gate.DistrictSlot, "gate", gate.Slot);
            if (!TryPlacement("gate", gate.Slot, gate.OfficerSlot,
                out S14CharacterPlacement governor)) continue;
            int gateLocation = gate.Slot == 0 ? 0 : 51 + gate.Slot;
            if (governor.CitySlot != gateLocation ||
                ForceOf(governor.DistrictSlot, "character", governor.Slot) != gateForce)
                throw new InvalidDataException(
                    $"S14 gate {gate.Slot} governor {governor.Slot} location or faction differs: " +
                    $"{governor.RawBytesHex}.");
        }

        foreach (S14ScenarioForce force in forces)
        {
            if (!TryPlacement("force", force.Slot, force.RulerSlot,
                out S14CharacterPlacement ruler)) continue;
            if (ForceOf(ruler.DistrictSlot, "character", ruler.Slot) != force.Slot)
                throw new InvalidDataException(
                    $"S14 force {force.Slot} ruler {ruler.Slot} faction differs: {ruler.RawBytesHex}.");
        }

        foreach (S14ScenarioDistrict district in districts)
        {
            if (!TryPlacement("district", district.Slot, district.LeaderSlot,
                out S14CharacterPlacement leader)) continue;
            // CE30's second district names a leader from its faction's first district.
            // The source agrees on faction ownership; exact district equality is not required.
            if (ForceOf(leader.DistrictSlot, "character", leader.Slot) != district.ForceSlot)
                throw new InvalidDataException(
                    $"S14 district {district.Slot} leader {leader.Slot} faction differs: " +
                    $"{leader.RawBytesHex}.");
        }

        return new ScenarioReferenceReport(unresolved)
        {
            UnresolvedPlacements = unresolvedPlacements
        };
    }
}
