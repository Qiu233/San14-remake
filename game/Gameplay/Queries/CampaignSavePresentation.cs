using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

/// <summary>One slot-list publication. Definition packages may be shared by
/// saves of the same scenario; every query still captures that save's world.
/// No session is restored, seeded or installed to inspect an existing file.</summary>
internal sealed class CampaignSavePresentation(GameResourceRepository repository)
{
    private readonly Dictionary<string, (ArmyCampaignRuleAdapter Army, LoadedArmyFlagPaneNames Names)> sources = [];

    internal CampaignSaveSummary Capture(GameSessionSave saved)
    {
        var summary = CampaignSaveSummary.Capture(saved);
        var world = saved.State;
        summary = summary with { AdvancedSettings = BattleRemakeCurrentSettings.ReadAdvancedSettings(world) };
        try
        {
            if (!sources.TryGetValue(world.ScenarioKey, out var source))
            {
                var selection = repository.EffectiveScenarios.SingleOrDefault(item =>
                    GameResourceId.File(item.SourcePath).ToString() == world.ScenarioKey);
                if (selection is null) return summary with { PresentationIssue = "save.presentation.scenario_unavailable" };
                var scenario = repository.LoadScenario(selection);
                var map = repository.LoadMapForScenario(selection);
                string revision = $"owned-campaign-army-v1:{world.ScenarioKey}:{scenario.ScenarioSource.Sha256}:{world.MapSha256}:{scenario.FixedDataSource.Sha256}";
                source = (ArmyCampaignRuleAdapter.FromOwnedCampaign(repository, scenario, map, selection.Locale, revision),
                    ArmyFlagPaneNames.Load(scenario));
                sources.Add(world.ScenarioKey, source);
            }
            OfficerPortraitRoute? portrait = world.Forces[saved.PlayerForceId].RulerId is int ruler
                ? source.Army.QueryOfficerPortraitRoutes(world, [ruler]).GetValueOrDefault(ruler) : null;
            var flag = source.Army.ReadDetailForces(world, source.Names, [saved.PlayerForceId])
                .GetValueOrDefault(saved.PlayerForceId);
            return summary with { Portrait = portrait, Flag = flag,
                MiniMap = source.Army.ReadCampaignSaveMiniMap(world),
                PresentationIssue = portrait?.Kind == OfficerPortraitRouteKind.Unresolved ? portrait.UnresolvedInput : null };
        }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException)
        { return summary with { PresentationIssue = error.Message }; }
    }
}
