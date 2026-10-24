using System.Globalization;
using Godot;
using San14.Data;
using San14.Client.Gameplay;

internal sealed record NewGameForceOfficersContent(NewGameForceOfficersPreview Source,
    string ForceName, Func<NewGameForceOfficerPreview, OfficerDetailView> Details);

/// <summary>One captured pre-start Person list. A row's ordinary activation
/// opens its existing detail page; closing it resumes this same list and scroll.
/// Native sort and final abilities remain independent from known base figures.</summary>
internal partial class NewGameForceOfficersView : Control
{
    private AdministrationPageChrome chrome = null!;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private Func<NewGameForceOfficerPreview, OfficerDetailView>? details;
    private Action? returned;
    private Action<Exception>? failed;
    private readonly List<UiSourceListRow> buttons = [];
    private OfficerDetailsView? officerDetails;
    private bool abandoned, busy;
    internal NewGameForceOfficersPreview Publication { get; private set; } = null!;
    internal IReadOnlyList<NewGameForceOfficerPreview> Rows { get; private set; } = [];

    internal void Configure(GameResourceRepository repository, PreviewText text,
        NewGameForceOfficersContent content, Action returned, Action<Exception> failed)
    {
        this.repository = repository; this.text = text; this.returned = returned;
        this.failed = failed; details = content.Details; Publication = content.Source;
        Rows = Publication.NativeSortedRows ?? Publication.Rows;
        Name = "NewGameForceOfficers"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { Color = new("#292d2c"), AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });
        chrome = new AdministrationPageChrome(); AddChild(chrome);
        chrome.Configure(repository, text, 0x371, RequestBack, () => { }, returnOnly: true);
        chrome.ApplyDefaultHint(text.Get("new_game_officers_hint"));
        var source = new UiSourceLayout(repository, 0xAB6ADAA7);
        var profile = MapFontProfile.FromNode(source.Nodes.Single(node => node.Type == 2),
            source.Source.Source.ResourceUri).WithPreset(MapTextPresets.Create()[13]);
        Label(chrome.Canvas, "ForceName", content.ForceName, new(260, 105), 700);
        Label(chrome.Canvas, "OfficerCount", text.Format(Publication.MembershipComplete ? "new_game_officers_count" :
            "new_game_officers_known_count", Rows.Count), new(980, 105), 350);
        Label(chrome.Canvas, "ListOrder", text.Get(Publication.NativeSortedRows is null ?
            "new_game_officers_registry_order" : "new_game_officers_native_order"), new(1390, 105), 270);
        Label(chrome.Canvas, "AbilityKind", text.Get("new_game_officers_base_abilities"), new(1060, 165), 600);
        string[] headings = [text.Get("play_personnel_officer"), text.Get("detail_identity"), text.Get("detail_office"),
            text.Get("detail_location"), text.Get("leadership"), text.Get("strength"), text.Get("intelligence"),
            text.Get("politics"), text.Get("charm")];
        int[] widths = [240, 150, 210, 200, 120, 120, 120, 120, 120];
        int left = 260;
        for (int column = 0; column < headings.Length; column++)
        { Label(chrome.Canvas, "Heading" + column, headings[column], new(left, 215), widths[column]); left += widths[column]; }
        var scroll = new ScrollContainer { Name = "OfficerListScroll", Position = new(260, 265), Size = new(1410, 580),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        chrome.Canvas.AddChild(scroll);
        var body = new Control { Name = "OfficerRows", MouseFilter = MouseFilterEnum.Ignore };
        scroll.AddChild(body);
        float top = 0;
        for (int index = 0; index < Rows.Count; index++)
        {
            var row = Rows[index];
            var button = new UiSourceListRow { Name = "OfficerRow" + index, Position = new(0, top),
                InputAdmission = ReadAdmission, ActionMode = BaseButton.ActionModeEnum.Release };
            body.AddChild(button); buttons.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, "", 1400, false);
            button.Disabled = row.DetailOfficerId is null;
            string Unknown(string? value) => value ?? text.Get("detail_unknown");
            string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? text.Get("detail_unknown");
            var abilities = row.BaseAbilities;
            string[] cells = [Unknown(row.Name), Role(row.ServiceStatus), Unknown(row.OfficeName), Unknown(row.LocationName),
                Number(abilities.Leadership), Number(abilities.Strength), Number(abilities.Intelligence),
                Number(abilities.Politics), Number(abilities.Charm)];
            int x = 0;
            for (int column = 0; column < cells.Length; column++)
            { Label(button, "Column" + column, cells[column], new(x + 8, 8), widths[column] - 8); x += widths[column]; }
            button.Pressed += () => { if (ReadAdmission() && !button.Disabled) OpenDetails(row, button); };
            button.SetMeta("physical_person", row.PhysicalPerson);
            if (row.OfficerId is int id) button.SetMeta("officer_id", id);
            top += button.Size.Y;
        }
        body.CustomMinimumSize = new(1400, Math.Max(580, top));
        SetMeta("source_information_pane", 0xE); SetMeta("force_id", Publication.ForceId);

        void Label(Control parent, string name, string value, Vector2 position, int width)
        {
            var label = new UiSourceText { Name = name, Position = position, Size = new(width - 12, 32), MouseFilter = MouseFilterEnum.Ignore };
            parent.AddChild(label); label.Configure(chrome.Font, profile); label.Text = value;
        }
        string Role(byte? status) => text.Get(status switch { 1 => "detail_role_ruler", 2 => "detail_role_district_leader",
            3 => "detail_role_governor", 4 => "detail_role_officer", _ => "detail_unknown" });
    }

    private bool ReadAdmission() => !abandoned && !busy && officerDetails is null && IsVisibleInTree() && CanProcess();
    private void OpenDetails(NewGameForceOfficerPreview row, UiSourceListRow selected)
    {
        if (!ReadAdmission() || details is null) return;
        var view = new OfficerDetailsView(); officerDetails = view; AddChild(view);
        try
        {
            var model = details(row);
            view.Configure(repository, model, text, () => CloseDetails(view));
            foreach (var button in buttons) button.Selected = button == selected;
            chrome.Hide(); chrome.ProcessMode = ProcessModeEnum.Disabled;
        }
        catch (Exception error)
        {
            CloseDetails(view);
            if (failed is not { } report) throw;
            report(error);
        }
    }
    private void CloseDetails(OfficerDetailsView view)
    {
        if (officerDetails != view) return;
        officerDetails = null; view.Hide();
        if (view.GetParent() == this) RemoveChild(view);
        view.QueueFree();
        if (!abandoned) { chrome.ProcessMode = ProcessModeEnum.Inherit; chrome.Show(); chrome.ApplyReturn(!busy); }
    }
    internal void RequestBack()
    {
        if (abandoned || busy) return;
        if (officerDetails is { } view) view.RequestClose();
        else returned?.Invoke();
    }
    internal void SetBusy(bool value)
    {
        busy = value; chrome?.ApplyReturn(!value);
        if (officerDetails is { } view) view.ProcessMode = value ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
    }
    internal void Abandon()
    {
        abandoned = true; details = null; returned = null; failed = null;
        if (officerDetails is { } view) CloseDetails(view);
        foreach (var button in buttons) { button.InputAdmission = null; button.Disabled = true; }
        chrome?.Abandon(); Hide();
    }
}
