using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The detached post-slot table. Only the owning Main may turn its
/// draft into an order; scrolling, choosing and cancelling leave the world alone.</summary>
internal partial class OfficerOfficePage : Control
{
    private sealed record Row(Control Root, UiSourceHitButton Hit, IReadOnlyList<UiSourceText> Cells);
    private readonly Dictionary<int, Row> rows = [];
    private AdministrationPageChrome chrome = null!;
    internal UiSourceCommonBlur CommonBackdrop { get; private set; } = null!;
    private UiSourceScrollbar scroll = null!;
    private UiSourceBottomButton autoPromote = null!, autoAssign = null!, dismissAll = null!;
    private UiSourceText promoted = null!, demoted = null!;
    private OfficerOfficeDraft draft = null!;
    private PreviewText text = null!;
    private Func<bool>? admitted;
    private Action<int>? choose;
    private bool busy, abandoned;
    private string vacant = "";
    private string defaultGuide = "";
    private static readonly (int Width, int Caption)[] Columns =
    [ (132,0x05A8),(132,0x05DD),(160,0x05C8),(132,0x021D),(132,0x0822),
      (132,0x03D2),(104,0x0429),(132,0x0238),(132,0x0239),(132,0x023A),(132,0x023B) ];

    internal void Configure(GameResourceRepository repository, PreviewText text, OfficerOfficeDraft draft,
        Func<bool> admitted, Action<int> choose, Action returned, Action accepted,
        Action promoteAutomatically, Action assignAutomatically, Action dismissEveryone, Func<int> scrollSpeed)
    {
        this.draft = draft; this.text = text; this.admitted = admitted; this.choose = choose;
        Name = "OfficerOfficePage"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        CommonBackdrop = new UiSourceCommonBlur(); AddChild(CommonBackdrop); CommonBackdrop.Configure(repository);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x021C, returned, accepted);
        defaultGuide = GameLiteral(0x5978);
        chrome.ApplyReturnHint(GameLiteral(0x2733));
        var source = new UiSourceLayout(repository, 0x8ACC2F29);
        Vector2 mount = new(122,171);
        // The original replaces its static header and sample row with a
        // dynamic InfoList<Post>. Their template field names are not columns.
        foreach (int node in new[] {1,4,6,10,12})
            source.Art(chrome.Canvas,node,source.Rectangle(node,mount),chrome.Assets,chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing office caption.");
        vacant = Message(0x053F);
        source.Text(chrome.Canvas,5,mount,Message(0x029B),chrome.Font);
        source.Text(chrome.Canvas,11,mount,Message(0x029C),chrome.Font);
        promoted = source.Text(chrome.Canvas,7,mount,"",chrome.Font);
        demoted = source.Text(chrome.Canvas,13,mount,"",chrome.Font);
        var headerBox = source.Rectangle(14,mount);
        int headerX = 80;
        var profile = MapFontProfile.FromNode(source.Nodes[28],source.Source.Source.ResourceUri);
        var faceHeader = new UiSourceText { Position = headerBox.Position + new Vector2(10,7), Size = new(60,28),
            MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(faceHeader);faceHeader.Configure(chrome.Font,profile.WithPreset(MapTextPresets.Create()[3]));
        faceHeader.Text=Message(0x0628);
        foreach (var column in Columns)
        {
            var label = new UiSourceText { Position = headerBox.Position + new Vector2(headerX+7,7),
                Size = new(column.Width-14,28), MouseFilter = MouseFilterEnum.Ignore };
            chrome.Canvas.AddChild(label); label.Configure(chrome.Font,profile.WithPreset(MapTextPresets.Create()[3]));
            label.Text = Message(column.Caption); headerX += column.Width;
        }
        var listBox = source.Rectangle(26,mount);
        var clip = new Control { Name = "OfficeRows", Position = listBox.Position, Size = listBox.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(clip);
        var scrollBox = source.Rectangle(57,mount);
        scroll = new() { Name = "OfficeScrollbar", Position = scrollBox.Position, Size = scrollBox.Size };
        chrome.Canvas.AddChild(scroll);
        scroll.Configure(repository,chrome.Assets,chrome.SpriteShader,Math.Max(0,draft.Rows.Count*50-(int)listBox.Size.Y),
            scrollSpeed(),_=>PositionRows(),currentScrollSpeed:scrollSpeed);
        clip.GuiInput += input => { if (Active) scroll.HandleWheel(input); };
        foreach (var post in draft.Rows)
        {
            int id = post.OfficeId;
            var root = new Control { Name = "Office"+id, Size = new(listBox.Size.X,48), MouseFilter = MouseFilterEnum.Pass };
            clip.AddChild(root);
            var hit = new UiSourceHitButton { Name = "ChooseOfficeHolder", Size = root.Size,
                IgnoreTextureSize = true, FocusMode = FocusModeEnum.None, ActionMode = BaseButton.ActionModeEnum.Release };
            root.AddChild(hit);
            hit.Pressed += () => { if (Active && !hit.Disabled) this.choose?.Invoke(id); };
            hit.GuiInput += input => { if (Active) scroll.HandleWheel(input); };
            var cells = new List<UiSourceText>();
            int x = 80;
            foreach (var column in Columns)
            {
                var cell = new UiSourceText { Position = new(x+10,10), Size = new(column.Width-20,28),
                    MouseFilter = MouseFilterEnum.Ignore };
                root.AddChild(cell); cell.Configure(chrome.Font,profile.WithPreset(MapTextPresets.Create()[8]));
                cells.Add(cell); x += column.Width;
            }
            rows.Add(id,new(root,hit,cells));
        }
        var footer = new UiSourceLayout(repository,0xD8913AB2);
        autoPromote = Bottom("AutoPromoteOffices",14,0x05E7,promoteAutomatically);
        autoAssign = Bottom("AutoAssignOffices",17,0x04B8,assignAutomatically);
        dismissAll = Bottom("DismissAllOffices",18,0x059A,dismissEveryone);
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        SetMeta("office_preview_fields_pending",true);
        Apply(); PositionRows();

        UiSourceBottomButton Bottom(string name,int node,int caption,Action action)
        {
            var button = new UiSourceBottomButton { Name = name, Position = footer.Rectangle(node,new(0,904)).Position,
                InputAdmission = ()=>Active };
            chrome.Canvas.AddChild(button);
            button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,Message(caption),()=>{if(Active)action();});
            return button;
        }
        string GameLiteral(int id)
        {
            var record=repository.LoadMessages(text.SourceLocale,MessageKind.Game).Messages.Sections[id/10000].Records[id%10000];
            if(record.Literals is not [{Offset:3} literal]||record.RawBytes.Length!=literal.ByteLength+9)
                throw new InvalidDataException("Office guide requires a complete literal message.");
            return literal.Text;
        }
    }

    private bool Active => !abandoned && !busy && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);

    internal void Apply()
    {
        if (abandoned) return;
        string unknown = text.Get("detail_unknown");
        foreach (var post in draft.Rows)
        {
            var row = rows[post.OfficeId];
            string assigned = post.CurrentOfficer?.Id == post.AssignedOfficer?.Id ? "" : post.AssignedOfficer?.Name ?? vacant;
            var preview = post.Preview;
            string Value(AreaAdministrationQuery<string> value) => value.Available ? value.Value ?? "" : unknown;
            string[] values = [post.OfficeName,post.CurrentOfficer?.Name ?? vacant,assigned,
                Value(preview.Loyalty),Value(preview.MeritStage),post.CommandLimit.ToString(CultureInfo.InvariantCulture),
                Value(preview.Salary),Value(preview.Leadership),Value(preview.Strength),
                Value(preview.Intelligence),Value(preview.Politics)];
            for(int index=0;index<values.Length;index++)row.Cells[index].Text=values[index];
            row.Hit.Disabled = busy || !post.Enabled;
        }
        promoted.Text = draft.PromoteCount.ToString(CultureInfo.InvariantCulture);
        demoted.Text = draft.DemoteCount.ToString(CultureInfo.InvariantCulture);
        chrome.ApplyDecision(!busy && draft.CanCommit,true);
        chrome.ApplyReturn(!busy);
        autoPromote.Disabled = busy || !draft.CanAutoPromote;
        autoAssign.Disabled = busy || !draft.CanAutoAssign;
        dismissAll.Disabled = busy || !draft.CanDismissAll;
        chrome.ApplyDefaultHint(string.IsNullOrEmpty(draft.Status) ? defaultGuide : text.Get("detail_unknown"));
    }

    internal void SetBusy(bool value) { if(abandoned)return; busy=value; Apply(); }
    private void PositionRows()
    {
        int index=0;
        foreach(var post in draft.Rows)
        {
            var row=rows[post.OfficeId].Root;row.Position=new(0,index++*50-scroll.Value);
            row.Visible=row.Position.Y>-48&&row.Position.Y<592;
        }
    }
    public override void _ExitTree() => CommonBackdrop = null!;

    internal void Abandon()
    {
        abandoned=true;admitted=null;choose=null;
        foreach(var row in rows.Values)row.Hit.Disabled=true;
        scroll?.SetProcessInput(false);chrome?.Abandon();Hide();
    }
}
