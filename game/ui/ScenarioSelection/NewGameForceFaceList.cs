using Godot;
using San14.Data;

/// <summary>The original fourteen-slot startup strip. Paging only rebinds
/// visible cards; hover and release remain separate application requests.</summary>
internal partial class NewGameForceFaceList : Control
{
    private readonly NewGameForceFaceButton[] buttons = new NewGameForceFaceButton[14];
    private NewGameForceFaceContent[] rows = [];
    private ScenarioSelectionPageButton previous = null!, next = null!;
    private UiSourceText pageText = null!;
    private Control pageArea = null!;
    private int? selectedRuler;
    private Action<int?>? preview;
    private Func<bool>? admitted;
    private bool abandoned;
    internal int PageIndex { get; private set; }
    internal int PageCount => Math.Max(1, (rows.Length + buttons.Length - 1) / buttons.Length);
    internal IReadOnlyList<NewGameForceFaceContent> Rows => rows;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        IReadOnlyList<NewGameForceFaceContent> orderedRows, int? selectedRuler,
        Action<int?> preview, Action<int> selected, Func<bool> admitted)
    {
        this.rows = orderedRows.ToArray(); this.selectedRuler = selectedRuler;
        this.preview = preview; this.admitted = admitted;
        MouseFilter = MouseFilterEnum.Ignore;
        var source = new UiSourceLayout(repository, 0x999728A3);
        Size = source.Rectangle(0).Size;
        for (int index = 0; index < buttons.Length; index++)
        {
            string slotName = "02_03_01_seiryoku_sel_offset_00_" + (index + 1);
            // These semantic names are in the original resource. This is not
            // a guessed continuation of the incompletely exported PE table.
            int node = source.Nodes.Single(row => row.ParentIndex == 0 && row.Name == slotName).Index;
            var button = new NewGameForceFaceButton { Name = "RulerSlot" + index,
                Position = source.Rectangle(node).Position, InputAdmission = ReadAdmission };
            AddChild(button); buttons[index] = button;
            button.Configure(repository, assets, shader,
                force => { if (ReadAdmission()) this.preview?.Invoke(force); },
                force => { if (ReadAdmission()) selected(force); });
        }
        previous = Pager(false, 30); next = Pager(true, 32);
        pageArea = new Control { Name = "PageArea", MouseFilter = MouseFilterEnum.Ignore, Size = Size };
        AddChild(pageArea);
        source.Art(pageArea, 38, source.Rectangle(38), assets, shader);
        pageText = source.Text(pageArea, 39, default, "", font, MapTextPresets.Create()[27]);
        // Source22/28/29 device guides remain hidden for this mouse/keyboard
        // publication, matching the application's existing guide policy.
        ApplyPage();
        SetMeta("source_layout", source.Source.Source.ResourceUri);

        ScenarioSelectionPageButton Pager(bool right, int node)
        {
            var pager = new ScenarioSelectionPageButton { Name = right ? "NextRulers" : "PreviousRulers",
                Position = source.Rectangle(node).Position, InputAdmission = ReadAdmission };
            AddChild(pager);
            pager.Configure(repository, assets, shader, right, () => ChangePage(right ? 1 : -1));
            return pager;
        }
    }

    internal void ApplySelected(int? rulerId)
    {
        selectedRuler = rulerId;
        foreach (var button in buttons)
            button.Selected = button.Content is { } content && content.RulerId == selectedRuler;
    }

    internal void ChangePage(int direction)
    {
        if (!ReadAdmission() || PageCount == 1 || direction == 0) return;
        PageIndex = (PageIndex + Math.Sign(direction) + PageCount) % PageCount;
        ApplyPage();
        preview?.Invoke(null);
    }

    private void ApplyPage()
    {
        int start = PageIndex * buttons.Length;
        for (int index = 0; index < buttons.Length; index++)
            buttons[index].ApplyContent(start + index < rows.Length ? rows[start + index] : null);
        ApplySelected(selectedRuler);
        previous.Visible = next.Visible = pageArea.Visible = rows.Length > buttons.Length;
        pageText.Text = (PageIndex + 1) + "/" + PageCount;
    }

    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    internal void Abandon()
    {
        abandoned = true; preview = null; admitted = null;
        foreach (var button in buttons) button?.Abandon();
        if (previous is not null) previous.Disabled = true;
        if (next is not null) next.Disabled = true;
        Hide();
    }
    public override void _ExitTree() { preview = null; admitted = null; }
}
