using Godot;
using San14.Data;

internal partial class MapCommandRail
{
    private readonly Dictionary<MapMenuCommand, MapMainCommandEntryPresentation> _entryFacts = [];
    private readonly Dictionary<MapMenuCommand, string> _entryHintTexts = [];
    private readonly Dictionary<MapMenuCommand, (MapCommandRailButton Button, bool Bound)> _entryButtons = [];
    private AdministrationMessages _entryMessages = null!;
    private Control? _entryHintCanvas;
    private UiSourceSimpleTooltip? _entryHint;
    private MapMenuCommand? _hoveredEntry;
    private string _entryUnknownText = "";

    private void ConfigureEntryHints(GameResourceRepository repository, string unknownText)
    {
        _entryMessages = new(repository, _locale); _entryUnknownText = unknownText;
        ConfigureKnownHints(repository);
        // 887DE0 constructs exactly CSimpleBalloonLayout (cmn_tooltip),
        // background alpha B4 and the normal pointer-following controller.
        _entryHintCanvas = new Control { Name = "MainCommandHintCanvas", Size = new(1920, 1080),
            MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_entryHintCanvas);
        _entryHint = new UiSourceSimpleTooltip(); _entryHintCanvas.AddChild(_entryHint);
        _entryHint.Configure(repository, _assets!, _shader!, _font!);
    }

    private void PublishEntryPresentation()
    {
        foreach (var entry in _presentation?.Entries ?? [])
        {
            _entryFacts.TryGetValue(entry.Command, out var previous);
            var published = entry.RetainGameMessage ? entry with { GameMessageId = previous?.GameMessageId } : entry;
            if (previous == published) continue;
            _entryFacts[entry.Command] = published;
            _entryHintTexts[entry.Command] = published.GameMessageId is int message
                ? _fixedHintTexts.GetValueOrDefault(message) ?? _entryMessages.Format(message) : _entryUnknownText;
            if (_entryButtons.TryGetValue(entry.Command, out var live))
                ApplyEntryPresentation(live.Button, entry.Command, live.Bound);
            if (_hoveredEntry == entry.Command) ShowEntryHint(entry.Command);
        }
    }

    private void BindEntryPresentation(MapCommandRailButton button, MapMenuCommand command, bool bound)
    {
        _entryButtons.Add(command, (button, bound));
        _entryHintCommands.Add(button,command);
        ApplyEntryPresentation(button, command, bound);
    }

    private void ApplyEntryPresentation(MapCommandRailButton button, MapMenuCommand command, bool bound)
    {
        if (!MapMainCommandEntryPresentation.RequiresPublishedAdmission(command))
        { button.Disabled = !bound; return; }
        _entryFacts.TryGetValue(command, out var entry);
        // Unknown prevents a claim of usable input, but stays nullable in the
        // actual publication and metadata; it is not a fabricated source No.
        button.Disabled = !bound || entry?.Enabled != true;
        button.SetMeta("source_entry_enabled", entry?.Enabled?.ToString() ?? "Unresolved");
        button.SetMeta("source_entry_help", entry?.GameMessageId ?? -1);
        button.SetMeta("source_entry_issue", entry?.UnresolvedInput ??
            (entry is null ? "map.main_command.entry_not_prepared" : ""));
    }

    private void ShowEntryHint(MapMenuCommand command)
    {
        if (_entryHint is null || AutomaticallyHidden || OpenCategory is null || !IsVisibleInTree()) return;
        var text = _entryHintTexts.GetValueOrDefault(command, _entryUnknownText);
        if (text.Contains('^')) _entryHint.DisplayKnownColourText(text);
        else _entryHint.DisplayText(text);
    }

    private void HideEntryHint() { _hoveredEntry = null; _entryHint?.DisplayText(null); }
    private void ClearEntryHints() { HideEntryHint(); _entryButtons.Clear(); _entryHintCommands.Clear(); }

    public override void _Process(double delta)
    {
        ProcessCommandHints();
    }
}
