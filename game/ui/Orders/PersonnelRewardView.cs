using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record PersonnelRewardPage(IReadOnlyList<PersonnelRewardCardValues> People,
    IReadOnlyDictionary<int, string> Fields, bool CanSubmit, bool CanChoose, string Guide);

/// <summary>The original reward draft and its eighteen paged preview cards.
/// Candidate selection and accounting are supplied by the owning draft.</summary>
internal partial class PersonnelRewardView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly List<PersonnelRewardCardView> cards = [];
    private readonly Dictionary<int, UiSourceText> fields = [];
    private UiSourceCommandButton choose = null!;
    private UiSourceButton previous = null!, next = null!;
    private UiSourceText pageNumber = null!, pageCount = null!, peopleCount = null!;
    private Func<bool>? admitted;
    private Action? returned, accepted;
    private PersonnelRewardPage? publication;
    private bool abandoned, busy;
    private int currentPage;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        Func<bool> admitted, Action choosePeople, Action returned, Action accepted)
    {
        this.admitted = admitted; this.returned = returned; this.accepted = accepted;
        Name = "PersonnelReward"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop = new UiSourceCommonBlur(); AddChild(backdrop); backdrop.Configure(repository);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x0245, RequestBack, RequestApply);
        var source = new UiSourceLayout(repository, 0x97FA0FCA);
        Vector2 mount = new(464, 235);
        foreach (int node in new[] { 1, 3, 5, 63, 66, 68, 70, 76, 78, 80 })
            source.Art(chrome.Canvas, node, source.Rectangle(node, mount), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing reward caption.");
        var presets = MapTextPresets.Create();
        Label(4, 0x542); Label(13, 0x685); Label(53, 0x60C); Label(67, 0x20C); Label(77, 0x292);
        peopleCount = Value(14); pageNumber = Value(52); pageCount = Value(54);
        foreach (int node in new[] { 69, 71, 72, 73, 79, 81, 82, 83 }) fields.Add(node, Value(node));
        Rect2 chooseBox = source.Rectangle(6, mount);
        choose = new() { Name = "ChooseRewardOfficers", Position = chooseBox.Position, Size = chooseBox.Size,
            InputAdmission = ReadAdmission };
        chrome.Canvas.AddChild(choose);
        choose.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Message(0x03FE),
            () => { if (ReadAdmission() && publication?.CanChoose == true) choosePeople(); });
        int[] mounts = [16, 26, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51];
        for (int index = 0; index < mounts.Length; index++)
        {
            var card = new PersonnelRewardCardView { Name = "RewardPreview" + index,
                Position = source.Rectangle(mounts[index], mount).Position };
            chrome.Canvas.AddChild(card);
            card.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, dark: (index / 2 & 1) != 0);
            cards.Add(card);
        }
        previous = Arrow("RewardPreviousPage", 55, 58, -1);
        next = Arrow("RewardNextPage", 59, 62, 1);
        chrome.ApplyReturnHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2733));
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetBusy(false);

        void Label(int node, int id) => source.Text(chrome.Canvas, node, mount, Message(id), chrome.Font,
            presets[node == 53 ? 0 : 13]);
        UiSourceText Value(int node) => source.Text(chrome.Canvas, node, mount, "", chrome.Font, presets[0]);
        UiSourceButton Arrow(string name, int node, int art, int direction)
        {
            Rect2 box = source.Rectangle(node, mount);
            var button = new UiSourceButton { Name = name, Position = box.Position, Size = box.Size,
                IgnoreTextureSize = true, FocusMode = FocusModeEnum.None, InputAdmission = ReadAdmission };
            chrome.Canvas.AddChild(button);
            Rect2 rectangle = source.Rectangle(art, mount); rectangle.Position -= box.Position;
            source.Art(button, art, rectangle, chrome.Assets, chrome.SpriteShader);
            button.Pressed += () =>
            {
                if (!ReadAdmission() || publication is null) return;
                currentPage = Math.Clamp(currentPage + direction, 0, Math.Max(0, (publication.People.Count - 1) / 18));
                Apply(publication);
            };
            return button;
        }
    }

    internal void Apply(PersonnelRewardPage value)
    {
        if (abandoned) return;
        publication = value;
        int count = Math.Max(1, (value.People.Count + 17) / 18);
        currentPage = Math.Clamp(currentPage, 0, count - 1);
        for (int index = 0; index < cards.Count; index++)
        {
            int person = currentPage * 18 + index;
            cards[index].Apply(person < value.People.Count ? value.People[person] : null);
        }
        peopleCount.Text = value.People.Count.ToString(CultureInfo.InvariantCulture);
        pageNumber.Text = (currentPage + 1).ToString(CultureInfo.InvariantCulture);
        pageCount.Text = count.ToString(CultureInfo.InvariantCulture);
        foreach (var (node, field) in fields) field.SetKnownColourLine(value.Fields.GetValueOrDefault(node) ?? "");
        chrome.ApplyDefaultHint(value.Guide);
        RefreshAdmission();
    }

    private bool ReadAdmission() => !abandoned && !busy && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    private void RefreshAdmission()
    {
        if (abandoned) return;
        choose.Disabled = busy || publication?.CanChoose != true;
        previous.Disabled = busy || currentPage == 0;
        next.Disabled = busy || publication is null || (currentPage + 1) * 18 >= publication.People.Count;
        chrome.ApplyDecision(!busy && publication?.CanSubmit == true, true); chrome.ApplyReturn(!busy);
    }
    internal void SetBusy(bool value) { busy = value; if (choose is not null) RefreshAdmission(); }
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    private void RequestApply() { if (ReadAdmission() && publication?.CanSubmit == true) accepted?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = accepted = null; publication = null;
        if (choose is not null) choose.Abandon(); chrome?.Abandon(); Hide();
    }
}
