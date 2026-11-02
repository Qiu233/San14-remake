namespace San14.Data;

/// <summary>Compatibility facade for one accepted frame's typed Cloud weather.
/// Declaration import and current preparation each belong to their actual owner.</summary>
public sealed class SkyAppliedSourceCloudWeatherFields
{
    private SkyAppliedSourceCloudWeatherFields(MapSourceCreatedSeasonLightingFrame present,
        SkyCloudWeatherState state)
    { AcceptedPresent = present; State = state; }
    public MapSourceCreatedSeasonLightingFrame AcceptedPresent { get; }
    public long FrameNumber => AcceptedPresent.FrameNumber;
    public string SourceProvenance => AcceptedPresent.SourceProvenance + ";owned-current-cloud-weather";
    public SkyCloudWeatherSettings Settings => State.Settings;
    public SkyCloudWeatherState State { get; }
    public float ReadFloat(int cameraOffset) => BitConverter.UInt32BitsToSingle(ReadBits(cameraOffset));
    public uint ReadBits(int cameraOffset) => SkyCloudWeatherEncoding.ReadCurrent(State, cameraOffset);
    public static IReadOnlyDictionary<int, uint> LoadSelectedReadset(MapSeasonCameraPackage package) =>
        SkyCloudWeatherEncoding.EncodeSelected(SkyCloudWeatherSettingsImport.Import(package));
    public void ValidateCurrent(MapSourceCreatedSeasonLightingFrame present)
    {
        if (!ReferenceEquals(AcceptedPresent, present))
            throw new InvalidDataException("Cloud weather fields belong to another accepted season frame.");
        present.AcceptedWeather.RequireCurrent(present.AcceptedSeasonCamera);
    }
    public static SkyAppliedSourceCloudWeatherFields PrepareCurrent(MapSourceCreatedSeasonLightingFrame present)
    {
        ArgumentNullException.ThrowIfNull(present);
        return present.CloudWeather;
    }
    internal static SkyAppliedSourceCloudWeatherFields Create(MapSourceCreatedSeasonLightingFrame present)
    {
        var inputs = present.AcceptedSeasonCamera.Inputs;
        var source = inputs.Source;
        if (!source.UsesSourceDatabaseReset || source.Selection.SecondPackage is not null ||
            (source.SuccessfulMainModeSelection is null && source.Selection.FirstPackage.Camera.ObjectId != MapSeasonCameraApplication.RootCameraObjectId))
            throw new NotSupportedException("Cloud weather continuation requires the actual source-created direct database package; edited weather/interpolated packages require their own writer.");
        var quality = inputs.MainPostSelection?.QualityPatch;
        var state = SkyCloudWeatherState.Prepare(source.CloudWeatherSettings,
            inputs.Weather.Climate.Cloudiness, inputs.Weather.Climate.Humidity,
            present.AcceptedWeather.SourceWeatherSaturation22C,
            quality?.CloudLowCostMode, quality?.CloudResolutionMode);
        return new(present, state);
    }
}
