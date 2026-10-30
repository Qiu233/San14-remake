using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The eight real title lessons. Resource loading and read-only
/// browsing never complete a course, grant its rewards or create a campaign.</summary>
public sealed class TutorialCatalog
{
    public string Locale { get; }
    public string Title { get; }
    public string LayoutName => "02_03_00_scenario_sel_re";
    public string ButtonSetLayoutName => "02_03_00_scenario_btn_set_offset_00";
    public uint ButtonLayoutId => 0xF2C9CA9A;
    public string DescriptionLayoutName => "02_03_00_scenario_sel_offset_01";
    public string RewardSetLayoutName => "13_00_03_tutorial_sel_offset_00";
    public string RewardLayoutName => "13_00_03_tutorial_sel_offset_01";
    public string FooterTitle { get; }
    public string FooterGuide { get; }
    public IReadOnlyList<int> ButtonMountNodes => TutorialDefinitions.ButtonMountNodes;
    public IReadOnlyList<TutorialCourse> Courses { get; }
    public IReadOnlyList<SourceProvenance> Sources { get; }

    private TutorialCatalog(GameResourceRepository repository, string locale, TutorialProgressInputs? progress)
    {
        Locale = locale;
        var messages = repository.LoadMessages(locale, MessageKind.Data);
        var game = repository.LoadMessages(locale, MessageKind.Game);
        var formatter = new NarrationResourceMessages(game.Messages);
        Title = messages.Messages.Sections[1].Records[0x2B].Text
            ?? throw new InvalidDataException("tutorial.catalog.title");
        FooterTitle = formatter.ResolveTutorialCatalog(0x9C49);
        FooterGuide = formatter.ResolveTutorialCatalog(0x9C53);
        //The title already owns scenario08's prototype people. Reproduce that
        //normal resource producer for the reward cards, without a fake World.
        var prototype = repository.EffectiveScenarios.SingleOrDefault(s => s.Locale == locale && s.Number == 8)
            ?? throw new InvalidDataException("tutorial.catalog.prototype_scenario08");
        var loaded = repository.LoadScenario(prototype);
        var people = loaded.Snapshot.Characters.Where(p => p.CharacterId != 0)
            .ToDictionary(p => p.CharacterId);
        var rows = new List<TutorialCourse>();
        var portraitNames = ArmyMapUiSources.Create();
        for (int index = 0; index < 8; index++)
        {
            var rewards = new List<TutorialReward>();
            for (int slot = 0; slot < 3; slot++)
            {
                int id = TutorialDefinitions.Rewards[index][slot];
                if (id == 0) { rewards.Add(new(slot, 0, string.Empty, false, null, null, null)); continue; }
                bool? available = progress?.ReadRewardAvailable?.Invoke(id);
                bool? unlocked = progress?.ReadRewardUnlocked?.Invoke(id);
                string? unresolved = available is null ? "tutorial.reward.application_availability" :
                    available == true && unlocked is null ? "tutorial.reward.application_unlock" : null;
                OfficerPortraitRoute? portrait = null;
                if (!people.TryGetValue(id, out var person))
                {
                    rewards.Add(new(slot, id, string.Empty, available, unlocked, null,
                        "tutorial.reward.prototype_person")); continue;
                }
                if (available == true)
                {
                    ushort appearance = checked((ushort)person.AppearanceCode);
                    var names = portraitNames.PortraitNames(appearance);
                    portrait = new(id, OfficerPortraitRouteKind.StockNamed, appearance,
                        names.ResolvedAppearanceIndex, names.ResourceName, names.SpriteName, null, null);
                }
                rewards.Add(new(slot, id, person.Name, available, unlocked, portrait, unresolved));
            }
            rows.Add(new(index, formatter.ResolveTutorialCatalog(0x9C41 + index),
                formatter.ResolveTutorialCatalog(0x9C4A + index), index < 6 ? 71 : index == 6 ? 72 : 73,
                progress?.ReadCourseCompleted?.Invoke(index), rewards.AsReadOnly(),
                Array.AsReadOnly(TutorialDefinitions.Paragraphs[index].Select((name, position) =>
                    new TutorialParagraph(position, name)).ToArray())));
        }
        Courses = rows.AsReadOnly();
        Sources = Array.AsReadOnly(new[] { messages.Source, game.Source, loaded.ScenarioSource, loaded.FixedDataSource });
    }

    public static TutorialCatalog Load(GameResourceRepository repository, string locale,
        TutorialProgressInputs? progress = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (locale is not ("TC" or "SC")) throw new ArgumentOutOfRangeException(nameof(locale));
        return new(repository, locale, progress);
    }
    public TutorialCourse ReadCourse(int index) => (uint)index < 8 ? Courses[index]
        : throw new ArgumentOutOfRangeException(nameof(index));

    public TutorialLessonPreparation PrepareLesson(GameResourceRepository repository,
        int index, TutorialPreparationInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(inputs.Options);
        var course = ReadCourse(index);
        LoadedScenario? scenario = null; LoadedMap? map = null;
        GameState? world = null; ArmyCampaignRuleAdapter? rules = null;
        try
        {
            var selected = repository.EffectiveScenarios.SingleOrDefault(s =>
                s.Locale == Locale && s.Number == course.ScenarioNumber)
                ?? throw new InvalidDataException("tutorial.preparation.selected_scenario");
            scenario = repository.LoadScenario(selected); map = repository.LoadMapForScenario(selected);
            var initial = ScenarioInitializer.Create(scenario, map, inputs.RandomSeed, inputs.Options).State;
            rules = ArmyCampaignRuleAdapter.FromOwnedCampaign(repository, scenario, map,
                Locale, SimulationRuleIdentity.ForLocale(Locale));
            rules.ConfigurePersonnelNarrationVoice(inputs.VoiceSettings);
            initial = rules.CreateFreshScenarioMapObjects(initial);
            var started = PlayableSkeleton.Start(initial) with
                { CampaignAppRandom32 = inputs.AppRandom, CampaignCrtRandom = inputs.CrtRandom };
            world = rules.CreateFreshOwnedSimulation(started);
            world = MapSeasonRuntime.CreateForFreshCampaign(repository, world, MapSeasonNativeSource.Load(string.Empty, Locale));
            world = TutorialLessonLifecycleSource.SelectCurrent(world, index);
            //Only4FAE70's actual allocation prefix. Its camera, views, timer,
            //paragraph activation and later bit writers require the lesson host.
            world = TutorialLessonLifecycleSource.InitializeSelectedLessonBitmapCurrent(world);
            world = rules.InitializeSelectedTutorialWorldCurrent(world);
            return new(course, scenario, map, world, rules, null);
        }
        catch (InvalidDataException error) { return new(course, scenario, map, world, rules, error.Message); }
    }
}

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveTutorialCatalog(int root)
    {
        bool Allowed(int id) => id is >= 0x9C41 and <= 0x9C53;
        if (!Allowed(root)) throw new InvalidDataException("tutorial.catalog.message_root");
        _prefix = "tutorial.catalog";
        return ExecuteMessage(InformationExplanationArguments.Instance,
            _ => throw Failure("unexpected_random"), root, Get(root, true), _ => false, true,
            null, Allowed, (group, code) => (group, code) switch
            { ('C', 'J') => "^02", ('C', 'L') => "^05", _ => throw Failure($"escape_{group:X2}_{code:X2}") }, true).Text;
    }
}
