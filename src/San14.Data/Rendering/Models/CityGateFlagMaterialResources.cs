using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>CPU input to the actual Native clear/glyph producer. Preparing or
/// writing this job does not complete a runtime material or texture group.</summary>
public sealed class CityGateFlagMaterialJob
{
    public int Manager { get; }
    public D3D11CityGateFlagMaterialKey Key { get; }
    public string RelativeDirectory { get; }
    public LoadedArmyFlagMaterialVariation Variation { get; }
    public ArmyFlagMaterialTextureUse Primary { get; }
    public IReadOnlyList<ArmyFlagMaterialTextureUse> TextureUses { get; }
    public LoadedArmyFlagGlyphAtlasPrefix? Atlas { get; }
    public LoadedArmyFlagFreshPaneLayout? Layout { get; }
    public LoadedArmyFlagEmptyPaneLayout? EmptyLayout { get; }
    public ReadOnlyMemory<byte> NativeJob { get; }

    internal CityGateFlagMaterialJob(int manager, D3D11CityGateFlagMaterialKey key, string directory,
        LoadedArmyFlagMaterialVariation variation, ArmyFlagMaterialTextureUse primary,
        IReadOnlyList<ArmyFlagMaterialTextureUse> uses, LoadedArmyFlagGlyphAtlasPrefix? atlas,
        LoadedArmyFlagFreshPaneLayout? layout, LoadedArmyFlagEmptyPaneLayout? empty, byte[] job)
        => (Manager, Key, RelativeDirectory, Variation, Primary, TextureUses, Atlas, Layout, EmptyLayout, NativeJob) =
            (manager, key, directory, variation, primary, uses, atlas, layout, empty, job);
}

public sealed class CityGateFlagMaterialRequest
{
    public D3D11CityGateFlagMaterialKey Key { get; }
    public ArmyFlagPaneNameSelection SourceName { get; }
    public IReadOnlyList<CityGateFlagMaterialJob> Jobs { get; }
    internal CityGateFlagMaterialRequest(D3D11CityGateFlagMaterialKey key, ArmyFlagPaneNameSelection name,
        CityGateFlagMaterialJob[] jobs) => (Key, SourceName, Jobs) = (key, name, Array.AsReadOnly(jobs));
}

/// <summary>Source-owned flag material jobs for the actual City/Gate and Area
/// requests. Immutable full-key directories retain old color/glyph media while
/// Native independently imports and completes newly requested materials.</summary>
public sealed class CityGateFlagMaterialResources
{
    public const uint MaterialMagic = FlagMaterialInputWriter.MaterialMagic;
    readonly GameResourceRepository repository;
    readonly CityGateAccessoryGeometryResources geometry;
    readonly FlagMaterialInputWriter writer;
    readonly LoadedObjects materials;
    readonly LoadedObjects shaderObjects;
    readonly Dictionary<(int Manager, int Force), LoadedArmyFlagMaterialVariation> variations = [];
    readonly Dictionary<D3D11CityGateFlagMaterialKey, CityGateFlagMaterialRequest> requests = [];
    public string Locale { get; }
    public LoadedArmyFlagFontSource Font => writer.Font;
    public uint PoleMaterialObjectId { get; }
    public IReadOnlyList<ArmyFlagMaterialTextureUse> PoleTextureUses { get; }

    CityGateFlagMaterialResources(GameResourceRepository repository, string locale,
        CityGateAccessoryGeometryResources geometry)
    {
        this.repository = repository; this.geometry = geometry; Locale = locale;
        writer = new(repository, locale);
        var pole = geometry.Draws.Single(d => d.MaterialManager == -1);
        PoleMaterialObjectId = geometry.MaterialGroups.ReadDeclaredDraw(pole.DrawIndex).DeclaredDefaultMaterialObjectId
            ?? throw new NotSupportedException("Accessory pole has no actual declared default material.");
        materials = repository.LoadObjects("KIDSSystemResource", ArmyFlagMaterialVariations.MaterialDictionaryId);
        shaderObjects = repository.LoadObjects("system", 0x718CCB8B);
        var material = Object(materials, PoleMaterialObjectId, 0xA8F14404);
        var textures = ModelMaterialTextureDeclaration.Read(material);
        var group = Object(materials, textures.RegularGroup, 0x3059B9C3);
        var table = repository.LoadRdbAsset("MaterialEditor", Scalar(group, 0x7A1E1EF8));
        var slots = ArmyFlagMaterialVariations.ReadTextureSlots(materials, table);
        var stage = ArmyFlagMaterialVariations.ReadStageTable(
            repository.LoadRdbAsset("MaterialEditor", textures.StageTableResource).Bytes);
        PoleTextureUses = ArmyFlagMaterialVariations.ResolveTextureUsesForActualShader(stage, slots, pole.Shader, pole.Combination);
        if (PoleTextureUses.Any(u => u.Slot.RequiresProducerImage))
            throw new NotSupportedException("Accessory default pole requires a separately produced runtime image.");
        writer.PrepareStatic("default-pole", PoleTextureUses);
    }
    public static CityGateFlagMaterialResources Load(GameResourceRepository repository, string locale,
        CityGateAccessoryGeometryResources geometry)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(geometry);
        if (locale is not ("SC" or "TC")) throw new ArgumentException("Flag material locale must be SC or TC.", nameof(locale));
        return new(repository, locale, geometry);
    }

    /// <summary>Names are the actual current World selections, including force
    /// identity, color49 and the copied UTF16 unit. No scenario is recreated.</summary>
    public IReadOnlyList<CityGateFlagMaterialRequest> Prepare(IEnumerable<ArmyFlagPaneNameSelection> actualRequests)
    {
        ArgumentNullException.ThrowIfNull(actualRequests);
        var result = new List<CityGateFlagMaterialRequest>();
        var seen = new HashSet<D3D11CityGateFlagMaterialKey>();
        foreach (var name in actualRequests)
        {
            ArgumentNullException.ThrowIfNull(name);
            if (!name.ForceValidity || name.PhysicalForce is not int force || force is < 1 or > 51 ||
                name.CurrentForceColor49 is not byte color || color > 60 || name.Glyph.NativeCopyUnitCount != 2 ||
                name.Glyph.TwoUtf16Units.Length != 2 || name.Glyph.TwoUtf16Units[1] != '\0')
                throw new InvalidDataException("Flag material request lacks its actual force, color49 or copied source glyph.");
            var key = new D3D11CityGateFlagMaterialKey(force, color, name.Glyph.TwoUtf16Units[0]);
            if (!seen.Add(key)) continue;
            if (!requests.TryGetValue(key, out var request))
            {
                var jobs = new[] { PrepareJob(0, key, name), PrepareJob(1, key, name) };
                request = new(key, name, jobs);
                requests.Add(key, request);
            }
            result.Add(request);
        }
        return result.AsReadOnly();
    }

    public static string RequestDirectory(D3D11CityGateFlagMaterialKey key, int manager)
        => FormattableString.Invariant($"requests/{key.Force}-{key.Color49}-{key.Glyph:X4}/{manager}");

    public void WriteNativeInputs(string materialSourceDirectory) => writer.WriteNativeInputs(materialSourceDirectory);

    CityGateFlagMaterialJob PrepareJob(int manager, D3D11CityGateFlagMaterialKey key,
        ArmyFlagPaneNameSelection name)
    {
        if (!variations.TryGetValue((manager, key.Force), out var variation))
        {
            variation = ArmyFlagMaterialVariations.Load(repository, geometry.Catalog, manager, key.Force, materials, shaderObjects);
            variations.Add((manager, key.Force), variation);
        }
        if (!variation.ControllerGateNonzero)
            throw new NotSupportedException("Flag controller's source row gate retains an earlier material; its current owner is required.");
        var draw = geometry.Draws.Single(d => d.MaterialManager == manager);
        var uses = ArmyFlagMaterialVariations.ResolveTextureUsesForActualShader(
            variation.StageTable, variation.Slots, draw.Shader, draw.Combination);
        string directory = RequestDirectory(key, manager);
        var job = writer.PrepareJob(manager, key.Force, (byte)key.Color49, name.Glyph,
            name.NativeSourceProvenance, directory, uses);
        return new(manager, key, directory, variation, job.Primary, job.TextureUses,
            job.Atlas, job.Layout, job.EmptyLayout, job.NativeJob);
    }

    static DokObject Object(LoadedObjects objects, uint id, uint classId)
    {
        var rows = objects.Document.Objects.Where(o => o.ObjectId == id).ToArray();
        return rows.Length == 1 && rows[0].Kind == DokObjectKind.Inline && rows[0].ClassId == classId
            ? rows[0] : throw new InvalidDataException($"Flag material object {id:X8} has no unique declared class {classId:X8}.");
    }
    static uint Scalar(DokObject item, uint hash)
    {
        var rows = item.Properties.Where(p => p.NameHash == hash).ToArray();
        return rows.Length == 1 && rows[0].TypeCode == 5 && rows[0].ElementCount == 1 && rows[0].RawValue.Length == 4
            ? BinaryPrimitives.ReadUInt32LittleEndian(rows[0].RawValue)
            : throw new InvalidDataException($"Flag material field {hash:X8} is not its declared resource reference.");
    }
}
