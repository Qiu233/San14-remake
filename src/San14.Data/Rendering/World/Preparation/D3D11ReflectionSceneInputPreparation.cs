using System.Numerics;
using System.Text;

namespace San14.Data;

/// <summary>Initial native Scene input from one selected resource generation.
/// Root declaration membership is preserved independently of Model-bank support
/// and current World activation. Dynamic/action Fields have their own native
/// registration lifecycle; this file never declares those populations empty.</summary>
public sealed class D3D11ReflectionSceneInputPreparation
{
    public const string NativeDirectory = "reflection-scene-input";
    public const string NativeFilename = "scene.bin";
    private readonly byte[] bytes;

    private D3D11ReflectionSceneInputPreparation(LoadedReflectionSceneRootSource source,
        int modelCount, int fieldCount, byte[] prepared)
    {
        Source = source; ModelCount = modelCount; FieldCount = fieldCount; bytes = prepared;
    }

    public LoadedReflectionSceneRootSource Source { get; }
    public int ChildCount => Source.SourceReferences.Count;
    public int ModelCount { get; }
    /// <summary>Direct E637 children of this selected root only.</summary>
    public int FieldCount { get; }

    /// <summary>Prepare once at normal resource loading. Placements and model
    /// mappings must belong to the same actual selected root and initial fresh
    /// Model publication. Source indices are joined through actual object and
    /// document identities; an index is never treated as a root-row ordinal.</summary>
    public static D3D11ReflectionSceneInputPreparation Prepare(
        LoadedReflectionSceneRootSource actualRoot, ScenePlacements actualRootPlacements,
        D3D11ModelInputPreparation actualFieldModels, CommonRenderSettings actualCommon,
        MapCameraConstructionPacket actualView0)
    {
        ArgumentNullException.ThrowIfNull(actualRoot);
        ArgumentNullException.ThrowIfNull(actualRootPlacements);
        ArgumentNullException.ThrowIfNull(actualFieldModels);
        ArgumentNullException.ThrowIfNull(actualCommon);
        ArgumentNullException.ThrowIfNull(actualView0);
        if (!ReferenceEquals(actualRoot.Scene, actualRootPlacements.Scene) ||
            !ReferenceEquals(actualRoot.Scene, actualFieldModels.Scene))
            throw new InvalidDataException("Native reflection inputs require the same loaded Scene generation.");
        uint flags14 = actualCommon.Flags14 ?? throw new InvalidDataException(
            "Native reflection inputs require the actual Common FlagEX2 loader publication.");

        var placements = actualRootPlacements.Instances.ToDictionary(item => item.Source.Index);
        var shadows = actualFieldModels.SourceShadowMappings.ToDictionary(item => item.SceneInstanceIndex);
        var bankByObject = new Dictionary<uint, (ScenePlacement Placement,
            D3D11SourceModelMapping Model, D3D11SourceModelShadowMapping Shadow)>();
        foreach (var model in actualFieldModels.SourceModelMappings)
        {
            if (!placements.TryGetValue(model.SceneInstanceIndex, out var placement) || placement.Model is null ||
                !shadows.TryGetValue(model.SceneInstanceIndex, out var shadow) || shadow.ModelOrdinal != model.ModelOrdinal)
                throw new InvalidDataException("Native Model-bank mapping has no matching actual root placement and shadow prefix.");
            if (!bankByObject.TryAdd(placement.Source.Object.ObjectId, (placement, model, shadow)))
                throw new InvalidDataException("Native Model-bank object identity is ambiguous in this selected root.");
        }

        var fields = actualRoot.SourceReferences
            .Where(reference => reference.ResolvedDefinition.ClassId == ReflectionCaptureFieldSource.ClassId)
            .Select(reference => (Reference: reference,
                Definition: ReflectionResourceFieldActorOwner.ReadResolvedField(reference))).ToArray();
        var prefixes = new Dictionary<(LoadedObjects Document, uint ObjectId),
            (uint Flags44, DeferredEarlyShadowCurrentWorldCallerFields World)>();
        var matched = new HashSet<int>();
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("RSFROOT1"u8);
            writer.Write(1u);
            writer.Write(actualRoot.ObjectId);
            writer.Write(actualRoot.Capacity);
            writer.Write(actualRoot.ExternalObjectId);
            writer.Write(1u); // The required actual Common owner is present.
            writer.Write(flags14);
            writer.Write(actualView0.SourceReflectionProbeRsmCaptureMode15A0);
            writer.Write(checked((uint)actualRoot.SourceReferences.Count));
            writer.Write(checked((uint)fields.Length));
            Vector(actualRoot.Position); Vector(actualRoot.Scale);
            foreach (var reference in actualRoot.SourceReferences)
            {
                writer.Write(checked((uint)reference.Index));
                writer.Write(reference.ObjectId);
                writer.Write(reference.ResolvedDefinition.ClassId ?? throw new InvalidDataException(
                    "Native Scene child definition has no actual inline class identifier."));
                writer.Write(reference.Flags);
                if (bankByObject.TryGetValue(reference.ObjectId, out var row))
                {
                    var source = row.Placement.Source;
                    if (!ReferenceEquals(reference.Instance, source.Object) ||
                        !ReferenceEquals(reference.InstanceDocument, source.InstanceDocument) ||
                        !ReferenceEquals(reference.ResolvedDefinition, source.Definition) ||
                        !ReferenceEquals(reference.DefinitionDocument, source.DefinitionDocument))
                        throw new InvalidDataException("Native Model source index does not join the actual selected root child.");
                    matched.Add(row.Model.ModelOrdinal);
                    var key = (reference.DefinitionDocument, reference.ResolvedDefinition.ObjectId);
                    if (!prefixes.TryGetValue(key, out var prefix))
                    {
                        var flags = MapModelNativeVisibilitySources.ReadWorldPqSourceFlags(key.Item1, key.Item2);
                        var world = DeferredEarlyShadowCasterAdmissionSource.ReadImported(key.Item1, key.Item2,
                            row.Placement.Model!.ModelObject.ObjectId).CaptureRetainedFresh();
                        prefix = ((uint)flags.Flag | ((uint)flags.DynamicFlag << 8), world);
                        prefixes.Add(key, prefix);
                    }
                    // These are the accepted bank's fresh source prefixes.
                    // Later native setters retain their separate current owner.
                    writer.Write(checked((uint)row.Model.ModelOrdinal));
                    writer.Write(prefix.Flags44);
                    writer.Write(prefix.World.ActualEligibilityV408 ? 1u : 0u);
                    writer.Write(prefix.World.Mask328); writer.Write(prefix.World.Mask32C); writer.Write(prefix.World.Mask330);
                    writer.Write(row.Shadow.Exclusion78); writer.Write(row.Shadow.Flags10C); writer.Write(row.Shadow.OpacityB0);
                }
                else
                {
                    // No accepted static-bank binding: the typed child remains
                    // declared. These unused fields attest neither CPU absence
                    // nor readiness. Native type owners handle other children;
                    // unsupported Models remain in the Model-bank boundaries.
                    writer.Write(uint.MaxValue);
                    for (int word = 0; word < 8; word++) writer.Write(0u);
                }
            }
            if (matched.Count != actualFieldModels.SourceModelMappings.Count)
                throw new InvalidDataException("A native Model-bank mapping is outside the actual selected Scene root.");
            foreach (var field in fields)
            {
                var definition = field.Definition;
                writer.Write(checked((uint)field.Reference.Index)); writer.Write(field.Reference.ObjectId);
                writer.Write(definition.FieldDimension.X); writer.Write(definition.FieldDimension.Y); writer.Write(definition.FieldDimension.Z);
                writer.Write(definition.CellSize); writer.Write(definition.TileSize); writer.Write(definition.GBufSize);
                writer.Write(definition.ViewRange); writer.Write(definition.GroupMask); writer.Write(definition.MaxPrimitiveNum);
                writer.Write(definition.ProbeFieldShadowMapSize); Vector(definition.WorldPosition);
                writer.Write(definition.WorldQuaternion.X); writer.Write(definition.WorldQuaternion.Y);
                writer.Write(definition.WorldQuaternion.Z); writer.Write(definition.WorldQuaternion.W);
            }

            void Vector(Vector3 value) { writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z); }
        }
        return new(actualRoot, matched.Count, fields.Length, stream.ToArray());
    }

    /// <summary>Write this prepared resource generation under the same package
    /// root as its native Model bank. Reprepare after resource replacement.</summary>
    public void WriteNativeInputs(string packageRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        string directory = Path.Combine(packageRoot, NativeDirectory);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, NativeFilename), bytes);
    }
}
