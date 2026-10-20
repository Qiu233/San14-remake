namespace San14.Data;

public sealed partial class MapSeasonCameraPostEffectScalars
{
    internal MapSeasonCameraPostEffectScalars WithSelectedCurrentReaders(MapSeasonCameraSelection selected, MapCameraConstructionPacket camera)
    {
        ValidateForCamera(camera); selected.Validate();
        if (selected.SecondPackage is not null) throw new NotSupportedException("Season optical readers require a direct package.");
        return new(camera, OpticsPatch.Then(CameraOpticsImport.Current(selected.FirstPackage)), selectionEvidence, QualityPatch, VegetationWind);
    }
}
