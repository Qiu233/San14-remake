using San14.Core;

namespace San14.Data;

/// <summary>Operation-owned proposal rules. The saved board owns offers; this
/// object holds only the current command's world queries and shared App stream.</summary>
internal sealed partial class ProposalLifecycleSource
{
    private readonly Func<GameState, SourceWorldContext> capture;
    private readonly SharedSourceRandom32 random;
    internal ArmyCampaignRuleAdapter Package { get; }
    internal OwnedBaseCapacityInputs Panels { get; }
    internal NormalArmyAftermathInputs Aftermath { get; }
    internal GameResourceRepository Repository { get; }
    internal LoadedScenario Scenario { get; }
    internal LoadedMap Map { get; }
    internal string Language { get; }
    internal int ForceId { get; }
    internal GameState State { get; set; }
    internal SourceWorldContext Current { get; private set; }
    internal AreaAdministrationPreviewSource Preview => new(State, Current, Scenario, Language, Panels, true);

    internal ProposalLifecycleSource(GameState state, int forceId, ArmyCampaignRuleAdapter package,
        GameResourceRepository repository, LoadedScenario scenario, LoadedMap map, string language,
        OwnedBaseCapacityInputs panels, NormalArmyAftermathInputs aftermath,
        Func<GameState, SourceWorldContext> capture)
    {
        State = state; ForceId = forceId; Package = package; Repository = repository;
        Scenario = scenario; Map = map; Language = language; Panels = panels; Aftermath = aftermath;
        this.capture = capture;
        random = new(state.CampaignAppRandom32?.State ?? throw Missing("App_random_owner"));
        Current = capture(state);
    }

    internal void RefreshCurrent() => Current = capture(State);
    internal int Next(int bound)
    {
        random.InstallCommittedState(State.CampaignAppRandom32?.State ?? throw Missing("App_random_owner"));
        int result = random.Next(bound);
        State = State with { CampaignAppRandom32 = new(random.State) };
        return result;
    }

    internal int[] CreateRanks(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        int[] ranks = Enumerable.Range(0, count).ToArray();
        //424CE0 uses the full bound for every swap, including the last one.
        for (int index = 0; index < count; index++)
        {
            int other = Next(count);
            (ranks[index], ranks[other]) = (ranks[other], ranks[index]);
        }
        return ranks;
    }

    internal int PersonPhysical(int identity) => Current.ResolveOfficerSource(identity)
        ?? throw Missing("Person_lookup");
    internal int PersonIdentity(int physical) => K(Current.ReadPersonIdentity(physical), "Person_identity");

    internal int Rule(string name, int sourceInitial)
    {
        var parameters = State.NativeArmySimulation?.Parameters ?? throw Missing("parameter_owner");
        string key = "simulation.proposal." + name;
        if (parameters.Float32Values.ContainsKey(key)) throw Missing("parameter_type." + name);
        return parameters.Int32Values.TryGetValue(key, out int value) ? value : sourceInitial;
    }
    internal int RequiredRule(string name)
    {
        var parameters = State.NativeArmySimulation?.Parameters ?? throw Missing("parameter_owner");
        return parameters.Int32Values.TryGetValue("simulation.proposal." + name, out int value)
            ? value : throw Missing("current_parameter." + name);
    }

    internal void WriteAreaDevelopment(int area, int dimension, int value)
    {
        var field = dimension switch
        {
            0 => CeAreaDevelopmentField.Commerce, 1 => CeAreaDevelopmentField.Agriculture,
            2 => CeAreaDevelopmentField.Barracks,
            _ => throw Missing("Area_development_dimension")
        };
        var writer = new OwnedAreaDevelopmentContext(State, Current, Aftermath);
        Aftermath.Development.WriteCurrentProgress(new(area), field, value, 1, writer);
        State = writer.State;
    }

    internal static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    internal static InvalidDataException Missing(string field) => new("proposal." + field);
}
