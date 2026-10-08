using San14.Core;

namespace San14.Data;

/// <summary>The current owned campaign package supplies source definitions;
/// the separate phase owner supplies the temporal Army snapshot. Neither a
/// captured period-end state nor image defaults fill an absent phase capture.</summary>
public static class OriginalNormalArmyCombatOwnedStage13Consumer
{
    public static OriginalNormalArmyCombatCapturedStage13Consumer Create(OriginalGameRepository repository,
        OriginalOwnedArmyCampaignRules package, LoadedMap map,
        IOriginalNormalArmyCombatPhaseLocalCaptureSource phaseCaptures, string revision)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(map); ArgumentNullException.ThrowIfNull(phaseCaptures);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        var skills = repository.LoadSkillDefinitions($"0010_{package.Seed.Language}/fixdataexce.s14");
        var terrain = repository.LoadTerrainTypes($"0010_{package.Seed.Language}/fixdataexce.s14");
        var catalog = OriginalTacticSourceParameters.Read(skills.Formations, skills.Source);
        return new(phaseCaptures, captured => {
            var definitions = package.CaptureCurrentDefinitions(captured);
            if (definitions is null) return null;
            var context = new OriginalTacticRemakeCurrentContext(definitions, catalog, map, terrain,
                new(package.Seed.Language, package.Seed.Source.Sha256), package.Revision);
            return new OriginalNormalArmyCombatSourcePlanner(new OriginalNormalArmyCombatCurrentQueries(context),
                package.Seed.Language, package.Seed.Source.Sha256, revision);
        }, revision);
    }
}
