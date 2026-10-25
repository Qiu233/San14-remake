using System.Buffers.Binary;
using System.Numerics;
using San14.Data;
using San14.Data.Formats;

/// <summary>The actual prepared View inputs which are not part of the season
/// numeric packet. Null primary light means an absent current View1B18 owner.</summary>
public sealed record G1EffectMapRuntimeSelection(uint ContextFlags, uint TableFeatureWord,
    bool LowResolutionOwnerPresent, bool LowResolutionOwnerEnabled, bool SceneInputPresent,
    uint? PrimaryLightObjectId);

public sealed record G1EffectMapInputFrame(G1EffectDrawCamera CpuCamera,
    G1EffectGpuCameraConstants GpuCamera,
    Func<G1EffectDrawPacket, G1EffectGpuPacketInputs> PacketInputs,
    IReadOnlyList<G1EffectGpuFrameMissing> Missing,
    G1EffectDirectComputeView? DirectView = null,
    G1EffectDirectLightingClusterPrepared? LightingCluster = null,
    uint LightingIndexFormat = 0);

/// <summary>One normal map generation's Effect2 preparation. Original resources
/// are read only at construction. Each frame borrows its accepted Main, actual
/// registered current lights and prepared Sky media; no cache files are read.</summary>
public sealed class G1EffectMapInputs
{
    private readonly D3D11CurrentSeasonResources resources;
    private readonly DeferredEarlyIndirectPatternMapState pattern;
    private readonly LoadedSkyPatternProjectionMatrixSource patternProjection;
    private readonly G1EffectGpuSampler depthSampler;
    private readonly CommonRenderSettings common;
    private readonly float ambientRate;
    private readonly uint blendMode;
    private readonly RenderTextureDefinition depthTarget;
    private readonly uint tableFeatureWord;
    private readonly G1EffectDirectLightingCluster lightingCluster;
    private readonly LoadedShaderContainer lightingConsumer;
    private readonly SkyWeatherNativePowerSource lightingPower;
    private readonly uint lightingIndexFormat;

    public G1EffectMapInputs(GameResourceRepository repository, D3D11CurrentSeasonResources resources)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(resources);
        this.resources = resources;
        var objects = repository.LoadObjects("system", 0x93FB3280);
        depthTarget = RenderTextureResources.Read(objects, 0xE13DDB4F);
        var samplerSource = RenderSamplerSourceAdapters.Load(repository.SourceDirectory, resources.Source.Locale);
        depthSampler = new(RenderSamplerSourceAdapters.DecodeKey(samplerSource,
            RenderSamplerSourceAdapters.FinalRenderTextureKey(samplerSource, depthTarget)).EncodeD3D11Descriptor());
        pattern = LoadedDeferredEarlyIndirectPatternMapSource.Load(repository, resources.Source.Locale)
            .ConstructAfterSourceFactoryPreparation();
        patternProjection = LoadedSkyPatternProjectionMatrixSource.Load(repository, resources.Source.Locale);
        var commonObjects = repository.LoadObjects("KIDSSystemResource", CommonRenderResources.SourceFileId);
        common = resources.InitialCommonSettings;
        blendMode = CommonRenderResources.ReadScalar(commonObjects, 0xABA611F7, 4);
        var view = repository.LoadObjects("system", DefaultView0Source.ViewResourceId)
            .Document.Objects.Single(value => value.ObjectId == DefaultView0Source.ViewObjectId);
        tableFeatureWord = Word(view, 30, 0xF103F4C8, 5);
        lightingCluster = new(repository);
        lightingConsumer = HdrShaderResources.Load(repository)[HdrShaderResources.MainLightingNode].Shader;
        lightingPower = SkyWeatherNativePowerSource.Create(resources.Source.Locale);
        uint maximum(uint hash) => BinaryPrimitives.ReadUInt32LittleEndian(view.Properties.Single(value => value.NameHash == hash).RawValue);
        lightingIndexFormat = maximum(0x91221B9A) > 65535 || maximum(0x2103C71C) > 65535 ? 42u : 57u;
        if (resources.Source.CameraSource.Selection.SecondPackage is not null)
            throw new NotSupportedException("Effect Main preparation requires its actual paired season producer.");
        ambientRate = Scalar(resources.Source.CameraSource.Selection.FirstPackage.Camera, 977, 0x08367252);
    }

    /// <summary>The current native map renderer resets Q before Main and its
    /// implemented writers contribute only400/40/20. None writes bank bit17;
    /// it owns no low-resolution Effect2 wrappers. Zero here is this explicit
    /// bank-relevant projection, not a captured complete Q-word snapshot.</summary>
    public G1EffectMapRuntimeSelection OwnedRendererSelection(uint? currentPrimaryLightObjectId) =>
        new(0, tableFeatureWord, false, false, false, currentPrimaryLightObjectId);

    /// <summary>The current native PreparedView clears1B18 and has no external
    /// record publisher for it. Kind-one base association publishes1B20 only;
    /// terrain/army population does not imply a primary modifier record.</summary>
    public G1EffectMapRuntimeSelection OwnedRendererSelection(D3D11CurrentSeasonFrame current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return OwnedRendererSelection((uint?)null);
    }

    public G1EffectMapInputFrame Prepare(D3D11CurrentSeasonFrame current, int width, int height,
        SkyMainResourceOwner? currentMainSky = null) =>
        Prepare(current, width, height, OwnedRendererSelection(current), currentMainSky);

    public G1EffectMapInputFrame Prepare(D3D11CurrentSeasonFrame current, int width, int height,
        G1EffectMapRuntimeSelection selection, SkyMainResourceOwner? currentMainSky = null)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(selection);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (!ReferenceEquals(current.Rgb.Inputs.Source.Selection.FirstPackage, resources.Source.CameraSource.Selection.FirstPackage))
            throw new InvalidDataException("Effect frame belongs to another installed season resource generation.");
        var bytes = current.PreparedMain;
        if (bytes.Length != 0x3C0) throw new InvalidDataException("Effect frame requires the complete prepared Main prefix.");
        var lens = CameraLens.DecodePrefix(bytes.Span[..0x160]);
        if (lens.IsOrthographic) throw new NotSupportedException("Effect Main requires its actual orthographic camera producer.");
        var worldToView = Matrix(bytes, 0x80);
        var projection = Matrix(bytes, 0xC0);
        var cameraToWorld = NativeMatrixMath.Invert(worldToView, out float viewDeterminant);
        var projectionToView = NativeMatrixMath.Invert(projection, out float projectionDeterminant);
        if (viewDeterminant == 0 || projectionDeterminant == 0)
            throw new InvalidDataException("Current effect camera matrix is singular.");
        float tangent = NativeCameraTangent.Tangent1020A90(lens.VerticalFovRadians * .5f, NativeCameraTangentDispatch.Sse);
        // AA9FE0's independent symmetric, far-normalized CPU projection.
        Matrix4x4 effectProjection = new(1f / (tangent * lens.Far * lens.Aspect), 0, 0, 0,
            0, 1f / (tangent * lens.Far), 0, 0,
            0, 0, -(lens.Near + lens.Far) / ((lens.Far - lens.Near) * lens.Far), -1f / lens.Far,
            0, 0, lens.Near * -2f / (lens.Far - lens.Near), 0);
        var eye = Vector(bytes, 0x10);
        var cpuCamera = new G1EffectDrawCamera(TerrainFrustumMath.Multiply(worldToView, effectProjection),
            cameraToWorld, effectProjection, new(eye.X, eye.Y, eye.Z), lens.Near, lens.Far);
        if (depthTarget.PixelFormat != 0x6C)
            throw new NotSupportedException("Effect MainD1 requires its actual depth-sampling mode producer.");
        var port = new G1EffectGpuViewport(0, 0, width, height, width, height,
            width, height, 0, 0, width, height, true).Prepare();
        var gpuCamera = new G1EffectGpuCameraConstants(Matrix(bytes, 0x100), projectionToView,
            eye, lens.Near, lens.Far, lens.ReverseDepth, 3, true, false, 0, 1, port, port);
        var primary = selection.PrimaryLightObjectId is { } primaryId
            ? current.Lights.Single(light => light.ObjectId == primaryId) : null;
        int selector = common.ViewAux3F2;
        D3D11CurrentSeasonLight? outer;
        if (selector == 0) outer = primary;
        else
        {
            if (selector > 32) throw new InvalidDataException("Effect base-light group exceeds the current source mask.");
            uint mask = 1u << (selector - 1);
            outer = current.Lights.FirstOrDefault(light => (light.GroupMask & ~mask) == 0);
        }
        if (outer is null)
            throw new NotSupportedException("Effect requires its actual View1940 fallback-light producer.");
        bool hasModifier = primary?.Lighting.Modifier.Present == true;
        bool suppressAmbient = !common.InitialAuxiliary.LightingOptions.HasFlag(CommonLightingOptions.PreserveAmbient);
        // D32's nonzero Aux selector takes the modifier exclusively from1B18.
        // An absent owner does not fall back to the base light's modifier.
        var preparedLighting = hasModifier
            ? outer.Lighting.PrepareLighting(primary!.Lighting, suppressAmbient).Lighting
            : PrepareUnmodifiedLighting(outer.Lighting, suppressAmbient);
        Vector4 firstColor = preparedLighting.Lights[0].Color;
        var scene = new DeferredSceneInputs(Lighting: preparedLighting,
            Camera: new(worldToView, Matrix(bytes, 0x100), eye),
            Fog: DeferredSceneConstants.PrepareSharedFog(outer.State.Fog,
                SkySceneSourceDefaults.SharedContextInitializer().FogEnabled120,
                new(firstColor.X, firstColor.Y, firstColor.Z)),
            Ambient: outer.Lighting.PrepareAmbient(ambientRate),
            Occlusion: hasModifier ? new(primary!.Lighting.Modifier.OcclusionRates) : null);
        Vector4 flex = outer.Lighting.Ambient;
        if (hasModifier)
        {
            float factor = primary!.Lighting.Modifier.AmbientFactor;
            flex = new(flex.X * factor, flex.Y * factor, flex.Z * factor, flex.W);
        }
        uint ActualWord(int at, int ordinal, uint hash, uint type)
        {
            foreach (var word in current.PreparedWords) if (word.Offset == at) return word.Bits;
            var camera = current.Mode.AcceptedMainFrame.Camera;
            if (camera.CurrentSeasonPostEffectScalars is { } scalars && scalars.TryReadWord(at, out uint value)) return value;
            return Word(resources.Source.CameraSource.Selection.FirstPackage.Camera, ordinal, hash, type);
        }
        int lightGroup = unchecked((int)ActualWord(0xA00, 24, 0x0611DBC4, 4));
        float directionScale = BitConverter.UInt32BitsToSingle(ActualWord(0xA28, 129, 0x0D8D3389, 8));
        uint shadowMode = ActualWord(0xA3C, 26, 0x96E32C4A, 4);
        var feature = G1EffectGpuFeatureSource.Prepare(new(selection.TableFeatureWord, outer.State.Fog.Mode,
            true, lightGroup, outer.Lighting.ShadowModes.Receive, outer.Lighting.ShadowModes.Local,
            directionScale, shadowMode)).ToBytes();
        int bank = G1EffectGpuFeatureSource.SelectEffect2RuntimeBank(selection.ContextFlags,
            selection.LowResolutionOwnerPresent, selection.LowResolutionOwnerEnabled, selection.SceneInputPresent);
        var missing = new List<G1EffectGpuFrameMissing>();
        var patternConstants = PreparePattern(current, currentMainSky, outer, missing);
        var lightConstants = new G1EffectGpuLightConstants(flex, scene);
        if (blendMode != 1)
            missing.Add(new(-1, "Blend", "Effect Main requires the actual renderer recipe production for its current Common3BC mode."));
        var directView = new G1EffectDirectComputeView(cpuCamera.ClipMatrix,
            new(cameraToWorld.M11, cameraToWorld.M12, cameraToWorld.M13, cameraToWorld.M14),
            new(cameraToWorld.M21, cameraToWorld.M22, cameraToWorld.M23, cameraToWorld.M24),
            new(effectProjection.M11 * .5f, effectProjection.M22 * .5f),
            new Vector4(worldToView.M13, worldToView.M23, worldToView.M33, worldToView.M43));
        var clusterMain = new D3D11PrimaryForwardMainFields(lens.Near,lens.Far,lens.ReverseDepth,
            BitConverter.UInt32BitsToSingle(ActualWord(0xE84,166,0xE836FC0A,8)),
            BitConverter.UInt32BitsToSingle(ActualWord(0xE88,167,0x0AB376BC,8)),width,height,
            // Fresh global construction; this renderer has no temporary scale setter.
            1,0,0);
        var cluster = lightingCluster.PrepareCurrent(new(worldToView,projection,lens.IsOrthographic,
            checked((uint)width),checked((uint)height),0,0,lightingIndexFormat,ReadOnlyMemory<float>.Empty),
            lightingConsumer,clusterMain,DeferredClusterFloatLogPath.Sse,lightingPower);
        return new(cpuCamera, gpuCamera, PacketInputs, missing.AsReadOnly(), directView,cluster,lightingIndexFormat);

        G1EffectGpuPacketInputs PacketInputs(G1EffectDrawPacket packet)
        {
            if (packet.IsTail)
                throw new NotSupportedException("Effect tail requires its actual special141/147 scope preparation.");
            uint recipeIndex = packet.MeshSubdrawIndex is null ? packet.HeaderFlags >> 3 & 7 : packet.HeaderFlags & 7;
            uint[] recipes = [0x00151440, 0x00111440, 0x00111140, 0x00111442, 0x00111142, 0x00101140, 0x00001100];
            if (blendMode != 1 || recipeIndex >= recipes.Length)
                throw new NotSupportedException("Effect row requires its actual current renderer blend recipe.");
            var effect = G1EffectGpuEffectConstants.CreateFreshOwned(packet, packet.AlphaReference != 0);
            return new(bank, feature, new(188, 0, uint.MaxValue),
                new(gpuCamera, effect, lightConstants, patternConstants),
                G1EffectGpuBlend.EncodePackedRecipe(recipes[recipeIndex]), External);
        }
    }

    private static DeferredLightingInputs PrepareUnmodifiedLighting(HdrLightState light, bool suppressAmbient)
    {
        int count = light.Directional.Count;
        while (count > 1)
        {
            var color = light.Directional[count - 1].Color;
            if (color.X > 0 || color.Y > 0 || color.Z > 0) break;
            count--;
        }
        Vector4 ambient = light.Ambient;
        if (suppressAmbient) ambient = new(0, 0, 0, ambient.W);
        return new(light.Directional.Take(count).ToArray(), ambient);
    }

    private G1EffectGpuPatternConstants? PreparePattern(D3D11CurrentSeasonFrame current,
        SkyMainResourceOwner? sky, D3D11CurrentSeasonLight selected, List<G1EffectGpuFrameMissing> missing)
    {
        int cameraKind = unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(current.PreparedMain.Span[0x3AC..]));
        if (sky is null || cameraKind == 5)
        {
            missing.Add(new(-1, "Pattern", "Effect Pattern needs its current prepared Sky owner and actual palette selection."));
            return null;
        }
        var program = D3D11AtmosphereInitialConstantPreparation.SelectSkyProgram(current.Present,
            selected.State, sky.Program.Resources, sky.Sampling.Presence);
        sky.RequireCurrent(program);
        var sta = current.Rgb.Inputs.Source.CloudTextures.SkylightSta;
        if (!sky.Media.Textures.Any(texture => texture.Slot == 0) || sta is null)
        {
            missing.Add(new(-1, "Pattern", "Effect special Pattern requires the actual prepared Main STA resource."));
            return null;
        }
        float scale = BinaryPrimitives.ReadSingleLittleEndian(current.PreparedMain.Span[0x27C..]);
        var weather = current.Present.CloudWeather.State;
        bool noPatternWrapper = !(weather.Coverage > 0) || !(weather.CirrusCoverage > 0) ||
            scale == 0 || cameraKind is < 1 or > 4;
        if (!noPatternWrapper)
        {
            if (!current.CommonPolicy.EnableSkyDrawing)
            {
                missing.Add(new(-1, "Pattern", "Current Sky retains a selected Pattern wrapper without running its projection; that retained matrix owner is required."));
                return null;
            }
            // The current owned native pass prepares this wrapper, publishes
            // D49EE0's matrix before material/draw, then consumes it at188.
            // Use that same numerical producer and same prepared base light.
            // A native allocation/draw failure still fails the frame receipt.
            Vector4 direction = selected.Lighting.Directional[0].Direction;
            var projection = patternProjection.Prepare(SkyPatternProjectionInputs.ReadCurrentCamera(
                current.PreparedMain.Span, new(direction.X, direction.Y, direction.Z)), NativeCameraTangentDispatch.Sse);
            return new(projection.Matrix180, scale != 0 ? scale : pattern.Pattern,
                [Vector(pattern.PatternPlaneSubAndPattern1, 0), Vector(pattern.PatternPlaneSubAndPattern1, 16)]);
        }
        // The current E46EC0 weather/scale/kind gate selects palette index4.
        // No one of its four Pattern wrappers is selected; D74F30 retains
        // this real factory-reset matrix. Camera27C alone may write scale.
        return new(Matrix(pattern.WorldToPointMatrix, 0), scale != 0 ? scale : pattern.Pattern,
            [Vector(pattern.PatternPlaneSubAndPattern1, 0), Vector(pattern.PatternPlaneSubAndPattern1, 16)]);
    }

    private G1EffectGpuFrameBinding? External(G1EffectGpuExternalBinding external)
    {
        var declaration = external.Declaration;
        if (external.StageCode != 0) return null;
        bool depth = external.Registrations.Any(entry => entry.PrimaryEncoded == 35 && entry.Name == "sDepth");
        if (depth)
            return declaration.InputType == 3
                ? G1EffectGpuFrameBinding.StandaloneSampler(external.StageCode, declaration.BindPoint, depthSampler)
                : declaration.InputType == 2
                    ? G1EffectGpuFrameBinding.Graph(external.StageCode, declaration.BindPoint, G1EffectGpuGlobalTexture.MainDepthMirror) : null;
        if (declaration.InputType != 2) return null;
        // These two are direct fixed setter slots in the selected original
        // Effect2 ABI, not entries in the encoded local texture registration.
        if (declaration.BindPoint == 25 && declaration.Name.Text == "sAutoHDRScaleExposure")
            return G1EffectGpuFrameBinding.Graph(0, 25, G1EffectGpuGlobalTexture.AutoExposure);
        if (declaration.BindPoint == 27 && declaration.Name.Text == "sS2fAccl")
            return G1EffectGpuFrameBinding.Graph(0, 27, G1EffectGpuGlobalTexture.ScatteringFogAcceleration);
        return null;
    }

    private static uint Word(DokObject camera, int ordinal, uint hash, uint type)
    {
        var property = camera.Properties[ordinal];
        if (property.NameHash != hash || property.TypeCode != type || property.ElementCount != 1 || property.RawValue.Length != 4)
            throw new InvalidDataException("Effect Main camera scalar has another source type or identity.");
        return BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
    }
    private static float Scalar(DokObject camera, int ordinal, uint hash) =>
        BitConverter.UInt32BitsToSingle(Word(camera, ordinal, hash, 8));
    private static float F(ReadOnlyMemory<byte> bytes, int at) => BinaryPrimitives.ReadSingleLittleEndian(bytes.Span[at..]);
    private static Vector4 Vector(ReadOnlyMemory<byte> bytes, int at) => new(F(bytes, at), F(bytes, at + 4), F(bytes, at + 8), F(bytes, at + 12));
    private static Matrix4x4 Matrix(ReadOnlyMemory<byte> bytes, int at) => new(F(bytes, at), F(bytes, at + 4), F(bytes, at + 8), F(bytes, at + 12),
        F(bytes, at + 16), F(bytes, at + 20), F(bytes, at + 24), F(bytes, at + 28),
        F(bytes, at + 32), F(bytes, at + 36), F(bytes, at + 40), F(bytes, at + 44),
        F(bytes, at + 48), F(bytes, at + 52), F(bytes, at + 56), F(bytes, at + 60));
}
