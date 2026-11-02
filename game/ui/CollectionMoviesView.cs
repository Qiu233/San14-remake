using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The original four-column movie grid and its retained scroll owner.</summary>
internal partial class CollectionMoviesView : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private CollectionMovieCatalog catalog = null!;
    private bool ownsResources;
    private AdministrationPageChrome.Resources? resources;
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!, grid = null!;
    private UiSourceScrollbar scrollbar = null!;
    private readonly List<UiSourceMovieButton> cells = [];
    private UiSourceCommandButton subtitleChoice = null!;
    private CollectionMoviePlaybackView? playback;
    private DeploymentTargetConfirmation? question;
    private Func<bool>? admitted;
    private Action? returned, movieEntered;
    private Action<bool>? movieLeft;
    private Action<Action> runUiAction = null!;
    private Func<float> movieVolume = null!;
    private Func<bool?> moviePlaybackEnabled = null!;
    private int firstRow = -1;
    private bool showSubtitles, abandoned;
    private bool musicOwned;
    private string nativeLibrary = "";

    internal void Configure(GameResourceRepository repository, PreviewText text,
        CollectionMovieCatalog catalog, string nativeLibrary, Func<bool> admitted,
        Action returned, Func<int> scrollSpeed, Action<Action> runUiAction,
        Func<float> movieVolume, Action movieEntered, Action<bool> movieLeft, Func<bool?> moviePlaybackEnabled, AdministrationPageChrome.Resources? resources = null)
    {
        this.repository = repository; this.text = text; this.catalog = catalog; this.nativeLibrary = nativeLibrary;
        this.admitted = admitted; this.returned = returned; this.runUiAction = runUiAction;
        this.movieVolume = movieVolume; this.movieEntered = movieEntered; this.movieLeft = movieLeft;
        this.moviePlaybackEnabled = moviePlaybackEnabled;
        Name = "CollectionMovies"; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        this.resources = resources; ownsResources = resources is null;
        assets = resources?.Assets ?? new(repository);
        font = resources?.Font ?? new(repository, text.SourceLocale);
        shader = resources?.SpriteShader ?? UiSourceSprite.CreateShader();
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var source = assets.SourceLayout(catalog.LayoutName);
        source.Blur(canvas, 2, source.Rectangle(2));
        source.Art(canvas, 3, source.Rectangle(3), assets, shader);
        var headerSource = assets.SourceLayout("cmn_header_re_new");
        headerSource.Art(canvas, 1, headerSource.Rectangle(1), assets, shader);
        headerSource.Text(canvas, 2, default, catalog.Title, font, MapTextPresets.Create()[56]);
        var counts = assets.SourceLayout("cmn_header_offset_00");
        var countHost = new Control { Name = "MovieUnlockCount", Position = headerSource.Rectangle(18).Position,
            Size = counts.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        canvas.AddChild(countHost);
        counts.Art(countHost, 1, counts.Rectangle(1), assets, shader);
        counts.Text(countHost, 2, default, catalog.ProgressCaption, font);
        counts.Text(countHost, 5, default, catalog.UnlockedCount?.ToString() ?? text.Get("detail_unknown"), font);
        counts.Text(countHost, 4, default, repository.LoadMessages(text.SourceLocale, MessageKind.Data)
            .Messages.Sections[1].Records[0x60C].Text ?? throw new InvalidDataException("Movie count separator is missing."), font);
        counts.Text(countHost, 3, default, catalog.TotalCount.ToString(), font);
        var area = source.Rectangle(12);
        grid = new() { Name = "OriginalMovieGrid", Position = area.Position, Size = area.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        canvas.AddChild(grid);
        var scrollBox = source.Rectangle(4);
        scrollbar = new() { Name = "MovieScrollbar", Position = scrollBox.Position, Size = scrollBox.Size };
        canvas.AddChild(scrollbar);
        scrollbar.Configure(repository, assets, shader, Math.Max(0, ((catalog.Rows.Count + 3) / 4) * 280 - (int)area.Size.Y),
            scrollSpeed(), _ => PositionCells(), currentScrollSpeed: scrollSpeed);
        grid.GuiInput += input => { if (GridAdmission()) scrollbar.HandleWheel(input); };
        var footer = assets.SourceLayout(0xD8913AB2);
        var back = new UiSourceRoundButton { Name = "CollectionMoviesReturn",
            Position = footer.Rectangle(1, new(0, 904)).Position, InputAdmission = GridAdmission };
        canvas.AddChild(back);
        back.ConfigureScenarioReturn(repository, assets, shader, text.SourceLocale, RequestBack);
        var choice = footer.Rectangle(14, new(0, 904));
        showSubtitles = catalog.ShowSubtitlesByDefault;
        subtitleChoice = new() { Name = "MovieSubtitleChoice", Position = choice.Position, Size = choice.Size,
            Selected = showSubtitles, InputAdmission = GridAdmission };
        canvas.AddChild(subtitleChoice);
        subtitleChoice.Configure(repository, assets, shader, font, catalog.SubtitleCaption,
            () => { if (GridAdmission()) { showSubtitles = !showSubtitles; subtitleChoice.Selected = showSubtitles; } });
        PositionCells(); Resized += Fit; Fit();
        SetMeta("catalog_count", catalog.Rows.Count); SetMeta("campaign_required", false);
    }

    private void PositionCells()
    {
        int first = scrollbar.Value / 280;
        if (first != firstRow)
        {
            foreach (var old in cells) { old.Hide(); old.QueueFree(); }
            cells.Clear(); firstRow = first;
            for (int index = first * 4; index < Math.Min(catalog.Rows.Count, (first + 4) * 4); index++)
            {
                var row = catalog.Rows[index];
                var cell = new UiSourceMovieButton { Name = "MovieCell" + index, InputAdmission = GridAdmission };
                grid.AddChild(cell); cells.Add(cell);
                cell.Configure(repository, assets, shader, font, row, () => runUiAction(() => SelectMovie(row.RowId)));
                cell.GuiInput += input => { if (GridAdmission()) scrollbar.HandleWheel(input); };
            }
        }
        for (int slot = 0; slot < cells.Count; slot++)
        {
            int index = first * 4 + slot;
            cells[slot].Position = new(20 + index % 4 * 340, 20 + index / 4 * 280 - scrollbar.Value);
        }
    }

    private void SelectMovie(int id)
    {
        if (!GridAdmission()) return;
        if (movieVolume() != 0) { ShowMovie(id); return; }
        var view = new DeploymentTargetConfirmation(); question = view; AddChild(view);
        view.ConfigureWrappedMessage(repository, text, new AdministrationMessages(repository, text.SourceLocale).Format(0x2913),
            accepted => runUiAction(() =>
            {
                if (question != view) return;
                CloseQuestion(); if (accepted && GridAdmission()) ShowMovie(id);
            }));
    }

    private void ShowMovie(int id)
    {
        var publication = catalog.ReadPlayback(id, showSubtitles);
        bool enabled = MoviePlaybackSource.IsPlaybackEnabled(publication.MovieId, moviePlaybackEnabled())
            ?? throw new InvalidDataException("movie.playback.environment_setting");
        if (!enabled) return;
        var view = new CollectionMoviePlaybackView(); playback = view; AddChild(view); canvas.Hide();
        try
        {
            movieEntered?.Invoke(); musicOwned = true;
            view.Configure(repository, text, publication, nativeLibrary,
                () => ReadAdmission() && playback == view, movieVolume,
                () => runUiAction(() => CloseMovie(true)),
                error => runUiAction(() => { CloseMovie(true); throw error; }), resources: resources);
        }
        catch { CloseMovie(true); throw; }
    }

    private void CloseQuestion()
    {
        var view = question; question = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
    }
    private void CloseMovie(bool restore)
    {
        var view = playback; playback = null;
        if (view is not null) { view.Abandon(); _ = ObserveRetirement(view.Retirement); view.QueueFree(); }
        if (!abandoned) canvas.Show();
        if (musicOwned) { musicOwned = false; movieLeft?.Invoke(restore && !abandoned); }
    }
    private static async Task ObserveRetirement(Task completion)
    {
        try { await completion; }
        catch (Exception error) { GD.PushWarning(error.Message); }
    }
    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    private bool GridAdmission() => ReadAdmission() && playback is null && question is null;
    internal void RequestBack()
    {
        if (!ReadAdmission()) return;
        if (question is { } asking) asking.RequestCancel();
        else if (playback is { } movie) movie.RequestBack(); else returned?.Invoke();
    }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; CloseQuestion(); CloseMovie(false);
        admitted = null; returned = null; movieEntered = null; movieLeft = null; Hide();
    }
    private void Fit()
    {
        if (canvas is null) return;
        float factor = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * factor; canvas.Position = (Size - new Vector2(1920, 1080) * factor) / 2;
    }
    public override void _ExitTree()
    { Abandon(); if (ownsResources) { font?.Dispose(); assets?.Dispose(); shader?.Dispose(); } resources = null; }
}
