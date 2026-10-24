using Godot;
using San14.Data;

/// <summary>A music selection is returned only by Apply; Return preserves the parent draft.</summary>
internal partial class MusicSelectionView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly List<(int Index, MusicCandidateRowView Button)> buttons = [];
    private Control list = null!;
    private UiSourceScrollbar scroll = null!;
    private Func<bool>? admitted;
    private Action<int?>? completed;
    private Action<int?>? preview;
    private int? current, previewIndex;
    private bool abandoned, selectionChanged;
    private Vector2 firstRow, secondRow;
    private const int RowHeight = 62;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<MusicSelectionRow> candidates, int initial,
        Func<bool> admitted, Action<int?> completed, Action<int?>? preview = null,
        Func<int>? currentScrollSpeed = null)
    {
        this.admitted = admitted; this.completed = completed; this.preview = preview;
        current = candidates.Any(row => row.Index == initial) ? initial : null;
        Name = "MusicSelection"; ZIndex = 6; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x02DF, RequestBack, () => Finish(current));
        var source = new UiSourceLayout(MusicEditLayoutSource.LoadSelection(repository));
        source.Blur(chrome.Canvas, 2, source.Rectangle(2));
        source.Art(chrome.Canvas, 3, source.Rectangle(3), chrome.Assets, chrome.SpriteShader);
        var rectangle = source.Rectangle(12);
        firstRow = source.Rectangle(13).Position - rectangle.Position;
        secondRow = source.Rectangle(14).Position - rectangle.Position;
        list = new Control { Name = "MusicCandidates", Position = rectangle.Position, Size = rectangle.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(list);
        list.GuiInput += input => { if (CanEdit()) scroll?.HandleWheel(input); };
        foreach (var row in candidates)
        {
            int index = row.Index;
            var button = new MusicCandidateRowView { Name = "MusicCandidate" + index, Size = source.Rectangle(13).Size };
            list.AddChild(button); buttons.Add((index, button));
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, row.Name, row.Number, CanEdit,
                () => { if (CanEdit()) { current = index; selectionChanged = true; ApplySelection(); } },
                () => TogglePreview(index));
            button.ScrollInput += input => { if (CanEdit()) scroll?.HandleWheel(input); };
        }
        var scrollRectangle = source.Rectangle(4);
        scroll = new UiSourceScrollbar { Name = "MusicCandidateScrollbar", Position = scrollRectangle.Position, Size = scrollRectangle.Size };
        chrome.Canvas.AddChild(scroll);
        int height = (int)(firstRow.Y + Math.Max(0, (candidates.Count + 1) / 2 - 1) * RowHeight + source.Rectangle(13).Size.Y);
        scroll.Configure(repository, chrome.Assets, chrome.SpriteShader, Math.Max(0, height - (int)list.Size.Y),
            0, _ => PositionRows(), currentScrollSpeed: currentScrollSpeed);
        chrome.ApplyDefaultHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x287D));
        PositionRows(); ApplySelection();
    }
    private bool CanEdit() => !abandoned && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    private void ApplySelection()
    {
        foreach (var row in buttons) row.Button.Apply(row.Index == current, row.Index == previewIndex);
        chrome.ApplyDecision(selectionChanged && current.HasValue, true);
    }
    private void PositionRows()
    {
        for (int index = 0; index < buttons.Count; index++)
        {
            var button = buttons[index].Button;
            button.Position = (index % 2 == 0 ? firstRow : secondRow) + new Vector2(0, index / 2 * RowHeight - scroll.Value);
            button.Visible = button.Position.Y > -RowHeight && button.Position.Y < list.Size.Y;
        }
    }
    private void Finish(int? value)
    {
        if (!CanEdit() || value is null || !selectionChanged) return;
        var callback = completed; Abandon(); callback?.Invoke(value);
    }
    internal void RequestBack()
    {
        if (!CanEdit()) return;
        var callback = completed; Abandon(); callback?.Invoke(null);
    }
    internal void ApplyNotice(string text) { if (!abandoned) chrome.ApplyDefaultHint(text); }
    private void TogglePreview(int index)
    {
        if (!CanEdit()) return;
        previewIndex = previewIndex == index ? null : index;
        ApplySelection(); preview?.Invoke(previewIndex);
    }
    internal void Abandon()
    {
        abandoned = true; admitted = null; completed = null; preview = null;
        foreach (var row in buttons) row.Button.Abandon();
        if (scroll is not null) scroll.SetProcessInput(false);
        chrome?.Abandon(); Hide();
    }
}
