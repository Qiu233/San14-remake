using System.Buffers.Binary;
using System.Globalization;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Normal-resource publications for the twenty title poem events.
/// The small configuration directory was extracted offline and expressed as
/// named inputs; neither an executable nor a campaign is read at runtime.</summary>
public sealed class PoemEventCatalog
{
    private sealed record Configuration(int LayoutIndex, int ItemId,
        int MaximumScrollX, int FirstScrollImage, int ScrollImageCount);

    private static readonly Configuration[] Configurations =
    [
        new(1, 132, 0, 1, 1),
        new(2, 133, 0, 2, 1),
        new(3, 134, 0, 3, 1),
        new(4, 135, 971, 4, 2),
        new(5, 136, 0, 6, 1),
        new(6, 137, 0, 7, 1),
        new(7, 138, 8620, 8, 6),
        new(8, 139, 5215, 14, 4),
        new(9, 140, 0, 18, 1),
        new(10, 141, 3748, 19, 3),
        new(12, 142, 0, 40, 1),
        new(13, 143, 0, 41, 1),
        new(11, 144, 32394, 22, 18),
        new(15, 145, 0, 43, 1),
        new(14, 146, 0, 42, 1),
        new(20, 147, 0, 48, 1),
        new(16, 148, 0, 44, 1),
        new(17, 149, 0, 45, 1),
        new(18, 150, 0, 46, 1),
        new(19, 151, 0, 47, 1)
    ];

    private readonly GameResourceRepository repository;
    private readonly CollectionCatalog collection;
    private readonly Dictionary<int, PoemEventPublication> publications = [];
    private LoadedLayout? infoLayout;
    private ImageSources? imageSources;

    public string Locale => collection.Locale;
    public IReadOnlyList<int> ItemIds { get; }

    private PoemEventCatalog(GameResourceRepository repository, string locale)
    {
        this.repository = repository;
        collection = CollectionCatalog.Load(repository, locale);
        ItemIds = Array.AsReadOnly(collection.Poems.Select(poem => poem.ItemId).ToArray());
    }

    /// <summary>Own this catalog with the title's resource set and recreate it
    /// after resource overrides change. Each selected event is loaded once.</summary>
    public static PoemEventCatalog Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new(repository, locale);
    }

    public PoemEventPublication Read(int itemId)
    {
        if (publications.TryGetValue(itemId, out var found)) return found;
        var poem = collection.ReadPoem(itemId);
        var config = Configurations.Single(row => row.ItemId == itemId);
        string layoutName = "09_01_03_event_poems_" +
            config.LayoutIndex.ToString("D2", CultureInfo.InvariantCulture) + "_cn";
        var layout = MapMarkerLayout.LoadNamed(repository, layoutName);
        var images = new PoemEventImage[config.ScrollImageCount];
        int firstNode = images.Length < 2 ? 2 : 7;
        for (int index = 0; index < images.Length; index++)
        {
            int imageId = config.FirstScrollImage + index;
            imageSources ??= new(repository);
            images[index] = new(firstNode + index * 3,
                imageSources.Resolve(config.LayoutIndex, index + 1, imageId));
        }
        infoLayout ??= MapMarkerLayout.LoadNamed(repository, "02_07_06_info_sibun_cn");
        var result = new PoemEventPublication(itemId, config.LayoutIndex, layoutName,
            layout, config.MaximumScrollX, Array.AsReadOnly(images),
            new(infoLayout, poem, collection.DescriptionCaption))
        {
            Animations = PoemEventAnimationReader.Read(layout.Layout)
        };
        publications.Add(itemId, result);
        return result;
    }

    // Kind29's registry alias and the actual atlas sprite name are different.
    // Follow its explicit normal declaration/wrapper, retaining the installed
    // language artwork rather than fabricating a translated sprite suffix.
    private sealed class ImageSources
    {
        private readonly GameResourceRepository repository;
        private readonly Dictionary<uint, DokObject> declarations;
        private readonly Dictionary<uint, DokObject> textures;
        private readonly Dictionary<uint, uint> names;
        public ImageSources(GameResourceRepository repository)
        {
            this.repository = repository;
            declarations = repository.LoadObjects("KIDSSystemResource", 0x208BE13A).Document.Objects.ToDictionary(o => o.ObjectId);
            textures = repository.LoadObjects("KIDSSystemResource", 0x2865E466).Document.Objects.ToDictionary(o => o.ObjectId);
            var settings = declarations[0xE0D0BB40];
            byte[] keys = Property(settings, 0x39187761), values = Property(settings, 0x4066624D);
            if (keys.Length % 4 != 0 || values.Length != keys.Length)
                throw new InvalidDataException("poem-event.image.declaration_table");
            names = [];
            for (int i = 0; i < keys.Length; i += 4)
                names.Add(BinaryPrimitives.ReadUInt32LittleEndian(keys.AsSpan(i, 4)),
                    BinaryPrimitives.ReadUInt32LittleEndian(values.AsSpan(i, 4)));
        }
        public CollectionImageRoute Resolve(int group, int part, int graphicId)
        {
            string pack = "texture_stl_event_poems_" + group.ToString("D2", CultureInfo.InvariantCulture) +
                "_" + part.ToString("D3", CultureInfo.InvariantCulture);
            if (!names.TryGetValue(ResourceNameHash.Compute(pack), out uint objectId))
                throw new InvalidDataException("poem-event.image.named_declaration:" + pack);
            var declaration = declarations[objectId];
            var visited = new HashSet<uint>();
            while (true)
            {
                if (declaration.ClassId != 0x79991E79 || !visited.Add(declaration.ObjectId))
                    throw new InvalidDataException("poem-event.image.wrapped_declaration");
                uint wrapped = Word(declaration, 0x2856A1B3);
                if (wrapped == 0) break;
                declaration = textures.TryGetValue(wrapped, out var target) ? target
                    : throw new InvalidDataException("poem-event.image.wrapper_target");
            }
            uint atlasId = Word(declaration, 0x2276FC56);
            var atlas = SpriteAtlasReader.Read(repository.LoadRdbAsset("ScreenLayout", atlasId).Bytes);
            if (atlas.Count != 1) throw new InvalidDataException("poem-event.image.single_scroll_sprite");
            return new(pack, atlas[0].Name, 29, graphicId);
        }
        private static byte[] Property(DokObject owner, uint hash) => owner.Properties.Single(p => p.NameHash == hash).RawValue;
        private static uint Word(DokObject owner, uint hash)
        {
            byte[] value = Property(owner, hash);
            if (value.Length != 4) throw new InvalidDataException("poem-event.image.scalar_declaration");
            return BinaryPrimitives.ReadUInt32LittleEndian(value);
        }
    }
}
