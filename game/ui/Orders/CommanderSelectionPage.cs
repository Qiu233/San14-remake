using Godot;
using San14.Data;
using San14.Data.Formats;
using System.Globalization;

internal partial class CommanderSelectionPage : Control
{
    private AdministrationPageChrome chrome = null!;
    private ItemList list = null!;
    private UiSourceText count = null!;
    private readonly List<int> selected = [];
    private Action? returned;
    private Action<IReadOnlyList<int>>? accepted;
    private int capacity;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text, Theme theme,
        CommanderSelectionView model, Action returned, Action<IReadOnlyList<int>> accepted)
    {
        this.returned = returned; this.accepted = accepted;
        capacity = model.Capacity ?? 0;
        Name = "CommanderSelectionPage"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository,text,0x223,RequestCancel,Confirm);
        chrome.ApplyTitle(text.Get("play_commander_cards_title"));
        chrome.ApplyDefaultHint(text.Get("play_commander_cards_hint"));
        var source = new UiSourceLayout(repository,0xAB6ADAA7);
        var profile = MapFontProfile.FromNode(source.Nodes.Single(node => node.Type == 2),source.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[13]);
        count = new() { Name = "CommanderSelectionCount", Position = new(320,140), Size = new(1280,40) };
        chrome.Canvas.AddChild(count); count.Configure(chrome.Font,profile);
        var columns = new UiSourceText { Name = "CommanderColumns", Position = new(320,185), Size = new(1280,40) };
        chrome.Canvas.AddChild(columns); columns.Configure(chrome.Font,profile);
        var messages = repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        columns.Text = string.Join("　",new[] { 0x211,0x238,0x239,0x23A,0x23B,0x23C }.Select(id =>
            messages.Records[id].Text ?? throw new InvalidDataException("Missing commander person column caption.")));
        list = new() { Name = "CommanderCandidates", Position = new(320,235), Size = new(1280,585), Theme = theme,
            SelectMode = ItemList.SelectModeEnum.Multi, AllowReselect = true };
        chrome.Canvas.AddChild(list);
        var ids = model.Cards.Select(card => card.OfficerId).ToHashSet();
        foreach (var person in model.Candidates)
        {
            var value = person.Abilities;
            int index = list.AddItem($"{person.Name}　{value.Leadership.ToString(CultureInfo.InvariantCulture)}　" +
                $"{value.Strength.ToString(CultureInfo.InvariantCulture)}　{value.Intelligence.ToString(CultureInfo.InvariantCulture)}　" +
                $"{value.Politics.ToString(CultureInfo.InvariantCulture)}　{value.Charm.ToString(CultureInfo.InvariantCulture)}");
            list.SetItemMetadata(index,person.OfficerId);
            if (ids.Contains(person.OfficerId)) { list.Select(index,false); selected.Add(person.OfficerId); }
        }
        list.MultiSelected += (index, enabled) =>
        {
            if (abandoned) return;
            int id = list.GetItemMetadata((int)index).AsInt32();
            if (enabled && selected.Contains(id)) return;
            if (enabled && selected.Count >= capacity) { list.Deselect((int)index); return; }
            if (enabled) selected.Add(id); else selected.Remove(id);
            Refresh();
        };
        list.MouseFilter = model.UnresolvedOwner is null && model.Capacity.HasValue ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        Refresh();

        void Refresh()
        {
            count.Text = model.Capacity is int maximum
                ? text.Format("play_commander_cards_capacity",maximum,selected.Count) : text.Get("detail_unknown");
            chrome.ApplyDecision(model.UnresolvedOwner is null && model.Capacity.HasValue && selected.Count <= capacity,true);
        }
    }

    private void Confirm() { if (!abandoned) accepted?.Invoke(selected.ToArray()); }
    internal void ApplyNotice(string notice) { if (!abandoned) chrome.ApplyDefaultHint(notice); }
    internal void RequestCancel() { if (!abandoned) returned?.Invoke(); }
    internal void Abandon() { abandoned = true; returned = null; accepted = null; chrome?.Abandon(); Hide(); }
    public override void _Input(InputEvent input)
    {
        if (abandoned || !IsVisibleInTree()) return;
        if (input is InputEventKey { Keycode: Key.Escape, Pressed: true, Echo: false } or
            InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false })
        { GetViewport().SetInputAsHandled(); RequestCancel(); }
    }
}
