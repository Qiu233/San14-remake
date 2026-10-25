using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Localized UI artwork, resolved through the Common texture
/// declarations and their wrapped texture catalog rather than atlas-name ambiguity.
/// Own this source with the UI resource set and recreate it when that set changes.</summary>
public sealed class LocalizedUiSpriteSource
{
    private const string DefinitionsArchive = "KIDSSystemResource";
    private readonly GameResourceRepository _repository;
    private readonly Dictionary<uint, DokObject> _declarations;
    private readonly Dictionary<uint, DokObject> _textures;
    private readonly Dictionary<uint, uint> _names;
    private readonly Dictionary<uint, (LoadedRdbAsset Source, IReadOnlyList<SpriteDefinition> Sprites)> _atlases = [];
    private readonly Dictionary<uint,uint> _layoutTextureObjects = [];

    public LocalizedUiSpriteSource(GameResourceRepository repository)
    {
        _repository = repository;
        _declarations = repository.LoadObjects(DefinitionsArchive, 0x208BE13A).Document.Objects.ToDictionary(o => o.ObjectId);
        _textures = repository.LoadObjects(DefinitionsArchive, 0x2865E466).Document.Objects.ToDictionary(o => o.ObjectId);
        DokObject settings = _declarations[0xE0D0BB40];
        byte[] keys = Property(settings, 0x39187761).RawValue;
        byte[] values = Property(settings, 0x4066624D).RawValue;
        if (keys.Length % 4 != 0 || values.Length != keys.Length)
            throw new InvalidDataException("Common UI texture name and object tables differ in length.");
        _names = new(keys.Length / 4);
        for (int offset = 0; offset < keys.Length; offset += 4)
            _names.Add(BinaryPrimitives.ReadUInt32LittleEndian(keys.AsSpan(offset, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(values.AsSpan(offset, 4)));
    }

    public LoadedSprite LoadCommandCaption(string locale, string baseName)
    {
        string language = Language(locale);
        bool expansion = baseName switch
        {
            "000_cmn_btn_main_txt_01shussei" or "000_cmn_btn_main_txt_02seifu" or
            "000_cmn_btn_main_txt_03jinzai" or "000_cmn_btn_main_txt_04keiryaku" or
            "000_cmn_btn_main_txt_05gaikou" or "000_cmn_btn_main_txt_06kessai" => false,
            "000_cmn_btn_main_txt_07gunjo" => true,
            _ => throw new ArgumentException("Not an audited main command caption.", nameof(baseName))
        };
        string declarationName = $"texture_{(expansion ? "ce_" : "")}lang_{language}";
        return LoadTexturePackSprite(declarationName,$"{language}_{baseName}");
    }

    /// <summary>Loads the audited round-button artwork from texture_lang_ch/tw,
    /// retaining the same Common declaration and wrapper resolution as commands.</summary>
    public LoadedSprite LoadCommonCaption(string locale, string baseName)
    {
        if (baseName is not ("000_cmn_txt_decision_kettei" or "000_cmn_txt_decision_modoru" or
            "000_cmn_txt_decision_yes" or "000_cmn_txt_decision_no" or
            "000_cmn_txt_decision_next" or "000_cmn_txt_decision_shoudaku" or "000_cmn_txt_decision_jyudaku" or "000_cmn_txt_decision_kyohi" or
            "000_cmn_txt_decision_konin" or "000_cmn_txt_decision_gikyoudai"))
            throw new ArgumentException("Not an audited common round-button caption.", nameof(baseName));
        string language = Language(locale);
        if (baseName is "000_cmn_txt_decision_konin" or "000_cmn_txt_decision_gikyoudai")
            return LoadTexturePackSprite($"texture_pk_lang_{language}", $"{baseName}_{language}");
        return LoadTexturePackSprite($"texture_lang_{language}",$"{language}_{baseName}");
    }

    /// <summary>Loads a sprite from an explicit named texture-pack declaration.
    /// The caller owns stock, DLC, edit and availability routing decisions.</summary>
    public LoadedSprite LoadTexturePackSprite(string resourceName,string spriteName)
    {
        uint declaration=_names[ResourceNameHash.Compute(resourceName)];
        return LoadDeclared(_declarations[declaration],spriteName);
    }

    /// <summary>Bank zero uses the Common Settings texture object directly,
    /// rather than a texture-pack name or a global sprite-name lookup.</summary>
    public LoadedSprite LoadCommonSprite(string name, bool expansion = false)
    {
        if(expansion)
        {
            return LoadTexturePackSprite("texture_pk_common",name);
        }
        uint textureObject = Word(_declarations[0xE0D0BB40], 0x483946F1);
        return LoadDeclared(_textures[textureObject], name);
    }

    private static string Language(string locale) => locale switch
    {
        "SC" => "ch", "TC" => "tw",
        _ => throw new NotSupportedException($"UI artwork locale {locale} is not supported.")
    };

    /// <summary>Loads an explicitly local sprite from the layout's own texture
    /// declaration. Common or localized registry references use their own paths.</summary>
    public LoadedSprite LoadLayoutLocalSprite(MapMarkerLayout layout, string name)
    {
        LcskSpriteReference reference = layout.Layout.SpriteReferences.Single(r => r.Name == name);
        if (reference.Value4 != 0)
            throw new ArgumentException($"Sprite {name} is not a local layout texture reference.", nameof(name));
        uint textureObject = Word(_declarations[layout.NamedObjectId], 0xDA7D5749);
        return LoadDeclared(_textures[textureObject], name);
    }

    public LoadedSprite LoadLayoutLocalSprite(LoadedLayout layout, string name)
    {
        var reference=layout.Layout.SpriteReferences.Single(r=>r.Name==name);
        if(reference.Value4!=0)throw new ArgumentException($"Sprite {name} is not local to this layout.",nameof(name));
        if(!_layoutTextureObjects.TryGetValue(layout.Source.FileId,out uint textureObject))
        {
            var owners=_declarations.Values.Where(d=>d.Properties.Any(p=>p.NameHash==0x15BE61AD&&
                p.RawValue.Length==4&&BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue)==layout.Source.FileId)).ToArray();
            var textures=owners.Select(d=>Word(d,0xDA7D5749)).Distinct().ToArray();
            if(textures.Length!=1)throw new InvalidDataException("Layout local texture owner is missing or ambiguous.");
            textureObject=textures[0];_layoutTextureObjects.Add(layout.Source.FileId,textureObject);
        }
        return LoadDeclared(_textures[textureObject],name);
    }

    private LoadedSprite LoadDeclared(DokObject declaration, string name)
    {
        var visited = new HashSet<uint>();
        while (true)
        {
            if (declaration.ClassId != 0x79991E79 || !visited.Add(declaration.ObjectId))
                throw new InvalidDataException($"Invalid wrapped UI texture declaration for {name}.");
            uint wrapped = Word(declaration, 0x2856A1B3);
            if (wrapped == 0) break;
            declaration = _textures.TryGetValue(wrapped, out DokObject? target) ? target :
                throw new InvalidDataException($"Wrapped UI texture object {wrapped:X8} is missing.");
        }
        uint textureId = Word(declaration, 0xC8BB66DA);
        uint atlasId = Word(declaration, 0x2276FC56);
        if (textureId == 0 || atlasId == 0)
            throw new InvalidDataException($"UI texture declaration for {name} has no texture or atlas.");
        if (!_atlases.TryGetValue(atlasId, out var atlas))
        {
            LoadedRdbAsset source = _repository.LoadRdbAsset("ScreenLayout", atlasId);
            atlas = (source, SpriteAtlasReader.Read(source.Bytes));
            _atlases.Add(atlasId, atlas);
        }
        SpriteDefinition definition = atlas.Sprites.SingleOrDefault(s => s.Name == name)
            ?? throw new InvalidDataException($"Declared UI atlas {atlasId:X8} does not contain {name}.");
        if (definition.Value64 > int.MaxValue)
            throw new InvalidDataException($"Sprite {name} has an invalid texture index.");
        LoadedTexture texture = _repository.LoadTexture("ScreenLayout", textureId, (int)definition.Value64);
        _ = definition.GetRectangle(texture.Texture.Width, texture.Texture.Height);
        return new LoadedSprite(definition, texture, atlas.Source);
    }

    private static DokProperty Property(DokObject source, uint hash) => source.Properties.Single(p => p.NameHash == hash);
    private static uint Word(DokObject source, uint hash)
    {
        DokProperty property = Property(source, hash);
        if (property.ElementSize != 4 || property.ElementCount != 1 || property.RawValue.Length != 4)
            throw new InvalidDataException($"UI texture property {hash:X8} is not a scalar word.");
        return BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
    }
}
