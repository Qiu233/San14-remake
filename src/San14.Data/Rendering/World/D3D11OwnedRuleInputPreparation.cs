using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Small initialization inputs for project rules and loaded RDB
/// declarations. Current camera/weather writes remain with their live owners.</summary>
public static class D3D11OwnedRuleInputPreparation
{
    public static void WriteFreshClockAndDefaultPlayers(GameResourceRepository repository, string inputRoot)
    {
        var common = D3D11TimingInputPreparation.LoadSourceCommon(repository);
        D3D11TimingInputPreparation.WriteSourceCreatedFrameClockInputs(
            Path.Combine(inputRoot, "frame-clock-input"), common);
        WriteDefaultPlayers(Path.Combine(inputRoot, "default-player-phase-input"),
            new ArmyGraphicsActions(repository));
    }

    public static void WriteDefaultPlayers(string directory, ArmyGraphicsActions actions)
    {
        Directory.CreateDirectory(directory);
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, "default-player-loop-source.bin")));
        writer.Write(4u);
        for (int category = 0; category < 4; category++)
        {
            var action = actions.Actions[(category, 0)];
            writer.Write((uint)category); writer.Write(action.Duration); writer.Write(action.LoopDeclaration);
        }
    }

    /// <summary>Only the source-created Common scope: its exclusion word is
    /// constructor zero until a settings overlay is installed.</summary>
    public static void WriteFreshView(string directory, GameResourceRepository repository,
        LoadedSceneResources scene, CameraViewSettings currentCamera,
        MapSeasonDirectionalLightSourceSet directional,
        HdrShadowModes selectedShadowModes)
    {
        var common = D3D11TimingInputPreparation.LoadSourceCommon(repository);
        var terrain = TerrainViewPublicationSource.ReadFieldScene(TerrainParameterResources.Read(scene));
        var queue = ModelRendererStateSourceReader.ReadQueue(repository.LoadRenderGraph("system", 0x024496EE), 54);
        var records = MapSourceCreatedWorldKind1Publication.ReadSeasonSourceRecords(directional);
        Directory.CreateDirectory(directory);
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, "profile.bin")));
        writer.Write(0x31505756u); writer.Write(records.Count);
        writer.Write(currentCamera.Shadow.LightGroup); writer.Write(currentCamera.Shadow.DirectionScale);
        writer.Write(currentCamera.Shadow.CascadeMode); writer.Write(currentCamera.OceanGroup); writer.Write(currentCamera.RainGroup);
        writer.Write(terrain.Exclusion2B8); writer.Write((uint)selectedShadowModes.Receive);
        writer.Write(1u); writer.Write(common.Flags8); writer.Write((uint)common.ViewAux3F2);
        writer.Write(0u); writer.Write((uint)common.ModelGroupSelector400);
        writer.Write((uint)queue.Flag); writer.Write(queue.Flag2); writer.Write((uint)queue.ShaderStateOffset);
        foreach (var record in records)
        {
            writer.Write(record.DeclaredGroupMask); writer.Write(record.MaxPrimitiveNum);
            writer.Write((uint)record.SourceLightDataF20);
        }
        CommonRenderPolicyEncoding.WriteNativeInputs(directory,
            CommonRenderPolicy.FromAuxiliary(common.InitialAuxiliary));
        // These are only the actual TGK21 collection arguments. The Native
        // View owner supplies registration, preparation success and inherited
        // masks when the corresponding normal operations actually complete.
        var collections=LoadedDeferredEarlyShadowTaskModelCollectionSource.Load(repository);
        var collection=collections.ReadTask(21);
        File.WriteAllText(Path.Combine(directory,"collection-masks.txt"),
            FormattableString.Invariant($"{collection.FirstMask} {collection.SecondMask} {collection.ShadowMask}\n"));
        var fieldCollection=collections.ReadTask(22);
        File.WriteAllText(Path.Combine(directory,"field-collection-masks.txt"),
            FormattableString.Invariant($"{fieldCollection.FirstMask} {fieldCollection.SecondMask} {fieldCollection.ShadowMask}\n"));
    }

    public static void WriteWeather(string directory, SkySeasonMainCameraRgbFrame camera,
        SkySeasonMainWeatherFogFrame weather, Vector3 actualGlobalLightRgb)
    {
        weather.RequireCurrent(camera);
        void WriteSelection()
        {
            using var writer=new BinaryWriter(File.Create(Path.Combine(directory,"selection.bin")));
            writer.Write(0x31534657u); // WFS1: actual primary atmosphere used by weather sampling.
            writer.Write(camera.Inputs.Source.Controller.DeclaredTransmittanceTable);
        }
        void WritePower(BinaryWriter writer)
        {
            var power=camera.Inputs.Source.Transmittance.Power;
            for(int index=0;index<257;index++)writer.Write(power.Reciprocal(index));
            for(int index=0;index<257;index++)writer.Write(power.Logarithm(index));
            for(int index=0;index<64;index++)writer.Write(power.Exponential(index));
            for(int index=0;index<11;index++)writer.Write(power.Constant(index));
        }
        void WriteRgb()
        {
            using var writer=new BinaryWriter(File.Create(Path.Combine(directory,"effective-light-rgb.bin")));
            writer.Write(actualGlobalLightRgb.X);writer.Write(actualGlobalLightRgb.Y);writer.Write(actualGlobalLightRgb.Z);
        }
        var tables = camera.Inputs.Source.Transmittance.Tables;
        if(tables is null)
        {
            Directory.CreateDirectory(directory);
            WriteSelection();
            using(var writer=new BinaryWriter(File.Create(Path.Combine(directory,"power.bin"))))WritePower(writer);
            WriteRgb();
            return;
        }
        if(tables.Width!=64||tables.Height!=256)
            throw new NotSupportedException("The current Native weather input format requires a 64 by 256 atmosphere table.");
        var package = camera.Inputs.Source.Selection.FirstPackage.Camera;
        float Field(int ordinal, int lane = 0) => BinaryPrimitives.ReadSingleLittleEndian(
            package.Properties.Single(p => p.Index == ordinal).RawValue.AsSpan(lane * 4, 4));
        float Prefix(int offset) => BinaryPrimitives.ReadSingleLittleEndian(camera.SunMoonRgb.Prefix.Span.Slice(offset, 4));
        int[] ordinals = [770,879,880,923,925,924,896,897,898,899,900,901,920,973,974,921,595,919,914,915];
        // Above-atmosphere samples do not need an angle. Retain the actual
        // angular producer, rather than substitute an invented coordinate.
        float Angle(SkyWeatherAmbientSample ambient, Vector4 direction)
            => ambient.AngleCoordinate ?? SkyWeatherAmbientSampler.AngleCoordinate(
                camera.Inputs.Source.Transmittance, direction, NativeCameraTangentDispatch.Sse);
        float[] fields = [Prefix(0x1D0), Prefix(0x1D8), ..ordinals.Select(i => Field(i)),
            Field(926,0),Field(926,1),Field(926,2),Field(927,0),Field(927,1),Field(927,2),
            tables.MieExtinction.X,tables.MieExtinction.Y,tables.MieExtinction.Z,
            tables.RayleighExtinction.X,tables.RayleighExtinction.Y,tables.RayleighExtinction.Z,
            tables.PlanetRadius,tables.AtmoTopHeight,camera.Inputs.Phase.Parameters.Altitude,
            Angle(weather.SunAmbient,camera.SunMoonRgb.Directions.Sun),
            Angle(weather.MoonAmbient,camera.SunMoonRgb.Directions.Moon),
            Prefix(0x2C4),Prefix(0x2C8),Prefix(0x2CC),Prefix(0x308),Prefix(0x30C),Prefix(0x310),Prefix(0x1EC)];
        Directory.CreateDirectory(directory);
        WriteSelection();
        using (var writer = new BinaryWriter(File.Create(Path.Combine(directory, "profile.bin"))))
        {
            foreach (float field in fields) writer.Write(field);
            WritePower(writer);
        }
        File.WriteAllBytes(Path.Combine(directory, "ambient2d-half4.bin"), tables.Ambient2D.CopyBytes());
        WriteRgb();
    }
}
