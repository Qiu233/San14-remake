using System.Buffers.Binary;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The actual current Unit-selected key and source text. For category
/// three, SourceName is the carried name while force/color still belong to Unit.</summary>
public sealed record ArmyFlagMaterialRequest(int Category, int ForceVariant, byte? Color49,
    ArmyFlagPaneNameSelection SourceName, bool CarriedKind2Text);

public readonly record struct ArmyMaterialKey(int Category, int ForceVariant, int Color49, ushort Glyph);

public sealed record ArmyMaterialTemplate(int DrawIndex, uint TableObjectId, string TableResourceUri, int Role,
    string RelativeDirectory, int Queue, uint AllowedRecords, string? UnsupportedReason);

public sealed class ArmyMaterialJob
{
    internal ArmyMaterialJob(int manager, string directory, LoadedArmyFlagMaterialVariation variation,
        LoadedArmyMaterialReadySource declarations, FlagMaterialPreparedJob glyph,
        IReadOnlyList<ArmyMaterialTemplate> templates)
        => (Manager, RelativeDirectory, Variation, Declarations, Primary, TextureUses, Atlas, Templates) =
            (manager, directory, variation, declarations, glyph.Primary, glyph.TextureUses, glyph.Atlas, templates);
    public int Manager { get; }
    public string RelativeDirectory { get; }
    public LoadedArmyFlagMaterialVariation Variation { get; }
    public LoadedArmyMaterialReadySource Declarations { get; }
    public ArmyFlagMaterialTextureUse Primary { get; }
    public IReadOnlyList<ArmyFlagMaterialTextureUse> TextureUses { get; }
    public LoadedArmyFlagGlyphAtlasPrefix? Atlas { get; }
    public IReadOnlyList<ArmyMaterialTemplate> Templates { get; }
}

public sealed record ArmyMaterialRequestResources(ArmyMaterialKey Key,
    ArmyFlagMaterialRequest Source, IReadOnlyList<ArmyMaterialJob> Jobs);

/// <summary>Actual Army Material declarations and complete candidate draw media.
/// The Native owner performs dependency readiness, glyph execution and selected
/// template installation before registering completion for a current full key.</summary>
public sealed class ArmyMaterialResources
{
    public const uint StateMagic = 0x33534D41; // AMS3: source declarations with the actual stage-table resource.
    readonly GameResourceRepository repository;
    readonly ArmyGeometryResources geometry;
    readonly LoadedObjects materials, shaderObjects;
    readonly LoadedArmyMaterialParameterNativeSource parameterRules;
    readonly FlagMaterialInputWriter inputs;
    readonly Dictionary<string, byte[]> drawFiles = new(StringComparer.Ordinal);
    readonly D3D11ModelResourceWriter drawWriter;
    readonly Dictionary<(int Manager, int Force), LoadedArmyFlagMaterialVariation> variations = [];
    readonly Dictionary<ArmyMaterialKey, ArmyMaterialRequestResources> requests = [];
    readonly Dictionary<uint, (LoadedArmyMaterialReadySource Ready, LoadedArmyMaterialParameterSource Parameters)> declarationsByMaterial = [];
    readonly Dictionary<(int Category, int Manager, int Force, int Draw, int Role),
        (ArmyGeometryDrawResources Draw, IReadOnlyList<ArmyFlagMaterialTextureUse> Uses, byte[] Stage2Pixel)> preparedDraws = [];

    public string Locale { get; }
    public LoadedArmyFlagFontSource Font => inputs.Font;

    ArmyMaterialResources(GameResourceRepository repository, string locale, ArmyGeometryResources geometry)
    {
        this.repository = repository; this.geometry = geometry; Locale = locale;
        materials = repository.LoadObjects("KIDSSystemResource", ArmyFlagMaterialVariations.MaterialDictionaryId);
        shaderObjects = repository.LoadObjects("system", 0x718CCB8B);
        parameterRules = LoadedArmyMaterialParameterNativeSource.Load(repository, locale);
        inputs = new(repository, locale); drawWriter = new(repository, drawFiles);
        var defaults = new List<string>();
        foreach (int categoryId in new[] { 2, 3 })
        {
            var category = geometry.Categories[categoryId];
            foreach (var draw in category.Draws.Where(d => d.MaterialManager < 0))
            {
                uint materialId = category.MaterialGroups!.ReadDeclaredDraw(draw.DrawIndex).DeclaredDefaultMaterialObjectId
                    ?? throw new NotSupportedException("Army pole lacks its actual default material declaration.");
                var material = Object(materialId, 0xA8F14404);
                var textures = ModelMaterialTextureDeclaration.Read(material);
                var group = Object(textures.RegularGroup, 0x3059B9C3);
                var slots = ArmyFlagMaterialVariations.ReadTextureSlots(materials,
                    repository.LoadRdbAsset("MaterialEditor", Scalar(group, 0x7A1E1EF8)));
                var stage = ArmyFlagMaterialVariations.ReadStageTable(
                    repository.LoadRdbAsset("MaterialEditor", textures.StageTableResource).Bytes);
                var uses = ArmyFlagMaterialVariations.ResolveTextureUsesForActualShader(stage, slots, draw.Shader, draw.Combination);
                if (uses.Any(u => u.Slot.RequiresProducerImage))
                    throw new NotSupportedException("Army default pole requires its actual runtime image producer.");
                string path = $"defaults/{categoryId}/{draw.DrawIndex}";
                inputs.PrepareBindings(path, uses, path);
                defaults.Add(FormattableString.Invariant($"{categoryId} {draw.DrawIndex} {materialId} {path}"));
            }
        }
        inputs.AddMedia("defaults.txt", Text(defaults));
    }

    public static ArmyMaterialResources Load(GameResourceRepository repository, string locale,
        ArmyGeometryResources geometry)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(geometry);
        if (locale is not ("SC" or "TC")) throw new ArgumentException("Army material locale must be SC or TC.", nameof(locale));
        return new(repository, locale, geometry);
    }

    public IReadOnlyList<ArmyMaterialRequestResources> Prepare(IEnumerable<ArmyFlagMaterialRequest> actualRequests)
    {
        ArgumentNullException.ThrowIfNull(actualRequests);
        var result = new List<ArmyMaterialRequestResources>(); var seen = new HashSet<ArmyMaterialKey>();
        foreach (var source in actualRequests)
        {
            ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(source.SourceName);
            var glyph = source.SourceName.Glyph;
            if (source.Category is not (2 or 3) || (uint)source.ForceVariant >= 52 ||
                source.CarriedKind2Text != (source.Category == 3) || glyph.NativeCopyUnitCount != 2 ||
                glyph.TwoUtf16Units.Length != 2 || glyph.TwoUtf16Units[1] != '\0')
                throw new InvalidDataException("Army flag request requires its actual category, force, carried-name route and copied glyph.");
            var key = new ArmyMaterialKey(source.Category, source.ForceVariant, source.Color49 is byte color ? color : -1,
                glyph.TwoUtf16Units[0]);
            if (!seen.Add(key)) continue;
            if (!requests.TryGetValue(key, out var prepared))
            {
                var category = geometry.Categories[key.Category];
                var jobs = category.Draws.Where(d => d.MaterialManager >= 0).GroupBy(d => d.MaterialManager)
                    .Select(group => PrepareJob(source, key, group.Key)).ToArray();
                if (jobs.Length == 0) throw new NotSupportedException("Army flag category has no actual declared material manager.");
                prepared = new(key, source, Array.AsReadOnly(jobs)); requests.Add(key, prepared);
            }
            result.Add(prepared);
        }
        return result.AsReadOnly();
    }

    public static string RequestDirectory(ArmyMaterialKey key, int manager)
        => FormattableString.Invariant($"requests/{key.Category}/{key.ForceVariant}-{key.Color49}-{key.Glyph:X4}/{manager}");

    public void WriteNativeInputs(string materialSourceDirectory)
    {
        foreach (var (path, bytes) in drawFiles) inputs.AddMedia(path, bytes);
        inputs.WriteNativeInputs(materialSourceDirectory);
    }

    ArmyMaterialJob PrepareJob(ArmyFlagMaterialRequest source, ArmyMaterialKey key, int manager)
    {
        var category = geometry.Categories[key.Category];
        if (!variations.TryGetValue((manager, key.ForceVariant), out var variation))
        {
            variation = ArmyFlagMaterialVariations.Load(repository, geometry.Catalog, manager, key.ForceVariant, materials, shaderObjects);
            variations.Add((manager, key.ForceVariant), variation);
        }
        if (!variation.ControllerGateNonzero)
            throw new NotSupportedException("Army controller gate retains an earlier Material Data; its actual retained owner is required.");
        if (!declarationsByMaterial.TryGetValue(variation.SelectedMaterialObjectId, out var imported))
        {
            var ready = ArmyMaterialReadySource.Load(repository, materials, variation.SelectedMaterialObjectId, shaderObjects);
            var parameterSource = parameterRules.ReadMaterial(ready);
            declarationsByMaterial.Add(variation.SelectedMaterialObjectId, imported = (ready, parameterSource));
        }
        var declarations = imported.Ready; var parameters = imported.Parameters;
        var first = category.Draws.First(d => d.MaterialManager == manager);
        var uses = ArmyFlagMaterialVariations.ResolveTextureUsesForActualShader(variation, first.Shader, first.Combination);
        string directory = RequestDirectory(key, manager);
        var glyph = inputs.PrepareJob(manager, key.ForceVariant, source.Color49, source.SourceName.Glyph,
            source.SourceName.NativeSourceProvenance, directory, uses);
        inputs.AddMedia(directory + "/stage-source-slots.txt", Text(uses.GroupBy(use => use.Slot.Index).Select(group =>
        {
            var use = group.First();
            return FormattableString.Invariant($"{use.Slot.Index} {use.TextureRegister} {use.SamplerRegister}");
        })));
        uint preserve = ArmyMaterialReadySource.ReadWorldPreserve2EC(geometry.Catalog, key.Category);
        WriteState(directory, key, manager, variation, declarations, parameters, preserve);
        inputs.AddMedia(directory + "/texture-group-source.txt", Text([
            FormattableString.Invariant($"{variation.Materials.Source.ResourceUri} {variation.TextureGroup.ObjectId} {variation.TextureGroup.Index}")]));
        inputs.AddMedia(directory + "/material-source.txt", Text([
            FormattableString.Invariant($"{variation.Materials.Source.ResourceUri} {variation.MaterialDefinition.ObjectId} {variation.MaterialDefinition.Index} {variation.SelectedSourceVariationRow}")]));
        inputs.AddMedia(directory + "/filename-context.bin", parameters.FilenameContextSourceBytes.ToArray());
        // These are constructor inputs for candidate interfaces. Runtime context
        // registration and ready callbacks can supply later parameter writers.
        var stage1 = parameters.ConstructInitialRecord12(ArmyMaterialParameterPhase.RegularStage1);
        var stage2 = parameters.ConstructInitialRecord12(ArmyMaterialParameterPhase.RenderTargetStage2);
        var templates = new List<ArmyMaterialTemplate>();
        foreach (var draw in category.Draws.Where(d => d.MaterialManager == manager))
        {
            PrepareTemplate(draw.DrawIndex, draw.BaseTable, 0, null);
            PrepareTemplate(draw.DrawIndex, declarations.NormalShader, 1, stage1);
            PrepareTemplate(draw.DrawIndex, declarations.ClothShader, 2, stage1);
            PrepareTemplate(draw.DrawIndex, declarations.StaticNormalShader, 3, stage1);
            PrepareTemplate(draw.DrawIndex, declarations.StaticClothShader, 4, stage1);
        }
        inputs.AddMedia(directory + "/templates.txt", Text(templates.Select(t =>
            FormattableString.Invariant($"{t.DrawIndex} {t.TableObjectId} {t.Role} {Path.GetRelativePath(directory, t.RelativeDirectory).Replace('\\', '/')} {t.Queue} {t.AllowedRecords} {t.TableResourceUri}"))));
        return new(manager, directory, variation, declarations, glyph, templates.AsReadOnly());

        void PrepareTemplate(int drawIndex, NamedModelShader? table, int role, ArmyMaterialParameterRecord12? record)
        {
            if (table is null) return; // An actual zero shader reference has no candidate table.
            string target = directory + $"/draws/{drawIndex}-{role}";
            try
            {
                var recipeKey = (key.Category, manager, key.ForceVariant, drawIndex, role);
                if (!preparedDraws.TryGetValue(recipeKey, out var prepared))
                {
                    var selected = role == 0 ? category.Draws.Single(d => d.DrawIndex == drawIndex) :
                        category.PrepareMaterialDraw(drawIndex, table, record, parameters.DeclaredMaterialId);
                    var bindings = ArmyFlagMaterialVariations.ResolveTextureUsesForActualShader(variation, selected.Shader, selected.Combination);
                    if (bindings.Where(u => u.Slot.RequiresProducerImage).Any(u => u.Slot.ProxyObjectId != glyph.Primary.Slot.ProxyObjectId))
                        throw new NotSupportedException("Selected Army material requires a different runtime image producer.");
                    byte[] stage2Bytes;
                    if (record is null) stage2Bytes = selected.InitialPixel.ToArray();
                    else
                    {
                        var bytes = stage2.MaterialRecord64.Span;
                        var stage2Material = selected.Material with { RawMaterialFlags = stage2.Flags1C,
                            SourceAlphaReference = stage2.AlphaReference3E,
                            SourceSpecular = new(F(bytes,32), F(bytes,36), F(bytes,40), F(bytes,44)) };
                        stage2Bytes = category.PreparePixelConstants(drawIndex,
                            selected.Shader.ReadResourceDefinitions(selected.PixelProgram), stage2Material, selected.Queue);
                    }
                    preparedDraws.Add(recipeKey, prepared = (selected, bindings, stage2Bytes));
                }
                var actual = prepared.Draw;
                var registrations = actual.Shader.ReadG1ResourceBindings(actual.Combination);
                inputs.AddMedia(target + "/stage-registrations.txt", Text(registrations.Entries.Select(entry =>
                    FormattableString.Invariant($"{entry.SlotIndex} {entry.PrimaryEncoded} {entry.SecondarySamplerEncoded}"))));
                var rawDraw = G1ModelDrawReader.Read(category.Transforms.Geometry).Draws.Single(d => d.Index == drawIndex);
                drawWriter.WriteGeometryAndPrograms(target, $"geometry/{key.Category}", category.Transforms.Geometry, rawDraw,
                    actual.Shader, actual.VertexProgram, actual.PixelProgram, actual.InitialVertex.ToArray(), actual.InitialPixel.ToArray(),
                    category.ShaderBindings, actual.Queue, category.ReverseDepth,
                    ArmySkinVertexAdapter.SingleInstanceSourceStreams(category.Transforms.Geometry, drawIndex), actual.StencilEnabled, actual.StencilReference);
                byte[] skinControls = new byte[12];
                uint[] controls = ArmyGeometryConstants.SkinControls(actual.MatrixInput);
                for (int i = 0; i < controls.Length; ++i)
                    BinaryPrimitives.WriteUInt32LittleEndian(skinControls.AsSpan(i * 4), controls[i]);
                inputs.AddMedia(target + "/skin-controls.bin", skinControls);
                // The shared full-key material owns the actual images and
                // samplers. The selected shader draw borrows them through its
                // texture-stage binding plan instead of uploading a second set.
                inputs.AddMedia(target + "/textures-native.txt", []);
                inputs.AddMedia(target + "/samplers-native.txt", []);
                inputs.AddMedia(target + "/ps-b0-stage2.bin", prepared.Stage2Pixel);
                templates.Add(new(drawIndex, table.Object.ObjectId, table.Objects.Source.ResourceUri, role, target, actual.Queue.Node.Index, actual.AllowedRecords, null));
            }
            catch (NotSupportedException error)
            {
                inputs.AddMedia(target + "/unsupported.txt", Encoding.UTF8.GetBytes(error.Message + "\n"));
                templates.Add(new(drawIndex, table.Object.ObjectId, table.Objects.Source.ResourceUri, role, target, -1, 0, error.Message));
            }
        }
    }

    void WriteState(string directory, ArmyMaterialKey key, int manager, LoadedArmyFlagMaterialVariation variation,
        LoadedArmyMaterialReadySource declarations, LoadedArmyMaterialParameterSource parameters, uint preserve)
    {
        byte[] bytes = new byte[68];
        uint[] words = [StateMagic, (uint)key.Category, (uint)manager, (uint)key.ForceVariant, unchecked((uint)key.Color49), key.Glyph,
            variation.SelectedMaterialObjectId, variation.ControllerGateMaterialObjectId, preserve,
            declarations.RegularTextureGroupObjectId, declarations.RenderTargetTextureGroupObjectId, declarations.AlternateTextureGroupObjectId,
            declarations.NormalShader?.Object.ObjectId ?? 0, declarations.ClothShader?.Object.ObjectId ?? 0,
            declarations.StaticNormalShader?.Object.ObjectId ?? 0, declarations.StaticClothShader?.Object.ObjectId ?? 0,
            parameters.StageTableResourceId];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), words[i]);
        inputs.AddMedia(directory + "/material-state.bin", bytes);
    }


    DokObject Object(uint id, uint classId)
    {
        var rows = materials.Document.Objects.Where(o => o.ObjectId == id).ToArray();
        return rows.Length == 1 && rows[0].Kind == DokObjectKind.Inline && rows[0].ClassId == classId
            ? rows[0] : throw new InvalidDataException($"Army material object {id:X8} is not its unique declared class {classId:X8}.");
    }
    static uint Scalar(DokObject item, uint hash)
    {
        var rows = item.Properties.Where(p => p.NameHash == hash).ToArray();
        if (rows.Length != 1 || rows[0].TypeCode != 5 || rows[0].ElementCount != 1 || rows[0].RawValue.Length != 4)
            throw new InvalidDataException($"Army material field {hash:X8} is not its declared resource reference.");
        return BinaryPrimitives.ReadUInt32LittleEndian(rows[0].RawValue);
    }

    static float F(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(at, 4));

    static byte[] Text(IEnumerable<string> rows) => Encoding.UTF8.GetBytes(string.Join('\n', rows) + "\n");
}
