using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum TitleMenuCommand { Load, NewGame, Tutorial, ConquestChronicle, Edit, Settings, Collection, Exit }

// Static playable composition. The original still/logo and common buttons are
// reused; the intro, shell animation endpoints and final paint order remain
// pending visual comparison. Metadata container capacities are not draw flags.
/// <summary>The entered title-menu phase. Its application owner supplies real
/// availability and admission and handles each request, including any questions
/// before a state change. Intro rendering and those state changes stay outside.</summary>
internal partial class TitleMenuView : Control
{
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Control canvas = null!;
    private readonly List<UiSourceTitleMenuButton> buttons = [];
    private readonly List<UiSourceRadioButton> languages = [];
    private Func<bool>? inputAdmitted;
    private Action<TitleMenuCommand>? selected;
    private bool abandoned;
    private bool ownsResources;

    internal void Configure(GameResourceRepository repository, PreviewText text, IReadOnlyList<bool> enabled,
        Func<bool> inputAdmitted, Action<TitleMenuCommand> selected, Action<int> languageChanged,
        AdministrationPageChrome.Resources? resources = null)
    {
        ArgumentNullException.ThrowIfNull(enabled);
        ArgumentNullException.ThrowIfNull(inputAdmitted);
        ArgumentNullException.ThrowIfNull(selected);
        if (enabled.Count != 8) throw new ArgumentException("Title menu availability must cover all eight entries.", nameof(enabled));
        this.inputAdmitted = inputAdmitted;
        this.selected = selected;
        Name="TitleMenu";ZIndex=2;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ownsResources = resources is null;
        assets = resources?.Assets ?? new(repository);
        font = resources?.Font ?? new(repository, text.SourceLocale);
        shader = resources?.SpriteShader ?? UiSourceSprite.CreateShader();
        canvas = new Control { Name = "TitleMenuCanvas", Size = new(1920,1080), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(canvas);
        var source = assets.SourceLayout(0xE7A28866);
        var still = assets.SourceLayout(0x583BEE54);
        still.Art(canvas, 1, still.Rectangle(1, source.Rectangle(36).Position), assets, shader);
        var logo = assets.SourceLayout(0x5B2BB9DD);
        logo.Art(canvas, 1, logo.Rectangle(1, source.Rectangle(34).Position), assets, shader);

        int[] mounts = [13,20,22,24,26,28,30,32];
        int[] messages = [0x2E,0x2F,0x2D,0x7B,0x66,0x31,0x32,0x33];
        var captions = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        var shellSource = assets.SourceLayout(0xE37E0C60);
        var entries = new UiSourceTitleMenuButton[mounts.Length];
        for (int index = 0; index < mounts.Length; index++)
        {
            var command = (TitleMenuCommand)index;
            var shell = new Control { Name = "TitleMenuShell" + command, Position = source.Rectangle(mounts[index]).Position,
                Size = shellSource.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            canvas.AddChild(shell);
            shellSource.Art(shell, 1, shellSource.Rectangle(1), assets, shader);
            var box = shellSource.Rectangle(2);
            // The source factory passes the integer-extracted position and
            // dimensions to the common button; the outer pane is not its hit.
            var button = new UiSourceTitleMenuButton { Name = "TitleMenu" + command,
                Position = new((int)box.Position.X, (int)box.Position.Y), Size = new((int)box.Size.X, (int)box.Size.Y) };
            shell.AddChild(button);
            button.Configure(repository, assets, shader, font, captions.Records[messages[index]].Text
                ?? throw new InvalidDataException("Missing original title menu caption."),
                () => { if (!abandoned && this.inputAdmitted?.Invoke() == true) this.selected?.Invoke(command); });
            button.InputAdmission = () => !abandoned && IsInsideTree() && !IsQueuedForDeletion() && this.inputAdmitted?.Invoke() == true;
            button.SetMeta("source_menu_node", mounts[index]);
            entries[index] = button;
            foreach (int node in new[] { 4,5 }) shellSource.Art(shell, node, shellSource.Rectangle(node), assets, shader);
        }
        buttons.AddRange(entries);
        var language = new Control { Name="TitleLanguage", Position=new(1490,1010),
            Size=new(380,48), MouseFilter=MouseFilterEnum.Ignore };
        canvas.AddChild(language);
        string[] languageCaptions = [text.LocaleSimplified,text.LocaleTraditional];
        for (int index=0;index<languageCaptions.Length;index++)
        {
            int locale=index;
            var choice = new UiSourceRadioButton { Name="TitleLanguage"+index,
                Position=new(index*190,0), Size=new(190,48), Selected=index==(text.SourceLocale=="TC"?1:0),
                InputAdmission=()=>!abandoned&&IsInsideTree()&&!IsQueuedForDeletion()&&this.inputAdmitted?.Invoke()==true };
            language.AddChild(choice);
            choice.Configure(repository,assets,shader,font,index==0?UiSourceRadioSegment.Left:UiSourceRadioSegment.Right,
                languageCaptions[index],()=>
                {if(!abandoned&&this.inputAdmitted?.Invoke()==true)languageChanged(locale);});
            languages.Add(choice);
        }
        ApplyAvailability(enabled);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Resized += Fit;
        Fit();
    }

    internal void ApplyAvailability(IReadOnlyList<bool> enabled)
    {
        ArgumentNullException.ThrowIfNull(enabled);
        if (enabled.Count != buttons.Count) throw new ArgumentException("Title menu availability does not match its entries.", nameof(enabled));
        if (abandoned) return;
        for (int index = 0; index < buttons.Count; index++) buttons[index].Disabled = !enabled[index];
    }

    internal void Abandon()
    {
        abandoned = true;
        inputAdmitted = null;
        selected = null;
        foreach (var button in buttons) button.Disabled = true;
        foreach (var language in languages) language.Abandon();
        Hide();
    }

    private void Fit()
    {
        if (canvas is null) return;
        float factor = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * factor;
        canvas.Position = (Size - new Vector2(1920,1080) * factor) / 2;
    }

    public override void _ExitTree()
    {
        inputAdmitted = null;
        selected = null;
        if (ownsResources)
        {
            font?.Dispose();
            assets?.Dispose();
            shader?.Dispose();
        }
    }
}
