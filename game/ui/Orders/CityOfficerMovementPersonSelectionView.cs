using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Selection over the current movement publication. Candidate order,
/// journey knowledge and maximum count are supplied by Data.</summary>
internal partial class CityOfficerMovementPersonSelectionView : Control
{
    private AdministrationPageChrome chrome = null!;
    private UiSourceScrollbar scroll = null!;
    private UiSourceBottomButton selectAll = null!;
    private readonly List<(int Id, UiSourcePersonRow Row)> rows = [];
    private readonly List<int> selected = [];
    private readonly List<(int Id, UiSourceColumnButton Button)> headers = [];
    private readonly Dictionary<int, bool> nextAscending = [];
    private Func<bool>? admitted;
    private Action<IReadOnlyList<int>?>? closed;
    private int maximum;
    private bool abandoned, busy;
    private string selectCaption = "", clearCaption = "";
    private static readonly DeploymentPersonColumn[] Columns =
        [new(0, 300, 0x0211, 0x01, 1, true), new(1, 300, 0x0478, 0x01, 1), new(2, 180, 0, 0x21, 3)];

    internal IReadOnlyList<int> SelectedOfficerIds => selected.AsReadOnly();

    internal void Configure(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<CityOfficerMovementCandidate> candidates, IReadOnlyList<int> initial, int maximum,
        Func<bool> admitted, Action<IReadOnlyList<int>?> closed, Func<int> scrollSpeed)
    {
        this.maximum = maximum; this.admitted = admitted; this.closed = closed; selected.AddRange(initial);
        Name = "CityOfficerMovementPersons"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x032E, RequestCancel, () => Finish(selected.ToArray()), showFooterHint: false);
        chrome.ApplyDecision(true, true);
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Blur(chrome.Canvas, 2, shell.Rectangle(2, new(191, 147)));
        shell.Art(chrome.Canvas, 3, shell.Rectangle(3, new(191, 147)), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? text.Get("detail_unknown");
        selectCaption = Message(0x003A); clearCaption = Message(0x003B);
        string check = Message(0x0628), unknown = text.Get("detail_unknown");
        int x = 213;
        Header(-1,check,48); Header(0,Message(0x0211),300);
        Header(1,text.Get("detail_city_kind"),300); Header(2,text.Get("detail_days"),180);
        scroll = new() { Name = "MovementPersonScrollbar", Position = new(1687, 265), Size = new(16, 597) };
        chrome.Canvas.AddChild(scroll);
        scroll.Configure(repository, chrome.Assets, chrome.SpriteShader, Math.Max(0, candidates.Count * 48 - 596),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        var clip = new Control { Name = "MovementPersonsViewport", Position = new(213, 264), Size = new(1464, 596),
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(clip);
        clip.GuiInput += input => { if (ReadAdmission()) scroll.HandleWheel(input); };
        foreach (var candidate in candidates)
        {
            int id = candidate.OfficerId;
            var row = new UiSourcePersonRow { Name = "MovementOfficer" + id, InputAdmission = ReadAdmission };
            clip.AddChild(row);
            row.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, check, Columns,
                [new(candidate.Name), new(candidate.SourceCityName), new(candidate.TravelDays?.ToString(CultureInfo.InvariantCulture) ?? unknown)]);
            row.ApplyHealthStyle(null);
            rows.Add((id, row));
            row.GuiInput += input => { if (ReadAdmission()) scroll.HandleWheel(input); };
            row.Pressed += () =>
            {
                if (!ReadAdmission() || row.Disabled) return;
                if (!selected.Remove(id) && selected.Count < maximum) selected.Add(id);
                RefreshSelection();
            };
        }
        selectAll = new() { Name = "MovementSelectAll", Position = new(189, 948), InputAdmission = ReadAdmission };
        chrome.Canvas.AddChild(selectAll);
        selectAll.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, selectCaption, () =>
        {
            if (!ReadAdmission()) return;
            if (selected.Count == Math.Min(maximum, rows.Count)) selected.Clear();
            else { selected.Clear(); selected.AddRange(rows.Take(maximum).Select(value => value.Id)); }
            RefreshSelection();
        });
        SetMeta("source_dispatch_selector", "74CAE0 / 141A31D80");
        SetMeta("source_summon_selector", "74ACF0 / 141A31EA0");
        // The current Data publication supplies name, source and travel days.
        // Other original preset fields remain unknown rather than copied from a new world capture.
        SetMeta("original_movement_columns_recovered", false);
        PositionRows(); RefreshSelection();

        void Header(int column, string caption, int width)
        {
            var heading = new UiSourceColumnButton { Name = "MovementPersonColumn"+column,
                Position = new(x,214), Size = new(width,48), InputAdmission = ReadAdmission };
            chrome.Canvas.AddChild(heading);
            heading.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,caption,personList:true);
            headers.Add((column,heading)); nextAscending[column]=true;
            heading.Pressed += () =>
            {
                if (!ReadAdmission()) return;
                bool ascending=nextAscending[column]; nextAscending[column]=!ascending;
                var indexed=rows.Select((row,index)=>(row,index)).ToArray();
                var byId=candidates.ToDictionary(person=>person.OfficerId);
                int Compare(int left,int right) => column switch
                {
                    -1 => selected.Contains(left).CompareTo(selected.Contains(right)),
                    0 => StringComparer.CurrentCulture.Compare(byId[left].Name,byId[right].Name),
                    1 => StringComparer.CurrentCulture.Compare(byId[left].SourceCityName,byId[right].SourceCityName),
                    _ => Nullable.Compare(byId[left].TravelDays,byId[right].TravelDays)
                };
                Array.Sort(indexed,(left,right)=>
                { int compared=Compare(left.row.Id,right.row.Id); return compared==0?left.index.CompareTo(right.index):ascending?compared:-compared; });
                rows.Clear(); rows.AddRange(indexed.Select(value=>value.row));
                foreach(var (id,button) in headers)button.ApplySortDirection(id==column?ascending:null);
                PositionRows();
            };
            x += width;
        }
    }
    private bool ReadAdmission() => !abandoned && !busy && closed is not null && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    private void PositionRows()
    {
        for (int index = 0; index < rows.Count; index++)
        {
            var row = rows[index].Row;
            row.Position = new(0, index * 48 - scroll.Value);
            row.Visible = row.Position.Y > -48 && row.Position.Y < 596;
        }
    }
    private void RefreshSelection()
    {
        foreach (var (id, row) in rows) { row.Selected = selected.Contains(id); row.Disabled = busy || !row.Selected && selected.Count >= maximum; }
        selectAll.Disabled = busy || rows.Count == 0 || maximum == 0;
        selectAll.ApplyCaption(selected.Count == Math.Min(maximum, rows.Count) ? clearCaption : selectCaption);
        foreach(var (_,button) in headers)button.Disabled=busy;
        chrome.ApplyDecision(!busy,true);chrome.ApplyReturn(!busy);
    }
    internal void SetBusy(bool value) { if(!abandoned){busy=value;RefreshSelection();} }
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
        foreach(var (_,button) in headers){button.InputAdmission=null;button.Disabled=true;}
        scroll?.SetProcessInput(false);
        if (selectAll is not null) { selectAll.InputAdmission = null; selectAll.Disabled = true; }
        chrome?.Abandon(); Hide();
    }
}
