using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>A detached display projection. Draft legality and exact option
/// ranges remain with the actual personality catalog and application owner.</summary>
internal sealed record PersonalityEditorPageValues(string Name, string Condition, string ConditionOption,
    string Effect1, string Effect2, string ConditionValue, string Effect1Value, string Effect2Value,
    bool ConditionOptionEnabled, bool Effect2Enabled, bool CanConfirm, string Notice);

internal partial class PersonalityEditorView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly Dictionary<int, UiSourceText> fields = [];
    private readonly List<UiSourceCommandButton> commands = [];
    private readonly List<(Control Host, int Field)> parameters = [];
    private RegisteredOfficerTraitMarker nameMarker = null!;
    private Func<PersonalityEditorPageValues>? read;
    private Func<bool>? admitted;
    private Action? returned, accepted;
    private Action<int>? edit;
    private bool abandoned, busy;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        Func<PersonalityEditorPageValues> read, Func<bool> admitted, Action returned, Action accepted,
        Action<int> edit, int titleMessage = 0x7F5)
    {
        this.read = read; this.admitted = admitted; this.returned = returned; this.accepted = accepted; this.edit = edit;
        Name = "PersonalityEditor"; ZIndex = 6; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome); chrome.Configure(repository, text, titleMessage, RequestBack, Accept);
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_41_01_01_edit_kosei_ot"));
        // Input placeholders and source parameter groups are mounted below.
        int[] excluded = [9, 16, 26, 32, 42, 19, 20, 35, 36, 45, 46];
        foreach (var node in source.Nodes.Where(node => node.Type is 1 or 3))
            if (!excluded.Contains(node.Index)) source.Art(chrome.Canvas, node.Index, source.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Caption(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing new-personality editor caption.");
        int[] mounts = [8, 15, 25, 31, 41], captions = [0x5A8, 0x7FB, 0x7FC, 0x404, 0x405];
        for (int index = 0; index < mounts.Length; index++)
        {
            int field = index; var box = source.Rectangle(mounts[index]);
            var button = new UiSourceCommandButton { Name = "PersonalityEditorField" + index,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            chrome.Canvas.AddChild(button); commands.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Caption(captions[index]),
                () => { if (CanEdit() && !button.Disabled) this.edit?.Invoke(field); });
        }
        var nameBox = source.Rectangle(12);
        nameMarker = new RegisteredOfficerTraitMarker { Name = "PersonalityEditorNamePreview", Position = nameBox.Position,
            Size = nameBox.Size, InputAdmission = CanEdit };
        chrome.Canvas.AddChild(nameMarker); nameMarker.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
            "", () => { if (CanEdit()) this.edit?.Invoke(0); });
        foreach (int node in new[] { 17, 27, 33, 43 })
            fields[node] = source.Text(chrome.Canvas, node, default, "", chrome.Font);
        Parameter(0, 19, 20, 21, 22, 0x7FF);
        Parameter(1, 35, 36, 37, 38, 0x800);
        Parameter(2, 45, 46, 47, 48, 0x801);
        SetMeta("source_layout", source.Source.Source.ResourceUri); Refresh();

        void Parameter(int field, int left, int right, int label, int value, int caption)
        {
            var host = new Control { Name = "PersonalityParameter" + field, MouseFilter = MouseFilterEnum.Ignore };
            chrome.Canvas.AddChild(host); parameters.Add((host, field));
            source.Art(host, left, source.Rectangle(left), chrome.Assets, chrome.SpriteShader);
            source.Art(host, right, source.Rectangle(right), chrome.Assets, chrome.SpriteShader);
            source.Text(host, label, default, Caption(caption), chrome.Font);
            fields[value] = source.Text(host, value, default, "", chrome.Font);
        }
    }
    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void Refresh()
    {
        if (abandoned || read is null) return;
        var value = read(); nameMarker.Caption = value.Name;
        fields[17].Text = value.Condition; fields[27].Text = value.ConditionOption;
        fields[33].Text = value.Effect1; fields[43].Text = value.Effect2;
        fields[22].Text = value.ConditionValue; fields[38].Text = value.Effect1Value; fields[48].Text = value.Effect2Value;
        string[] values = [value.ConditionValue, value.Effect1Value, value.Effect2Value];
        foreach (var (host, field) in parameters) host.Visible = values[field].Length != 0;
        for (int index = 0; index < commands.Count; index++)
            commands[index].Disabled = busy || index == 2 && !value.ConditionOptionEnabled || index == 4 && !value.Effect2Enabled;
        nameMarker.Disabled = busy;
        chrome.ApplyDecision(!busy && value.CanConfirm, true); chrome.ApplyDefaultHint(value.Notice);
    }
    private void Accept() { if (CanEdit() && read?.Invoke().CanConfirm == true) accepted?.Invoke(); }
    internal void SetBusy(bool value) { if (abandoned) return; busy = value; chrome.ApplyReturn(!value); Refresh(); }
    internal void ApplyNotice(string notice) { if (!abandoned) chrome.ApplyDefaultHint(notice); }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; read = null; admitted = null; returned = null; accepted = null; edit = null;
        foreach (var button in commands) button.Abandon();
        if (nameMarker is not null) { nameMarker.Disabled = true; nameMarker.InputAdmission = null; }
        chrome?.Abandon(); Hide();
    }
}
