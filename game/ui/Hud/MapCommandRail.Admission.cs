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
            if (_entryFacts.TryGetValue(entry.Command, out var previous) && previous == entry) continue;
            _entryFacts[entry.Command] = entry;
            _entryHintTexts[entry.Command] = entry.GameMessageId is int message
                ? _entryMessages.Format(message) : _entryUnknownText;
            if (_entryButtons.TryGetValue(entry.Command, out var live))
                ApplyEntryPresentation(live.Button, entry.Command, live.Bound);
            if (_hoveredEntry == entry.Command) ShowEntryHint(entry.Command);
        }
    }

    private void BindEntryPresentation(MapCommandRailButton button, MapMenuCommand command, bool bound)
    {
        _entryButtons.Add(command, (button, bound));
        ApplyEntryPresentation(button, command, bound);
        if (command != MapMenuCommand.Proposals) return;
        long generation = _menuGeneration;
        button.HoverChanged += hovered =>
        {
            if (_retired || generation != _menuGeneration || OpenCategory is null) return;
            if (hovered) { _hoveredEntry = command; ShowEntryHint(command); }
            else if (_hoveredEntry == command) HideEntryHint();
        };
    }

    private void ApplyEntryPresentation(MapCommandRailButton button, MapMenuCommand command, bool bound)
    {
        if (command != MapMenuCommand.Proposals) { button.Disabled = !bound; return; }
        _entryFacts.TryGetValue(command, out var entry);
        // Unknown prevents a claim of usable input, but stays nullable in the
        // actual publication and metadata; it is not a fabricated source No.
        button.Disabled = !bound || entry?.Enabled != true;
        button.SetMeta("source_entry_enabled", entry?.Enabled?.ToString() ?? "Unresolved");
        button.SetMeta("source_entry_help", entry?.GameMessageId ?? -1);
        button.SetMeta("source_entry_issue", entry?.UnresolvedInput ??
            (entry is null ? "map.main_command.proposal_not_prepared" : ""));
    }

    private void ShowEntryHint(MapMenuCommand command)
    {
        if (_entryHint is null || AutomaticallyHidden || OpenCategory is null || !IsVisibleInTree()) return;
        _entryHint.DisplayText(_entryHintTexts.GetValueOrDefault(command, _entryUnknownText));
    }

    private void HideEntryHint() { _hoveredEntry = null; _entryHint?.DisplayText(null); }
    private void ClearEntryHints() { HideEntryHint(); _entryButtons.Clear(); }

    public override void _Process(double delta)
    {
        if (_entryHint is { Visible: true } && _entryHintCanvas is not null)
            _entryHint.UpdatePointer(_entryHintCanvas.GetLocalMousePosition());
    }
}
