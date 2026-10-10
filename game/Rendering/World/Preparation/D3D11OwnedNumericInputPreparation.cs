using System.Buffers.Binary;
using System.Numerics;
using San14.Data;
using San14.Data.Formats;

/// <summary>Initial transport of live owned numerical rules to native.
/// Resource media remain in their resource preparation caches.</summary>
public static class D3D11OwnedNumericInputPreparation
{
    public static void WriteFreshNativeInputs(GameResourceRepository repository,
        LoadedSceneResources scene, MainMapCameraFrame actualMain,
        D3D11CurrentSeasonFrame current, D3D11CurrentSeasonResources resources,
        D3D11CurrentSeasonLibrary library, string inputRoot, int outputWidth, int outputHeight)
    {
        D3D11OwnedRuleInputPreparation.WriteFreshClockAndDefaultPlayers(repository, inputRoot);
        var words = current.PreparedWords.ToDictionary(word => word.Offset, word => word.Bits);
        int selector = unchecked((int)words[0xA00]);
        if (selector is < 1 or > 32)
            throw new NotSupportedException("Initial View requires its current kind1 selector.");
        var selected = current.Lights.First(light => (light.GroupMask & (1u << (selector - 1))) != 0);
        D3D11OwnedRuleInputPreparation.WriteFreshView(Path.Combine(inputRoot, "view-continuous-input"),
            repository, scene, current.Rgb.Inputs.Source.Selection.FirstPackage, words,
            resources.Source.DirectionalDeclarations, selected.CompleteData.Span);
        var global = current.Lights.First(light => (light.GroupMask & 0x80) != 0).ShaderData328.Span;
        var rgb = new Vector3(BinaryPrimitives.ReadSingleLittleEndian(global[0x10..]),
            BinaryPrimitives.ReadSingleLittleEndian(global[0x14..]), BinaryPrimitives.ReadSingleLittleEndian(global[0x18..]));
        D3D11OwnedRuleInputPreparation.WriteWeather(
            Path.Combine(inputRoot, "weather-fog-camera-input", "initial", "weather"),
            current.Rgb, current.Present.AcceptedWeather, rgb);
        WriteAtmosphereProfile(inputRoot, current, outputWidth, outputHeight);

        string locale = actualMain.Camera.ExecutableLocale;
        var common = D3D11TimingInputPreparation.LoadSourceCommon(repository);
        var viewOwner = MainCameraView19B8Owner.ConstructFresh(
            DefaultView0Source.LoadDeclarationDefaults(repository), "native-map-exposure-fresh-main");
        var view = viewOwner.ProjectCurrentMain0(actualMain.Camera, "initial-owned-Main-view-mask");
        D3D11MapExposureInputPreparation.Load(repository, locale).WriteNativeInputs(
            Path.Combine(inputRoot, "map-exposure-input"), words[0x1510], unchecked((int)view.CurrentView19B8));
        var configuration = MapPostEffect3ControlledFreshCallerSource.Load(repository, locale)
            .CreateFreshOwnedConfiguration("native-initial-post-configuration");
        var projectionRules = D3D11NormalPostCameraInputPreparation.LoadRules(repository, locale);
        D3D11CurrentSeasonLight? projectionLight = current.ExposureLight.Present
            ? current.Lights.Single(light => light.ObjectId == current.ExposureLight.ObjectId) : null;
        D3D11NormalPostCameraInputPreparation.WriteNativeInputs(
            Path.Combine(inputRoot, D3D11NormalPostCameraInputPreparation.NativeDirectory),
            projectionRules, actualMain.Camera.FrameNumber, projectionLight?.CompleteData,
            projectionLight is null ? null : actualMain.Camera.FrameNumber, configuration.ProjectionConvention);
        WritePostCallers(repository, library, inputRoot, common, projectionRules.LockedAdaptationBits);
        WriteCloudWeather(repository, library, inputRoot);
    }

    /// <summary>Refresh only installed season declarations at a real load
    /// boundary. Current sparse Main stores keep native precedence.</summary>
    public static void WritePostCallers(GameResourceRepository repository,
        D3D11CurrentSeasonLibrary library, string inputRoot)
    {
        var common = D3D11TimingInputPreparation.LoadSourceCommon(repository);
        string locale = library.LoadedInstallations[0].Resources.Source.Locale;
        var rules = D3D11NormalPostCameraInputPreparation.LoadRules(repository, locale);
        WritePostCallers(repository, library, inputRoot, common, rules.LockedAdaptationBits);
        WriteCloudWeather(repository, library, inputRoot);
    }

    private static void WriteAtmosphereProfile(string inputRoot, D3D11CurrentSeasonFrame current,
        int outputWidth, int outputHeight)
    {
        string directory = Path.Combine(inputRoot, "atmosphere-camera-continuous-input", "initial");
        Directory.CreateDirectory(directory);
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, "profile.bin")));
        writer.Write(current.PreparedMain.Span); writer.Write(current.Atmosphere.NumericBytes.Span[..200]);
        writer.Write(outputWidth); writer.Write(outputHeight);
    }

    private static void WriteCloudWeather(GameResourceRepository repository,
        D3D11CurrentSeasonLibrary library, string inputRoot)
    {
        string directory = Path.Combine(inputRoot, "current-cloud-weather-input");
        Directory.CreateDirectory(directory);
        var shader = repository.LoadShaderContainer("system", 0x42ED52ED);
        string[] names = ["g_vBaseTexScale","g_vDetailTexScale","g_vDistortTexScale","g_vCirrusTexScale",
            "g_vCirrusDistortTexScale","g_vRaySampleNum","g_vEarthRadius","g_vCloudParams1","g_vCloudParams2",
            "g_vCloudParams0","g_vDistortScale","g_vCirrusParams","g_vCirrusDistortScale","g_vErodeScale",
            "g_vHgParams0","g_vHgParams1","g_vSky2RayleighExtinction","g_vSky2MieExtinction","g_vStableDim","g_vClampValue"];
        WriteLayout(184, "main-layout.txt"); WriteLayout(47, "work-layout.txt");
        var seasons = library.LoadedInstallations.OrderBy(row => row.SeasonIndex).ToArray();
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, "profiles.bin")));
        writer.Write((uint)seasons.Length);
        foreach (var season in seasons)
        {
            var source = season.Resources.Source.CameraSource;
            var words = SkyAppliedSourceCloudWeatherFields.LoadSelectedReadset(source.Selection.FirstPackage);
            writer.Write((uint)season.SeasonIndex); writer.Write((uint)words.Count);
            foreach (var word in words.OrderBy(word => word.Key)) { writer.Write(word.Key); writer.Write(word.Value); }
            var tables = source.Transmittance.Tables;
            writer.Write(tables.PlanetRadius); Vector(tables.RayleighExtinction); Vector(tables.MieExtinction);
        }
        void Vector(Vector4 vector)
        { writer.Write(vector.X); writer.Write(vector.Y); writer.Write(vector.Z); writer.Write(vector.W); }
        void WriteLayout(int program, string filename)
        {
            var globals = shader.ReadResourceDefinitions(program).Buffers.Single();
            List<string> lines = [globals.ByteSize.ToString(System.Globalization.CultureInfo.InvariantCulture)];
            for (int index = 0; index < names.Length; index++)
                if (globals.Variables.SingleOrDefault(variable => variable.Name.Text == names[index]) is { } variable)
                    lines.Add(FormattableString.Invariant($"{index} {variable.StartOffset} {variable.ByteSize / 4}"));
            File.WriteAllLines(Path.Combine(directory, filename), lines);
        }
    }

    private static void WritePostCallers(GameResourceRepository repository,
        D3D11CurrentSeasonLibrary library, string inputRoot,
        CommonRenderSettings common, uint lockedAdaptationBits)
    {
        var main = MapCameraConstructionSource.ReadPerspective(
            repository.LoadObjects("system", 0x908AB6E2), MainMapCameraAdapter.MainCameraObjectId).Camera;
        uint Word(DokObject camera, int ordinal) => BinaryPrimitives.ReadUInt32LittleEndian(camera.Properties[ordinal].RawValue);
        string directory = Path.Combine(inputRoot, "current-season-resource-input", "post-caller");
        Directory.CreateDirectory(directory);
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, "caller-source.bin")));
        var seasons = library.LoadedInstallations.OrderBy(row => row.SeasonIndex).ToArray();
        writer.Write((uint)seasons.Length); writer.Write((uint)common.ViewAux3F2);
        writer.Write(common.Flags8); writer.Write(lockedAdaptationBits);
        foreach (var season in seasons)
        {
            var camera = season.Resources.Source.CameraSource.Selection.FirstPackage.Camera;
            uint mask = Word(main,1021), fade = Word(main,1020), lumi = Word(main,1022);
            if (Word(camera,1021) is uint selectedMask && selectedMask != 0)
            { mask = selectedMask; fade = Word(camera,1020); lumi = Word(camera,1022); }
            writer.Write((uint)season.SeasonIndex); writer.Write(mask); writer.Write(fade); writer.Write(lumi);
            writer.Write((uint)season.Resources.Lights.Count);
            foreach (var light in season.Resources.Lights)
            {
                writer.Write(light.ObjectId); writer.Write(light.GroupMask);
                writer.Write((uint)light.DefaultCaller.Bytes.Span[0x10B0]);
            }
        }
    }
}
