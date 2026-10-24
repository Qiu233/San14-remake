using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record PersonnelCreditPage(IReadOnlyList<PersonnelCreditOfficerValues> People,
    AreaAdministrationOfficerValues? Representative, IReadOnlyDictionary<int, string> Fields,
    int SelectedCount, bool CanChoose, bool CanSubmit, string Guide);

/// <summary>The original credit promotion and merit-giving pages. Both retain
/// their own draft while the common person selector is open.</summary>
internal partial class PersonnelCreditView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly Dictionary<int, UiSourceText> fields = [];
    private readonly List<PersonnelCreditOfficerView> cards = [];
    private readonly List<UiSourceBottomButton> automatic = [];
    private AreaAdministrationOfficerView? representative;
    private UiSourceCommandButton? choose;
    private UiSourceButton? previous, next;
    private UiSourceText count = null!;
    private UiSourceText? pageNumber, pageCount;
    private Func<bool>? admitted;
    private Action? returned, accepted;
    private PersonnelCreditPage? publication;
    private string chooseCaption = "";
    private bool rankup, busy, abandoned;
    private int currentPage;

    internal void Configure(GameResourceRepository repository, PreviewText text, bool rankup,
        Func<bool> admitted, Action returned, Action accepted, Action choosePeople,
        Action<int, int> changeChoice, Action<int> applyAutomatic)
    {
        this.rankup = rankup; this.admitted = admitted; this.returned = returned; this.accepted = accepted;
        Name = rankup ? "PersonnelCreditRankup" : "PersonnelCreditGive"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, rankup ? 0x81E : 0x820, RequestBack, RequestApply);
        var source = new UiSourceLayout(repository, rankup ? 0x2CC45ACFu : 0x9DF18E97u);
        Vector2 mount = rankup ? new(334, 184) : new(450, 341);
        int[] replaced = rankup ? [8, 20, 21, 22, 25, 32, 36] : [6];
        foreach (var node in source.Nodes)
        {
            bool hidden = false;
            for (int? at = node.Index; at is int current; at = source.Nodes[current].ParentIndex)
                if (replaced.Contains(current)) { hidden = true; break; }
            if (!hidden && node.Type is 1 or 3)
                source.Art(chrome.Canvas, node.Index, source.Rectangle(node.Index, mount), chrome.Assets, chrome.SpriteShader);
        }
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing personnel credit caption.");
        chooseCaption = Message(0x3FE);
        var presets = MapTextPresets.Create();
        Label(4, 0x542); Label(rankup ? 17 : 36, 0x685);
        Label(rankup ? 80 : 51, 0x20C); Label(rankup ? 90 : 61, 0x292);
        count = Value(rankup ? 19 : 38);
        foreach (int node in rankup ? new[] { 82, 84, 85, 86, 92, 94, 95 } : new[] { 44, 46, 53, 55, 56, 57, 63, 65, 66 })
            fields.Add(node, Value(node));
        if (rankup)
        {
            Label(30, 0x60C); Label(44, 0x81F);
            pageNumber = Value(29); pageCount = Value(31);
            for (int node = 47; node <= 75; node++)
                if (source.Nodes[node].Type == 2) fields.Add(node, Value(node));
            Rect2 box = source.Rectangle(8, mount);
            choose = new() { Position = box.Position, Size = box.Size, InputAdmission = ReadAdmission };
            chrome.Canvas.AddChild(choose);
            choose.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Message(0x3FE),
                () => { if (ReadAdmission() && publication?.CanChoose == true) choosePeople(); });
            foreach (int node in new[] { 20, 21 })
            {
                var card = new PersonnelCreditOfficerView { Position = source.Rectangle(node, mount).Position };
                chrome.Canvas.AddChild(card); card.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, ReadAdmission,
                    (person, choice) => { if (ReadAdmission()) changeChoice(person, choice); }); cards.Add(card);
            }
            previous = Arrow(32, 35, -1); next = Arrow(36, 39, 1);
            var footer = new UiSourceLayout(repository, 0xD8913AB2);
            int[] mounts = [14, 17, 18];
            for (int index = 0; index < mounts.Length; index++)
            {
                int mode = index + 3;
                var button = new UiSourceBottomButton { Position = footer.Rectangle(mounts[index], new(0, 904)).Position,
                    InputAdmission = ReadAdmission };
                chrome.Canvas.AddChild(button);
                button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Message(0xA0 + index),
                    () => { if (ReadAdmission()) applyAutomatic(mode); }); automatic.Add(button);
            }
        }
        else
        {
            Label(42, 0x821);
            representative = new() { Position = source.Rectangle(6, mount).Position };
            chrome.Canvas.AddChild(representative);
            representative.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text,
                () => { if (ReadAdmission() && publication?.CanChoose == true) choosePeople(); }, chrome.Canvas);
        }
        chrome.ApplyReturnHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2733));
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetBusy(false);

        UiSourceText Value(int node) => source.Text(chrome.Canvas, node, mount, "", chrome.Font, presets[0]);
        void Label(int node, int id) => source.Text(chrome.Canvas, node, mount, Message(id), chrome.Font, presets[node == 30 ? 0 : 13]);
        UiSourceButton Arrow(int node, int art, int direction)
        {
            Rect2 box = source.Rectangle(node, mount);
            var button = new UiSourceButton { Position = box.Position, Size = box.Size, IgnoreTextureSize = true,
                FocusMode = FocusModeEnum.None, InputAdmission = ReadAdmission };
            chrome.Canvas.AddChild(button);
            Rect2 rectangle = source.Rectangle(art, mount); rectangle.Position -= box.Position;
            source.Art(button, art, rectangle, chrome.Assets, chrome.SpriteShader);
            button.Pressed += () =>
            {
                if (!ReadAdmission() || publication is null) return;
                currentPage = Math.Clamp(currentPage + direction, 0, Math.Max(0, (publication.People.Count - 1) / 2));
                Apply(publication);
            };
            return button;
        }
    }

    internal void Apply(PersonnelCreditPage value)
    {
        if (abandoned) return;
        publication = value; count.Text = value.SelectedCount.ToString(CultureInfo.InvariantCulture);
        foreach (var (node, field) in fields) field.SetKnownColourLine(value.Fields.GetValueOrDefault(node) ?? "");
        if (rankup)
        {
            int pages = Math.Max(1, (value.People.Count + 1) / 2); currentPage = Math.Clamp(currentPage, 0, pages - 1);
            pageNumber!.Text = (currentPage + 1).ToString(CultureInfo.InvariantCulture); pageCount!.Text = pages.ToString(CultureInfo.InvariantCulture);
            for (int index = 0; index < cards.Count; index++)
                cards[index].Apply(currentPage * 2 + index < value.People.Count ? value.People[currentPage * 2 + index] : null, busy);
        }
        else representative!.Apply(value.Representative ?? new(chooseCaption, null, null, null, null), !busy && value.CanChoose);
        chrome.ApplyDefaultHint(value.Guide); RefreshAdmission();
    }
    private bool ReadAdmission() => !abandoned && !busy && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    private void RefreshAdmission()
    {
        if (choose is not null) choose.Disabled = busy || publication?.CanChoose != true;
        if (previous is not null) previous.Disabled = busy || currentPage == 0;
        if (next is not null) next.Disabled = busy || publication is null || (currentPage + 1) * 2 >= publication.People.Count;
        foreach (var button in automatic) button.Disabled = busy || publication?.CanChoose != true;
        chrome.ApplyDecision(!busy && publication?.CanSubmit == true, true); chrome.ApplyReturn(!busy);
    }
    internal void SetBusy(bool value)
    {
        busy = value; if (publication is { } current) Apply(current); else if (chrome is not null) RefreshAdmission();
    }
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    private void RequestApply() { if (ReadAdmission() && publication?.CanSubmit == true) accepted?.Invoke(); }
    internal void Abandon() { abandoned = true; admitted = null; returned = accepted = null; publication = null; chrome?.Abandon(); Hide(); }
}
