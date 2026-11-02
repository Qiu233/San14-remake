using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum MapSystemCommand { Save, Load, Exit, Environment, ReturnToTitle, Manual, ScenarioSettings, Edit }

/// <summary>The map Config menu. Only supported commands receive a callback;
/// the owner retains the parent while an ordinary save window is open.</summary>
internal partial class MapSystemMenuView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly List<(UiSourceCommandButton Button, bool Supported)> buttons = [];
    private Action<MapSystemCommand>? selected;
    private Action? returned;
    private bool abandoned, busy, contentSuspended;
    private MapSystemMenuPreparedResources? preparedResources;
    private UiSourceCommonBlur? backdrop;
    private UiSourceModalTransition? transition;
    private Action? releasePreparedResources;
    internal bool IsPrepared => chrome is not null;
    internal bool IsTransitioning => transition?.IsPlaying == true;
    internal void BeginPreparation()
    {
        Name = "MapSystemMenuPreparation"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); busy = true;
    }
    internal Task<bool> PlayTransition(bool opening) => transition?.Play(opening) ?? Task.FromResult(true);

    internal bool ContentSuspended => contentSuspended;

    internal void Configure(GameResourceRepository repository, PreviewText text, int titleMessage,
        Action returned, Action<MapSystemCommand> selected, bool scenarioSettingsSupported = false, bool currentWorldEditSupported = false, MapSystemMenuPreparedResources? prepared = null, Action? releasePrepared = null,
        bool saveSupported = true, bool loadSupported = true)
    {
        preparedResources = prepared; // Ownership transfers before any Godot construction.
        releasePreparedResources = releasePrepared;
        this.returned = returned; this.selected = selected;
        Name = "MapSystemMenu"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        backdrop = new UiSourceCommonBlur(); AddChild(backdrop); backdrop.Configure(repository, prepared?.CommonBlurSource);
        chrome = new AdministrationPageChrome(); AddChild(chrome);
        chrome.Configure(repository, text, titleMessage, () => { if (!abandoned && !busy && !contentSuspended) this.returned?.Invoke(); }, () => { }, returnOnly: true, resources: prepared?.CreateChromeResources(), modalMotion: prepared is not null);
        chrome.ApplyDecision(false, false);
        var source = prepared?.MenuSource ?? chrome.Assets.SourceLayout(0x33EF187B);
        var body = new Control { Name = "ConfigBodyMotion", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore }; chrome.Canvas.AddChild(body);
        Vector2 size = source.Rectangle(0).Size;
        // Config's initializer recentres the loaded control around (960,514).
        Vector2 mount = new((1920 - (int)size.X) / 2, 514 - (int)size.Y / 2);
        source.Art(body, 2, source.Rectangle(2, mount), chrome.Assets, chrome.SpriteShader,
            blend: UiSourceSprite.Blend.AlphaMask);
        var firstBlur = source.Blur(body, 3, source.Rectangle(3, mount));
        Rect2 secondBlur = source.Rectangle(4, mount);
        source.Blur(firstBlur, 4, new(secondBlur.Position - firstBlur.Position, secondBlur.Size));
        source.Art(body, 5, source.Rectangle(5, mount), chrome.Assets, chrome.SpriteShader);
        var messages = (prepared?.Messages ?? repository.LoadMessages(text.SourceLocale, MessageKind.Data)).Messages.Sections[1];
        foreach (var binding in new[] { (6, 0x0037), (10, 0x002E), (14, 0x0031), (34, 0x007A),
            (35, 0x0075), (18, 0x0064), (19, 0x0038), (23, 0x00D1), (27, 0x0063), (31, 0x0039), (32, 0x0033) })
        {
            int node = binding.Item1;
            MapSystemCommand? command = node switch
            { 6 => MapSystemCommand.Save, 10 => MapSystemCommand.Load, 14 => MapSystemCommand.Environment,
                18 when scenarioSettingsSupported => MapSystemCommand.ScenarioSettings,
                34 when currentWorldEditSupported => MapSystemCommand.Edit,
                27 when UiSourceOnlineManual.ForLocale(text.SourceLocale) is not null => MapSystemCommand.Manual,
                31 => MapSystemCommand.ReturnToTitle, 32 => MapSystemCommand.Exit, _ => null };
            bool supported = command is not null && (command != MapSystemCommand.Save || saveSupported) &&
                (command != MapSystemCommand.Load || loadSupported);
            var box = source.Rectangle(node, mount);
            var button = new UiSourceCommandButton { Name = command is { } namedCommand ? "System" + namedCommand : "SystemCommand" + node,
                Position = box.Position, Size = box.Size, Disabled = !supported };
            body.AddChild(button); buttons.Add((button, supported));
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                messages.Records[binding.Item2].Text ?? throw new InvalidDataException("Missing map system command caption."),
                () => { if (!abandoned && !busy && !contentSuspended && supported && !button.Disabled && command is { } selectedCommand) this.selected?.Invoke(selectedCommand); });
            button.SetMeta("source_node_index", node);
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        if (prepared is not null)
        {
            transition = new UiSourceModalTransition(); AddChild(transition);
            transition.Configure(prepared.TransitionSource, backdrop, body, chrome.HeaderMotion, chrome.FooterMotion);
        }
        SetBusy(busy);
    }

    internal void SetBusy(bool value)
    {
        if (abandoned) return;
        busy = value; if (chrome is null) return; chrome.ApplyReturn(!value && !contentSuspended);
        foreach (var row in buttons) row.Button.Disabled = value || contentSuspended || !row.Supported;
    }

    // Save/Load borrows this state's background while its menu is hidden.
    // Other child states with their own background still hide the whole view.
    internal void SuspendContent()
    {
        if (abandoned) return;
        contentSuspended = true; SetBusy(busy);
        chrome.Hide(); Show();
    }

    internal void ResumeContent()
    {
        if (abandoned) return;
        contentSuspended = false; SetBusy(busy);
        chrome.Show(); Show();
    }

    public override void _ExitTree()
    {
        var resources = preparedResources; preparedResources = null;
        var release = releasePreparedResources; releasePreparedResources = null;
        // Children release their borrowed font and canvas RIDs first.
        if (resources is not null)
        {
            // Exit is child-first in the real scene tree; all borrowers have
            // retired before this page's own _ExitTree notification.
            if (release is not null) release(); else resources.Dispose();
        }
    }

    internal void Abandon()
    {
        abandoned = true; returned = null; selected = null;
        foreach (var row in buttons) row.Button.Abandon();
        chrome?.Abandon(); Hide();
    }
}
