using Godot;
using San14.Data;

internal sealed record UnitFacilitySelectionEntry(FacilityConstructionChoice Choice, bool? Allowed,
    string? Reason = null, bool CommandSupported = true)
{
    internal bool CanConfirm => Allowed == true && CommandSupported;
}

/// <summary>The existing army's single-definition child page. Accepting a
/// definition only enters map placement; it never publishes a game order.</summary>
internal partial class UnitFacilitySelectionView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly List<(UnitFacilitySelectionEntry Entry, UiSourceListRow Row)> rows = [];
    private Action<FacilityConstructionChoice?>? complete;
    private FacilityConstructionChoice? selected;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<UnitFacilitySelectionEntry> entries, Action<FacilityConstructionChoice?> complete,
        IReadOnlyList<string>? preselectedDefinitionKeys=null)
    {
        this.complete = complete;
        Name = "UnitFacilitySelection"; ZIndex = 4; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new AdministrationPageChrome(); AddChild(chrome);
        chrome.Configure(repository, text, 0x5B9, RequestCancel, Accept);
        chrome.ApplyDefaultHint(text.Get("play_facility_construction"));
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Blur(chrome.Canvas, 2, shell.Rectangle(2, new(191, 147)));
        shell.Art(chrome.Canvas, 3, shell.Rectangle(3, new(191, 147)), chrome.Assets, chrome.SpriteShader);
        // Keep the full definition directory accessible inside the existing
        // list area. Exact original column/scroll art remains a separate gap.
        var scroll = new ScrollContainer { Name = "FacilityDirectoryScroll", Position = new(213, 264),
            Size = new(1464, Math.Min(624, chrome.MenuFooterTop - 264)),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        chrome.Canvas.AddChild(scroll);
        var list = new VBoxContainer { Name = "FacilityDirectoryRows" };
        list.AddThemeConstantOverride("separation", 0);
        scroll.AddChild(list);
        for (int index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var row = new UiSourceListRow { Name = "FacilityChoice" + index, Disabled = !entry.CanConfirm };
            list.AddChild(row);
            row.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, entry.Choice.Name, 1464, false);
            row.CustomMinimumSize = row.Size;
            row.SetMeta("definition_key", entry.Choice.DefinitionKey);
            row.SetMeta("eligibility_known", entry.Allowed.HasValue);
            row.SetMeta("command_supported", entry.CommandSupported);
            rows.Add((entry, row));
            row.MouseEntered += () => chrome.ApplyHoverHint(!entry.CommandSupported
                ? text.Get("play_facility_order_unavailable") : entry.Reason == "structure.selection.policy"
                ? text.Get("play_facility_blocked_policy") : entry.Allowed != true
                    ? text.Get("play_facility_unavailable") : entry.Choice.Name);
            row.MouseExited += () => chrome.ApplyHoverHint(null);
            row.Pressed += () =>
            {
                if (this.complete is null || !entry.CanConfirm) return;
                selected = entry.Choice;
                foreach (var item in rows) item.Row.Selected = item.Entry.Choice == selected;
                chrome.ApplyDecision(true, true);
            };
        }
        // A resolved seed is one original visible index. When aliases need
        // the still-unknown original sorting order, the caller supplies no
        // seed instead of inventing an order or selecting multiple rows.
        var seeds=rows.Where(item=>preselectedDefinitionKeys?.Contains(item.Entry.Choice.DefinitionKey)==true).ToArray();
        if(seeds.Length==1)
        {
            seeds[0].Row.Selected=true;
            if(seeds[0].Entry.CanConfirm)selected=seeds[0].Entry.Choice;
        }
        chrome.ApplyDecision(selected is not null,true);
    }

    private void Accept()
    { if (selected is not null) Complete(selected); }
    internal void RequestCancel() => Complete(null);
    internal void ApplyHint(string value) => chrome.ApplyDefaultHint(value);
    private void Complete(FacilityConstructionChoice? choice)
    { var callback = complete; complete = null; callback?.Invoke(choice); }
    internal void Abandon() { complete = null; chrome?.Abandon(); Hide(); }
}
