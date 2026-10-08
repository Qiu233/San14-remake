using System.Buffers.Binary;
using San14.Data;
using San14.Data.Formats;

public partial class OriginalSceneView
{
    private OriginalSceneViewSourceCameraInputs ReadFreshOriginalSourceCameraInputs() =>
        CreateFreshOriginalSourceCameraInputs(_repository,_text.SourceLocale);

    public static OriginalSceneViewSourceCameraInputs CreateFreshOriginalSourceCameraInputs(
        OriginalGameRepository repository,string locale)
    {
        var context=OriginalMainMapContextOwnerSource.LoadFreshHostDefaults(repository,locale);
        var startup=OriginalMainMapCameraStartupSource.Load(repository,locale);
        return new(System.Numerics.Vector3.One,context.ContextOwner3E4Bit29,startup.StaticProjectionProfile,
            OriginalNativeCameraTangentDispatch.Sse,startup.OwnerFovOverride290AfterReset,
            startup.MaximumYOverrideAfterControllerReset,startup.ImageBase,
            $"{context.SourceProvenance};{startup.SourceEvidence};"+
            "unparented-source-getter90-fallbackOne;remake-owned-SSE-tangent-dispatch");
    }

    private void ConstructOriginalSourceCameraTerrain()
    {
        if(_loadedSceneResources is not { } scene)return;
        _sourceMainCameraTerrain=CreateFreshOriginalSourceCameraTerrain(_repository,scene);
        SetMeta("source_main_camera_terrain",_sourceMainCameraTerrain.SourceProvenance);
    }

    public static OriginalMainMapCameraTerrainSource CreateFreshOriginalSourceCameraTerrain(
        OriginalGameRepository repository,LoadedSceneResources scene)
    {
        // This is the remake's concrete successful source-created wrapper. Its
        // retained grid is prepared once and is shared with the camera sampler.
        var field=OriginalMapStaticModelGroundHeightSource.PrepareCurrentField(repository,scene,
            OriginalUnitWorldGroundHeightFieldPreparationBranch.RetainedWrapperCreated,true,[]);
        DokProperty[] positions=field.Terrain.RootObject.Properties.Where(p=>p.NameHash==0xA45AD0E0).ToArray();
        if(positions.Length!=1||positions[0].TypeCode!=8||positions[0].ElementCount!=3||positions[0].RawValue.Length!=12)
            throw new InvalidDataException("Source-created current Field requires its single declared position.");
        var raw=positions[0].RawValue;
        var position=new System.Numerics.Vector3(BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(0,4)),
            BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(4,4)),BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(8,4)));
        var edit=new OriginalMapStaticModelFieldPositionEdit(position);
        field=field with {Grid=OriginalUnitWorldGroundHeightWriters.SetPositionA0D650(field.Grid,position),CurrentEdits=new[]{edit}};
        return OriginalMainMapCameraTerrainSource.FromCurrentField(field);
    }

}
