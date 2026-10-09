using San14.Core;

namespace San14.Client.Gameplay;

public sealed record FormationCommandChoice(int Id, string Name, FormationBaseCoefficients? BaseCoefficients);

/// <summary>Officer loadout previews. Factory results retain independent display
/// values; deployment and tactic admission remain simulation decisions.</summary>
public static class MilitaryLoadoutChoices
{
    public static IReadOnlyList<FormationCommandChoice> Formations(GameState world, int? selectedOfficer)
    {
        if (selectedOfficer is not int officerId || !world.Officers.TryGetValue(officerId, out OfficerState? officer) ||
            officer.BaseFormationIds is not { } ids) return Array.Empty<FormationCommandChoice>();
        return Array.AsReadOnly(ids.Where(id => id > 0).Distinct().Select(id =>
        {
            if (!world.Formations.TryGetValue(id, out FormationDefinition? formation))
                throw new InvalidDataException($"Officer {officer.Id} references missing formation {id}.");
            return new FormationCommandChoice(id, formation.Name, formation.SourceBase);
        }).ToArray());
    }

    /// <summary>Null means there is no selected officer/formation. An empty list
    /// means the selected combination has no available tactic, retaining None.</summary>
    public static IReadOnlyList<NamedCommandChoice>? Tactics(GameState world, int? selectedOfficer, int? selectedFormation)
    {
        if (selectedOfficer is not int officerId || selectedFormation is not int formationId ||
            !world.Officers.TryGetValue(officerId, out OfficerState? officer)) return null;
        return Array.AsReadOnly((officer.BaseTacticIds ?? []).Where(id => id > 0).Distinct().Where(id =>
        {
            if (!world.Tactics.TryGetValue(id, out TacticDefinition? tactic))
                throw new InvalidDataException($"Officer {officer.Id} references missing tactic {id}.");
            return tactic.FormationIds?.Contains(formationId) == true;
        }).Select(id => new NamedCommandChoice(id, world.Tactics[id].Name)).ToArray());
    }
}
