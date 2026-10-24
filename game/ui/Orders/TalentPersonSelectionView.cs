using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record TalentPersonChoice(int OfficerId, string Name, string LocationName,
    string ForceName, int? Intelligence, int? Politics, int? Charisma,
    bool Enabled = true, string? DisabledReason = null);

/// <summary>Selection over a supplied talent publication. Candidate membership,
/// permission and the selection limit belong to the caller.</summary>
internal partial class TalentPersonSelectionView : Control
{
    private sealed record Entry(TalentPersonChoice Choice, int InputIndex, UiSourcePersonRow View);
    private AdministrationPageChrome chrome = null!;
    private UiSourceScrollbar scroll = null!;
    private UiSourceBottomButton selectAll = null!;
    private readonly List<Entry> entries = [];
    private readonly List<Entry> ordered = [];
    private readonly List<int> selected = [];
    private readonly Dictionary<int,UiSourceColumnButton> headers = [];
    private Func<bool>? admitted;
    private Action<IReadOnlyList<int>?>? closed;
    private bool abandoned, single, ascending;
    private int maximum, sortColumn = -1;
    private string selectCaption = "", clearCaption = "";
    private string? extraCaption;
    private Action? extraAction;
    private bool extraEnabled;
    private static readonly DeploymentPersonColumn[] Columns =
        [new(0,300,0x0211,0x01,1,true),new(1,300,0,0x01,1),new(2,300,0,0x01,1),
         new(3,172,0,0x21,3),new(4,172,0,0x21,3),new(5,172,0,0x21,3)];

    internal IReadOnlyList<int> SelectedOfficerIds => selected.AsReadOnly();

    internal void Configure(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<TalentPersonChoice> rows, IReadOnlyList<int> initial, int maximum, bool single,
        Func<bool> admitted, Action<IReadOnlyList<int>?> closed, Func<int> scrollSpeed, int? titleMessage=null,
        string? firstContextCaption=null, string? extraCaption=null, Action? extraAction=null, bool extraEnabled=false)
    {
        if (maximum < 0) throw new ArgumentOutOfRangeException(nameof(maximum));
        this.maximum = single ? Math.Min(1,maximum) : maximum;
        this.single = single; this.admitted = admitted; this.closed = closed;
        this.extraCaption = extraCaption; this.extraAction = extraAction; this.extraEnabled = extraEnabled;
        Name = "TalentPersonSelection"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository,text,titleMessage??(single ? 0x058D : 0x058F),RequestCancel,RequestApply,showFooterHint:false);
        var shell = new UiSourceLayout(repository,0xA3B4A82E);
        shell.Blur(chrome.Canvas,2,shell.Rectangle(2,new(191,147)));
        shell.Art(chrome.Canvas,3,shell.Rectangle(3,new(191,147)),chrome.Assets,chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? text.Get("detail_unknown");
        selectCaption = Message(0x003A); clearCaption = Message(0x003B);
        string check = Message(0x0628), unknown = text.Get("detail_unknown");
        int x = 213;
        Header(-1,check,48); Header(0,Message(0x0211),300); Header(1,firstContextCaption??text.Get("detail_location"),300);
        Header(2,text.Get("force"),300); Header(3,text.Get("intelligence"),172);
        Header(4,text.Get("politics"),172); Header(5,text.Get("charm"),172);
        scroll = new() { Name = "TalentPersonScrollbar", Position = new(1687,265), Size = new(16,597) };
        chrome.Canvas.AddChild(scroll);
        scroll.Configure(repository,chrome.Assets,chrome.SpriteShader,Math.Max(0,rows.Count * 48 - 596),
            scrollSpeed(),_ => PositionRows(),currentScrollSpeed:scrollSpeed);
        var clip = new Control { Name = "TalentPersonsViewport", Position = new(213,264), Size = new(1464,596),
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(clip);
        clip.GuiInput += input => { if (ReadAdmission()) scroll.HandleWheel(input); };
        for (int index = 0; index < rows.Count; index++)
        {
            var choice = rows[index]; int id = choice.OfficerId;
            var row = new UiSourcePersonRow { Name = "TalentOfficer" + id, InputAdmission = ReadAdmission };
            clip.AddChild(row);
            row.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,check,Columns,
                [new(choice.Name),new(choice.LocationName),new(choice.ForceName),
                 new(Number(choice.Intelligence)),new(Number(choice.Politics)),new(Number(choice.Charisma))]);
            row.ApplyHealthStyle(null);
            entries.Add(new(choice,index,row));
            row.GuiInput += input => { if (ReadAdmission()) scroll.HandleWheel(input); };
            row.Pressed += () =>
            {
                if (!ReadAdmission() || row.Disabled) return;
                if (single) { selected.Clear(); selected.Add(id); }
                else if (!selected.Remove(id) && selected.Count < this.maximum) selected.Add(id);
                RefreshSelection();
            };
        }
        ordered.AddRange(entries);
        selectAll = new() { Name = "TalentSelectAll", Position = new(189,948), InputAdmission = ReadAdmission,
            Visible = !single || extraAction is not null };
        chrome.Canvas.AddChild(selectAll);
        selectAll.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,selectCaption,() =>
        {
            if (!ReadAdmission()) return;
            if (single) { if (this.extraEnabled) this.extraAction?.Invoke(); return; }
            if (selected.Count == Math.Min(this.maximum,entries.Count)) selected.Clear();
            else foreach (var entry in ordered)
            {
                if (selected.Count >= this.maximum) break;
                if (!entry.Choice.Enabled) continue;
                if (!selected.Contains(entry.Choice.OfficerId)) selected.Add(entry.Choice.OfficerId);
            }
            RefreshSelection();
        });
        SetMeta("original_talent_columns_recovered",false);
        SetMeta("initial_order_is_supplied_publication",true);
        Apply(initial); PositionRows();

        string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? unknown;
        void Header(int id, string caption, int width)
        {
            var header = new UiSourceColumnButton { Name = "TalentHeader" + id, Position = new(x,214),
                Size = new(width,48), Disabled = id < 0, InputAdmission = ReadAdmission };
            chrome.Canvas.AddChild(header);
            header.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,caption,personList:true);
            if (id >= 0) { headers.Add(id,header); header.Pressed += () => Sort(id); }
            x += width;
        }
    }

    internal void Apply(IReadOnlyList<int> selection)
    {
        if (abandoned || closed is null) return;
        selected.Clear();
        foreach (int id in selection)
        {
            if (selected.Count >= maximum) break;
            if (!selected.Contains(id) && entries.Any(entry => entry.Choice.OfficerId == id && entry.Choice.Enabled)) selected.Add(id);
        }
        RefreshSelection();
    }

    private void Sort(int column)
    {
        if (!ReadAdmission()) return;
        ascending = sortColumn == column ? !ascending : true; sortColumn = column;
        ordered.Sort((first,second) =>
        {
            var a = first.Choice; var b = second.Choice;
            int comparison;
            if (column < 3)
            {
                string left = column == 0 ? a.Name : column == 1 ? a.LocationName : a.ForceName;
                string right = column == 0 ? b.Name : column == 1 ? b.LocationName : b.ForceName;
                comparison = StringComparer.Ordinal.Compare(left,right);
                if (!ascending) comparison = -comparison;
            }
            else
            {
                int? left = column == 3 ? a.Intelligence : column == 4 ? a.Politics : a.Charisma;
                int? right = column == 3 ? b.Intelligence : column == 4 ? b.Politics : b.Charisma;
                comparison = left.HasValue != right.HasValue ? left.HasValue ? -1 : 1 :
                    left.HasValue ? ascending ? left.Value.CompareTo(right!.Value) : right!.Value.CompareTo(left.Value) : 0;
            }
            return comparison != 0 ? comparison : first.InputIndex.CompareTo(second.InputIndex);
        });
        foreach (var (id,header) in headers) header.ApplySortDirection(id == column ? ascending : null);
        PositionRows();
    }

    private bool ReadAdmission() => !abandoned && closed is not null && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    private void PositionRows()
    {
        for (int index = 0; index < ordered.Count; index++)
        {
            var row = ordered[index].View; row.Position = new(0,index * 48 - scroll.Value);
            row.Visible = row.Position.Y > -48 && row.Position.Y < 596;
        }
    }
    private void RefreshSelection()
    {
        foreach (var entry in entries)
        {
            entry.View.Selected = selected.Contains(entry.Choice.OfficerId);
            entry.View.Disabled = !entry.Choice.Enabled || !entry.View.Selected && (maximum == 0 || !single && selected.Count >= maximum);
        }
        int eligible = entries.Count(entry => entry.Choice.Enabled);
        selectAll.Disabled = single ? !extraEnabled : maximum == 0 || eligible == 0;
        selectAll.ApplyCaption(single ? extraCaption ?? "" : selected.Count == Math.Min(maximum,eligible) ? clearCaption : selectCaption);
        chrome.ApplyDecision(!single || selected.Count == 1,true);
    }
    internal void RequestApply() { if (!single || selected.Count == 1) Finish(selected.ToArray()); }
    internal void RequestCancel() => Finish(null);
    private void Finish(IReadOnlyList<int>? result)
    {
        if (!ReadAdmission()) return;
        var callback = closed; closed = null; admitted = null; Hide(); callback?.Invoke(result);
    }
    internal void Abandon()
    {
        abandoned = true; closed = null; admitted = null; extraAction = null;
        foreach (var entry in entries) { entry.View.InputAdmission = null; entry.View.Disabled = true; }
        foreach (var header in headers.Values) { header.InputAdmission = null; header.Disabled = true; }
        scroll?.SetProcessInput(false);
        if (selectAll is not null) { selectAll.InputAdmission = null; selectAll.Disabled = true; }
        chrome?.Abandon(); Hide();
    }
}
