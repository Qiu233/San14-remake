using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>A usable merchant selection over the real candidate publication.
/// The original merchant column-preset table is still unavailable. These known
/// name/intelligence/politics columns reuse the common person controls; they do
/// not claim to recover that preset or its page count.</summary>
internal partial class CityMerchantPersonSelectionView : Control
{
    private AdministrationPageChrome chrome = null!;
    private UiSourceScrollbar scroll = null!;
    private UiSourceBottomButton selectAll = null!;
    private readonly List<(CityMerchantCandidate Candidate, UiSourcePersonRow Row)> rows = [];
    private readonly List<(int Id, UiSourceColumnButton Button)> headers = [];
    private readonly List<int> order = [], selected = [];
    private readonly Dictionary<int, bool> nextAscending = [];
    private Func<bool>? admitted;
    private Action<IReadOnlyList<int>?>? closed;
    private int maximum;
    private bool abandoned;
    private string selectCaption = "", clearCaption = "";
    private static readonly DeploymentPersonColumn[] Columns =
    [new(0, 132, 0x0211, 0x01, 1, true),
     new(10, 76, 0x023A, 0x21, 3), new(11, 76, 0x023B, 0x21, 3)];

    internal IReadOnlyList<int> SelectedOfficerIds => selected.AsReadOnly();
    internal void Configure(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<CityMerchantCandidate> candidates, IReadOnlyList<int> initial, int maximum,
        Func<bool> admitted, Action<IReadOnlyList<int>?> closed, Func<int> scrollSpeed)
    {
        this.maximum = maximum; this.admitted = admitted; this.closed = closed;
        selected.AddRange(initial); order.AddRange(candidates.Select(candidate => candidate.OfficerId));
        Name = "CityMerchantPersons"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x032E, RequestCancel, () => Finish(selected.ToArray()), showFooterHint: false);
        chrome.ApplyDecision(true, true);
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Blur(chrome.Canvas, 2, shell.Rectangle(2, new(191, 147)));
        shell.Art(chrome.Canvas, 3, shell.Rectangle(3, new(191, 147)), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing merchant person caption.");
        selectCaption = Message(0x003A); clearCaption = Message(0x003B);
        string check = Message(0x0628), unknown = text.Get("detail_unknown");
        scroll = new() { Name = "MerchantPersonScrollbar", Position = new(1687, 265), Size = new(16, 597) };
        chrome.Canvas.AddChild(scroll);
        scroll.Configure(repository, chrome.Assets, chrome.SpriteShader, Math.Max(0, candidates.Count * 48 - 596),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        var clip = new Control { Name = "MerchantPersonsViewport", Position = new(213, 264), Size = new(1464, 596),
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(clip); clip.GuiInput += scroll.HandleWheel;
        int x = 213;
        Header(-1, check, 48);
        foreach (var column in Columns) Header(column.Id, Message(column.CaptionMessage), column.Width);
        foreach (var candidate in candidates)
        {
            var row = new UiSourcePersonRow { Name = "MerchantPerson" + candidate.OfficerId, InputAdmission = ReadAdmission };
            clip.AddChild(row);
            row.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, check, Columns,
                [new(candidate.Name), new(Number(candidate.Intelligence)), new(Number(candidate.Politics))]);
            row.ApplyHealthStyle(null); row.GuiInput += scroll.HandleWheel;
            rows.Add((candidate, row));
            row.Pressed += () =>
            {
                if (!ReadAdmission() || row.Disabled) return;
                if (!selected.Remove(candidate.OfficerId) && selected.Count < maximum) selected.Add(candidate.OfficerId);
                RefreshSelection();
            };
        }
        selectAll = new() { Name = "MerchantSelectAll", Position = new(189, 948) };
        chrome.Canvas.AddChild(selectAll);
        selectAll.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, selectCaption, () =>
        {
            if (!ReadAdmission()) return;
            if (selected.Count == Math.Min(maximum, order.Count)) selected.Clear();
            else { selected.Clear(); selected.AddRange(order.Take(maximum)); }
            RefreshSelection();
        });
        SetMeta("original_column_preset_recovered", false);
        PositionRows(); RefreshSelection();

        string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? unknown;
        void Header(int id, string caption, int width)
        {
            var button = new UiSourceColumnButton { Name = "MerchantPersonHeader" + id,
                Position = new(x, 214), Size = new(width, 48), InputAdmission = ReadAdmission };
            chrome.Canvas.AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption, personList: true);
            headers.Add((id, button)); nextAscending[id] = id == 0;
            button.Pressed += () => Sort(id); x += width;
        }
    }

    private bool ReadAdmission() => !abandoned && closed is not null && IsVisibleInTree() && CanProcess() &&
        (admitted?.Invoke() ?? false);
    private void Sort(int column)
    {
        if (!ReadAdmission()) return;
        bool ascending = nextAscending[column]; nextAscending[column] = !ascending;
        var byId = rows.ToDictionary(row => row.Candidate.OfficerId, row => row.Candidate);
        var sorted = order.OrderBy(id => id, Comparer<int>.Create((left, right) =>
        {
            var a = byId[left]; var b = byId[right];
            int comparison = column switch
            {
                -1 => selected.Contains(left).CompareTo(selected.Contains(right)),
                0 => StringComparer.Ordinal.Compare(a.Name, b.Name),
                10 => Nullable.Compare(a.Intelligence, b.Intelligence),
                11 => Nullable.Compare(a.Politics, b.Politics),
                _ => 0
            };
            return ascending ? comparison : -comparison;
        })).ToArray();
        order.Clear(); order.AddRange(sorted);
        foreach (var (id, button) in headers) button.ApplySortDirection(id == column ? ascending : null);
        PositionRows();
    }
    private void PositionRows()
    {
        foreach (var (candidate, row) in rows)
        {
            row.Position = new(0, order.IndexOf(candidate.OfficerId) * 48 - scroll.Value);
            row.Visible = row.Position.Y > -48 && row.Position.Y < 596;
        }
    }
    private void RefreshSelection()
    {
        foreach (var (candidate, row) in rows)
        { row.Selected = selected.Contains(candidate.OfficerId); row.Disabled = !row.Selected && selected.Count >= maximum; }
        selectAll.Disabled = rows.Count == 0 || maximum == 0;
        selectAll.ApplyCaption(selected.Count == Math.Min(maximum, rows.Count) ? clearCaption : selectCaption);
    }
    internal void RequestCancel() => Finish(null);
    private void Finish(IReadOnlyList<int>? result)
    {
        if (!ReadAdmission()) return;
        var callback = closed; closed = null; admitted = null; Hide(); callback?.Invoke(result);
    }
    internal void Abandon()
    {
        abandoned = true; closed = null; admitted = null;
        foreach (var (_, row) in rows) { row.InputAdmission = null; row.Disabled = true; }
        foreach (var (_, button) in headers) { button.InputAdmission = null; button.Disabled = true; }
        if (selectAll is not null) { selectAll.InputAdmission = null; selectAll.Disabled = true; }
        chrome?.Abandon(); Hide();
    }
}
