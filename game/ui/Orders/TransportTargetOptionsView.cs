using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum TransportTargetOption { BehaviorChoice, ReturnOfficer }

/// <summary>The actual mode2 CAU two-pair panel. Selection/default preference
/// transactions and placement belong to the caller; this view emits intents.</summary>
internal partial class TransportTargetOptionsView : Control
{
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private readonly List<(UiSourceCaptionButton First, UiSourceCaptionButton Second)> pairs = [];
    private UiSourceCaptionButton saveDefaults = null!;
    private Action<TransportTargetOption, int>? choose;
    private Action? save;
    private bool configured, abandoned, hasSelected, canSave;
    internal Func<bool>? InputAdmission { get; set; }

    internal void Configure(GameResourceRepository repository, PreviewText text,
        Action<TransportTargetOption, int> choose, Action save)
    {
        if (assets is not null) throw new InvalidOperationException("Transport options are configured once.");
        ArgumentNullException.ThrowIfNull(choose); ArgumentNullException.ThrowIfNull(save);
        this.choose = choose; this.save = save;
        Name = "TransportTargetOptions"; MouseFilter = MouseFilterEnum.Stop;
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        //7D1A80 changes Control height, then9313A0 changes only the two
        //background pane sizes. Actual9313A0 ASM reads both OLD dimensions
        //before writing the new pair and930FF0 preserves integer top-left.
        //The centered Blur therefore moves125->85; Size-only would be wrong.
        var source = new UiSourceLayout(repository, 0x96B33F19,
            new Dictionary<int, UiSourceNodeGeometry>
            {
                [0] = new(Size: new(390, 170)),
                [1] = new(Size: new(380, 154), Offset: new(195, 85)),
                [2] = new(Size: new(390, 170))
            });
        Size = new(390, 170);
        source.Blur(this, 1, source.Rectangle(1));
        source.Art(this, 2, source.Rectangle(2), assets, shader);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException($"Missing transport option message {id}.");
        var presets = MapTextPresets.Create();
        foreach (var (artNode, textNode, message) in new[] { (3, 4, 0x278), (8, 9, 0x283), (17, 18, 0x284) })
        {
            source.Art(this, artNode, source.Rectangle(artNode), assets, shader);
            source.Text(this, textNode, default, Message(message), font, presets[13]);
        }
        int[][] captions = [[0x25A, 0x282], [0x265, 0x285]];
        for (int group = 0; group < 2; group++)
        {
            int pairIndex = group;
            var first = Button($"TransportOption{group}First", 10 + group * 9, 0xA97E37BE,
                Message(captions[group][0]), () => Choose(pairIndex, true));
            var second = Button($"TransportOption{group}Second", 12 + group * 9, 0x01649C44,
                Message(captions[group][1]), () => Choose(pairIndex, false));
            pairs.Add((first, second));
        }
        saveDefaults = Button("SaveTransportDefaults", 5, 0x60C991A4, Message(0x367),
            () => { if (Admitted() && canSave) this.save?.Invoke(); });
        //Source v90(-11,0):871570->v148/871540 is a relative integer
        //position adjustment after the replacement's own integer mounting.
        saveDefaults.Position += new Vector2(-11, 0);
        configured = true;
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("source_mode", 2);
        SetMeta("source_blur_top_left", source.Rectangle(1).Position);
        SetMeta("source_shrink_keeps_top_left", true);
        Apply(false, null, null, false);

        UiSourceCaptionButton Button(string name, int node, uint layout, string caption, Action released)
        {
            var rectangle = source.Rectangle(node); var authored = source.Nodes[node];
            var button = new UiSourceCaptionButton
            {
                Name = name,
                Position = new((int)rectangle.Position.X, (int)rectangle.Position.Y),
                Size = new((int)(authored.Value44 * authored.HeaderValues[7]),
                    (int)(authored.Value46 * authored.HeaderValues[8])),
                Disabled = true
            };
            AddChild(button);
            button.Configure(repository, assets, shader, font, layout, caption,
                () => { if (Admitted()) released(); });
            button.InputAdmission = Admitted;
            button.SetMeta("source_replacement_node", node);
            return button;
        }
    }

    internal void Apply(bool selected, int? raw41, bool? returnOfficer, bool canSave)
    {
        if (abandoned) return;
        if (!configured) throw new InvalidOperationException("Transport options are not configured.");
        if (raw41 is int value && value is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(raw41));
        hasSelected = selected;
        this.canSave = selected && canSave && (raw41 is 1 or 2) && returnOfficer.HasValue;
        pairs[0].First.Selected = selected && raw41 == 1;
        pairs[0].Second.Selected = selected && raw41 == 2;
        pairs[1].First.Selected = selected && returnOfficer == false;
        pairs[1].Second.Selected = selected && returnOfficer == true;
        foreach (var pair in pairs) pair.First.Disabled = pair.Second.Disabled = !selected;
        saveDefaults.Disabled = !this.canSave;
        SetMeta("source_raw41_known", raw41.HasValue);
        if (raw41 is int known) SetMeta("source_raw41", known);
        else if (HasMeta("source_raw41")) RemoveMeta("source_raw41");
        SetMeta("source_return_officer_known", returnOfficer.HasValue);
        if (returnOfficer is bool returning) SetMeta("source_return_officer", returning);
        else if (HasMeta("source_return_officer")) RemoveMeta("source_return_officer");
    }

    private bool Admitted() => configured && !abandoned && hasSelected && IsInsideTree() &&
        IsVisibleInTree() && !IsQueuedForDeletion() && CanProcess() && (InputAdmission?.Invoke() ?? true);
    private void Choose(int pair, bool first)
    {
        if (!Admitted()) return;
        if (pair == 0) choose?.Invoke(TransportTargetOption.BehaviorChoice, first ? 1 : 2);
        else choose?.Invoke(TransportTargetOption.ReturnOfficer, first ? 0 : 1);
    }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; hasSelected = canSave = false; choose = null; save = null; InputAdmission = null;
        foreach (var pair in pairs) pair.First.Disabled = pair.Second.Disabled = true;
        if (saveDefaults is not null) saveDefaults.Disabled = true;
        Hide();
    }
    public override void _ExitTree()
    { Abandon(); font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
}
