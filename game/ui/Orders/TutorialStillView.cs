using System.Buffers.Binary;
using System.Globalization;
using Godot;
using San14.Data;

/// <summary>The tutorial still mounted in the event illustration layer.
/// It owns no input or story completion.</summary>
internal partial class TutorialStillView : Control
{
    private UiAssets? assets;
    private Shader? shader;
    private Control canvas = null!, illustration = null!;
    private UiSourceLayout layout = null!;
    private Vector2 mount;
    private string language = null!;
    private readonly HashSet<uint> textureNames = [];

    internal void Configure(GameResourceRepository repository, PreviewText text)
    {
        language = text.SourceLocale switch
        {
            "TC" => "tw", "SC" => "ch",
            _ => throw new InvalidDataException("tutorial.still.resource_language")
        };
        var settings = repository.LoadObjects("KIDSSystemResource", 0x208BE13A)
            .Document.Objects.Single(value => value.ObjectId == 0xE0D0BB40);
        byte[] names = settings.Properties.Single(value => value.NameHash == 0x39187761).RawValue;
        if (names.Length % 4 != 0)
            throw new InvalidDataException("tutorial.still.texture_name_directory");
        for (int offset = 0; offset < names.Length; offset += 4)
            textureNames.Add(BinaryPrimitives.ReadUInt32LittleEndian(names.AsSpan(offset, 4)));

        assets = new(repository);
        shader = UiSourceSprite.CreateShader();
        layout = new(repository, 0x60DC1202);
        mount = new UiSourceLayout(repository, 0xC6CCCD9C).Rectangle(13).Position;
        Name = "TutorialStill";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(canvas);
        illustration = new() { Name = "TutorialIllustration", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        canvas.AddChild(illustration);
        SetMeta("source_layout", layout.Source.Source.ResourceUri);
        Resized += Fit;
        Fit();
        Hide();
    }

    internal void SetIllustration(int identity)
    {
        foreach (var child in illustration.GetChildren())
        {
            illustration.RemoveChild(child);
            child.QueueFree();
        }
        Hide();
        SetMeta("source_identity", identity);
        if (identity == 0) return;

        string number = identity.ToString("D3", CultureInfo.InvariantCulture);
        string family = "texture_stl_tutorial_" + language + "_";
        string suffix = textureNames.Contains(ResourceNameHash.Compute(family + "ce_" + number)) ? "ce" :
            textureNames.Contains(ResourceNameHash.Compute(family + "pk_" + number)) ? "pk" : "";
        string pack = family + (suffix.Length == 0 ? "" : suffix + "_") + number;
        if (!textureNames.Contains(ResourceNameHash.Compute(pack)))
            throw new InvalidDataException("tutorial.still.texture_declaration:" + pack);
        string sprite = language + "_win_stl_tutorial_" + number + (suffix.Length == 0 ? "" : "_" + suffix);

        layout.Art(illustration, 1, layout.Rectangle(1, mount), assets!, shader!);
        layout.Art(illustration, 2, layout.Rectangle(2, mount), assets!, shader!,
            commonTexturePack: pack, commonSpriteOverride: sprite);
        layout.Art(illustration, 4, layout.Rectangle(4, mount), assets!, shader!);
        SetMeta("source_texture_pack", pack);
        SetMeta("source_sprite", sprite);
        Show();
    }

    private void Fit()
    {
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale;
        canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }

    public override void _ExitTree()
    {
        assets?.Dispose();
        shader?.Dispose();
    }
}
