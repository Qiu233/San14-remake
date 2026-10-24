using Godot;
using San14.Data;

/// <summary>Selectable current-period text over the original all-records row
/// and scrollbar. Only the visible row pool is mounted; no history is written.</summary>
internal partial class PeriodReportEntriesView : Control
{
    private readonly List<(ActionReportAllRow Row, Button Button, ColorRect Highlight)> rows = [];
    private UiSourceScrollbar scrollbar = null!;
    private IReadOnlyList<string> entries = [];
    private Action<int>? selected;
    private bool retired;
    internal int SelectedIndex { get; private set; } = -1;
    internal int ScrollOffset => scrollbar.Value;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        IReadOnlyList<string> entries, Rect2 scrollRectangle, int scrollSpeed, Func<int> currentScrollSpeed, Action<int> selected)
    {
        this.entries = entries; this.selected = selected;
        MouseFilter = MouseFilterEnum.Pass;
        var clip = new Control { Name = "ReportEntryClip", Size = Size, ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        AddChild(clip);
        for (int index = 0; index < (int)Size.Y / 62 + 2; index++)
        {
            int slot = index;
            var row = new ActionReportAllRow { Name = "PeriodEntry" + index };
            clip.AddChild(row); row.Configure(repository, assets, shader, font);
            var highlight = new ColorRect { Color = new(0.95f, 0.8f, 0.4f, 0.15f), Size = row.Size,
                MouseFilter = MouseFilterEnum.Ignore, Visible = false };
            row.AddChild(highlight);
            var button = new Button { Name = "SelectEntry", Flat = true, Size = row.Size,
                MouseDefaultCursorShape = CursorShape.PointingHand };
            foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
                button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            row.AddChild(button);
            button.Pressed += () => Select(scrollbar.Value / 62 + slot);
            button.GuiInput += input => scrollbar.HandleWheel(input);
            rows.Add((row, button, highlight));
        }
        scrollbar = new UiSourceScrollbar { Name = "PeriodEntriesScrollbar", Position = scrollRectangle.Position, Size = scrollRectangle.Size };
        AddChild(scrollbar);
        scrollbar.Configure(repository, assets, shader, Math.Max(checked(entries.Count * 62) - (int)Size.Y, 0),
            scrollSpeed, _ => PositionRows(), currentScrollSpeed: currentScrollSpeed);
        GuiInput += scrollbar.HandleWheel; clip.GuiInput += scrollbar.HandleWheel;
        PositionRows();
        if (entries.Count != 0) Select(0);
    }

    private void PositionRows()
    {
        int first = scrollbar.Value / 62;
        for (int slot = 0; slot < rows.Count; slot++)
        {
            var (row, _, highlight) = rows[slot]; int index = first + slot;
            row.Position = new(0, index * 62 - scrollbar.Value);
            row.Visible = index < entries.Count;
            if (!row.Visible) continue;
            if (row.SourceText != entries[index]) row.ApplyPlainText(entries[index]);
            highlight.Visible = index == SelectedIndex;
        }
    }

    internal void Select(int index)
    {
        if (retired || (uint)index >= (uint)entries.Count || !IsVisibleInTree()) return;
        SelectedIndex = index;
        PositionRows(); selected?.Invoke(index);
    }

    internal void Abandon() { retired = true; selected = null; }
    public override void _ExitTree() => selected = null;
}
