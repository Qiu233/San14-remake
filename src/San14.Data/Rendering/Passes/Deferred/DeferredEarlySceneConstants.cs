using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>An explicit current native named write. Its bytes include the
/// source element spacing, and belong to the stated current producer.</summary>
public sealed record DeferredEarlyNativeConstantWrite(string Name,
    ReadOnlyMemory<byte> Bytes, string CurrentProducerProvenance);

public sealed record DeferredEarlyConstantPacket(ReadOnlyMemory<byte> Bytes,
    IReadOnlyList<DeferredSceneWrite> CommonWrites,
    IReadOnlyList<DeferredEarlyNativeConstantWrite> AdditionalWrites,
    IReadOnlyList<string> RetainedVariables, string CurrentProducerProvenance);

/// <summary>Projects the recovered F103 named setters into the four original
/// early layouts. The complete current template remains authoritative for
/// Caustics, LightMap, LAS and all other unwritten values. No template or
/// source declaration establishes a later current producer.</summary>
public static class DeferredEarlySceneConstants
{
    private sealed record LoadedLayout(DxbcResourceDefinitions Definitions, DxbcRdefBuffer Globals, DxbcRdefBinding Declaration);
    private sealed class LoadedDefinitions(LoadedShaderContainer shader)
    {
        internal readonly LoadedShaderContainer Shader = shader;
        internal readonly Dictionary<int, DxbcResourceDefinitions> Programs = new();
        internal readonly Dictionary<int, LoadedLayout> Combinations = new();
        internal DxbcResourceDefinitions ReadProgram(int index)
        {
            lock (this)
            {
                if (!Programs.TryGetValue(index, out var definitions)) Programs.Add(index, definitions = Shader.ReadResourceDefinitions(index));
                return definitions;
            }
        }
        internal LoadedLayout ReadCombination(int index)
        {
            lock (this)
            {
                if (!Combinations.TryGetValue(index, out var layout))
                {
                    var resources = Shader.ReadG1ResourceBindings(index);
                    int pixel = resources.Combination.Stages.Single(s => s.StageCode == 0 && s.Entry is not null).Entry!.ProgramIndex;
                    var definitions = ReadProgram(pixel);
                    var globals = definitions.Buffers.Single(b => b.Name.Text == "$Globals");
                    var declaration = definitions.Bindings.Single(b => b.InputType == 0 && b.Name.Text == "$Globals");
                    if (declaration.BindPoint != 0 || declaration.BindCount != 1 ||
                        globals.Variables.Select(v => v.Name.Text).Distinct().Count() != globals.Variables.Count)
                        throw new InvalidDataException("Early F103 requires its exact cb0 and unique named fields.");
                    Combinations.Add(index, layout = new(definitions, globals, declaration));
                }
                return layout;
            }
        }
    }
    private static readonly ConditionalWeakTable<LoadedRdbAsset, LoadedDefinitions> ImportedShaders = new();
    private static readonly ConditionalWeakTable<LoadedRenderGraph, LoadedRenderGraph> ImportedGraphs = new();

    private static LoadedDefinitions ReadLoadedDefinitions(LoadedShaderContainer shader)
        => ImportedShaders.GetValue(shader.Source, static source =>
        {
            if (source.OverrideSource is not null || Convert.ToHexString(SHA256.HashData(source.Bytes)) != source.DecodedSha256)
                throw new InvalidDataException("Early named source bytes differ from their loaded declared identity.");
            return new(new(G2ShaderContainerReader.Read(source.Bytes), source));
        });
    /// <summary>Concrete ordinary Main shader126/PS95 cb0. Every declared
    /// variable receives a current native writer: thirteen common fields and
    /// three Caustics fields. Only gaps between source variables are zeroed by
    /// this backend. Caustics coefficients and projection are never inferred.</summary>
    public static DeferredEarlyConstantPacket CreateOrdinary126(
        DeferredEarlyF103SelectedSource selected, LoadedShaderContainer canonicalF103Shader,
        MapSourceCreatedLightingPacket lighting, DeferredOcclusionInputs currentOcclusion,
        DeferredShadowFadeInputs currentShadowFade,
        IReadOnlyList<DeferredEarlyNativeConstantWrite> currentCausticsWrites,
        string currentProducerProvenance)
    {
        ArgumentNullException.ThrowIfNull(selected); ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(currentOcclusion); ArgumentNullException.ThrowIfNull(currentShadowFade);
        ArgumentNullException.ThrowIfNull(currentCausticsWrites);
        selected.Validate();
        if (selected.SourcePlan.Node.Index != 78 || selected.Combination.Index != 126 || selected.PixelProgramIndex != 95 ||
            selected.PixelProgramSha256 != "19103754FF90ADC50E3CC7269C1AA93013D0FC3EA8341D3EE7BB2396D9F3030C" ||
            selected.OwnedFeatureSource is not { } feature || lighting.SceneInputs.Camera is null || lighting.SceneInputs.Ambient is null)
            throw new NotSupportedException("Complete ordinary early constants require the actual source node78/126/PS95 owned feature and Camera.");
        feature.ValidateFrame(selected.Camera, lighting);
        // D74820 consumes selected modifier H, which is independent of the
        // outer light L in the World loop. The Camera rate retains its own
        // accepted Camera producer, exactly as in the late PS383 writer.
        DeferredAmbientInputs ambient;
        if (lighting.AcceptedInputs.SelectedBuiltinAmbientCameraRate is float mapOcclusionRate)
            ambient = DeferredSceneConstants.ReadAmbientModifier(
                feature.SelectedModifierLightData.Span.Slice(0x1B0, 0x178), mapOcclusionRate);
        else
        {
            if (!feature.SelectedModifierOwnerIdentity.StartsWith(lighting.Binding.LightOwnerIdentity + ":", StringComparison.Ordinal))
                throw new InvalidDataException("An independent early modifier requires its actual accepted Camera map-occlusion producer.");
            // Existing explicit same-owner fixtures may supply the complete
            // ambient packet; they do not authorize another modifier owner.
            ambient = lighting.SceneInputs.Ambient;
        }
        string[] caustics = ["fCausticsScaler", "vCausticsRange", "mW2Ca"];
        if (!currentCausticsWrites.Select(write => write.Name).Order(StringComparer.Ordinal).SequenceEqual(caustics.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Ordinary early cb0 requires all three actual current Caustics named producers.");
        var scene = new DeferredSceneInputs(
            Lighting: DeferredSceneConstants.ReadPreparedBaseLighting(feature.EffectiveLocalLightData.Span[..0x1B0],
                checked((int)feature.Scratch.DirectionalCount)), Camera: lighting.SceneInputs.Camera,
            Ambient: ambient, Occlusion: currentOcclusion, ShadowFade: currentShadowFade);
        var packet = Apply(selected.ReadSelectedTexturePlan(), canonicalF103Shader, new byte[480], scene,
            currentCausticsWrites, currentProducerProvenance);
        if (packet.RetainedVariables.Count != 0 || packet.CommonWrites.Count != 13 || packet.AdditionalWrites.Count != 3)
            throw new InvalidDataException("An ordinary early active native parameter owner remains unresolved.");
        return packet;
    }

    public static DeferredEarlyConstantPacket Apply(LoadedDeferredTexturePlan current,
        LoadedShaderContainer canonicalF103Shader, ReadOnlySpan<byte> completeCurrentTemplate,
        DeferredSceneInputs actualCurrentScene,
        IReadOnlyList<DeferredEarlyNativeConstantWrite> additionalNativeWrites,
        string currentProducerProvenance)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(canonicalF103Shader);
        ArgumentNullException.ThrowIfNull(actualCurrentScene); ArgumentNullException.ThrowIfNull(additionalNativeWrites);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentProducerProvenance);
        ValidateSource(current);
        if (canonicalF103Shader.Source.FileId != 0x9D8E811F || canonicalF103Shader.Source.OverrideSource is not null ||
            canonicalF103Shader.Source.DecodedSha256 != MapSourceCreatedShaderSelection.ShaderSha256)
            throw new InvalidDataException("Early named writes require the pinned canonical F103 setter layout.");
        // Source/layout validation belongs to the actual loaded asset. Each
        // request still consumes its complete current template and writers.
        var loaded = ReadLoadedDefinitions(current.Shader).ReadCombination(current.ResourceBindings.Combination.Index);
        var definitions = loaded.Definitions; var globals = loaded.Globals;
        if (completeCurrentTemplate.Length != globals.ByteSize)
            throw new InvalidDataException("Early F103 requires its complete exact current cb0.");
        var canonical = ReadLoadedDefinitions(canonicalF103Shader).ReadProgram(1);
        var originalGlobals = canonical.Buffers.Single(b => b.Name.Text == "$Globals");
        byte[] evaluation = new byte[DeferredSceneConstants.ByteSize];
        foreach (var target in globals.Variables)
        {
            var original = originalGlobals.Variables.SingleOrDefault(v => v.Name.Text == target.Name.Text);
            if (original is null) continue;
            RequireShape(canonical, original, definitions, target);
            completeCurrentTemplate.Slice(target.StartOffset, target.ByteSize).CopyTo(evaluation.AsSpan(original.StartOffset, original.ByteSize));
        }
        var computed = DeferredSceneConstants.Apply(canonical, evaluation, actualCurrentScene);
        byte[] result = completeCurrentTemplate.ToArray();
        var common = new List<DeferredSceneWrite>();
        foreach (var write in computed.Writes)
        {
            var target = globals.Variables.SingleOrDefault(v => v.Name.Text == write.Name);
            if (target is null) continue;
            var original = originalGlobals.Variables.Single(v => v.Name.Text == write.Name);
            RequireShape(canonical, original, definitions, target);
            computed.Bytes.AsSpan(original.StartOffset, original.ByteSize).CopyTo(result.AsSpan(target.StartOffset, target.ByteSize));
            common.Add(write with { Offset = target.StartOffset });
        }
        var additional = additionalNativeWrites.Select(w => w ?? throw new InvalidDataException("Null early named producer.")).ToArray();
        if (additional.Select(w => w.Name).Distinct(StringComparer.Ordinal).Count() != additional.Length ||
            additional.Any(w => common.Any(c => c.Name == w.Name)))
            throw new InvalidDataException("Early named current producers cannot duplicate or replace a common setter.");
        var snapshots = new List<DeferredEarlyNativeConstantWrite>();
        foreach (var write in additional)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(write.CurrentProducerProvenance);
            var target = globals.Variables.SingleOrDefault(v => v.Name.Text == write.Name);
            if (target is null || write.Bytes.Length != target.ByteSize)
                throw new InvalidDataException("Early additional native field differs from its exact source declaration: " + write.Name);
            byte[] snapshot = write.Bytes.ToArray(); snapshot.CopyTo(result.AsSpan(target.StartOffset, target.ByteSize));
            snapshots.Add(write with { Bytes = snapshot });
        }
        var written = common.Select(w => w.Name).Concat(snapshots.Select(w => w.Name)).ToHashSet(StringComparer.Ordinal);
        return new(result, common.AsReadOnly(), snapshots.AsReadOnly(), Array.AsReadOnly(globals.Variables
            .Where(v => !written.Contains(v.Name.Text)).Select(v => v.Name.Text).ToArray()), currentProducerProvenance);
    }

    public static void ValidateSource(LoadedDeferredTexturePlan current)
    {
        ArgumentNullException.ThrowIfNull(current);
        (uint File, string Sha) expected = current.Node.Index switch
        {
            77 => (0xA64DAE7B, "850547D3939B616CBD9B5D359AD37F6A247C0AA8C5A2B52BA2235339949447D6"),
            78 => (0xB82C3BBF, "AF9DEDA3A8D19495640113ABD6186380D90B47421A6016520D834A5FB4D26A99"),
            79 => (0x1E5544DD, "0A9C0CA0AECF056DA0FE09755F54BBF77729F9EB86C43E800ECF1C9DC3562A26"),
            80 => (0xA4DAFF7C, "1E738DF59C7C498CAE89B233B7F08631E868D263D52CC2D4096A6F42D744DD3D"),
            _ => throw new InvalidDataException("Early F103 source is outside original nodes77–80.")
        };
        if (current.Node.Call.FactoryClassId != 0xF103DB00 || current.Graph.Source.OverrideSource is not null ||
            current.Graph.Source.ResourceUri != DeferredAuxiliaryPixelSourceRecipes.GraphResourceUri ||
            current.Graph.Source.DecodedSha256 != DeferredAuxiliaryPixelSourceRecipes.GraphSha256 ||
            current.Shader.Source.OverrideSource is not null || current.Shader.Source.FileId != expected.File ||
            current.Shader.Source.DecodedSha256 != expected.Sha ||
            current.ViewId != 0 || current.LightGroupId != 8)
            throw new InvalidDataException("Early F103 source differs from its exact original graph, shader, View or light group.");
        _ = ImportedGraphs.GetValue(current.Graph, static graph =>
        {
            if (Convert.ToHexString(SHA256.HashData(graph.Source.Bytes)) != DeferredAuxiliaryPixelSourceRecipes.GraphSha256)
                throw new InvalidDataException("Early F103 graph bytes differ from their loaded original source.");
            return graph;
        });
        _ = ReadLoadedDefinitions(current.Shader);
    }

    private static void RequireShape(DxbcResourceDefinitions source, DxbcRdefVariable original,
        DxbcResourceDefinitions targetDefinitions, DxbcRdefVariable target)
    {
        var a = source.Types.Single(t => t.Offset == original.TypeOffset);
        var b = targetDefinitions.Types.Single(t => t.Offset == target.TypeOffset);
        if (original.ByteSize != target.ByteSize || a.Class != b.Class || a.BaseType != b.BaseType || a.Rows != b.Rows ||
            a.Columns != b.Columns || a.Elements != b.Elements || a.Members.Count != 0 || b.Members.Count != 0)
            throw new InvalidDataException("Early F103 named setter changed its native element shape: " + target.Name.Text);
    }
}
