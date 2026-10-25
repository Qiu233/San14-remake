using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record PersonnelMediationPersonValues(string Name, LoadedSprite? Portrait, bool CanChoose, bool HasPerson);
internal sealed record PersonnelMediationPageValues(IReadOnlyList<PersonnelMediationPersonValues> People,
    IReadOnlyDictionary<int, string> Fields, bool CanMarry, bool CanSwear, bool CanClear, bool ShowThird, string Guide);

/// <summary>The three retained introduction slots. The draft determines
/// marriage or sworn brotherhood from the real selected people.</summary>
internal partial class PersonnelMediationPageView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly Dictionary<int, UiSourceText> fields = [];
    private readonly List<UiSourceCommandButton> buttons = [];
    private readonly List<Control> portraitHosts = [];
    private readonly UiSourcePortrait?[] portraits = new UiSourcePortrait?[3];
    private readonly LoadedSprite?[] sources = new LoadedSprite?[3];
    private UiSourceBottomButton clear = null!;
    private UiSourceRoundButton marriage = null!, sworn = null!;
    private Func<bool>? admitted;
    private Action? returned;
    private Action<bool>? accepted;
    private PersonnelMediationPageValues? publication;
    private GameResourceRepository repository = null!;
    private UiSourceLayout source = null!;
    private LoadedSprite emptyPortrait = null!;
    private bool busy, abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text, Func<bool> admitted,
        Action returned, Action<bool> accepted, Action<int> choosePerson, Action clearPeople)
    {
        this.repository = repository; this.admitted = admitted; this.returned = returned; this.accepted = accepted;
        Name = "PersonnelMediation"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x7E5, RequestBack, () => { }, returnOnly: true, returnNode: 3);
        source = new(repository, 0x1DE0C63E); Vector2 mount = new(0, 170);
        var placeholder = ArmyMapUiSources.Create().PortraitNames(0x896);
        emptyPortrait = new LocalizedUiSpriteSource(repository).LoadTexturePackSprite(placeholder.ResourceName, placeholder.SpriteName);
        int[] replaced = [10, 13, 15, 17, 21, 24, 26, 28, 32, 35, 37, 39];
        foreach (var node in source.Nodes)
        {
            bool hidden = false;
            for (int? at = node.Index; at is int current; at = source.Nodes[current].ParentIndex)
                if (replaced.Contains(current)) { hidden = true; break; }
            if (!hidden && node.Type is 1 or 3)
                source.Art(chrome.Canvas, node.Index, source.Rectangle(node.Index, mount), chrome.Assets, chrome.SpriteShader);
        }
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing introduction caption.");
        foreach (var (node, id) in new[] { (8, 0x542), (45, 0x20C), (53, 0x292) })
            source.Text(chrome.Canvas, node, mount, Message(id), chrome.Font, MapTextPresets.Create()[13]);
        foreach (int node in new[] { 47, 49, 50, 55, 57, 58 })
            fields.Add(node, source.Text(chrome.Canvas, node, mount, "", chrome.Font, MapTextPresets.Create()[0]));
        for (int index = 0; index < 3; index++)
        {
            int slot = index; Rect2 box = source.Rectangle(10 + slot * 11, mount);
            var button = new UiSourceCommandButton { Position = box.Position, Size = box.Size, InputAdmission = ReadAdmission };
            chrome.Canvas.AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Message(0x3FE), () =>
            { if (ReadAdmission() && publication?.People.ElementAtOrDefault(slot)?.CanChoose == true) choosePerson(slot); });
            buttons.Add(button);
            var host = new Control { Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
            chrome.Canvas.AddChild(host); portraitHosts.Add(host);
            //The source frame is above its own two-component face.
            source.Art(host, 16 + slot * 11, source.Rectangle(16 + slot * 11, mount), chrome.Assets, chrome.SpriteShader);
        }
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        sworn = Round(1, "000_cmn_txt_decision_gikyoudai", false);
        marriage = Round(2, "000_cmn_txt_decision_konin", true);
        clear = new() { Position = footer.Rectangle(14, new(0, 904)).Position, InputAdmission = ReadAdmission };
        chrome.Canvas.AddChild(clear);
        clear.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Message(0x3B),
            () => { if (ReadAdmission() && publication?.CanClear == true) clearPeople(); });
        chrome.ApplyReturnHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2733));
        SetMeta("source_layout", source.Source.Source.ResourceUri); SetBusy(false);

        UiSourceRoundButton Round(int node, string caption, bool marry)
        {
            var button = new UiSourceRoundButton { Position = footer.Rectangle(node, new(0, 904)).Position,
                InputAdmission = ReadAdmission };
            chrome.Canvas.AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, text.SourceLocale, caption, () => RequestApply(marry));
            return button;
        }
    }
    internal void Apply(PersonnelMediationPageValues value)
    {
        if (abandoned) return;
        publication = value;
        foreach (var (node, field) in fields) field.SetKnownColourLine(value.Fields.GetValueOrDefault(node) ?? "");
        for (int slot = 0; slot < 3; slot++)
        {
            var person = value.People.ElementAtOrDefault(slot);
            buttons[slot].Visible = slot != 2 || value.ShowThird;
            portraitHosts[slot].Visible = slot != 2 || value.ShowThird;
            buttons[slot].Caption = person?.Name ?? ""; buttons[slot].Disabled = busy || person?.CanChoose != true;
            var sprite = person?.HasPerson == true ? person.Portrait : emptyPortrait;
            if (ReferenceEquals(sources[slot], sprite)) continue;
            if (portraits[slot] is { } previous) { portraitHosts[slot].RemoveChild(previous); previous.QueueFree(); portraits[slot] = null; }
            sources[slot] = sprite;
            if (sprite is null) continue;
            Rect2 box = source.Rectangle(14 + slot * 11, new(0, 170));
            var portrait = new UiSourcePortrait { Position = box.Position, Size = box.Size };
            portraitHosts[slot].AddChild(portrait); portraitHosts[slot].MoveChild(portrait, 0);
            portrait.ConfigureMediation(repository, chrome.Assets, chrome.SpriteShader, sprite, slot); portraits[slot] = portrait;
        }
        chrome.ApplyDefaultHint(value.Guide); RefreshAdmission();
    }
    private bool ReadAdmission() => !abandoned && !busy && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    private void RefreshAdmission()
    {
        clear.Disabled = busy || publication?.CanClear != true;
        marriage.Disabled = busy || publication?.CanMarry != true;
        sworn.Disabled = busy || publication?.CanSwear != true;
        chrome.ApplyReturn(!busy);
    }
    internal void SetBusy(bool value) { busy = value; if (publication is { } current) Apply(current); else if (clear is not null) RefreshAdmission(); }
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    private void RequestApply(bool marry)
    { if (ReadAdmission() && (marry ? publication?.CanMarry : publication?.CanSwear) == true) accepted?.Invoke(marry); }
    internal void Abandon() { abandoned = true; admitted = null; returned = null; accepted = null; publication = null; chrome?.Abandon(); Hide(); }
}
