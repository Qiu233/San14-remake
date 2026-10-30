using System.Collections.Immutable;
using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>A child edit owns a local record until its actual decision. Cancelling
/// returns no record and cannot mutate either the parent draft or the library.</summary>
internal partial class RegisteredOfficerFieldEditorView : Control
{
    private AdministrationPageChrome chrome = null!;
    private RegisteredOfficer working = null!;
    private RegisteredOfficerEditorCatalog catalog = null!;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private Action<RegisteredOfficer?>? closed;
    private Func<bool>? admitted;
    private DeploymentCalculatorDialog? calculator;
    private OfficerPortraitSelectionView? portraitSelector;
    private Func<int> scrollSpeed=()=> (int)ListScrollSpeed.Normal;
    private readonly List<UiSourceCommandButton> buttons = [];
    private readonly List<LineEdit> inputs = [];
    private readonly List<(int Id, UiSourceListRow Row)> choiceRows = [];
    private UiSourceScrollbar? choiceScroll;
    private bool abandoned;
    private int section;
    internal RegisteredOfficer Working => working;

    internal void Configure(GameResourceRepository repository, PreviewText text, RegisteredOfficer original,
        int section, RegisteredOfficerEditorCatalog catalog, Func<bool> admitted, Action<RegisteredOfficer?> closed,
        Func<int>? scrollSpeed=null)
    {
        this.repository = repository; this.text = text; working = original;
        this.catalog = catalog; this.admitted = admitted; this.closed = closed; this.section = section;
        if(scrollSpeed is not null)this.scrollSpeed=scrollSpeed;
        Name = "RegisteredOfficerFieldEditor" + section; ZIndex = 7; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if(section==12)
        {
            var picker=new OfficerPortraitSelectionView();portraitSelector=picker;AddChild(picker);
            picker.Configure(repository,text,catalog.PortraitCatalog,working.PortraitId,CanEdit,chosen=>
            {
                if(abandoned||portraitSelector!=picker)return;
                portraitSelector=null;RemoveChild(picker);picker.QueueFree();
                Finish(chosen is int appearance?working with{PortraitId=appearance}:null);
            },this.scrollSpeed);
            return;
        }
        chrome = new(); AddChild(chrome);
        int[] titles = [0x51, 0x52, 0x452, 0x23D, 0x53, 0x234, 0x22B, 0x54, 0x55, 0x2BC, 0x2BD, 0x50, 0x42];
        if (section is < 0 or > 12) throw new ArgumentOutOfRangeException(nameof(section));
        chrome.Configure(repository, text, titles[section], RequestBack, Accept);
        chrome.ApplyDecision(true, true);
        if (section is 0 or 1 or 2 or 4 or 7 or 8 or 11) BuildForm();
        else BuildChoices();
    }

    private bool CanEdit() => !abandoned && calculator is null && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    private string Message(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1]
        .Records[id].Text ?? throw new InvalidDataException("Missing registered officer field caption.");
    private void BuildForm()
    {
        var source = new UiSourceLayout(RegisteredOfficerEditorLayoutSource.LoadWindow(repository, section));
        Vector2 size = source.Rectangle(0).Size;
        var panel = new Control { Name = "RegisteredOfficerSourceWindow", Size = size,
            Position = (new Vector2(1920, 904) - size) / 2, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(panel);
        // The biography is mounted by the normal full page, whose content occupies
        // the right side of its 1920-pixel design canvas.
        if (section == 11) panel.Position = new(0, 156);
        if (section == 11) source.Blur(panel, 1, source.Rectangle(1));
        source.Art(panel, 2, source.Rectangle(2), chrome.Assets, chrome.SpriteShader);
        switch (section)
        {
            case 0:
                foreach (int node in new[] { 3, 7, 11 })
                    source.Art(panel, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
                source.Text(panel, 4, default, Message(0x4DE), chrome.Font);
                source.Text(panel, 8, default, Message(0x4DF), chrome.Font);
                source.Text(panel, 12, default, Message(0x4E0), chrome.Font);
                Text(5, 0x4DE, working.FamilyName, 2, value => working = working with { FamilyName = value });
                Text(9, 0x4DF, working.GivenName, 2, value => working = working with { GivenName = value });
                Text(13, 0x4E0, working.CourtesyName, 2, value => working = working with { CourtesyName = value });
                // Both supported Chinese layouts hide the reading/sort-key
                // controls after constructing the shared editor window.
                // Preserve those stored keys when editing the display names.
                break;
            case 1:
                Choice(5, Message(0x495), () => working.Sex == 0, () => SetSex(0));
                Choice(7, Message(0x496), () => working.Sex == 1, () => SetSex(1));
                foreach (var voice in catalog.Voices)
                {
                    int voiceId = voice.Id;
                    if (voiceId is < 1 or > 13) continue;
                    int node = 16 + (voiceId - 1) * 3;
                    Choice(node, voice.Name, () => working.VoiceType == voiceId,
                        () => working = working with { VoiceType = (byte)voiceId },
                        () => working.Sex == 0 ? voiceId <= 9 : voiceId >= 10);
                }
                break;
            case 2:
                int[] nodes = [16, 34, 52, 70, 88];
                for (int i = 0; i < 5; i++)
                {
                    int ability = i;
                    Number(nodes[i], 0x238 + i, () => Ability(ability), 0, 100, value => SetAbility(ability, value));
                }
                break;
            case 4:
                Cycle(6, 0x53, catalog.Doctrines, () => working.DoctrineId, value => working = working with { DoctrineId = (byte)value });
                Cycle(13, 0x234, catalog.Policies, () => working.PolicyId, value => working = working with { PolicyId = (byte)value });
                Number(17, 0x250, () => working.PolicyBaseCost, 1, 5, value => working = working with { PolicyBaseCost = (byte)value });
                break;
            case 7:
                Number(3, 0x4E7, () => working.BirthYear, 0, 350, value => working = working with { BirthYear = (ushort)value });
                Number(21, 0x55F, () => working.DeathYear, 0, 350, value => working = working with { DeathYear = (ushort)value });
                break;
            case 8:
                Number(6, 0x55, () => working.FamilyId, 0, ushort.MaxValue, value => working = working with { FamilyId = (ushort)value });
                Person(16, 0x42C, () => working.FatherId, value => working = working with { FatherId = (ushort)value });
                Person(26, 0x42D, () => working.MotherId, value => working = working with { MotherId = (ushort)value });
                Person(36, 0x42E, () => working.SpouseId, value => working = working with { SpouseId = (ushort)value });
                Person(46, 0x2BF, () => working.SwornBrotherId, value => working = working with { SwornBrotherId = (ushort)value });
                Person(56, 0x4EA, () => working.CompatibilityPersonId, value => working = working with { CompatibilityPersonId = (ushort)value });
                Number(58, 0x4EA, () => working.Compatibility, 0, 149, value => working = working with { Compatibility = (byte)value });
                break;
            case 11:
                Rect2 rectangle = source.Rectangle(5);
                var biography = new TextEdit { Name = "RegisteredOfficerBiography", Position = rectangle.Position,
                    Size = rectangle.Size, Text = working.Description, WrapMode = TextEdit.LineWrappingMode.Boundary };
                panel.AddChild(biography);
                biography.TextChanged += () =>
                {
                    if (abandoned) return;
                    string value = biography.Text;
                    if (value.Length <= 500 && !value.Contains('\0')) working = working with { Description = value };
                    chrome.ApplyDecision(value.Length <= 500 && !value.Contains('\0'), true);
                };
                break;
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);

        void Text(int node, int caption, string initial, int maximum, Action<string> change)
        {
            Rect2 box = source.Rectangle(node);
            var input = new LineEdit { Name = "RegisteredOfficerText" + node, Position = box.Position, Size = box.Size,
                MaxLength = maximum, Text = initial, PlaceholderText = Message(caption) };
            panel.AddChild(input); inputs.Add(input);
            input.TextChanged += value => { if (!abandoned && !value.Contains('\0')) change(value); };
        }
        void Number(int node, int caption, Func<int> read, int minimum, int maximum, Action<int> write)
        {
            UiSourceCommandButton? button = null;
            button = Add(source.Rectangle(node), () => Message(caption) + " " + read(), () =>
            {
                if (!CanEdit()) return;
                calculator = new(); AddChild(calculator);
                calculator.Configure(repository, text, read(), minimum, maximum, result =>
                {
                    var old = calculator; calculator = null;
                    if (old is not null) { RemoveChild(old); old.QueueFree(); }
                    if (abandoned) return;
                    if (result is int value) write(value);
                    button!.Caption = Message(caption) + " " + read();
                });
            });
        }
        void Choice(int node, string caption, Func<bool> selected, Action action, Func<bool>? enabled = null)
        {
            var button = Add(source.Rectangle(node), () => caption, () =>
            {
                if (!CanEdit() || enabled?.Invoke() == false) return;
                action(); RefreshChoices();
            });
            button.SetMeta("choice_enabled", true);
            choices.Add((button, selected, enabled)); RefreshChoices();
        }
        void Cycle(int node, int caption, IReadOnlyList<RegisteredOfficerEditorChoice> options, Func<int> read, Action<int> write)
        {
            UiSourceCommandButton? button = null;
            string Label() => Message(caption) + " " + options.FirstOrDefault(value => value.Id == read())?.Name;
            button = Add(source.Rectangle(node), Label, () =>
            {
                if (!CanEdit() || options.Count == 0) return;
                int index = options.ToList().FindIndex(value => value.Id == read());
                write(options[(index + 1) % options.Count].Id); button!.Caption = Label();
            });
        }
        void Person(int node, int caption, Func<int> read, Action<int> write) =>
            Cycle(node, caption, new[] { new RegisteredOfficerEditorChoice(0, "") }.Concat(catalog.Officers).ToArray(), read, write);
        UiSourceCommandButton Add(Rect2 box, Func<string> caption, Action action)
        {
            var button = new UiSourceCommandButton { Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            panel.AddChild(button); buttons.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption(), action); return button;
        }
    }

    private readonly List<(UiSourceCommandButton Button, Func<bool> Selected, Func<bool>? Enabled)> choices = [];
    private void RefreshChoices()
    {
        foreach (var item in choices) { item.Button.Selected = item.Selected(); item.Button.Disabled = item.Enabled?.Invoke() == false; }
        if (section == 1) chrome.ApplyDecision(catalog.VoicesForSex(working.Sex).Any(voice => voice.Id == working.VoiceType), true);
    }
    private void SetSex(byte value)
    {
        working = working with { Sex = value };
        // Voice choices retain their source sex gate. The definition provider's
        // final validation owns spouse and personality compatibility.
    }
    private int Ability(int index) => index switch
    { 0 => working.Leadership, 1 => working.Strength, 2 => working.Intelligence, 3 => working.Politics, _ => working.Charm };
    private void SetAbility(int index, int value) => working = index switch
    {
        0 => working with { Leadership = (byte)value }, 1 => working with { Strength = (byte)value },
        2 => working with { Intelligence = (byte)value }, 3 => working with { Politics = (byte)value },
        _ => working with { Charm = (byte)value }
    };

    private void BuildChoices()
    {
        // All choices remain resource declarations supplied by the editor catalog.
        // Selection order is retained when serialized to the original fixed slots.
        IReadOnlyList<RegisteredOfficerEditorChoice> options = section switch
        { 3 => catalog.Personalities, 5 => catalog.Tactics, 6 => catalog.Formations,
          9 or 10 => catalog.Officers, 12 => catalog.Portraits, _ => throw new InvalidOperationException() };
        var selected = (section switch
        { 3 => working.TraitIds, 5 => working.TacticIds, 6 => working.FormationIds,
          9 => working.LikePersonIds, 10 => working.DislikePersonIds, _ => ImmutableArray.Create(working.PortraitId) })
            .Where(id => id != 0).ToList();
        int limit = section switch { 3 => 5, 5 => 10, 6 => 21, 9 or 10 => 8, _ => 1 };
        var frame = new UiSourceLayout(repository, 0xA3B4A82E);
        frame.Blur(chrome.Canvas, 2, frame.Rectangle(2, new Vector2(191, 147)));
        frame.Art(chrome.Canvas, 3, frame.Rectangle(3, new Vector2(191, 147)), chrome.Assets, chrome.SpriteShader);
        var clip = new Control { Name = "RegisteredOfficerChoiceViewport", Position = new(213, 264),
            Size = new(1464, 596), ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(clip);
        choiceScroll = new UiSourceScrollbar { Position = new(1687, 265), Size = new(16, 597) };
        chrome.Canvas.AddChild(choiceScroll);
        choiceScroll.Configure(repository, chrome.Assets, chrome.SpriteShader, Math.Max(0, options.Count * 48 - 596),
            0, _ => PositionChoices());
        clip.GuiInput += input => { if (CanEdit()) choiceScroll.HandleWheel(input); };
        foreach (var option in options)
        {
            int id = option.Id;
            var button = new UiSourceListRow { Name = "RegisteredOfficerChoice" + id, InputAdmission = CanEdit };
            clip.AddChild(button); choiceRows.Add((id, button));
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, option.Name, 1416, true);
            button.GuiInput += input => { if (CanEdit()) choiceScroll.HandleWheel(input); };
            button.Pressed += () =>
            {
                if (!CanEdit()) return;
                if (selected.Contains(id)) selected.Remove(id);
                else
                {
                    if (limit == 1) selected.Clear();
                    if (selected.Count >= limit) return;
                    selected.Add(id);
                }
                ImmutableArray<int> Slots(int count) => [.. selected.Take(count).Concat(Enumerable.Repeat(0, Math.Max(0, count - selected.Count)))];
                working = section switch
                {
                    3 => working with { TraitIds = Slots(9) }, 5 => working with { TacticIds = Slots(10) },
                    6 => working with { FormationIds = [.. selected] }, 9 => working with { LikePersonIds = Slots(8) },
                    10 => working with { DislikePersonIds = Slots(8) },
                    _ => working with { PortraitId = selected.Count == 0 ? working.PortraitId : selected[0] }
                };
                foreach (var item in choiceRows) item.Row.Selected = selected.Contains(item.Id);
            };
            button.Selected = selected.Contains(id);
        }
        PositionChoices();
        SetMeta("source_layout", frame.Source.Source.ResourceUri);
    }
    private void PositionChoices()
    {
        for (int index = 0; index < choiceRows.Count; index++)
        {
            var row = choiceRows[index].Row; row.Position = new(0, index * 48 - (choiceScroll?.Value ?? 0));
            row.Visible = row.Position.Y > -48 && row.Position.Y < 596;
        }
    }
    private void Accept()
    {
        if (!CanEdit()) return;
        if (section == 1 && !catalog.VoicesForSex(working.Sex).Any(voice => voice.Id == working.VoiceType)) return;
        try { working.Validate(requireName: false); }
        catch (InvalidDataException error) { chrome.ApplyDefaultHint(error.Message); return; }
        Finish(working);
    }
    private void Finish(RegisteredOfficer? result)
    { var callback = closed; Abandon(); callback?.Invoke(result); }
    internal void RequestBack()
    {
        if (abandoned) return;
        if (portraitSelector is not null) { portraitSelector.RequestCancel(); return; }
        if (calculator is not null) { calculator.RequestCancel(); return; }
        if (CanEdit()) Finish(null);
    }
    internal void Abandon()
    {
        abandoned = true; admitted = null; closed = null;
        calculator?.Abandon(); calculator = null;
        portraitSelector?.Abandon(); portraitSelector = null;
        foreach (var button in buttons) button.Abandon();
        foreach (var row in choiceRows) row.Row.Disabled = true;
        foreach (var input in inputs) input.Editable = false;
        chrome?.Abandon(); Hide();
    }
}
