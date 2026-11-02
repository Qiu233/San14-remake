using System.Collections.Immutable;
using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

/// <summary>UI-thread snapshots for one normal new-game preparation. The
/// transform may borrow captured editor records, but must use its supplied
/// repository and must not read or change a Main, store, Control or session.</summary>
public sealed record CampaignNewGameInputs(string SourceDirectory, string? OverrideDirectory,
    EffectiveScenario Selection, NewGameOptions Options,
    ImmutableArray<int?> PersonnelMoveDestinations, NarrationVoiceSettings VoiceSettings,
    Func<GameResourceRepository, LoadedScenario, LoadedScenario>? TransformScenario = null)
{
    public ApplicationPresentationOptions? PresentationOptions { get; init; }
    /// <summary>Caller-captured resource reader template. Preparation creates
    /// its own reader while borrowing this process owner's static sources and
    /// preserving the captured user-portrait application facts.</summary>
    public GameResourceRepository? ResourceSources { get; init; }
}

/// <summary>Read-only definitions and initializing world captured before a
/// worker begins. No live composition, query cache or session is borrowed.</summary>
public sealed record CampaignNewGameRetainedPreview(LoadedScenario Scenario, LoadedMap Map, GameState World);

/// <summary>Fully prepared CPU content. Once preparation returns, the caller
/// may transfer this repository, composition and session together to its UI.</summary>
public sealed record CampaignNewGamePreviewPreparation(GameResourceRepository Repository,
    CampaignLoadResult Campaign, NewGameForceSelectionPreview ForceSelection,
    NewGameSettingsPreview Settings);

public sealed record CampaignNewGameStartPreparation(GameResourceRepository Repository,
    CampaignComposition Composition, GameplaySession Session, CampaignPresentation Presentation);

public sealed partial class CampaignComposition
{
    private static void TraceNewGamePreparation(Action<string>? timing, string stage)
    {
        if(timing is null)return;
        try { timing(stage); }
        catch(Exception) { } // An optional diagnostic sink cannot change game preparation.
    }

    public static ImmutableArray<int?> CaptureNewGamePersonnelMoveDestinations(
        PersonnelGroupMoveDestinations source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Enumerable.Range(0, 10).Select(source.GetDestination).ToImmutableArray();
    }

    /// <summary>CPU-only preparation for the caller's worker. Godot object
    /// creation and adoption of the completed result remain on the UI thread.</summary>
    public static CampaignNewGamePreviewPreparation PrepareNewGamePreview(CampaignNewGameInputs inputs,
        CancellationToken cancellationToken = default, Action<string>? timing = null)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        cancellationToken.ThrowIfCancellationRequested();
        var repository = CreateNewGameReadScope(inputs);
        TraceNewGamePreparation(timing, "preview-load-begin");
        CampaignLoadResult campaign = LoadNewGameCandidate(repository, inputs, cancellationToken);
        TraceNewGamePreparation(timing, "preview-load-end");
        ConfigureNewGameCandidate(campaign.Composition, inputs);
        cancellationToken.ThrowIfCancellationRequested();
        var army = campaign.Composition.PrepareArmyQueries(campaign.Session.World);
        var forces = army.CaptureNewGameForceSelection(campaign.Session.World,
            //326230 freshly constructs World16A8=0; static scenario loading
            //does not replace it. This route has not adopted a saved campaign.
            ArmyFlagPaneNames.Load(campaign.Composition.Scenario), new(false, null, false),
            previewWorldFlags16A8: 0u);
        cancellationToken.ThrowIfCancellationRequested();
        TraceNewGamePreparation(timing, "force-publication-prepared");
        var settings = army.CaptureNewGameSettings(inputs.Options);
        TraceNewGamePreparation(timing, "settings-publication-prepared");
        cancellationToken.ThrowIfCancellationRequested();
        return new(repository, campaign, forces, settings);
    }

    /// <summary>Prepares one confirmed new game on independent resource,
    /// application, query and session owners. A null retained preview requests
    /// the same reload used when the user's settings or editor choices changed.</summary>
    public static CampaignNewGameStartPreparation PrepareNewGameStart(CampaignNewGameInputs inputs,
        CampaignNewGameRetainedPreview? retainedPreview, int playerForceId,
        CampaignAppRandomState appRandomSnapshot, CampaignCrtRandomState crtRandomSnapshot,
        CancellationToken cancellationToken = default, Action<string>? timing = null)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(appRandomSnapshot);
        ArgumentNullException.ThrowIfNull(crtRandomSnapshot);
        cancellationToken.ThrowIfCancellationRequested();
        var repository = CreateNewGameReadScope(inputs);
        CampaignComposition composition;
        GameplaySession preview;
        if (retainedPreview is null)
        {
            TraceNewGamePreparation(timing, "start-preview-reload-begin");
            CampaignLoadResult campaign = LoadNewGameCandidate(repository, inputs, cancellationToken);
            TraceNewGamePreparation(timing, "start-preview-reload-end");
            composition = campaign.Composition;
            preview = campaign.Session;
            // ShowScenario formerly adopted this reloaded preview first. Its
            // saved App32/CRT, when present, replaced the host streams before
            // StartPlayable captured them. Preserve that order without a UI
            // adoption or a mutation of the visible preview.
            appRandomSnapshot = preview.World.CampaignAppRandom32 ?? appRandomSnapshot;
            crtRandomSnapshot = preview.World.CampaignCrtRandom ?? crtRandomSnapshot;
        }
        else
        {
            if (retainedPreview.World.Phase != TurnPhase.Initializing ||
                retainedPreview.World.NewGameOptions != inputs.Options ||
                !string.Equals(retainedPreview.Scenario.Metadata.SourcePath,
                    inputs.Selection.SourcePath, StringComparison.OrdinalIgnoreCase) ||
                retainedPreview.Scenario.Snapshot.Locale != inputs.Selection.Locale)
                throw new InvalidOperationException("The retained new-game preview differs from the confirmed selection.");
            TraceNewGamePreparation(timing, "start-retained-preview");
            composition = new CampaignComposition(repository, retainedPreview.Scenario, retainedPreview.Map,
                NewGamePersonnelMoveDestinations(inputs.PersonnelMoveDestinations));
            preview = GameplaySession.Preview(retainedPreview.World,
                PlayableSkeleton.Bind(new HexTravelMap(retainedPreview.Map)), composition._saveCodec);
        }
        ConfigureNewGameCandidate(composition, inputs);
        cancellationToken.ThrowIfCancellationRequested();
        TraceNewGamePreparation(timing, "start-simulation-begin");
        CampaignStartResult started = composition.Start(preview, playerForceId, appRandomSnapshot, crtRandomSnapshot);
        TraceNewGamePreparation(timing, "start-simulation-end");
        cancellationToken.ThrowIfCancellationRequested();
        return new(repository, composition, started.Session, started.Presentation);
    }

    private static GameResourceRepository CreateNewGameReadScope(CampaignNewGameInputs inputs)
    {
        // Retain the existing directory-only constructor contract for callers
        // that prepare an independent campaign outside the application host.
        if (inputs.ResourceSources is not { } sources)
            return new GameResourceRepository(inputs.SourceDirectory, inputs.OverrideDirectory);
        string directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(inputs.SourceDirectory));
        string? overrides = inputs.OverrideDirectory is null ? null :
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(inputs.OverrideDirectory));
        if (!StringComparer.OrdinalIgnoreCase.Equals(directory, sources.SourceDirectory) ||
            !StringComparer.OrdinalIgnoreCase.Equals(overrides, sources.OverrideDirectory))
            throw new InvalidDataException("The captured new-game resource source differs from its selected directories.");
        return sources.CreateReadScope();
    }

    private static CampaignLoadResult LoadNewGameCandidate(GameResourceRepository repository,
        CampaignNewGameInputs inputs, CancellationToken cancellationToken)
    {
        var destinations = NewGamePersonnelMoveDestinations(inputs.PersonnelMoveDestinations);
        var campaign = Load(repository, inputs.Selection, inputs.Options,
            retainedPersonnelGroupMoveDestinations: destinations,
            loadedScenarioTransform: inputs.TransformScenario is { } transform
                ? source => { cancellationToken.ThrowIfCancellationRequested(); return transform(repository, source); }
                : null);
        cancellationToken.ThrowIfCancellationRequested();
        return campaign;
    }

    private static PersonnelGroupMoveDestinations NewGamePersonnelMoveDestinations(ImmutableArray<int?> values)
    {
        if (values.IsDefault || values.Length != 10)
            throw new InvalidDataException("New-game preparation requires the application's ten group-move selections.");
        var destinations = new PersonnelGroupMoveDestinations();
        for (int index = 0; index < values.Length; index++) destinations.SetDestination(index, values[index]);
        return destinations;
    }

    private static void ConfigureNewGameCandidate(CampaignComposition composition, CampaignNewGameInputs inputs)
    {
        composition.ConfigurePersonnelNarrationVoice(inputs.VoiceSettings);
        if (inputs.PresentationOptions is { } presentation)
            composition.ConfigureApplicationPresentationOptions(presentation);
    }
}
