using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record PersonnelHirePageValues(string CityName, AreaAdministrationOfficerValues? Officer,
    int Count, IReadOnlyDictionary<int, string> Fields, bool CanChooseCity, bool CanChoosePeople,
    bool CanSubmit, bool CanContinue, string Guide);

/// <summary>The original city and hire-template page. A selection is retained
/// locally until the application submits the draft's formal batch order.</summary>
internal partial class PersonnelHirePageView : Control
{
    private AdministrationPageChrome chrome = null!;
    private AreaAdministrationOfficerView officer = null!;
    private UiSourceCommandButton city = null!;
    private UiSourceRoundButton again = null!;
    private UiSourceText count = null!;
    private readonly Dictionary<int, UiSourceText> fields = [];
    private Func<bool>? admitted;
    private Action? returned;
    private Action<bool>? accepted;
    private PersonnelHirePageValues? publication;
    private bool busy, abandoned;
    private string chooseCaption = "";

    internal void Configure(GameResourceRepository repository, PreviewText text, Func<bool> admitted,
        Action returned, Action<bool> accepted, Action chooseCity, Action choosePeople)
    {
        this.admitted = admitted; this.returned = returned; this.accepted = accepted;
        Name = "PersonnelHire"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x859, RequestBack, () => RequestApply(false), threeButtonFooter: true);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        again = new() { Position = footer.Rectangle(3, new(0, 904)).Position, InputAdmission = ReadAdmission };
        chrome.Canvas.AddChild(again);
        again.ConfigureContinue(repository, chrome.Assets, chrome.SpriteShader, text.SourceLocale, () => RequestApply(true));
        var source = new UiSourceLayout(repository, 0xD40AACA9); Vector2 mount = new(450, 313);
        foreach (var node in source.Nodes)
        {
            bool replaced = false;
            for (int? at = node.Index; at is int current; at = source.Nodes[current].ParentIndex)
                if (current is 6 or 18) { replaced = true; break; }
            if (!replaced && node.Type is 1 or 3)
                source.Art(chrome.Canvas, node.Index, source.Rectangle(node.Index, mount), chrome.Assets, chrome.SpriteShader);
        }
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing hire caption.");
        chooseCaption = Message(0x3FE);
        foreach (var (node, id) in new[] { (5, 0x85A), (16, 0x542), (86, 0x85B), (88, 0x540),
            (94, 0x8A0), (103, 0x899), (106, 0x540), (112, 0x20C), (121, 0x292) })
            source.Text(chrome.Canvas, node, mount, Message(id), chrome.Font, MapTextPresets.Create()[node is 88 or 106 ? 0 : 13]);
        count = source.Text(chrome.Canvas, 87, mount, "", chrome.Font, MapTextPresets.Create()[0]);
        foreach (int node in new[] { 96, 98, 99, 105, 114, 116, 117, 123, 125, 126 })
            fields.Add(node, source.Text(chrome.Canvas, node, mount, "", chrome.Font, MapTextPresets.Create()[0]));
        Rect2 box = source.Rectangle(6, mount);
        city = new() { Position = box.Position, Size = box.Size, InputAdmission = ReadAdmission };
        chrome.Canvas.AddChild(city);
        city.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, chooseCaption,
            () => { if (ReadAdmission() && publication?.CanChooseCity == true) chooseCity(); });
        officer = new() { Position = source.Rectangle(18, mount).Position };
        chrome.Canvas.AddChild(officer);
        officer.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text,
            () => { if (ReadAdmission() && publication?.CanChoosePeople == true) choosePeople(); }, chrome.Canvas);
        chrome.ApplyReturnHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2733));
        SetMeta("source_layout", source.Source.Source.ResourceUri); SetBusy(false);
    }
    internal void Apply(PersonnelHirePageValues value)
    {
        if (abandoned) return;
        publication = value; city.Caption = value.CityName;
        count.Text = value.Count.ToString(CultureInfo.InvariantCulture);
        officer.Apply(value.Officer ?? new(chooseCaption, null, null, null, null), !busy && value.CanChoosePeople);
        foreach (var (node, field) in fields) field.SetKnownColourLine(value.Fields.GetValueOrDefault(node) ?? "");
        chrome.ApplyDefaultHint(value.Guide); RefreshAdmission();
    }
    private bool ReadAdmission() => !abandoned && !busy && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    private void RefreshAdmission()
    {
        city.Disabled = busy || publication?.CanChooseCity != true;
        again.Disabled = busy || publication?.CanContinue != true;
        chrome.ApplyDecision(!busy && publication?.CanSubmit == true, true); chrome.ApplyReturn(!busy);
    }
    internal void SetBusy(bool value) { busy = value; if (publication is { } current) Apply(current); else if (city is not null) RefreshAdmission(); }
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    private void RequestApply(bool keepOpen) { if (ReadAdmission() && (keepOpen ? publication?.CanContinue : publication?.CanSubmit) == true) accepted?.Invoke(keepOpen); }
    internal void Abandon()
    { abandoned = true; admitted = null; returned = null; accepted = null; publication = null; officer?.Abandon(); city?.Abandon(); chrome?.Abandon(); Hide(); }
}
