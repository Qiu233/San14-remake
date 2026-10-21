using Godot;
using San14.Data;
using San14.Client.Gameplay;

/// <summary>Ordinary campaign slots. The owner performs file operations and
/// world replacement; this view only owns selection and its modal controls.</summary>
internal partial class CampaignSaveView : Control
{
    private AdministrationPageChrome chrome = null!;
    private UiSourceScrollbar scroll = null!;
    private readonly List<CampaignSaveRow> rows = [];
    private readonly Dictionary<int, CampaignSaveSlot> slots = [];
    private CampaignSaveDetails details = null!;
    private readonly Dictionary<int, LoadedSprite?> portraits = [];
    private Action<int>? accepted;
    private Action? returned;
    private int? selected;
    private bool saving, busy, abandoned;

    internal int? SelectedSlot => selected;

    internal void Configure(GameResourceRepository repository, PreviewText text, bool saving,
        IReadOnlyList<CampaignSaveSlot> entries, int initialSlot, Func<string, string> scenarioTitle,
        Action returned, Action<int> accepted)
    {
        this.saving = saving; this.returned = returned; this.accepted = accepted;
        if (entries.Count != LocalCampaignSaves.SlotCount ||
            !entries.Select(entry => entry.Number).SequenceEqual(Enumerable.Range(1, LocalCampaignSaves.SlotCount)))
            throw new ArgumentException("Ordinary save slots must include each file slot in order.", nameof(entries));
        Name = saving ? "CampaignSave" : "CampaignLoad";
        ZIndex = 4; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new AdministrationPageChrome(); AddChild(chrome);
        chrome.Configure(repository, text, saving ? 0x0037 : 0x002E,
            () => { if (!busy && !abandoned) this.returned?.Invoke(); },
            () => { if (!busy && !abandoned && selected is int slot && CanChoose(slots[slot])) this.accepted?.Invoke(slot); });
        var canvas = chrome.Canvas;
        var shell = new UiSourceLayout(repository, 0x761D3014);
        shell.Blur(canvas, 2, shell.Rectangle(2));
        shell.Art(canvas, 3, shell.Rectangle(3), chrome.Assets, chrome.SpriteShader);
        int[] tabNodes = [4, 6, 8, 10], captions = [0x062E, 0x0577, 0x06E1, 0x06E2];
        for (int i = 0; i < tabNodes.Length; i++)
        {
            var box = shell.Rectangle(tabNodes[i]);
            var tab = new UiSourceTabButton { Name = "SaveCategory" + i, Position = box.Position,
                Size = box.Size, Selected = i == 0, Disabled = i != 0 };
            canvas.AddChild(tab);
            tab.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Message(captions[i]),
                i == 0 ? 0x172EDC24u : i == 3 ? 0xD45873AAu : 0x9AFB27B7u);
        }
        Rect2 viewport = shell.Rectangle(18);
        var clip = new Control { Name = "SaveListViewport", Position = viewport.Position, Size = viewport.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        canvas.AddChild(clip);
        var rowSource = new UiSourceLayout(repository, 0x03757250);
        var spriteSource = new LocalizedUiSpriteSource(repository);
        var loadedPortraits = new Dictionary<(string Pack, string Sprite), LoadedSprite>();
        int height = rowSource.Nodes[0].Value46;
        Rect2 scrollbar = shell.Rectangle(31);
        scroll = new UiSourceScrollbar { Name = "SaveScrollbar", Position = scrollbar.Position, Size = scrollbar.Size };
        canvas.AddChild(scroll);
        scroll.Configure(repository, chrome.Assets, chrome.SpriteShader,
            Math.Max(0, entries.Count * height - (int)viewport.Size.Y), 0,
            _ => PositionRows(), stepPixels: height);
        clip.GuiInput += scroll.HandleWheel;
        foreach (var entry in entries)
        {
            slots.Add(entry.Number, entry);
            LoadedSprite? portrait = null;
            if (entry.Summary?.Portrait is { Kind: OfficerPortraitRouteKind.StockNamed } route)
            {
                var key = (route.ResourceName!, route.SpriteName!);
                if (!loadedPortraits.TryGetValue(key, out portrait))
                { portrait = spriteSource.LoadTexturePackSprite(key.Item1, key.Item2); loadedPortraits.Add(key, portrait); }
            }
            portraits.Add(entry.Number, portrait);
            var row = new CampaignSaveRow { Name = "SaveSlot" + entry.Number, Position = new(0, rows.Count * height) };
            clip.AddChild(row); rows.Add(row);
            row.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text, entry,
                entry.Summary is { } summary ? scenarioTitle(summary.ScenarioKey) : "", saving,
                () => Choose(entry.Number), portrait);
            row.GuiInput += scroll.HandleWheel;
        }
        details = new CampaignSaveDetails { Name = "SaveDetails" }; canvas.AddChild(details);
        details.Configure(repository, chrome, text);
        chrome.ApplyDefaultHint(text.Get(saving ? "save_choose_slot" : "load_choose_slot"));
        Choose(initialSlot);

        string Message(int id) => repository.LoadMessages(text.SourceLocale, San14.Data.Formats.MessageKind.Data)
            .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException("Missing save category caption.");

        void PositionRows()
        { for (int i = 0; i < rows.Count; i++) rows[i].Position = new(0, i * height - scroll.Value); }
    }

    private bool CanChoose(CampaignSaveSlot slot) => saving || slot.CanLoad;

    private void Choose(int number)
    {
        if (abandoned || busy || !slots.TryGetValue(number, out var slot)) return;
        selected = number;
        for (int i = 0; i < rows.Count; i++) rows[i].Selected = i + 1 == number && CanChoose(slot);
        details.Apply(slot.Summary, portraits[number]);
        chrome.ApplyDecision(CanChoose(slot), true);
    }

    internal void ApplyNotice(string notice) { if (!abandoned) chrome.ApplyDefaultHint(notice); }
    internal void SetBusy(bool value)
    {
        if (abandoned) return;
        busy = value; chrome.ApplyReturn(!value);
        chrome.ApplyDecision(!value && selected is int slot && CanChoose(slots[slot]), true);
        for (int i = 0; i < rows.Count; i++) rows[i].Disabled = value || !saving && !slots[i + 1].CanLoad;
    }

    internal void Abandon()
    {
        abandoned = true; accepted = null; returned = null;
        foreach (var row in rows) row.Abandon();
        chrome?.Abandon(); Hide();
    }
}
