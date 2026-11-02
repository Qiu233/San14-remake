using Godot;
using San14.Client.Gameplay;
using San14.Data;
using San14.Data.Formats;

/// <summary>The income report's current district tabs and numeric-mode child.
/// The outer report owns graphs, advice, footer and phase transitions.</summary>
internal partial class IncomeReportDistrictNumbersView : Control
{
    private readonly List<UiSourceTabButton> tabs = [];
    private IReadOnlyList<IncomeReportDistrict> districts = [];
    private IncomeReportNumbersView numbers = null!;
    private Control numericBackground = null!;
    private Func<DistrictReference, IncomeReportNumbers>? readNumbers;
    private Action<DistrictReference>? districtChanged;
    private Func<bool>? inputAdmitted;
    private Action<Exception>? failed;
    private bool active, abandoned;

    internal int SelectedIndex { get; private set; } = -1;
    internal int DistrictCount => districts.Count;
    internal DistrictReference? SelectedDistrict => SelectedIndex < 0 ? null : districts[SelectedIndex].Reference;
    internal byte? SelectedNumber => SelectedIndex < 0 ? null : districts[SelectedIndex].Number;
    internal void SetNumericVisible(bool visible) { numbers.Visible = visible; numericBackground.Visible = visible; }

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        string locale, SourceWorldContext current, ForceReference force,
        Func<DistrictReference, IncomeReportNumbers> readNumbers, Action<DistrictReference> districtChanged,
        Func<bool> inputAdmitted, Action<Exception> failed)
    {
        ArgumentNullException.ThrowIfNull(readNumbers);
        ArgumentNullException.ThrowIfNull(districtChanged);
        ArgumentNullException.ThrowIfNull(inputAdmitted);
        ArgumentNullException.ThrowIfNull(failed);
        districts = IncomeReportDistrictSelection.Read(current, force);
        this.readNumbers = readNumbers; this.districtChanged = districtChanged;
        this.inputAdmitted = inputAdmitted; this.failed = failed;
        var source = new UiSourceLayout(repository, 0x3E9CC8AD);
        var side = new UiSourceLayout(repository, 0xA3ABC4B3);
        Size = source.Rectangle(0).Size; MouseFilter = MouseFilterEnum.Pass;
        var captions = repository.LoadMessages(locale, MessageKind.Data).Messages.Sections[1].Records;
        int[] messages = [0x38D, 0x38D, 0x38E, 0x38F, 0x390, 0x391, 0x392, 0x393, 0x394];
        for (int i = 0; i < 8; i++)
        {
            int ordinal = i;
            bool present = i < districts.Count;
            string caption = "";
            if (present)
            {
                int number = districts[i].Number;
                if (number >= messages.Length) throw new InvalidDataException("Unsupported income report district number: " + number);
                caption = captions[messages[number]].Text ?? throw new InvalidDataException("Missing income district caption.");
            }
            var box = source.Rectangle(2 + i * 2);
            var tab = new UiSourceTabButton { Name = "IncomeDistrict" + (i + 1), Position = box.Position, Size = box.Size,
                ActionMode = BaseButton.ActionModeEnum.Release };
            AddChild(tab); tabs.Add(tab);
            uint file = districts.Count == 1 ? 0x9AFB27B7u : i == 0 ? 0x172EDC24u
                : i == districts.Count - 1 ? 0xD45873AAu : 0x9AFB27B7u;
            tab.Configure(repository, assets, shader, font, caption, file);
            tab.InputAdmission = ReadAdmission;
            tab.Visible = present;
            tab.Pressed += () => { if (ReadAdmission() && !tab.Disabled) Run(() => SelectTab(ordinal)); };
        }
        numericBackground = side.Art(this, 13, side.Rectangle(13), assets, shader);
        // The original getter truncates the centred native position before
        // adding the 1920x1080 screen centre. Node14 is left/top anchored;
        // its caller then subtracts thirty in screen coordinates.
        Vector2 screenCentre = new(960, 540);
        Vector2 centredPosition = side.Rectangle(14).Position - screenCentre;
        Vector2 numericMount = new(screenCentre.X + (int)centredPosition.X,
            screenCentre.Y - (int)(-centredPosition.Y) - 30);
        numbers = new IncomeReportNumbersView { Name = "IncomeNumbers", Position = numericMount };
        AddChild(numbers);
        numbers.Configure(repository, assets, shader, font, locale);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        active = true;
        if (districts.Count != 0) SelectTab(0);
        ReadAdmission();
    }

    internal void SelectTab(int index)
    {
        if ((uint)index >= (uint)districts.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (!active || SelectedIndex == index) return;
        SelectedIndex = index;
        for (int i = 0; i < tabs.Count; i++) tabs[i].Selected = i == index;
        var reference = districts[index].Reference;
        numbers.Apply(readNumbers!(reference) ?? throw new InvalidDataException("Income report totals are unknown."));
        districtChanged!(reference);
    }

    private bool ReadAdmission()
    {
        if (!active) return false;
        try
        {
            bool allowed = inputAdmitted!();
            for (int i = 0; i < tabs.Count; i++) tabs[i].Disabled = !allowed || i >= districts.Count;
            return allowed;
        }
        catch (Exception error) { Fail(error); return false; }
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception error) { Fail(error); }
    }

    private void Fail(Exception error)
    {
        var callback = failed;
        Abandon();
        callback?.Invoke(error);
    }

    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true;
        active = false; readNumbers = null; districtChanged = null; inputAdmitted = null; failed = null;
        foreach (var tab in tabs) tab.Disabled = true;
        Hide(); SetProcess(false); QueueFree();
    }

    public override void _Process(double delta) => ReadAdmission();
    public override void _ExitTree()
    { active = false; readNumbers = null; districtChanged = null; inputAdmitted = null; failed = null; }
}
