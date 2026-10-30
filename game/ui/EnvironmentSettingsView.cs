using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The original environment settings pages. The application owns its
/// entry snapshot and commit; each enabled row edits a real current consumer.</summary>
internal partial class EnvironmentSettingsView : Control
{
    private AdministrationPageChrome chrome = null!;
    private UiSourceBottomButton defaults = null!;
    private Control help = null!;
    private MapFontProfile helpProfile = null!;
    private string? helpText;
    private readonly List<(UiSourceRadioButton Button, int Row, int Index)> choices = [];
    private readonly List<UiSourceTabButton> tabs = [];
    private readonly Control[] pages = [new(), new(), new()];
    private Action? returned, accepted, reset;
    private bool abandoned, busy;
    private string defaultHelp = "";
    internal MapCameraNavigationOptions Draft { get; private set; } = new();
    internal ListScrollSpeed ListScrollDraft { get; private set; }
    internal bool AutomaticSavingDraft { get; private set; }
    internal VoicePreferences VoiceDraft { get; private set; } = new();
    internal AutomaticMessageAdvanceMode AutomaticMessageAdvanceDraft { get; private set; }
    private bool automaticMessageAdvanceSupported;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        MapCameraNavigationOptions initial, AudioVolumeOptions audio, ListScrollSpeed listScroll, bool automaticSaving,
        Action returned, Action accepted, Action reset,
        Action<AudioVolumeOptions> previewAudio, Action? previewEffects = null, VoicePreferences? voice = null,
        AutomaticMessageAdvanceMode automaticMessageAdvance = AutomaticMessageAdvanceMode.Manual,
        IReadOnlyList<int>? keyboardBindings = null, ApplicationPresentationOptions? presentation = null,
        DisplayPreferences? display = null, IReadOnlyList<DisplayResolution>? resolutions = null,
        Action<DisplayPreferences>? previewDisplay = null, CgQuality? cgQuality = null,
        IReadOnlyList<CgQuality>? supportedCgQualities = null, Func<CgQuality,bool>? previewCgQuality = null,
        GameAudioSpeakerLayout? speakerLayout = null, bool speakerLayoutEditable = false,
        Func<GameAudioSpeakerLayout,SpeakerLayoutAvailability>? speakerLayoutAvailability = null,
        bool mapBackdrop = false)
    {
        this.returned = returned; this.accepted = accepted; this.reset = reset;
        initial.Validate(); Draft = initial;
        audio.Validate(); AudioDraft = audio; this.previewAudio = previewAudio;
        this.previewEffects = previewEffects;
        PresentationDraft = presentation ?? new();
        DisplayDraft = display; displayResolutions = resolutions ?? []; this.previewDisplay = previewDisplay;
        CgQualityDraft = cgQuality; this.supportedCgQualities = supportedCgQualities ?? []; this.previewCgQuality = previewCgQuality;
        SpeakerLayoutDraft = speakerLayout; this.speakerLayoutEditable = speakerLayoutEditable;
        this.speakerLayoutAvailability = speakerLayoutAvailability;
        if (!Enum.IsDefined(listScroll)) throw new ArgumentOutOfRangeException(nameof(listScroll));
        ListScrollDraft = listScroll;
        AutomaticSavingDraft = automaticSaving;
        VoiceDraft = voice ?? new();
        _ = VoiceDraft.Capture();
        if (!Enum.IsDefined(automaticMessageAdvance)) throw new ArgumentOutOfRangeException(nameof(automaticMessageAdvance));
        AutomaticMessageAdvanceDraft = automaticMessageAdvance;
        automaticMessageAdvanceSupported = AutomaticMessageAdvanceSource.SupportsLocale(text.SourceLocale);
        Name = "EnvironmentSettings"; ZIndex = 4; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (mapBackdrop)
        {
            var backdrop = new UiSourceCommonBlur(); AddChild(backdrop); backdrop.Configure(repository);
        }
        chrome = new AdministrationPageChrome { Name = "EnvironmentChrome" }; AddChild(chrome);
        chrome.Configure(repository, text, 0x0031, RequestBack,
            () => { if (!abandoned && !busy && !KeyboardModal) this.accepted?.Invoke(); }, showFooterHint: false);
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].Name = "EnvironmentPage" + i; pages[i].Size = new(1920,1080);
            pages[i].MouseFilter = MouseFilterEnum.Ignore; chrome.Canvas.AddChild(pages[i]);
        }
        var source = new UiSourceLayout(repository, 0x6C019E60);
        var tabSource = new UiSourceLayout(repository, 0x99165634);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages;
        var narration = new AdministrationMessages(repository, text.SourceLocale);
        string Data(int table, int id) => messages.Sections[table].Records[id].Text ??
            throw new InvalidDataException($"Missing environment caption {table}/{id:X4}.");
        Vector2 tabMount = source.Rectangle(1).Position;
        tabSource.Art(chrome.Canvas, 6, tabSource.Rectangle(6, tabMount), chrome.Assets, chrome.SpriteShader);
        int[] tabNodes = [2, 3, 5], tabCaptions = [0x04DA, 0x04D9, 0x04DB];
        uint[] tabFiles = [0x172EDC24, 0x9AFB27B7, 0xD45873AA];
        for (int i = 0; i < tabNodes.Length; i++)
        {
            int page = i;
            var box = tabSource.Rectangle(tabNodes[i], tabMount);
            var tab = new UiSourceTabButton { Name = "EnvironmentTab" + i, Position = box.Position,
                Size = box.Size, Selected = i == 0 };
            chrome.Canvas.AddChild(tab);
            tab.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Data(1, tabCaptions[i]), tabFiles[i]);
            tab.Pressed += () => { if (!abandoned && !busy && !KeyboardModal && page < pages.Length) ShowPage(page); };
            tabs.Add(tab);
        }
        var hint = new UiSourceLayout(repository, 0xD6361A53);
        Vector2 hintMount = source.Rectangle(2).Position;
        hint.Art(chrome.Canvas, 1, hint.Rectangle(1, hintMount), chrome.Assets, chrome.SpriteShader);
        defaultHelp = narration.Format(0x2740);
        var helpBox = hint.Rectangle(2, hintMount);
        help = new Control { Name = "EnvironmentHelp", Position = helpBox.Position,
            Size = helpBox.Size, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(help);
        helpProfile = MapFontProfile.FromNode(hint.Nodes[2], hint.Source.Source.ResourceUri);
        if (helpProfile.RawFlags != 4 || helpProfile.RawAlign != 1 || helpProfile.RawLineArrange != 1 || helpProfile.WidthFitMode != 0)
            throw new InvalidDataException("Environment help requires its fixed-height centred source paragraph.");
        ApplyHelp(defaultHelp);
        var rowSource = new UiSourceLayout(repository, 0x8D9566B0);
        int[] radioNodes = [4, 7, 10, 13];
        int[] captions = [0x04C9, 0x04CA, 0x06EF, 0x04CC, 0x04CD, 0x04D2, 0x04D3, 0x04D4, 0x06C6];
        int[] helpMessages = [0x2741,0x2742,0x2759,0x2744,0x2745,0x274A,0x274B,0x2754,0x274C];
        int[][] values = [[0x58,0x59,0x5A,0x5B],[0x5C,0x5D,0x5E],[0x5C,0x5D,0x5E],
            [0x62,0x63,0x64,0x65],[0x66,0x67],[0x6B,0x6A],[0x6D,0x6C],[0x80,0x81],[0x83,0x82]];
        for (int row = 0; row < captions.Length; row++)
        {
            Vector2 mount = source.Rectangle(row + 4).Position;
            rowSource.Art(pages[0], 2, rowSource.Rectangle(2, mount), chrome.Assets, chrome.SpriteShader);
            rowSource.Text(pages[0], 3, mount, Data(1, captions[row]), chrome.Font);
            for (int index = 0; index < values[row].Length; index++)
            {
                int selectedRow = row, selectedIndex = index;
                var box = rowSource.Rectangle(radioNodes[index], mount);
                var button = new UiSourceRadioButton { Name = "EnvironmentOption" + row + "_" + index,
                    Position = box.Position, Size = box.Size, Disabled = !EnabledRow(row) };
                pages[0].AddChild(button);
                var segment = index == 0 ? UiSourceRadioSegment.Left : index == values[row].Length - 1 ? UiSourceRadioSegment.Right : UiSourceRadioSegment.Centre;
                button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, segment, Data(0, values[row][index]), () =>
                {
                    if (abandoned || busy || !EnabledRow(selectedRow)) return;
                    if (selectedRow is 4 or 5) ApplyPresentationOptions(selectedRow switch
                    {
                        4 => PresentationDraft with { TacticDirectionsEnabled = selectedIndex == 1 },
                        _ => PresentationDraft with { MoviesEnabled = selectedIndex == 1 }
                    });
                    else if (selectedRow == 8) ApplyAutomaticSaving(selectedIndex == 1);
                    else if (selectedRow == 7) ApplyVoice(VoiceDraft with { Language = (NarrationVoiceLanguage)selectedIndex });
                    else if (selectedRow == 3) ApplyAutomaticMessageAdvance((AutomaticMessageAdvanceMode)selectedIndex);
                    else if (selectedRow == 2) ApplyListScroll((ListScrollSpeed)selectedIndex);
                    else ApplyOptions(selectedRow == 0 ? Draft with { Edge = (MapEdgeScrollSpeed)selectedIndex } :
                        Draft with { Keyboard = (MapKeyboardScrollSpeed)selectedIndex });
                    ApplyHelp(narration.Format(helpMessages[selectedRow]));
                });
                if (EnabledRow(row))
                {
                    string rowHelp = narration.Format(helpMessages[row]);
                    button.MouseEntered += () => { if (!abandoned && !busy) ApplyHelp(rowHelp); };
                    button.MouseExited += () => { if (!abandoned && !busy && !KeyboardModal) ApplyHelp(CurrentPageHelp); };
                }
                choices.Add((button, row, index));
            }
        }
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        defaults = new UiSourceBottomButton { Name = "EnvironmentDefaults", Position = footer.Rectangle(14, new(0,904)).Position };
        chrome.Canvas.AddChild(defaults);
        defaults.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Data(1,0x006B),
            () => { if (!abandoned && !busy && !KeyboardModal) this.reset?.Invoke(); });
        defaults.MouseEntered += () => { if (!abandoned && !busy) ApplyHelp(narration.Format(0x275F)); };
        defaults.MouseExited += () => { if (!abandoned && !busy && !KeyboardModal) ApplyHelp(CurrentPageHelp); };
        ConfigureAudioPage(repository, text);
        ConfigureKeyboardPage(repository, text, keyboardBindings);
        ApplyOptions(initial); SetBusy(false);
        ApplyListScroll(listScroll);
        ApplyAutomaticSaving(automaticSaving);
        ApplyAudioOptions(audio, false); ApplyPresentationOptions(PresentationDraft);
        ApplyDisplayOptions(DisplayDraft, false); ApplyCgQualityOptions(CgQualityDraft, false); ApplySpeakerLayoutOptions(SpeakerLayoutDraft); ShowPage(0);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    internal void RequestBack()
    {
        if (CloseDisplayResolutions() || CancelKeyboardModal()) return;
        if (!abandoned && !busy) returned?.Invoke();
    }
    private bool EnabledRow(int row) => row <= 2 || row is 4 or 5 or 7 or 8 || row == 3 && automaticMessageAdvanceSupported;
    internal void ApplyOptions(MapCameraNavigationOptions options)
    {
        if (abandoned) return;
        options.Validate(); Draft = options;
        foreach (var row in choices)
            row.Button.Selected = row.Row switch
            {
                0 => row.Index == (int)Draft.Edge,
                1 => row.Index == (int)Draft.Keyboard,
                2 => row.Index == (int)ListScrollDraft,
                3 => automaticMessageAdvanceSupported && row.Index == (int)AutomaticMessageAdvanceDraft,
                4 => row.Index == (PresentationDraft.TacticDirectionsEnabled ? 1 : 0),
                5 => row.Index == (PresentationDraft.MoviesEnabled ? 1 : 0),
                6 => row.Index == (PresentationDraft.DuelsEnabled ? 1 : 0),
                7 => row.Index == (int)VoiceDraft.Language,
                8 => row.Index == (AutomaticSavingDraft ? 1 : 0),
                _ => false
            };
    }
    internal void ApplyAutomaticSaving(bool value)
    {
        if (abandoned) return;
        AutomaticSavingDraft = value;
        foreach (var row in choices.Where(row => row.Row == 8)) row.Button.Selected = row.Index == (value ? 1 : 0);
    }
    internal void ApplyVoice(VoicePreferences value)
    {
        if (abandoned) return;
        _ = value.Capture(); VoiceDraft = value;
        foreach (var row in choices.Where(row => row.Row == 7)) row.Button.Selected = row.Index == (int)value.Language;
    }
    internal void ApplyAutomaticMessageAdvance(AutomaticMessageAdvanceMode value)
    {
        if (abandoned) return;
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        AutomaticMessageAdvanceDraft = value;
        foreach (var row in choices.Where(row => row.Row == 3))
            row.Button.Selected = automaticMessageAdvanceSupported && row.Index == (int)value;
    }
    internal void ApplyListScroll(ListScrollSpeed value)
    {
        if (abandoned) return;
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        ListScrollDraft = value;
        foreach (var row in choices.Where(row => row.Row == 2)) row.Button.Selected = row.Index == (int)value;
    }
    private void ApplyHelp(string text)
    {
        if (helpText == text) return;
        string[] lines = text.Split('\n');
        var lineProfile = helpProfile with { RawAlign = 0, VerticalSpace = 0 };
        chrome.Font.Prepare(lines, lineProfile);
        if (lines.Any(line => chrome.Font.Measure(line, lineProfile).Width > help.Size.X))
            throw new InvalidDataException("Environment help requires its source wrapping branch.");
        // AC20D0/AD6140: truncate the scaled nominal height plus spacing
        // together. AD5F20 centres all explicit LF rows, removing only the
        // final spacing; B07260 subtracts spacing from the first baseline.
        int advance = (int)(chrome.Font.Source.Record('\u3042', helpProfile.Face).Height *
            helpProfile.ScaleY + helpProfile.VerticalSpace);
        int top = (int)((help.Size.Y - (lines.Length * advance - helpProfile.VerticalSpace)) * .5f);
        foreach (var child in help.GetChildren()) { help.RemoveChild(child); child.QueueFree(); }
        for (int index = 0; index < lines.Length; index++)
        {
            var line = new UiSourceText { Name = "HelpLine" + index, Position = new(0, top + index * advance),
                Size = new(help.Size.X, advance - helpProfile.VerticalSpace) };
            help.AddChild(line); line.Configure(chrome.Font, lineProfile); line.Text = lines[index];
        }
        helpText = text;
    }

    internal void ApplyNotice(string message) { if (!abandoned) ApplyHelp(message); }
    internal void SetBusy(bool value)
    {
        if (abandoned) return;
        busy = value; chrome.ApplyReturn(!value); chrome.ApplyDecision(!value && !KeyboardModal, true); defaults.Disabled = value || KeyboardModal;
        foreach (var row in choices) row.Button.Disabled = value || !EnabledRow(row.Row);
        for (int i = 0; i < tabs.Count; i++) tabs[i].Disabled = value || KeyboardModal;
        foreach (var row in audioChoices) row.Button.Disabled = value;
        RefreshKeyboardAdmission(); RefreshDisplayAdmission(); RefreshCgQualityAdmission(); RefreshSpeakerLayoutAdmission();
    }
    internal void Abandon()
    {
        abandoned = true; returned = null; accepted = null; reset = null;
        previewAudio = null;
        previewEffects = null; previewDisplay = null; previewCgQuality = null; CloseDisplayResolutions();
        AbandonKeyboard();
        foreach (var row in choices) row.Button.Abandon();
        foreach (var row in audioChoices) row.Button.Abandon();
        foreach (var row in displayButtons) row.Button.Abandon();
        foreach (var row in cgQualityButtons) row.Button.Abandon();
        foreach (var row in speakerLayoutButtons) row.Button.Abandon();
        speakerLayoutAvailability = null;
        if (displayResolutionButton is not null) displayResolutionButton.Disabled = true;
        foreach (var tab in tabs) tab.Disabled = true;
        if (defaults is not null) defaults.Disabled = true;
        chrome?.Abandon(); Hide();
    }
}
