using Godot;
using San14.Data;

internal sealed record PersonnelCreditChoiceValues(int Index, string Title, bool Selected, bool Enabled, bool Locked);
internal sealed record PersonnelCreditOfficerValues(int OfficerId, LoadedSprite? Portrait,
    DetailForceAppearance? Force, IReadOnlyDictionary<int, string> Fields,
    IReadOnlyList<PersonnelCreditChoiceValues> Choices, bool ShowDoctrine);

/// <summary>The two-row credit-effect editor. It displays the supplied choice
/// state; only the draft may decide the next packed military/domestic choice.</summary>
internal partial class PersonnelCreditOfficerView : Control
{
    private readonly Dictionary<int, UiSourceText> fields = [];
    private readonly List<(UiSourceCommandButton Button, Control Lock)> choices = [];
    private Control portraitHost = null!, flagHost = null!;
    private Control doctrineIcon = null!;
    private GameResourceRepository repository = null!;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private MapFont font = null!;
    private UiSourceLayout source = null!;
    private UiSourcePortrait? portrait;
    private UiSourceForceFlag? flag;
    private LoadedSprite? currentPortrait;
    private DetailForceAppearance? currentForce;
    private PersonnelCreditOfficerValues? value;
    private Func<bool>? admitted;
    private bool busy;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        Func<bool> admitted, Action<int, int> changed)
    {
        this.repository = repository; this.assets = assets; this.shader = shader; this.font = font; this.admitted = admitted;
        source = new(repository, 0xC5EA2F4C); Size = source.Rectangle(0).Size; MouseFilter = MouseFilterEnum.Ignore;
        foreach (int node in new[] { 9, 13, 15, 17, 19, 21, 24, 44, 58, 86, 100 })
            source.Art(this, node, source.Rectangle(node), assets, shader);
        doctrineIcon = source.Art(this, 27, source.Rectangle(27), assets, shader);
        portraitHost = new() { Size = Size, MouseFilter = MouseFilterEnum.Ignore }; AddChild(portraitHost);
        source.Art(this, 3, source.Rectangle(3), assets, shader);
        flagHost = new() { Size = Size, MouseFilter = MouseFilterEnum.Ignore }; AddChild(flagHost);
        var normal = MapTextPresets.Create()[0];
        foreach (int node in new[] { 10, 12, 14, 16, 18, 20, 25, 28, 29 })
            fields.Add(node, source.Text(this, node, default, "", font, normal));
        int[] mounts = [31, 45, 59, 73, 87, 101], locks = [41, 55, 69, 83, 97, 111];
        for (int index = 0; index < mounts.Length; index++)
        {
            int choiceIndex = index;
            Rect2 box = source.Rectangle(mounts[index]);
            var button = new UiSourceCommandButton { Position = box.Position, Size = box.Size,
                InputAdmission = () => !busy && IsVisibleInTree() && (this.admitted?.Invoke() ?? false) };
            AddChild(button);
            button.Configure(repository, assets, shader, font, "", () =>
            {
                if (!busy && value is { } current && current.Choices.FirstOrDefault(item => item.Index == choiceIndex) is { Enabled: true })
                    changed(current.OfficerId, choiceIndex);
            });
            Rect2 lockBox = source.Rectangle(locks[index]); lockBox.Position -= box.Position;
            var locked = source.Art(button, locks[index], lockBox, assets, shader);
            choices.Add((button, locked));
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri); Hide();
    }

    internal void Apply(PersonnelCreditOfficerValues? current, bool busy)
    {
        value = current; this.busy = busy;
        if (current is null) { Hide(); return; }
        foreach (var (node, field) in fields) field.SetKnownColourLine(current.Fields.GetValueOrDefault(node) ?? "");
        doctrineIcon.Visible = current.ShowDoctrine;
        if (!ReferenceEquals(currentPortrait, current.Portrait))
        {
            if (portrait is not null) { portraitHost.RemoveChild(portrait); portrait.QueueFree(); portrait = null; }
            currentPortrait = current.Portrait;
            if (current.Portrait is { } sprite)
            {
                Rect2 box = source.Rectangle(2); portrait = new() { Position = box.Position, Size = box.Size };
                portraitHost.AddChild(portrait); portrait.ConfigureRankup(repository, assets, shader, sprite);
            }
        }
        if (currentForce != current.Force)
        {
            if (flag is not null) { flagHost.RemoveChild(flag); flag.QueueFree(); flag = null; }
            currentForce = current.Force;
            if (current.Force is { } force)
            {
                flag = new() { Position = source.Rectangle(4).Position }; flagHost.AddChild(flag);
                flag.Configure(repository, assets, shader, font, force);
            }
        }
        for (int index = 0; index < choices.Count; index++)
        {
            var choice = current.Choices.FirstOrDefault(item => item.Index == index);
            var display = choices[index];
            display.Button.Caption = choice?.Title ?? "";
            display.Button.Selected = choice?.Selected == true;
            display.Button.Disabled = busy || choice?.Enabled != true;
            display.Lock.Visible = choice?.Locked == true;
        }
        Show();
    }
}
