using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A verified fresh source Camera construction and its exact audited CPU words.</summary>
public sealed class LoadedMapCameraConstructionSource
{
    private readonly IReadOnlyDictionary<int, uint> words;

    internal LoadedMapCameraConstructionSource(LoadedObjects resource, DokObject camera,
        IReadOnlyDictionary<int, uint> words, string constructorOrigin)
    {
        Resource = resource;
        Camera = camera;
        this.words = words;
        ConstructorOrigin = constructorOrigin;
    }

    public LoadedObjects Resource { get; }
    public DokObject Camera { get; }
    public string ConstructorOrigin { get; }

    /// <summary>Return an audited source CPU word after the literal constructor and property loader.
    /// Unproved offsets have no implicit zero or generic default.</summary>
    public uint CpuWord(int offset) => words.TryGetValue(offset, out uint value) ? value :
        throw new InvalidDataException($"Source Camera CPU offset {offset:X} has no audited construction binding.");
}

/// <summary>Read the explicitly selected stock system/908AB6E2 Perspective Camera.
/// Bindings are retained literals from the paired native descriptor, loader and constructor audit;
/// serialized quaternion components are copied without normalization.</summary>
public static class MapCameraConstructionSource
{
    private sealed class LoadedCameraDeclarations
    {
        public readonly object Gate=new();
        public readonly Dictionary<uint,LoadedMapCameraConstructionSource> Cameras=new();
    }
    private static readonly ConditionalWeakTable<LoadedObjects,LoadedCameraDeclarations> LoadedDeclarations = new();
    public const uint SourceFileId = 0x908AB6E2;
    public const uint PerspectiveCameraClassId = 0x23E7C64E;
    public const string SourceResourceUri = "san14://rdb/system/908AB6E2";
    public const string SourceSha256 = "3CB072E68CA9A2086D0230E3F9F86AD1B9B10E08CEA521A474CE1057A0E23B06";

    public static LoadedMapCameraConstructionSource ReadPerspective(LoadedObjects resource, uint objectId)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (objectId is not (0x38D04576u or 0x1F3FB203u or 0x937EEA62u))
            throw new InvalidDataException("Source Camera construction requires one explicitly selected stock Perspective Camera.");
        var loaded=LoadedDeclarations.GetValue(resource,_=>new());
        lock(loaded.Gate)
        {
            if(loaded.Cameras.TryGetValue(objectId,out var found))return found;
            var source=ReadLoadedPerspective(resource,objectId);
            loaded.Cameras.Add(objectId,source);
            // The reader's private canonical Resource is the same imported
            // generation. Accepted Camera prefix copies must not import that
            // DOK a second time; pose/lens/matrices remain current packet data.
            LoadedDeclarations.Add(source.Resource,loaded);
            return source;
        }
    }

    private static LoadedMapCameraConstructionSource ReadLoadedPerspective(LoadedObjects resource,uint objectId)
    {
        LoadedRdbAsset source = resource.Source;
        if (source is null || source.Bytes is null || source.FileId != SourceFileId ||
            source.ResourceUri != SourceResourceUri ||
            source.OverrideSource is not null || source.IndexSource is null || source.IndexSource.IsOverride)
            throw new InvalidDataException("Source Camera construction requires the original system908AB6E2 resource identity without overrides.");

        // Keep the decoded object and word values in the same resource generation
        // if the caller later edits its arrays.
        LoadedRdbAsset ownedSource = source with { Bytes = source.Bytes.ToArray() };
        DokDocument canonical = DokReader.Read(ownedSource.Bytes);
        VerifyDocument(resource.Document, canonical);
        var declaration = ReadCameraDeclaration(canonical, objectId);
        var ownedResource = new LoadedObjects(canonical, ownedSource);
        string origin = SourceResourceUri + $"#object/{objectId:X8}@{source.DecodedSha256};" +
            "fresh-source-constructor=SC:C3D610/TC:C3BF60;" +
            "property-loader=SC:C50CF0/TC:C4F6F0;" +
            "native-descriptors=SC:C89DA0/TC:C88780;literal1240=SC:C3F6C2";
        return new(ownedResource, declaration.Camera, declaration.Words, origin);
    }

    internal static (DokObject Camera, IReadOnlyDictionary<int, uint> Words) ReadCameraDeclaration(
        DokDocument document, uint objectId)
    {
        DokObject[] selected = document.Objects.Where(item => item.ObjectId == objectId).ToArray();
        if (selected.Length != 1 || selected[0].Kind != DokObjectKind.Inline ||
            selected[0].ClassId != PerspectiveCameraClassId || selected[0].Reference is not null ||
            selected[0].Properties.Count != 1111)
            throw new InvalidDataException("Source Camera construction requires its original inline1111-property Perspective Camera.");
        DokObject camera = selected[0];
        var values = new Dictionary<int, uint>(Bindings.Length + 1);
        foreach (CpuBinding binding in Bindings)
        {
            DokProperty property = camera.Properties[binding.Ordinal];
            if (property.Index != binding.Ordinal || property.NameHash != binding.NameHash ||
                property.TypeCode != binding.TypeCode || property.ElementCount != binding.ElementCount ||
                property.ElementSize != 4 || property.RawValue.Length != binding.ElementCount * 4 ||
                (uint)binding.Component >= binding.ElementCount)
                throw new InvalidDataException($"Source Camera property {binding.Ordinal} differs from its audited CPU binding.");
            values.Add(binding.Offset, BinaryPrimitives.ReadUInt32LittleEndian(
                property.RawValue.AsSpan(binding.Component * 4, 4)));
        }
        // The only unbound retained word is explicitly cleared by the source
        // constructor at SC C3F6C2, inside the paired C3D610/C3BF60 constructors.
        values.Add(0x1240, 0u);
        return (camera, values);
    }

    private static void VerifyDocument(DokDocument supplied, DokDocument canonical)
    {
        if (supplied is null || supplied.FormatCode != canonical.FormatCode ||
            supplied.Identifier != canonical.Identifier || !EqualBytes(supplied.RawHeader, canonical.RawHeader) ||
            supplied.Objects is null || supplied.Objects.Count != canonical.Objects.Count)
            throw new InvalidDataException("Supplied Camera DOK document metadata differs from its source bytes.");
        for (int i = 0; i < canonical.Objects.Count; i++)
        {
            DokObject a = supplied.Objects[i], b = canonical.Objects[i];
            if (a is null || a.Index != b.Index || a.Offset != b.Offset || a.Length != b.Length ||
                a.ObjectId != b.ObjectId || a.ClassId != b.ClassId || a.Kind != b.Kind || a.Reference != b.Reference ||
                !EqualBytes(a.RawHeader, b.RawHeader) || !EqualBytes(a.Padding, b.Padding) ||
                a.Properties is null || a.Properties.Count != b.Properties.Count)
                throw new InvalidDataException("Supplied Camera DOK object metadata differs from its source bytes.");
            for (int j = 0; j < b.Properties.Count; j++)
            {
                DokProperty x = a.Properties[j], y = b.Properties[j];
                if (x is null || x.Index != y.Index || x.DescriptorOffset != y.DescriptorOffset ||
                    x.ValueOffset != y.ValueOffset || x.TypeCode != y.TypeCode || x.ElementCount != y.ElementCount ||
                    x.NameHash != y.NameHash || x.ElementSize != y.ElementSize ||
                    !EqualBytes(x.RawDescriptor, y.RawDescriptor) || !EqualBytes(x.RawValue, y.RawValue))
                    throw new InvalidDataException("Supplied Camera DOK property metadata differs from its source bytes.");
            }
        }
    }

    private static bool EqualBytes(byte[]? a, byte[] b) => a is not null && a.AsSpan().SequenceEqual(b);

    private readonly record struct CpuBinding(int Offset, int Ordinal, uint NameHash,
        uint TypeCode, int ElementCount, int Component);

    // Exact retained offsets from source-properties-loader.json cameras.words.
    // Native indexed descriptor cases independently prove every used ordinal,
    // hash, type and count in source-properties-reflection.json (both profiles).
    // Repeated ordinals identify individual raw float32/vector components.
    private static readonly CpuBinding[] Bindings =
    [
        new(0x0EB8, 932, 0x2572A4E9u, 5u, 1, 0),
        new(0x0EBC, 933, 0xDF7EBEEDu, 5u, 1, 0),
        new(0x000C, 2, 0x04C1A415u, 8u, 1, 0),
        new(0x0010, 3, 0x06741408u, 8u, 1, 0),
        new(0x0014, 4, 0x003546E9u, 8u, 1, 0),
        new(0x0018, 7, 0x0624AD1Du, 8u, 1, 0),
        new(0x001C, 8, 0xBAE710B0u, 8u, 1, 0),
        new(0x0020, 9, 0xB90D1027u, 8u, 1, 0),
        new(0x0024, 10, 0xABB91759u, 8u, 1, 0),
        new(0x0028, 11, 0x3DF6821Au, 8u, 1, 0),
        new(0x002C, 88, 0x462994C1u, 8u, 1, 0),
        new(0x0030, 12, 0xC96AEEA5u, 8u, 1, 0),
        new(0x0034, 13, 0x721A508Au, 8u, 1, 0),
        new(0x0038, 14, 0xA0E5DA98u, 8u, 1, 0),
        new(0x003C, 15, 0x16B54787u, 8u, 3, 0),
        new(0x0040, 15, 0x16B54787u, 8u, 3, 1),
        new(0x0044, 15, 0x16B54787u, 8u, 3, 2),
        new(0x0048, 16, 0xD40D74C8u, 8u, 3, 0),
        new(0x004C, 16, 0xD40D74C8u, 8u, 3, 1),
        new(0x0050, 16, 0xD40D74C8u, 8u, 3, 2),
        new(0x0054, 17, 0x1951CDF3u, 4u, 1, 0),
        new(0x0058, 18, 0xDF73CE78u, 8u, 1, 0),
        new(0x005C, 19, 0x87454A68u, 8u, 1, 0),
        new(0x0060, 20, 0xA7C1A130u, 8u, 1, 0),
        new(0x0064, 28, 0x62AA34F8u, 8u, 1, 0),
        new(0x0068, 29, 0xF9989A1Cu, 8u, 1, 0),
        new(0x006C, 30, 0x13AC572Eu, 8u, 1, 0),
        new(0x0070, 31, 0xCD8B987Fu, 8u, 1, 0),
        new(0x0074, 54, 0x907B67C1u, 8u, 1, 0),
        new(0x0078, 55, 0x800DB8FFu, 8u, 1, 0),
        new(0x007C, 850, 0x2F6A6AF9u, 8u, 1, 0),
        new(0x0080, 851, 0xDF989EE1u, 8u, 1, 0),
        new(0x0084, 852, 0xAC082408u, 8u, 1, 0),
        new(0x0088, 853, 0x4669C572u, 8u, 1, 0),
        new(0x008C, 891, 0x90B9A1E5u, 8u, 1, 0),
        new(0x0090, 914, 0xEA5C2C8Du, 8u, 1, 0),
        new(0x0094, 923, 0x53E41154u, 8u, 1, 0),
        new(0x0098, 928, 0x273D81CEu, 8u, 1, 0),
        new(0x009C, 930, 0xBBE67AEDu, 8u, 1, 0),
        new(0x00A0, 931, 0x0C3B06E3u, 8u, 1, 0),
        new(0x00A4, 1047, 0x93B62B81u, 8u, 1, 0),
        new(0x00A8, 1048, 0xF8D97B10u, 8u, 1, 0),
        new(0x00AC, 826, 0x085B6991u, 8u, 1, 0),
        new(0x00B0, 827, 0x1EB4D0F0u, 8u, 1, 0),
        new(0x00B4, 828, 0x4170AF46u, 8u, 1, 0),
        new(0x00B8, 829, 0xFEEDD601u, 8u, 1, 0),
        new(0x00BC, 830, 0xE599A7F7u, 8u, 1, 0),
        new(0x00C0, 831, 0xF3554D1Du, 8u, 1, 0),
        new(0x00C4, 832, 0xBF2E4097u, 8u, 1, 0),
        new(0x00C8, 939, 0x3C750447u, 4u, 1, 0),
        new(0x00CC, 904, 0xC579866Bu, 8u, 1, 0),
        new(0x00D0, 973, 0xC9B352ACu, 8u, 1, 0),
        new(0x00D4, 974, 0x7F2CA7D3u, 8u, 1, 0),
        new(0x00D8, 896, 0x49E1FAACu, 8u, 1, 0),
        new(0x00DC, 897, 0x502747A5u, 8u, 1, 0),
        new(0x00E0, 898, 0xFFC8C20Du, 8u, 1, 0),
        new(0x00E4, 899, 0xB7299006u, 8u, 1, 0),
        new(0x00E8, 900, 0xF57EBD90u, 8u, 1, 0),
        new(0x00EC, 901, 0x591F7389u, 8u, 1, 0),
        new(0x00F0, 915, 0xFAF89ED8u, 8u, 1, 0),
        new(0x00F4, 919, 0x5E26AA91u, 8u, 1, 0),
        new(0x00F8, 920, 0x1F9BB007u, 8u, 1, 0),
        new(0x00FC, 921, 0xBC6DBC69u, 8u, 1, 0),
        new(0x0100, 924, 0x96A26C16u, 8u, 1, 0),
        new(0x0104, 925, 0x2F330B61u, 8u, 1, 0),
        new(0x0108, 926, 0x85FCC148u, 8u, 3, 0),
        new(0x010C, 926, 0x85FCC148u, 8u, 3, 1),
        new(0x0110, 926, 0x85FCC148u, 8u, 3, 2),
        new(0x0114, 927, 0x9BFA4BC8u, 8u, 3, 0),
        new(0x0118, 927, 0x9BFA4BC8u, 8u, 3, 1),
        new(0x011C, 927, 0x9BFA4BC8u, 8u, 3, 2),
        new(0x0120, 934, 0xA48ECB2Au, 8u, 1, 0),
        new(0x0124, 854, 0x810B5624u, 8u, 1, 0),
        new(0x0128, 840, 0xAA84CF1Cu, 8u, 1, 0),
        new(0x012C, 841, 0x71003548u, 8u, 1, 0),
        new(0x0130, 902, 0x7C80D02Bu, 8u, 1, 0),
        new(0x0134, 903, 0x484B95EBu, 8u, 1, 0),
        new(0x0138, 976, 0x97D65DD2u, 8u, 1, 0),
        new(0x013C, 1056, 0x7C1C051Eu, 8u, 1, 0),
        new(0x0140, 1057, 0x38E69314u, 8u, 1, 0),
        new(0x0144, 1058, 0xC4DB66F5u, 8u, 2, 0),
        new(0x0148, 1058, 0xC4DB66F5u, 8u, 2, 1),
        new(0x014C, 1059, 0xA5B5D92Au, 8u, 2, 0),
        new(0x0150, 1059, 0xA5B5D92Au, 8u, 2, 1),
        new(0x0154, 905, 0xBE47DAC5u, 4u, 1, 0),
        new(0x0158, 864, 0x0B6C81F1u, 4u, 1, 0),
        new(0x015C, 842, 0x2F288D0Cu, 4u, 1, 0),
        new(0x0160, 843, 0x48457126u, 4u, 1, 0),
        new(0x0164, 956, 0xDFC9D831u, 4u, 1, 0),
        new(0x01A8, 100, 0x307216E4u, 8u, 1, 0),
        new(0x01AC, 101, 0x77CBE856u, 8u, 1, 0),
        new(0x01B0, 102, 0x350DC6CEu, 8u, 1, 0),
        new(0x01B4, 103, 0xE9D823E9u, 8u, 1, 0),
        new(0x051C, 49, 0x4226C411u, 8u, 1, 0),
        new(0x0520, 50, 0x468E5E0Au, 8u, 1, 0),
        new(0x0524, 51, 0xD66B33B7u, 8u, 1, 0),
        new(0x0BE8, 373, 0x09D8471Eu, 8u, 1, 0),
        new(0x0BEC, 374, 0x4CFD293Au, 8u, 1, 0),
        new(0x0BF0, 165, 0xF3CBBCABu, 4u, 1, 0),
        new(0x1088, 421, 0xB406C48Au, 4u, 1, 0),
        new(0x10EC, 1092, 0xBC94863Eu, 4u, 1, 0),
        new(0x10F0, 1093, 0xF9B44A5Du, 4u, 1, 0),
        new(0x10F4, 1094, 0xDD69E8A8u, 8u, 1, 0),
        new(0x10F8, 1095, 0xC33E91F7u, 8u, 1, 0),
        new(0x12E8, 5, 0xBA7543EAu, 8u, 1, 0),
        new(0x12EC, 6, 0xA70685F8u, 8u, 1, 0),
        new(0x12FC, 0, 0x27D889E5u, 8u, 3, 0),
        new(0x1300, 0, 0x27D889E5u, 8u, 3, 1),
        new(0x1304, 0, 0x27D889E5u, 8u, 3, 2),
        new(0x1308, 1, 0xF52EA910u, 8u, 4, 0),
        new(0x130C, 1, 0xF52EA910u, 8u, 4, 1),
        new(0x1310, 1, 0xF52EA910u, 8u, 4, 2),
        new(0x1314, 1, 0xF52EA910u, 8u, 4, 3),
    ];
}
