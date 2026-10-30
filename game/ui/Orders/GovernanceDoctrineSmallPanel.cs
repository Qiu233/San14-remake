using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The selector's original GovernmentDoctrineSmall layout. Budget values
/// and ruler resource qualification remain owned by the selector publication.</summary>
internal partial class GovernanceDoctrineSmallPanel : Control
{
    private GameResourceRepository repository = null!;
    private AdministrationPageChrome chrome = null!;
    private UiSourceLayout source = null!;
    private UiSourceText current = null!, limit = null!, selected = null!, rulerName = null!;
    private UiSourcePortrait? portrait;
    private LoadedSprite? rulerFace;

    internal void Configure(GameResourceRepository repository, AdministrationPageChrome chrome,
        int doctrineId, ForceDoctrineProgress progress)
    {
        this.repository = repository; this.chrome = chrome;
        source = new(repository, 0x1BA909C5);
        var main = new UiSourceLayout(repository, 0xAB134AD2);
        Position = main.Rectangle(66).Position; Size = source.Rectangle(0).Size;
        MouseFilter = MouseFilterEnum.Ignore;
        chrome.Canvas.AddChild(this);
        foreach (int node in new[] { 1, 2, 3, 4, 14 })
            source.Art(this, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        //77D6C0 uses this order, not the numerical node order.
        int[] doctrines = [6, 8, 7, 10, 9, 11];
        if (doctrineId is >= 1 and <= 6)
        {
            int node = doctrines[doctrineId - 1];
            source.Art(this, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        }
        var messages = repository.LoadMessages(chrome.Font.Source.Locale, MessageKind.Data).Messages.Sections[1];
        var presets = MapTextPresets.Create();
        source.Text(this, 15, default, (messages.Records[0x54C].Text ?? throw new InvalidDataException("Missing government source caption.")), chrome.Font, presets[13]);
        source.Text(this, 19, default, (messages.Records[0x213].Text ?? throw new InvalidDataException("Missing government source caption.")), chrome.Font, presets[13]);
        source.Text(this, 21, default, (messages.Records[0x54B].Text ?? throw new InvalidDataException("Missing government source caption.")), chrome.Font, presets[13]);
        source.Text(this, 17, default, "/", chrome.Font, presets[0]);
        current = source.Text(this, 16, default, "", chrome.Font, presets[0]);
        limit = source.Text(this, 18, default, "", chrome.Font, presets[0]);
        selected = source.Text(this, 22, default, "", chrome.Font, presets[0]);
        rulerName = source.Text(this, 20, default, "", chrome.Font, presets[0]);
        //The original small layout has no progress/gauge nodes. The regular
        //doctrine view owns level and percentage; this mount only displays its art.
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    internal void Apply(int? currentCost, int? budgetLimit, int selectedCount, int? maximum)
    {
        string Number(int? value) => value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
        current.Text = Number(currentCost); limit.Text = Number(budgetLimit);
        selected.Text = maximum is int max ? $"{selectedCount}/{max}" : "";
        int palette = currentCost is int actual && budgetLimit is int available && actual > available ? 16 : 0;
        current.Configure(chrome.Font, MapFontProfile.FromNode(source.Nodes[16], source.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[palette]));
    }

    internal void ApplyRuler(string name, LoadedSprite? face)
    {
        rulerName.Text = name;
        if (ReferenceEquals(rulerFace, face)) return;
        if (portrait is not null) { RemoveChild(portrait); portrait.QueueFree(); portrait = null; }
        rulerFace = face;
        if (face is null) return;
        var box = source.Rectangle(12);
        portrait = new() { Name = "GovernmentSmallRuler",
            Position = new((int)box.Position.X, (int)box.Position.Y),
            Size = new((int)box.Size.X, (int)box.Size.Y) };
        AddChild(portrait);
        //6F4FC0 replaces placeholder12 with92F830(type7):92CA60 uses
        //CCmnFace(type1), which851E40 names cmn_face_type_02.
        portrait.ConfigureFace2(repository, chrome.Assets, chrome.SpriteShader, face);
    }
}
