using System.Buffers.Binary;
using System.Numerics;
using San14.Data;

/// <summary>The normal View1 entry and its independent camera. It draws at
/// node189 into the caller's current map target, never UI offscreen View3.
/// The owned entry starts with no registered primary/modifier-light records;
/// its packed default is the actual D730C0/D76340 constructor branch.</summary>
internal sealed class G1EffectStoryViewInputs
{
    private readonly byte[] feature;
    private readonly uint blendMode;

    internal G1EffectStoryViewInputs(GameResourceRepository repository)
    {
        var view = repository.LoadObjects("system", DefaultView0Source.ViewResourceId)
            .Document.Objects.Single(row => row.ObjectId == DefaultView0Source.ViewObjectId);
        var source = view.Properties.Single(row => row.NameHash == 0xF103F4C8);
        if (source.TypeCode != 5 || source.ElementCount != 1 || source.RawValue.Length != 4)
            throw new InvalidDataException("story_effect.view1_table_feature");
        uint tableFeature = BinaryPrimitives.ReadUInt32LittleEndian(source.RawValue);
        feature = G1EffectGpuFeatureSource.Prepare(new(TableFeatureWord:tableFeature,FogMode:0,
            SelectedLightPresent:false,SelectedLightGroup:0,ShadowReceiveType:0,LocalShadowType:0,
            ShadowDirectionScale:0,ShadowCameraMode:0)).ToBytes();
        blendMode = CommonRenderResources.ReadScalar(
            repository.LoadObjects("KIDSSystemResource", CommonRenderResources.SourceFileId), 0xABA611F7, 4);
    }

    internal G1EffectMapInputFrame Prepare(StoryEffectCommonCameraFrame camera,
        G1EffectMapInputFrame targetInputs, int actualWidth, int actualHeight)
    {
        if (actualWidth <= 0 || actualHeight <= 0) throw new InvalidDataException("story_effect.view1_actual_target_extent");
        var geometry = camera.Geometry;
        var lens = geometry.Lens;
        var view = geometry.Matrices.WorldToView80;
        var cameraToWorld = geometry.Matrices.ViewToWorld3F6EB0();
        float tangent = NativeCameraTangent.Tangent1020A90(lens.VerticalFovRadians * .5f, camera.TangentDispatch);
        Matrix4x4 projection = new(1f / (tangent * lens.Far * lens.Aspect), 0, 0, 0,
            0, 1f / (tangent * lens.Far), 0, 0,
            0, 0, -(lens.Near + lens.Far) / ((lens.Far - lens.Near) * lens.Far), -1f / lens.Far,
            0, 0, lens.Near * -2f / (lens.Far - lens.Near), 0);
        var cpu = new G1EffectDrawCamera(TerrainFrustumMath.Multiply(view, projection), cameraToWorld,
            projection, new(geometry.Eye.X, geometry.Eye.Y, geometry.Eye.Z), lens.Near, lens.Far);
        // The fresh View1 rectangle is 0,0,1,1. D75E50 scales it by this
        // actual current color target's dimensions, not a fixed screen extent.
        var port = new G1EffectGpuViewport(0, 0, actualWidth, actualHeight, actualWidth, actualHeight,
            actualWidth, actualHeight, 0, 0, actualWidth, actualHeight, true).Prepare();
        var gpu = G1EffectGpuCameraConstants.FromCurrentMatrices(view, geometry.Matrices.ProjectionC0,
            geometry.Eye, lens.Near, lens.Far, lens.ReverseDepth, targetInputs.GpuCamera.DepthSamplingMode,
            (camera.ClipProfile & 2) != 0, false, 0, 1, port, port);
        float defaultColor = BitConverter.UInt32BitsToSingle(0x3E19999A);
        var light = new G1EffectGpuLightConstants(new(defaultColor, defaultColor, defaultColor, 1), new());
        var direct = new G1EffectDirectComputeView(cpu.ClipMatrix,
            new(cameraToWorld.M11, cameraToWorld.M12, cameraToWorld.M13, cameraToWorld.M14),
            new(cameraToWorld.M21, cameraToWorld.M22, cameraToWorld.M23, cameraToWorld.M24),
            new(projection.M11 * .5f, projection.M22 * .5f),
            new Vector4(view.M13, view.M23, view.M33, view.M43));
        return new(cpu, gpu, Packet, [], direct)
            { EmitterTransformProvider = camera.EmitterBinding, ExternalBindings = targetInputs.ExternalBindings };

        G1EffectGpuPacketInputs Packet(G1EffectDrawPacket packet)
        {
            uint index = packet.MeshSubdrawIndex is null ? packet.HeaderFlags >> 3 & 7 : packet.HeaderFlags & 7;
            ReadOnlySpan<uint> recipes = [0x00151440, 0x00111440, 0x00111140, 0x00111442, 0x00111142, 0x00101140, 0x00001100];
            if (blendMode != 1 || index >= recipes.Length) throw new NotSupportedException("story_effect.view1_blend_recipe");
            return new(G1EffectGpuFeatureSource.SelectEffect2RuntimeBank(0, false, false, false), feature, new(189, 1, uint.MaxValue),
                new(gpu, G1EffectGpuEffectConstants.CreateFreshOwned(packet, packet.AlphaReference != 0), light),
                G1EffectGpuBlend.EncodePackedRecipe(recipes[(int)index]), targetInputs.ExternalBindings);
        }
    }
}
