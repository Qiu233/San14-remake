using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The original confirmation column pool. Scroll and tab rebinding
/// borrow immutable page rows; no world query or business callback is replayed.</summary>
internal partial class NewGameConfirmationList : Control
{
    private sealed record RowView(Control Root, UiSourceText Caption, UiSourceText Value, Control Icon);
    private readonly List<RowView> pool = [];
    private IReadOnlyList<NewGameConfirmationRow> rows = [];
    private UiSourceScrollbar? scrollbar;
    private Control area = null!;
    private GameResourceRepository repository = null!;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private UiSourceLayout source = null!;
    private IReadOnlyList<MessageRecord> data = null!;
    private Func<bool>? admitted;
    private Func<int>? scrollSpeed;
    private Action<int>? scrolled;
    private string unknown = "";
    private int stride, offset;
    private bool abandoned;
    internal int Offset => offset;
    internal int MaxOffset => Math.Max(0, rows.Count * stride - (int)area.Size.Y);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        UiSourceLayout source, string locale, string unknown, Func<bool> admitted, Func<int> scrollSpeed,
        Action<int> scrolled)
    {
        this.repository = repository; this.assets = assets; this.shader = shader; this.source = source;
        this.admitted = admitted; this.scrollSpeed = scrollSpeed; this.scrolled = scrolled; this.unknown = unknown;
        data = repository.LoadMessages(locale, MessageKind.Data).Messages.Sections[1].Records;
        Name = "ConfirmationList"; Size = source.Rectangle(0).Size; MouseFilter = MouseFilterEnum.Ignore;
        Rect2 box = source.Rectangle(19);
        area = new Control { Name = "ConfirmationRows", Position = box.Position, Size = box.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass }; AddChild(area);
        stride = (int)(source.Rectangle(21).Position.Y - source.Rectangle(20).Position.Y);
        var rowSource = new UiSourceLayout(repository, 0x10D915FE);
        int capacity = (int)area.Size.Y / stride + 2;
        for (int index = 0; index < capacity; index++)
        {
            var root = new Control { Name = "ConfirmationRow" + index, Size = rowSource.Rectangle(0).Size,
                MouseFilter = MouseFilterEnum.Ignore }; area.AddChild(root);
            rowSource.Art(root, 1, rowSource.Rectangle(1), assets, shader);
            rowSource.Art(root, 3, rowSource.Rectangle(3), assets, shader);
            var caption = rowSource.Text(root, 2, default, "", font, MapTextPresets.Create()[13]);
            var value = rowSource.Text(root, 4, default, "", font, MapTextPresets.Create()[0]);
            var icon = rowSource.Art(root, 5, rowSource.Rectangle(5), assets, shader);
            pool.Add(new(root, caption, value, icon));
        }
        area.GuiInput += input => { if (ReadAdmission()) scrollbar?.HandleWheel(input); };
        SetMeta("source_layout", rowSource.Source.Source.ResourceUri);
    }
    internal void Bind(IReadOnlyList<NewGameConfirmationRow> records, int retainedOffset)
    {
        if (abandoned) return;
        rows = records; offset = Math.Clamp(retainedOffset, 0, MaxOffset);
        if (scrollbar is { } previous) { RemoveChild(previous); previous.QueueFree(); }
        Rect2 box = source.Rectangle(18);
        scrollbar = new UiSourceScrollbar { Name = "ConfirmationScrollbar", Position = box.Position, Size = box.Size };
        AddChild(scrollbar);
        scrollbar.Configure(repository, assets, shader, MaxOffset, scrollSpeed!(), changed =>
        {
            if (!ReadAdmission()) return;
            offset = changed; ApplyRows(); scrolled?.Invoke(changed);
        }, currentScrollSpeed: scrollSpeed);
        scrollbar.SetValue(offset); ApplyRows();
    }
    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    private void ApplyRows()
    {
        int first = offset / stride;
        for (int slot = 0; slot < pool.Count; slot++)
        {
            var view = pool[slot]; int index = first + slot;
            if (index >= rows.Count) { view.Root.Hide(); continue; }
            var row = rows[index]; view.Root.Show(); view.Root.Position = new(0, index * stride - offset);
            view.Caption.Text = data[row.TitleMessageId].Text ?? throw new InvalidDataException("Confirmation title resource is missing.");
            view.Value.Text = row.Value ?? unknown; view.Icon.Visible = row.NonTrophy == true;
            if (row.UnresolvedInput is {} missing) view.Root.SetMeta("unresolved_owner", missing);
            else if (view.Root.HasMeta("unresolved_owner")) view.Root.RemoveMeta("unresolved_owner");
        }
    }
    internal void Abandon()
    { abandoned = true; admitted = null; scrollSpeed = null; scrolled = null; rows = []; Hide(); }
}
