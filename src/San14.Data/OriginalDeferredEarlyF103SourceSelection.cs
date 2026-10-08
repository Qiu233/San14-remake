using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Raw results from TC D5BA00 and D5B9D0 for the selected View/group
/// at this preparation boundary. They read registry+110, View+1C70/+1C78,
/// then object uint+50 (a null object returns zero). No light-kind meaning or
/// zero result is inferred from a kind1 feature packet.</summary>
public sealed record OriginalDeferredEarlyF103CurrentVariantInputs(
    OriginalDeferredF103CurrentVariantInputs Common,
    uint RawQueryD5BA00, uint RawQueryD5B9D0, string RawQueryProducerProvenance);

/// <summary>Selection for pinned early F103 nodes77..80. Selection is after
/// native color admission; it does not establish that the graph branch ran or
/// that its named input images and parameter bytes were produced.</summary>
public sealed class OriginalDeferredEarlyF103SelectedSource
{
    private readonly byte[] featureBytes, variantBytes;
    internal OriginalDeferredEarlyF103SelectedSource(LoadedOriginalDeferredTexturePlan source,
        OriginalMapCameraConstructionPacket camera, OriginalDeferredEarlyF103CurrentVariantInputs current,
        byte[] feature, byte[] variant, G1ShaderFeatureSelection featureMatch,
        G1ShaderVariantRecord variantRow, G2ShaderCombination combination,
        IReadOnlyList<OriginalDeferredF103VariantWrite> writes,
        OriginalMapSourceCreatedF103FeaturePacket? ownedFeature = null)
    {
        SourcePlan = source; Camera = camera.CopyAcceptedSnapshot(); EarlyCurrent = current;
        featureBytes = feature.ToArray(); variantBytes = variant.ToArray();
        FeatureMatch = featureMatch; BindingVariant = variantRow; Combination = combination;
        Writes = writes; OwnedFeatureSource = ownedFeature;
        var program = OriginalDeferredEarlyF103SourceSelection.ReadLoadedProgram(source, combination);
        PixelProgramIndex = program.PixelIndex; VertexProgramIndex = program.VertexIndex;
        PixelProgramSha256 = program.PixelSha256; VertexProgramSha256 = program.VertexSha256;
        PixelDefinitions = program.PixelDefinitions; ResourceBindings = program.ResourceBindings;
    }
    public LoadedOriginalDeferredTexturePlan SourcePlan { get; }
    public OriginalMapCameraConstructionPacket Camera { get; }
    public OriginalDeferredEarlyF103CurrentVariantInputs EarlyCurrent { get; }
    public OriginalDeferredF103CurrentVariantInputs Current => EarlyCurrent.Common;
    public ReadOnlyMemory<byte> CurrentFeature48 => featureBytes.ToArray();
    public ReadOnlyMemory<byte> RequestedVariant96 => variantBytes.ToArray();
    public G1ShaderFeatureSelection FeatureMatch { get; }
    public G1ShaderVariantRecord BindingVariant { get; }
    public G2ShaderCombination Combination { get; }
    public int PixelProgramIndex { get; }
    public int VertexProgramIndex { get; }
    public string PixelProgramSha256 { get; }
    public string VertexProgramSha256 { get; }
    public DxbcResourceDefinitions PixelDefinitions { get; }
    public G1ShaderResourceBindings ResourceBindings { get; }
    public IReadOnlyList<OriginalDeferredF103VariantWrite> Writes { get; }
    public OriginalMapSourceCreatedF103FeaturePacket? OwnedFeatureSource { get; }

    public void Validate()
    {
        // The sealed selector produced these private48/96-byte values from the
        // loaded source directory. Validate its current owner relationship;
        // loading/selection is not a second current producer at consumption.
        if (OwnedFeatureSource is { } feature &&
            (feature.FrameNumber != Current.FrameNumber || !feature.CurrentFeature48.Span.SequenceEqual(featureBytes) ||
             !feature.MatchesAcceptedCameraPrefix(Camera.Prefix.Span) ||
             feature.CurrentView.CurrentProducerProvenance != Current.CurrentProducerProvenance))
            throw new InvalidDataException("Early F103 feature differs from its accepted Camera/light/View frame.");
    }

    /// <summary>Check the source Camera and light frame before attaching an
    /// early parameter frame. An owned feature additionally checks light data,
    /// binding identity and producer provenance through its original validator.</summary>
    public void ValidateParameterFrame(OriginalMapCameraConstructionPacket acceptedCamera,
        OriginalMapSourceCreatedLightingPacket lighting)
    {
        ArgumentNullException.ThrowIfNull(acceptedCamera); ArgumentNullException.ThrowIfNull(lighting);
        Validate();
        if (acceptedCamera.FrameNumber != Current.FrameNumber || lighting.FrameNumber != Current.FrameNumber ||
            acceptedCamera.Source.Camera.ObjectId != Camera.Source.Camera.ObjectId ||
            acceptedCamera.Source.Resource.Source.DecodedSha256 != Camera.Source.Resource.Source.DecodedSha256 ||
            !acceptedCamera.Prefix.Span.SequenceEqual(Camera.Prefix.Span))
            throw new InvalidDataException("Early F103 parameters belong to a different accepted Camera/light frame.");
        OwnedFeatureSource?.ValidateFrame(acceptedCamera, lighting);
    }

    public LoadedOriginalDeferredTexturePlan ReadSelectedTexturePlan()
    {
        Validate();
        return OriginalDeferredEarlyF103SourceSelection.ReadTexturePlan(SourcePlan, Combination.Index);
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}

/// <summary>Fresh native row0/capability selection for the four pinned early
/// F103 programs. It has its own source validation and does not use node166's
/// late shader shape, capability assumptions or selection validator.</summary>
public static partial class OriginalDeferredEarlyF103SourceSelection
{
    public const string GraphSha256 = "B9AE1EC8209C067DBC73084CDC0EB149A65E282C25D4B8AD398F43071072971D";
    public const string TextureObjectsSha256 = "4082FCB18D13F992407B2168F0105B9C86F53D095E2BA630802AD0BCDF19FF12";
    public const string FullscreenVertexSha256 = "C51D71A59DC89E4B23B68395180A41473B35914AD33BCE80C92AF636A379757D";
    private sealed record Pin(uint Object, uint File, string Sha, bool Refraction, bool Thin, uint TransSpec, uint AtDistance, uint TransTarget);
    private static Pin ReadPin(int node) => node switch
    {
        77 => new(0x3BC227BD, 0xA64DAE7B, "850547D3939B616CBD9B5D359AD37F6A247C0AA8C5A2B52BA2235339949447D6", false, false, 0, 0, 0),
        78 => new(0x957AA501, 0xB82C3BBF, "AF9DEDA3A8D19495640113ABD6186380D90B47421A6016520D834A5FB4D26A99", false, false, 0, 0, 0),
        79 => new(0x6C87B61F, 0x1E5544DD, "0A9C0CA0AECF056DA0FE09755F54BBF77729F9EB86C43E800ECF1C9DC3562A26", true, true, 1, 0, 0),
        80 => new(0x86A334BE, 0xA4DAFF7C, "1E738DF59C7C498CAE89B233B7F08631E868D263D52CC2D4096A6F42D744DD3D", false, true, 0, 1, 2),
        _ => throw new InvalidDataException("Early F103 selection requires node77,78,79 or80.")
    };

    public static OriginalDeferredEarlyF103SelectedSource PrepareFreshShader(
        LoadedOriginalDeferredTexturePlan source, OriginalMapSourceCreatedF103FeaturePacket ownedFeature,
        OriginalMapSourceCreatedLightingPacket lighting, OriginalDeferredEarlyF103CurrentVariantInputs current)
    {
        ArgumentNullException.ThrowIfNull(ownedFeature); ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(current.Common);
        var camera = ownedFeature.AcceptedCamera; ownedFeature.ValidateFrame(camera, lighting);
        if (ownedFeature.CurrentView.CurrentProducerProvenance != current.Common.CurrentProducerProvenance)
            throw new InvalidDataException("Early F103 feature and variant fields have different producer boundaries.");
        var selected = PrepareFreshShader(source, camera, current, ownedFeature.CurrentFeature48.Span);
        return new(selected.SourcePlan, selected.Camera, selected.EarlyCurrent, selected.CurrentFeature48.ToArray(),
            selected.RequestedVariant96.ToArray(), selected.FeatureMatch, selected.BindingVariant, selected.Combination,
            selected.Writes, ownedFeature);
    }

    public static OriginalDeferredEarlyF103SelectedSource PrepareFreshShader(
        LoadedOriginalDeferredTexturePlan source, OriginalMapCameraConstructionPacket acceptedCamera,
        OriginalDeferredEarlyF103CurrentVariantInputs earlyCurrent, ReadOnlySpan<byte> actualCurrentFeature48)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(acceptedCamera);
        ArgumentNullException.ThrowIfNull(earlyCurrent); ArgumentNullException.ThrowIfNull(earlyCurrent.Common);
        var current = earlyCurrent.Common;
        if (current.FrameNumber <= 0 || current.FrameNumber != acceptedCamera.FrameNumber ||
            string.IsNullOrWhiteSpace(current.CurrentProducerProvenance) ||
            string.IsNullOrWhiteSpace(earlyCurrent.RawQueryProducerProvenance) || actualCurrentFeature48.Length != 48)
            throw new InvalidDataException("Early F103 requires an accepted frame, complete feature48 and explicit current raw View queries.");
        var loaded = ReadLoadedSource(source);
        Pin pin = loaded.Pin; source = loaded.Source;
        var tables = loaded.Tables;
        byte[] desired = loaded.InitialVariant.ToArray();
        uint[] masks = loaded.CapabilityMasks;
        var writes = new List<OriginalDeferredF103VariantWrite>();
        void Write(int word, uint value, string origin)
        {
            uint previous = Word(desired, word);
            bool accepted = (masks[word] & (1u << (int)(value & 31))) != 0;
            uint next = accepted ? value : previous;
            BinaryPrimitives.WriteUInt32LittleEndian(desired.AsSpan(word * 4, 4), next);
            writes.Add(new(word, value, previous, next, accepted, origin));
        }
        void Retain(int word, string origin) => writes.Add(new(word, null, Word(desired, word), Word(desired, word), null, origin));
        uint flags = current.ContextFlags3F0;
        // C14D07 Refraction sets bit2; C13D8D TransSpecSrc=1 sets
        // bit4. EAD4A3..EAD4E3 skips the word16 branch when both are set.
        // 79 has both; the other three have no Refraction bit2.
        Retain(16, pin.Refraction ? "TC:EAD4A3..EAD4E3;Refraction-bit2/Thin-bit4;writer-skipped" :
            "TC:EAD4A3..EAD4E3;Refraction-bit2-clear;writer-skipped");
        // Every possible CommonView+14 bit7 request is unsupported except
        // zero; row0 word20 remains zero, without inventing that field.
        Retain(20, "TC:EAD5F6..EAD610/997190;SC:EAE5E6..EAE600/998000;source-mask1;all-request-outcomes-retain0");
        bool specular = current.NativeColorMode == 0x60
            ? acceptedCamera.SourceSpecularOcclusionType3C0 == 1
            : acceptedCamera.SourceSpecularOcclusionType3C0 != 0;
        Write(1, specular ? 1u : 0u, "TC:EAD6E7..EAD722/997350");
        uint bit14Inverse = (flags & 0x4000) == 0 ? 1u : 0u;
        Write(2, bit14Inverse, "TC:EAD769..EAD78E/997110;source-nonocean");
        Write(5, bit14Inverse, "TC:EAD78E..EAD79A/997210");
        Write(17, bit14Inverse, "TC:EAD79A..EAD7A8/997010");
        uint lightFlagsInverse = (flags & 0x1800) == 0 ? 1u : 0u;
        Write(9, lightFlagsInverse, "TC:EAD7C5..EAD7E7/997290");
        Write(10, lightFlagsInverse, "TC:EAD7E9..EAD7F1/9972D0");
        Write(11, (flags & 0xC0000) == 0 ? 0u : 3u, "TC:EAD7F1..EAD80F/997860");
        Write(3, (flags & 0x8000) == 0 ? 1u : 0u, "TC:EAD80F..EAD826/997090");
        Write(4, (flags & 0x10000) == 0 ? 1u : 0u, "TC:EAD826..EAD83B/996FD0");
        Write(12, earlyCurrent.RawQueryD5BA00 == 0 && earlyCurrent.RawQueryD5B9D0 == 0 ? 1u : 0u,
            "TC:EAD83B..EAD86A/997150;explicit-View1C70/1C78-raw-uint50");
        Retain(13, (flags & 0x20000) == 0 ? "TC:EAD86A..EAD8A1;VolumetricFog-absent;writer-skipped" :
            "TC:EAD86A..EAD8A1;context-bit17-set;writer-skipped");
        bool reflection = current.ReflectionQueryD57E20 != 0 && current.SelectedCamera1808Present;
        bool mode = (flags & 0x100000) != 0 && current.NativeColorMode != 0x60;
        Write(15, reflection ? (mode ? 2u : 0u) : (mode ? 2u : 1u), "TC:EAD8A1..EAD8FC/9970D0");
        if (pin.Refraction)
        {
            bool registered = (current.CommonViewFlags8 & 0x200) != 0 && current.QueryD5BeA0 &&
                (current.CommonViewResource788Registered || current.CommonViewResource78CRegistered || current.CommonViewResource8B8Registered);
            Write(14, registered ? 0u : 1u, "TC:EAD900..EAD976/997050;Refraction-bit2/source-nonocean");
        }
        else Retain(14, "TC:EAD900..EAD976;Refraction-bit2-clear;writer-skipped");
        var feature = G1ShaderFeatureRowReader.ReadRow(actualCurrentFeature48);
        var match = G1ShaderFeatureMatcher.Select(feature.Words.ToArray(), tables.Features.Select(r => r.Feature).ToArray());
        if (match.RowIndex < 0) throw new InvalidDataException("Early F103 current feature has no source matcher result.");
        var variant = tables.Variants.Match(desired) ??
            throw new InvalidDataException("Early F103 complete96-byte variant has no exact source row; unresolved native fallback cannot be substituted.");
        uint[] dimensions = [(uint)match.RowIndex, 0, 0, 0, 0, 0, (uint)variant.Index];
        var combination = tables.ResolveCombination(dimensions, 0);
        var selected = new OriginalDeferredEarlyF103SelectedSource(source, acceptedCamera, earlyCurrent,
            actualCurrentFeature48.ToArray(), desired, match, variant, combination, writes.AsReadOnly());
        if (selected.VertexProgramSha256 != FullscreenVertexSha256)
            throw new InvalidDataException("Early F103 fullscreen vertex program differs from its pinned source.");
        return selected;
    }

    private static LoadedOriginalDeferredTexturePlan ReadPinnedSource(LoadedOriginalDeferredTexturePlan source, Pin pin)
    {
        if (source.Graph.Source.FileId != 0x024496EE || source.Graph.Source.OverrideSource is not null ||
            source.Graph.Source.DecodedSha256 != GraphSha256 || Hash(source.Graph.Source.Bytes) != GraphSha256 ||
            source.Shader.Source.FileId != pin.File || source.Shader.Source.OverrideSource is not null ||
            source.Shader.Source.DecodedSha256 != pin.Sha || Hash(source.Shader.Source.Bytes) != pin.Sha ||
            source.TextureObjects.Source.FileId != 0x93FB3280 || source.TextureObjects.Source.OverrideSource is not null ||
            source.TextureObjects.Source.DecodedSha256 != TextureObjectsSha256 || Hash(source.TextureObjects.Source.Bytes) != TextureObjectsSha256)
            throw new InvalidDataException("Early F103 requires its pinned original graph, shader and texture-object bytes.");
        var graph = new LoadedRenderGraph(RenderGraphReader.Read(source.Graph.Source.Bytes), source.Graph.Source);
        var node = graph.Document.Nodes[source.Node.Index];
        uint U32(uint hash) => WordArgument(node, hash);
        var ocean = node.Call.Arguments.Single(a => a.NameHash == 0xE8F3DC36);
        var view = node.Call.Arguments.Single(a => a.NameHash == 0x9075EDA0);
        var reference = source.ShaderReference;
        if (node.Call.FactoryClassId != 0xF103DB00 || U32(0x702A1052) != pin.Object ||
            ocean.DescriptorWord != 0x01000001 || ocean.RawValue.Length != 1 || ocean.RawValue[0] != 0 ||
            view.DescriptorWord != 0x01000001 || view.RawValue.Length != 1 || view.RawValue[0] != 0 ||
            U32(0x59BD6FC4) != 8 || source.ViewId != 0 || source.LightGroupId != 8 ||
            reference.ShaderObject.ObjectId != pin.Object || reference.ShaderObject.ClassId != 0x8687085E ||
            reference.ShaderObject.Kind != DokObjectKind.Inline || reference.ShaderFileHash != pin.File ||
            reference.G1SReference.NameHash != 0x193025DF || reference.G1SReference.TypeCode != 5 ||
            reference.G1SReference.ElementCount != 1 || reference.G1SReference.RawValue.Length != 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(reference.G1SReference.RawValue) != pin.File)
            throw new InvalidDataException("Early F103 source node, View/group or explicit ShaderObject/file reference changed.");
        var shader = new LoadedShaderContainer(G2ShaderContainerReader.Read(source.Shader.Source.Bytes), source.Shader.Source);
        var configs = G1ShaderConfigurationReader.Read(shader.Source.Bytes, shader.Container);
        if (configs.Records.Any(c => c.Name == "Refraction") != pin.Refraction ||
            configs.Records.Any(c => c.Name == "ThinSurfaceBSDF") != pin.Thin ||
            configs.Records.Any(c => c.Name is "VolumetricFog" or "SSSS"))
            throw new InvalidDataException("Early F103 source plugin branches changed.");
        if (pin.Thin)
        {
            var thin = configs.Records.Single(c => c.Name == "ThinSurfaceBSDF");
            if (thin.Find(0xFEB72A6D)?.RawValue != pin.TransSpec || thin.Find(0x2BC1B848)?.RawValue != pin.AtDistance ||
                thin.Find(0x42B7E6DD)?.RawValue != pin.TransTarget)
                throw new InvalidDataException("Early F103 ThinSurface source constructor mode changed.");
        }
        return source with { Graph = graph, Node = node, Shader = shader,
            ShaderReference = reference with { Node = node, ShaderObjectArgument = node.Call.Arguments.Single(a => a.NameHash == 0x702A1052) },
            TextureObjects = new(DokReader.Read(source.TextureObjects.Source.Bytes), source.TextureObjects.Source) };
    }

    internal static LoadedOriginalDeferredTexturePlan ReadTexturePlan(LoadedOriginalDeferredTexturePlan source, int combinationIndex)
        => ReadLoadedSource(source).TexturePlans.GetOrAdd(combinationIndex,
            index => ReadTexturePlanAtImport(ReadLoadedSource(source).Source, index));

    private static LoadedOriginalDeferredTexturePlan ReadTexturePlanAtImport(LoadedOriginalDeferredTexturePlan source, int combinationIndex)
    {
        var resources = source.Shader.ReadG1ResourceBindings(combinationIndex);
        int pixelIndex = resources.Combination.Stages.Single(s => s.StageCode == 0 && s.Entry is not null).Entry!.ProgramIndex;
        var declarations = source.Shader.ReadResourceDefinitions(pixelIndex);
        if (WordArgument(source.Node, 0xCA398DA4) != 0 || WordArgument(source.Node, 0x5102842C) == 0)
            throw new InvalidDataException("Early F103 requires its explicit individual-target RR texture plan.");
        (string Role, short Encoded, uint Hash)[] fields =
            [("D1", 8, 0xB7CCDD1F), ("AB", 128, 0x33E0132B), ("RS", 129, 0x454BF54B), ("NS", 130, 0xF43A3947), ("RR", 131, 0x5102842C)];
        var inputs = new List<OriginalDeferredTextureInput>();
        foreach (var field in fields)
        {
            uint key = WordArgument(source.Node, field.Hash);
            var texture = OriginalRenderTextureResources.Read(source.TextureObjects, key);
            var mapping = resources.Entries.Single(r => r.PrimaryEncoded == field.Encoded);
            var declaration = declarations.Bindings.Single(b => b.InputType == 2 && b.Name.Text == mapping.Name);
            uint dimension = texture.Kind == OriginalRenderTextureKind.Texture2D ? 4u : 5u;
            if (mapping.SlotIndex < 0 || declaration.BindPoint != (uint)mapping.SlotIndex || declaration.BindCount != 1 ||
                declaration.Dimension != dimension || declaration.ReturnType != 5)
                throw new InvalidDataException("Selected early F103 source texture and named pixel declaration differ.");
            inputs.Add(new(field.Role, field.Encoded, source.Node.Call.Arguments.Single(a => a.NameHash == field.Hash), mapping, declaration, texture));
        }
        return source with { ResourceBindings = resources, PixelDefinitions = declarations, Inputs = inputs.AsReadOnly() };
    }
    private static uint WordArgument(CompiledGraphNode node, uint hash)
    {
        var argument = node.Call.Arguments.Single(a => a.NameHash == hash);
        if (argument.DescriptorWord != 0x05000001 || argument.RawValue.Length != 4)
            throw new InvalidDataException($"Early F103 source argument {hash:X8} has a different type.");
        return BinaryPrimitives.ReadUInt32LittleEndian(argument.RawValue);
    }
    private static uint Word(byte[] bytes, int index) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4, 4));
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
