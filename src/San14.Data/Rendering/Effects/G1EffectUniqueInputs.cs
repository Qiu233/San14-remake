using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>The context which actually owns the two UI render tasks. These are
/// context and device values, independent of either dedicated camera declaration.</summary>
public sealed record G1EffectUniqueRendererInputs(uint ContextFlags3E4,
    uint NativeClipProfile, NativeCameraTangentDispatch TangentDispatch);

public sealed record G1EffectUniqueInputFrame(G1EffectDrawCamera CpuCamera,
    MapEffectAdmissionView? AdmissionView,
    Func<G1EffectDrawPacket, G1EffectGpuPacketInputs> PacketInputs,
    IReadOnlyList<string> Missing, bool ReverseDepth,
    Func<int, int, Matrix4x4?> EmitterTransformProvider,
    G1EffectUniqueTargetInputs Targets);

/// <summary>Normal declarations and fresh, owned UI renderer preparation for
/// the cutin and foreground lists. No current Main camera or light is borrowed.</summary>
public sealed class LoadedUniqueTacticEffectInputs
{
    private readonly G1EffectUniqueRendererInputs initialRenderer;
    private readonly byte[] feature;
    private readonly uint blendMode;
    private readonly G1EffectUniqueTargetSources targetSources;

    private LoadedUniqueTacticEffectInputs(LoadedUniqueTacticEffects effects,
        G1EffectUniqueRendererInputs initialRenderer, uint tableFeature, uint blendMode,
        G1EffectUniqueTargetSources targetSources)
    {
        Effects = effects; this.initialRenderer = initialRenderer; this.blendMode = blendMode;
        this.targetSources = targetSources;
        // D53440 resets Q3F0. This owner publishes only FX, so neither UI
        // View1B18/1B20 light list nor a low-resolution wrapper is registered.
        feature = G1EffectGpuFeatureSource.Prepare(new(tableFeature, 0, false, 0, 0, 0, 0, 0)).ToBytes();
    }

    public LoadedUniqueTacticEffects Effects { get; }

    public static LoadedUniqueTacticEffectInputs Load(GameResourceRepository repository) => Load(repository, "TC");

    public static LoadedUniqueTacticEffectInputs Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var effects = UniqueTacticEffectPublication.Load(repository);
        var view = effects.View.Document.Objects.Single(o => o.ObjectId == DefaultView0Source.ViewObjectId);
        var property = view.Properties.Single(p => p.NameHash == 0xF103F4C8);
        if (property.TypeCode != 5 || property.ElementCount != 1 || property.RawValue.Length != 4)
            throw new InvalidDataException("UI Effect2 requires the normal View table feature scalar.");
        uint tableFeature = BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
        var common = repository.LoadObjects("KIDSSystemResource", CommonRenderResources.SourceFileId);
        uint mode = CommonRenderResources.ReadScalar(common, 0xABA611F7, 4);
        // These factories describe the same native Host/device construction
        // used by the independent UI session. They provide no Main camera data.
        var host = MainMapContextOwnerSource.LoadFreshHostDefaults(locale);
        var projection = MainMapCameraStartupSource.Load(repository, locale);
        return new(effects, new(host.ContextFlags3E4, projection.StaticProjectionProfile,
            NativeCameraTangentDispatch.Sse), tableFeature, mode, new(repository, locale));
    }

    public G1EffectUniqueInputFrame CreateFrame(int selector, int actualWidth, int actualHeight) =>
        CreateFrame(selector, actualWidth, actualHeight, initialRenderer);

    public G1EffectUniqueInputFrame CreateFrame(int selector, int actualWidth, int actualHeight,
        G1EffectUniqueRendererInputs renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        if (actualWidth <= 0 || actualHeight <= 0) throw new ArgumentOutOfRangeException(nameof(actualWidth));
        var layer = Effects.Layer(selector);
        var targets = targetSources.CreateFrame(actualWidth, actualHeight);
        bool reverse = (renderer.ContextFlags3E4 & 0x20000000) != 0;
        var geometry = Effects.CreateCamera(selector, reverse, renderer.NativeClipProfile, renderer.TangentDispatch);
        var lens = geometry.Lens;
        Matrix4x4 worldToView = geometry.Matrices.WorldToView80;
        var cameraToWorld = geometry.Matrices.ViewToWorld3F6EB0();
        float tangent = NativeCameraTangent.Tangent1020A90(lens.VerticalFovRadians * .5f, renderer.TangentDispatch);
        // AA9FE0 uses this independent symmetric, far-normalized CPU lens.
        Matrix4x4 effectProjection = new(1f / (tangent * lens.Far * lens.Aspect), 0, 0, 0,
            0, 1f / (tangent * lens.Far), 0, 0,
            0, 0, -(lens.Near + lens.Far) / ((lens.Far - lens.Near) * lens.Far), -1f / lens.Far,
            0, 0, lens.Near * -2f / (lens.Far - lens.Near), 0);
        var eye = new Vector3(geometry.Eye.X, geometry.Eye.Y, geometry.Eye.Z);
        var cpu = new G1EffectDrawCamera(TerrainFrustumMath.Multiply(worldToView, effectProjection),
            cameraToWorld, effectProjection, eye, lens.Near, lens.Far);
        var port = new G1EffectGpuViewport(0, 0, actualWidth, actualHeight,
            actualWidth, actualHeight, actualWidth, actualHeight, 0, 0,
            actualWidth, actualHeight, true).Prepare();
        var gpu = G1EffectGpuCameraConstants.FromCurrentMatrices(worldToView,
            geometry.Matrices.ProjectionC0, geometry.Eye, lens.Near, lens.Far, reverse,
            targets.DepthSamplingMode, (renderer.NativeClipProfile & 2) != 0, false, 0, 1, port, port);
        var missing = new List<string>();
        if (blendMode != 1) missing.Add("UI Effect2 requires the actual Common blend mode recipe.");
        // The real View list entry owns a packed light, even with both light
        // lists absent. D730C0/D76340 initialize its color to binary32 .15
        // and alpha1; ABDB70 marks that payload present before Effect2 reads it.
        float fallbackColor = BitConverter.UInt32BitsToSingle(0x3E19999A);
        var light = new G1EffectGpuLightConstants(new(fallbackColor, fallbackColor, fallbackColor, 1), new());
        // Dedicated admission still uses the actual Main zoom at its caller.
        // A UI camera cannot supply that independent value.
        return new(cpu, null, Packet, missing.AsReadOnly(), reverse,
            UniqueTacticEmitterBinding.FromCamera(geometry), targets);

        G1EffectGpuPacketInputs Packet(G1EffectDrawPacket packet)
        {
            if (blendMode != 1)
                throw new NotSupportedException("UI Effect2 row requires its actual current blend recipe.");
            var effect = G1EffectGpuEffectConstants.CreateFreshOwned(packet,
                packet.RenderingType != 29 && packet.AlphaReference != 0);
            // E11A20 writes the offscreen descriptor's actual View index;
            // E85F70 consumes opcodes3/4 for the two dedicated UI Views.
            return new(G1EffectGpuFeatureSource.SelectEffect2RuntimeBank(0, false, false, false),
                feature, new((uint)layer.ViewSlot, (uint)layer.ViewSlot, uint.MaxValue),
                new(gpu, effect, light), PrepareBlend(packet), External);
        }

        G1EffectGpuFrameBinding? External(G1EffectGpuExternalBinding external)
        {
            if (external.StageCode != 0) return null;
            var declaration = external.Declaration;
            // D02010 leaves this independent owner's Common308 null. With
            // no HDR publisher the D32BE0 token41 gate does not bind an image.
            // The UI native consumer explicitly unbinds t25; the original PS
            // selects its own multiplier1 when that null texture returns0.
            if (declaration.InputType == 2 && declaration.BindPoint == 25 &&
                declaration.Name.Text == "sAutoHDRScaleExposure")
                return G1EffectGpuFrameBinding.Graph(0, 25, G1EffectGpuGlobalTexture.AutoExposure);
            if (external.Registrations.Any(entry =>
                entry.PrimaryEncoded == 35 && entry.Name == "sDepth"))
                return declaration.InputType == 2
                    ? G1EffectGpuFrameBinding.Graph(0, declaration.BindPoint, G1EffectGpuGlobalTexture.MainDepthMirror)
                    : declaration.InputType == 3
                        ? G1EffectGpuFrameBinding.StandaloneSampler(0, declaration.BindPoint, targets.Depth.Sampler) : null;
            // The RTT image belongs to the prepared forward scratch, not
            // the registered color target. Its native provider must populate
            // it from the actual preceding UI and particle rows before use.
            if (external.Registrations.Any(entry =>
                entry.PrimaryEncoded == 34 && entry.Name == "sRTT"))
                return declaration.InputType == 2
                    ? G1EffectGpuFrameBinding.Graph(0, declaration.BindPoint, G1EffectGpuGlobalTexture.SceneCapture)
                    : declaration.InputType == 3
                        ? G1EffectGpuFrameBinding.StandaloneSampler(0, declaration.BindPoint, targets.Capture.Sampler) : null;
            return null;
        }
    }

    private static byte[] PrepareBlend(G1EffectDrawPacket packet)
    {
        // AAA1E0's special recipe starts at00554400. D78E90 visits it
        // six times; the final UI alpha-destination selector is1. A4E8F0
        // selects this separate recipe for pass29; A50E90 keeps it.
        if (packet.RenderingType == 29) return G1EffectGpuBlend.EncodePackedRecipe(0x00150400);
        ReadOnlySpan<uint> recipes = [0x00151440, 0x00111440, 0x00111140,
            0x00111442, 0x00111142, 0x00101140, 0x00001100];
        uint index = packet.MeshSubdrawIndex is not null
            ? packet.HeaderFlags & 7 : packet.HeaderFlags >> 3 & 7;
        if (index >= recipes.Length)
            throw new NotSupportedException("UI Effect2 row has no original primary blend recipe.");
        uint recipe = recipes[(int)index];
        if (index < 6)
        {
            // View3/4 select D78E90's UI branch. It retains RGB factors/op,
            // clears alpha source/op and replaces alpha destination. The
            // secondary array is consumed by different callbacks, not these.
            ReadOnlySpan<uint> alphaDestination = [5, 1, 1, 5, 1, 0];
            recipe = (recipe & 0x000F0F0F) | alphaDestination[(int)index] << 20;
        }
        return G1EffectGpuBlend.EncodePackedRecipe(recipe);
    }
}
