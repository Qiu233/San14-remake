using System.Buffers.Binary;
using System.Collections.Concurrent;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The actual named renderer's Star shader and selected controller's
/// point stream. Its optional Star descriptor retains zero package assignments.</summary>
public sealed class SkyStarResources
{
    private readonly ConcurrentDictionary<int, SkyStarProgramSelection> selections = new();
    private readonly LoadedObjects definitions;
    private readonly G1ShaderBindingTables tables;
    private MapSeasonCameraPackage acceptedPackage;
    private MapSeasonCameraPackage acceptedControllerPackage;

    private SkyStarResources(LoadedShaderContainer shader, LoadedObjects definitions,
        MapSeasonCameraPackage package, uint controller, SkyStarGeometry? geometry,
        LoadedSkySamplingResource? texture)
    {
        Shader = shader; this.definitions = definitions; acceptedPackage = package; acceptedControllerPackage = package;
        tables = shader.ReadG1Bindings();
        ControllerObjectId = controller; Geometry = geometry; StarTexture = texture;
    }

    public LoadedShaderContainer Shader { get; }
    public uint ControllerObjectId { get; }
    public SkyStarGeometry? Geometry { get; }
    public LoadedSkySamplingResource? StarTexture { get; }

    /// <summary>A missing shader declaration means no prepared Star program;
    /// it does not assert absence of the separate generated-stream CPU state.</summary>
    public static SkyStarResources? Load(GameResourceRepository repository,
        NamedViewRendererResources renderer, MapSeasonCameraPackage package)
        => Load(repository, renderer, package, true);

    /// <summary>WORK does not install the package's Main Star texture. Import
    /// only the shader and controller points consumed by its untextured pass.</summary>
    public static SkyStarResources? LoadControllerPass(GameResourceRepository repository,
        NamedViewRendererResources renderer, MapSeasonCameraPackage package)
        => Load(repository, renderer, package, false);

    private static SkyStarResources? Load(GameResourceRepository repository,
        NamedViewRendererResources renderer, MapSeasonCameraPackage package, bool importSampling)
    {
        var declaration = renderer.Definitions.Document.Objects.Single(value => value.ObjectId == renderer.RendererObjectId);
        uint shaderKey = Key(declaration, 0xE0918EA2);
        if (shaderKey == 0) return null;
        uint controller = Key(package.Camera, 0x8D316B8D);
        var geometry = ReadGeometry(repository, renderer.Definitions, controller);
        var sampling = importSampling ? SkySeasonPackageTextures.Read(package).Star : null;
        return new(repository.LoadShaderContainer("system", shaderKey), renderer.Definitions, package,
            controller, geometry, sampling is null || sampling.IsSourceReferenceZero ? null : SkySeasonPackageTextures.LoadSampling(repository, sampling));
    }

    /// <summary>The actual exposure setter supplies the one-word variant.
    /// Pass zero consumes controller points; pass one consumes generated points.</summary>
    public SkyStarProgramSelection Select(uint pass, bool exposure)
    {
        if (pass > 1) throw new ArgumentOutOfRangeException(nameof(pass));
        Span<byte> condition = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(condition, exposure ? 1u : 0u);
        var variant = tables.Variants.Match(condition) ??
            throw new NotSupportedException("The selected Star shader has no current exposure variant.");
        var combination = tables.ResolveCombination([0, 0, 0, 0, 0, 0, (uint)variant.Index], pass);
        return selections.GetOrAdd(combination.Index, _ => new(this, combination));
    }

    /// <summary>Check new package resources once, before current values reach
    /// an installed device generation. Null texture references skip assignment.</summary>
    public void RequireCompatiblePackage(GameResourceRepository repository, MapSeasonCameraPackage package)
    {
        if (ReferenceEquals(package, acceptedPackage)) return;
        RequireCompatibleController(repository, package);
        var sampling = SkySeasonPackageTextures.Read(package).Star;
        if (!sampling.IsSourceReferenceZero)
        {
            var texture = SkySeasonPackageTextures.LoadSampling(repository, sampling);
            if (StarTexture is null || StarTexture.Texture.Identity != texture.Texture.Identity ||
                StarTexture.Texture.SamplerDescriptorSha256 != texture.Texture.SamplerDescriptorSha256)
                throw new NotSupportedException("Changed Star sampling requires scene resource replacement.");
        }
        acceptedPackage = package;
    }

    /// <summary>Check the actual point-stream generation independently of Main
    /// texture assignments. A WORK borrower never consumes those assignments.</summary>
    public void RequireCompatibleController(GameResourceRepository repository, MapSeasonCameraPackage package)
    {
        if (ReferenceEquals(package, acceptedControllerPackage)) return;
        var candidate = ReadGeometry(repository, definitions, Key(package.Camera, 0x8D316B8D));
        if (Geometry?.SourceResourceUri != candidate?.SourceResourceUri || Geometry?.SourceSha256 != candidate?.SourceSha256)
            throw new NotSupportedException("Changed Star controller points require scene resource replacement.");
        acceptedControllerPackage = package;
    }

    private static SkyStarGeometry? ReadGeometry(GameResourceRepository repository, LoadedObjects definitions, uint controllerKey)
    {
        if (controllerKey == 0) return null;
        var controller = definitions.Document.Objects.Single(value => value.ObjectId == controllerKey);
        if (controller.Kind != DokObjectKind.Inline || controller.ClassId != 0x12BD9185)
            throw new NotSupportedException("Star geometry requires an inline Sky controller declaration.");
        uint key = Key(controller, 0x78051B9F);
        return key == 0 ? null : SkyStarGeometry.Read(repository.LoadRdbAsset("system", key));
    }

    private static uint Key(DokObject owner, uint hash)
    {
        var field = owner.Properties.Single(value => value.NameHash == hash);
        if (field.TypeCode != 5 || field.ElementCount != 1 || field.ElementSize != 4 || field.RawValue.Length != 4)
            throw new InvalidDataException($"Star resource reference {hash:X8} requires a type5 scalar key.");
        return BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
    }
}

/// <summary>One actual vertex, geometry and pixel program combination, imported
/// once for both static media and current material destinations.</summary>
public sealed class SkyStarProgramSelection
{
    internal SkyStarProgramSelection(SkyStarResources resources, G2ShaderCombination combination)
    {
        Resources = resources; Combination = combination;
        if (combination.Stages.Count(value => value.Entry is not null) != 3 ||
            combination.Stages.Any(value => value.Entry is not null && value.StageCode > 2))
            throw new NotSupportedException("Main Stars require actual vertex, geometry and pixel programs.");
        VertexProgram = Program(1); GeometryProgram = Program(2); PixelProgram = Program(0);
        VertexDefinitions = resources.Shader.ReadResourceDefinitions(VertexProgram);
        GeometryDefinitions = resources.Shader.ReadResourceDefinitions(GeometryProgram);
        PixelDefinitions = resources.Shader.ReadResourceDefinitions(PixelProgram);
        MaterialLayout = SkyStarMaterialLayout.Import(VertexDefinitions, GeometryDefinitions, PixelDefinitions);
        int Program(uint stage) => combination.Stages.Single(value => value.StageCode == stage).Entry?.ProgramIndex ??
            throw new NotSupportedException("A selected Main Star stage is absent.");
    }
    public SkyStarResources Resources { get; }
    public G2ShaderCombination Combination { get; }
    public int VertexProgram { get; }
    public int GeometryProgram { get; }
    public int PixelProgram { get; }
    public DxbcResourceDefinitions VertexDefinitions { get; }
    public DxbcResourceDefinitions GeometryDefinitions { get; }
    public DxbcResourceDefinitions PixelDefinitions { get; }
    public SkyStarMaterialLayout MaterialLayout { get; }
    public byte[] CopyProgramBytes(uint stage) => Resources.Shader.Container.GetProgramBytes(Resources.Shader.Source.Bytes,
        stage switch { 1 => VertexProgram, 2 => GeometryProgram, 0 => PixelProgram, _ => throw new ArgumentOutOfRangeException(nameof(stage)) }).ToArray();
}
