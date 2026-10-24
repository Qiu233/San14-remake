using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The independent title Edit menu. Implemented editors keep their own drafts.</summary>
internal partial class TitleEditMenuView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly List<UiSourceCommandButton> buttons = [];
    private Func<bool>? admitted;
    private Action? returned, music;
    private bool abandoned, busy;
    internal void Configure(GameResourceRepository repository, PreviewText text, Func<bool> admitted, Action returned, Action music)
    {
        this.admitted = admitted; this.returned = returned; this.music = music;
        Name = "TitleEditMenu"; ZIndex = 4; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x0066, RequestBack, () => { }, returnOnly: true);
        var source = new UiSourceLayout(MusicEditLayoutSource.LoadMenuButtons(repository));
        foreach (int index in new[] { 1, 2 })
            source.Art(chrome.Canvas, index, source.Rectangle(index), chrome.Assets, chrome.SpriteShader);
        int[] captions = [0x0030, 0x0067, 0x02DF, 0x02E7, 0x072C, 0x07CD, 0x07F5];
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        for (int index = 0; index < captions.Length; index++)
        {
            int entry = index;
            var box = source.Rectangle(3 + index * 3);
            var button = new UiSourceCommandButton { Name = "TitleEditEntry" + index, Position = box.Position,
                Size = box.Size, Disabled = index != 2 };
            chrome.Canvas.AddChild(button); buttons.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                messages.Records[captions[index]].Text ?? throw new InvalidDataException("Missing edit menu caption."),
                () => { if (CanEdit() && entry == 2) this.music?.Invoke(); });
            button.InputAdmission = CanEdit;
        }
        chrome.ApplyDefaultHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2848));
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }
    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    internal void SetBusy(bool value)
    {
        if (abandoned) return;
        busy = value; chrome.ApplyReturn(!value);
        for (int index = 0; index < buttons.Count; index++) buttons[index].Disabled = value || index != 2;
    }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = null; music = null;
        foreach (var button in buttons) button.Abandon();
        chrome?.Abandon(); Hide();
    }
}
