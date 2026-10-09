using System.Buffers.Binary;
using San14.Data;
using San14.Data.Formats;

public partial class MapSceneView
{
    private SceneViewSourceCameraInputs ReadFreshSourceCameraInputs() =>
        CreateFreshSourceCameraInputs(_repository,_text.SourceLocale);

    public static SceneViewSourceCameraInputs CreateFreshSourceCameraInputs(
        GameResourceRepository repository,string locale)
    {
        var context=MainMapContextOwnerSource.LoadFreshHostDefaults(locale);
        var startup=MainMapCameraStartupSource.Load(repository,locale);
        return new(System.Numerics.Vector3.One,context.ContextOwner3E4Bit29,startup.StaticProjectionProfile,
            NativeCameraTangentDispatch.Sse,startup.OwnerFovOverride290AfterReset,
            startup.MaximumYOverrideAfterControllerReset,
            $"{context.SourceProvenance};{startup.SourceEvidence};"+
            "unparented-source-getter90-fallbackOne;remake-owned-SSE-tangent-dispatch");
    }

    private void ConstructSourceCameraTerrain()
    {
        if(_loadedSceneResources is not { } scene)return;
        _sourceMainCameraTerrain=CreateFreshSourceCameraTerrain(_repository,scene);
        SetMeta("source_main_camera_terrain",_sourceMainCameraTerrain.SourceProvenance);
    }

    public static MainMapCameraTerrainSource CreateFreshSourceCameraTerrain(
        GameResourceRepository repository,LoadedSceneResources scene)
    {
        // This is the remake's concrete successful source-created wrapper. Its
        // retained grid is prepared once and is shared with the camera sampler.
        var field=MapStaticModelGroundHeightSource.PrepareCurrentField(repository,scene,
            UnitWorldGroundHeightFieldPreparationBranch.RetainedWrapperCreated,true,[]);
        DokProperty[] positions=field.Terrain.RootObject.Properties.Where(p=>p.NameHash==0xA45AD0E0).ToArray();
        if(positions.Length!=1||positions[0].TypeCode!=8||positions[0].ElementCount!=3||positions[0].RawValue.Length!=12)
            throw new InvalidDataException("Source-created current Field requires its single declared position.");
        var raw=positions[0].RawValue;
        var position=new System.Numerics.Vector3(BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(0,4)),
            BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(4,4)),BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(8,4)));
        var edit=new MapStaticModelFieldPositionEdit(position);
        field=field with {Grid=UnitWorldGroundHeightWriters.SetPositionA0D650(field.Grid,position),CurrentEdits=new[]{edit}};
        return MainMapCameraTerrainSource.FromCurrentField(field);
    }

}
