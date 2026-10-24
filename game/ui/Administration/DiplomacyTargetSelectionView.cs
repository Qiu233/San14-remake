using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record DiplomacyTargetChoice(int Id, string Name, string Detail);

/// <summary>A single selection from a current diplomacy publication. This view
/// preserves candidate order and has no world or command-rule owner.</summary>
internal partial class DiplomacyTargetSelectionView : Control
{
    private AdministrationPageChrome chrome = null!;
    private UiSourceScrollbar scroll = null!;
    private readonly List<(int Id,UiSourcePersonRow View)> rows = [];
    private Func<bool>? admitted;
    private Action<int?>? closed;
    private int? selected;
    private bool abandoned;
    private static readonly DeploymentPersonColumn[] Columns =
        [new(0,520,0,0x01,1,true),new(1,896,0,0x01,1)];
    internal int? SelectedId => selected;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<DiplomacyTargetChoice> choices, int? selected, int titleMessage,
        Func<bool> admitted, Action<int?> closed, Func<int> scrollSpeed)
    {
        this.admitted = admitted; this.closed = closed;
        this.selected = selected.HasValue && choices.Any(row => row.Id == selected.Value) ? selected : null;
        Name = "DiplomacyTargetSelection"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository,text,titleMessage,RequestCancel,RequestApply,showFooterHint:false);
        var shell = new UiSourceLayout(repository,0xA3B4A82E);
        shell.Blur(chrome.Canvas,2,shell.Rectangle(2,new(191,147)));
        shell.Art(chrome.Canvas,3,shell.Rectangle(3,new(191,147)),chrome.Assets,chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        string check = messages.Records[0x0628].Text ?? text.Get("detail_unknown");
        string title = messages.Records[titleMessage].Text ?? text.Get("detail_unknown");
        Header(check,213,48); Header(title,261,1416);
        scroll = new() { Name = "DiplomacyTargetScrollbar", Position = new(1687,265), Size = new(16,597) };
        chrome.Canvas.AddChild(scroll);
        scroll.Configure(repository,chrome.Assets,chrome.SpriteShader,Math.Max(0,choices.Count * 48 - 596),
            scrollSpeed(),_ => PositionRows(),currentScrollSpeed:scrollSpeed);
        var clip = new Control { Name = "DiplomacyTargetsViewport", Position = new(213,264), Size = new(1464,596),
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(clip);
        clip.GuiInput += input => { if (ReadAdmission()) scroll.HandleWheel(input); };
        foreach (var choice in choices)
        {
            int id = choice.Id;
            var row = new UiSourcePersonRow { Name = "DiplomacyTarget" + id, InputAdmission = ReadAdmission };
            clip.AddChild(row);
            row.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,check,Columns,
                [new(choice.Name),new(choice.Detail)]);
            row.ApplyHealthStyle(null); rows.Add((id,row));
            row.GuiInput += input => { if (ReadAdmission()) scroll.HandleWheel(input); };
            row.Pressed += () =>
            {
                if (!ReadAdmission() || row.Disabled) return;
                this.selected = id; RefreshSelection();
            };
        }
        SetMeta("original_diplomacy_target_columns_recovered",false);
        PositionRows(); RefreshSelection();

        void Header(string caption, int x, int width)
        {
            var header = new UiSourceColumnButton { Position = new(x,214), Size = new(width,48),
                Disabled = true, InputAdmission = () => false };
            chrome.Canvas.AddChild(header);
            header.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,caption,personList:true);
        }
    }

    private bool ReadAdmission() => !abandoned && closed is not null && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    private void PositionRows()
    {
        for (int index = 0; index < rows.Count; index++)
        {
            var row = rows[index].View; row.Position = new(0,index * 48 - scroll.Value);
            row.Visible = row.Position.Y > -48 && row.Position.Y < 596;
        }
    }
    private void RefreshSelection()
    {
        foreach (var row in rows) row.View.Selected = row.Id == selected;
        chrome.ApplyDecision(selected.HasValue,true);
    }
    internal void RequestApply() { if (selected.HasValue) Finish(selected); }
    internal void RequestCancel() => Finish(null);
    private void Finish(int? result)
    {
        if (!ReadAdmission()) return;
        var callback = closed; closed = null; admitted = null; Hide(); callback?.Invoke(result);
    }
    internal void Abandon()
    {
        abandoned = true; closed = null; admitted = null;
        foreach (var row in rows) { row.View.InputAdmission = null; row.View.Disabled = true; }
        scroll?.SetProcessInput(false); chrome?.Abandon(); Hide();
    }
}
