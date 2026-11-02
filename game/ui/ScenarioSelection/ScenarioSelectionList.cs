using Godot;
using San14.Data;

internal sealed record ScenarioSelectionEntry(int ScenarioId, ScenarioSelectionButtonContent Content, bool? Enabled);

/// <summary>The scenario page's persistent nine rows and pager. The page owns
/// the ordered directory, source mount order, preview and activation transition.</summary>
internal partial class ScenarioSelectionList : Control
{
    private const int RowsPerPage = 9;
    private readonly List<ScenarioSelectionButton> rows = [];
    private ScenarioSelectionEntry[] entries = [];
    private UiSourceText pageCaption = null!;
    private UiSourceButton previous = null!, next = null!;
    private Action<int>? previewChanged, activated;
    private Func<bool>? inputAdmitted;

    internal int CurrentPage { get; private set; }
    internal int PageCount => (entries.Length + RowsPerPage - 1) / RowsPerPage;
    internal int? PreviewScenarioId { get; private set; }

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string locale, IReadOnlyList<ScenarioSelectionEntry> directory,
        IReadOnlyList<int> mountIndices, Action<int> previewChanged, Action<int> activated,
        Func<bool> inputAdmitted)
    {
        if (directory.Count == 0) throw new ArgumentException("An ordinary scenario page requires its resolved directory.", nameof(directory));
        if (mountIndices.Count != RowsPerPage || mountIndices.Distinct().Count() != RowsPerPage)
            throw new ArgumentException("The scenario page requires nine distinct resolved source mounts.", nameof(mountIndices));
        this.previewChanged = previewChanged;
        this.activated = activated;
        this.inputAdmitted = inputAdmitted;
        entries = directory.ToArray();
        var source = new UiSourceLayout(repository, 0xCC2D6FFD);
        Size = source.Rectangle(0).Size;
        MouseFilter = MouseFilterEnum.Ignore;
        source.Art(this, 1, source.Rectangle(1), assets, shader);
        source.Art(this, 2, source.Rectangle(2), assets, shader, sourceMaterial: true);
        var sprites = new LocalizedUiSpriteSource(repository);
        string language = locale switch { "TC" => "tw", "SC" => "ch", _ => throw new ArgumentOutOfRangeException(nameof(locale)) };
        LoadedSprite Art(string name) => sprites.LoadTexturePackSprite("texture_lang_" + language, language + "_" + name);
        var firstKind = Art("020_sho_mrk_scenario_1");
        var secondKind = Art("020_sho_mrk_scenario_2");
        var completed = Art("020_sho_seiha");
        for (int ordinal = 0; ordinal < RowsPerPage; ordinal++)
        {
            int slot = ordinal, mount = mountIndices[ordinal];
            Rect2 box = source.Rectangle(mount);
            if (source.Nodes[mount].ParentIndex != 0 || source.Nodes[mount].Type != 0 || box.Size != new Vector2(660, 64))
                throw new ArgumentException("A scenario row mount must be a natural-size root placeholder.", nameof(mountIndices));
            var row = new ScenarioSelectionButton { Name = "ScenarioSlot" + ordinal,
                Position = new((int)box.Position.X, (int)box.Position.Y) };
            AddChild(row); rows.Add(row);
            row.Configure(repository, assets, shader, font, firstKind, secondKind, completed,
                null, () => Preview(slot), () => Activate(slot));
            row.InputAdmission = IsInputAdmitted;
            row.SetMeta("source_mount_index", mount);
        }
        var pagerSource = new UiSourceLayout(repository, 0x9668AE5A);
        Rect2 pagerBox = source.Rectangle(30);
        var pager = new Control { Name = "ScenarioPager", Position = pagerBox.Position,
            Size = pagerSource.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(pager);
        pagerSource.Art(pager, 1, pagerSource.Rectangle(1), assets, shader);
        pageCaption = pagerSource.Text(pager, 2, default, "", font);
        pageCaption.Name = "ScenarioPageNumber";
        previous = Arrow(false, 3, "PreviousScenarioPage", () => MovePage(-1));
        next = Arrow(true, 5, "NextScenarioPage", () => MovePage(1));
        CurrentPage = 0;
        RebindPage();
        // Ordinary mode initializes the preview once. Paging itself does not.
        PreviewScenarioId = entries[0].ScenarioId;
        previewChanged(entries[0].ScenarioId);
        SetMeta("source_layout", source.Source.Source.ResourceUri);

        UiSourceButton Arrow(bool right, int mount, string name, Action released)
        {
            Rect2 box = pagerSource.Rectangle(mount);
            var arrow = new ScenarioSelectionPageButton { Name = name, Position = box.Position };
            pager.AddChild(arrow);
            arrow.Configure(repository, assets, shader, right, released);
            arrow.InputAdmission = IsInputAdmitted;
            return arrow;
        }
    }

    private bool IsInputAdmitted() => IsInsideTree() && IsVisibleInTree() && CanProcess() && inputAdmitted?.Invoke() == true;

    private ScenarioSelectionEntry? BoundEntry(int slot)
    {
        int index = CurrentPage * RowsPerPage + slot;
        return (uint)index < (uint)entries.Length ? entries[index] : null;
    }

    private void Preview(int slot)
    {
        if (!IsInputAdmitted() || BoundEntry(slot) is not { } entry) return;
        PreviewScenarioId = entry.ScenarioId;
        previewChanged?.Invoke(entry.ScenarioId);
    }

    private void Activate(int slot)
    {
        if (!IsInputAdmitted() || BoundEntry(slot) is not { Enabled: true } entry) return;
        activated?.Invoke(entry.ScenarioId);
    }

    private void MovePage(int direction)
    {
        if (!IsInputAdmitted()) return;
        int destination = Math.Clamp(CurrentPage + direction, 0, PageCount - 1);
        if (destination == CurrentPage) return;
        CurrentPage = destination;
        RebindPage();
    }

    private void RebindPage()
    {
        for (int slot = 0; slot < rows.Count; slot++)
        {
            var entry = BoundEntry(slot);
            rows[slot].ApplyContent(entry?.Content, entry?.Enabled);
            rows[slot].Disabled = entry?.Enabled != true;
        }
        pageCaption.Text = FormattableString.Invariant($"{CurrentPage + 1}/{PageCount}");
        previous.Disabled = CurrentPage == 0;
        next.Disabled = CurrentPage == PageCount - 1;
    }

    public override void _ExitTree()
    {
        previewChanged = null; activated = null; inputAdmitted = null;
        entries = []; rows.Clear(); PreviewScenarioId = null;
    }
}
