using San14.Data;

/// <summary>Owns the imported Cloud inputs and all four atmosphere Plane
/// textures. Cloud draws and Pattern Data borrow these allocations.</summary>
public sealed class SceneViewNativeMapSkyCloudTextureOwner : IAsyncDisposable
{
    private readonly SkySceneTextures textures;
    private SkyAppliedSeasonCloudTextureSources sourceGroups;
    private IReadOnlyList<Sky2DeviceInput>? deviceInputs;
    private Sky2DeviceInput? staEntry2;
    private bool retiring;
    public SkyAppliedCloudTextureSet TextureSet { get; }
    public SkyCloudSourceTextureGroup StaPlaneSource => sourceGroups.RequireAtmospherePlane();

    private SceneViewNativeMapSkyCloudTextureOwner(SkyAppliedSeasonCloudTextureSources groups,
        SkyAppliedCloudTextureSet imported, SkySceneTextures textures)
    { sourceGroups=groups;TextureSet=imported;this.textures=textures; }

    public static SceneViewNativeMapSkyCloudTextureOwner ReadSource(
        SkyAppliedSourceCloudSelection selection, SkyAppliedSeasonCloudTextureSources groups)
    {
        var imported=SkyAppliedCloudTextureSet.Import(groups);
        SkyAppliedCloudTextureBindings.ValidateProgram(selection,imported);
        return Create(groups,imported);
    }
    internal static SceneViewNativeMapSkyCloudTextureOwner Create(
        SkyAppliedSeasonCloudTextureSources groups,SkyAppliedCloudTextureSet imported)
    {
        var selections=imported.CloudInputs.Select(input=>new SkySceneTextureSelection(Key(input.SourceBindPoint),input.Texture))
            .Append(new(StaEntry2Key,imported.StaInputs[2])).ToArray();
        return new(groups,imported,new SkySceneTextures(selections));
    }
    public void RequireCurrentOwner()=>ObjectDisposedException.ThrowIf(retiring,this);
    public void ValidateCurrent(SkyAppliedSourceCloudSelection selection)
    {
        RequireCurrentOwner();
        sourceGroups.RequireSelectedPackage(selection.AcceptedPresent.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage);
    }
    internal void InstallPreparedSeasonSources(SkyAppliedSeasonCloudTextureSources groups)=>sourceGroups=groups;

    public Task<IReadOnlyList<Sky2DeviceInput>> GetCurrentInputsAsync(SkyAppliedSourceCloudSelection selection)
    {ValidateCurrent(selection);return GetInputsAsync();}
    internal async Task<IReadOnlyList<Sky2DeviceInput>> GetInputsAsync()
    {
        RequireCurrentOwner();
        if(deviceInputs is not null)return deviceInputs;
        var inputs=new List<Sky2DeviceInput>();
        foreach(var input in TextureSet.CloudInputs)
            inputs.Add(await textures.GetInputAsync(input.SourceBindPoint,Key(input.SourceBindPoint)));
        RequireCurrentOwner();
        return deviceInputs=inputs.AsReadOnly();
    }
    private static string Key(uint slot)=>"native-Cloud/t"+slot;
    private const string StaEntry2Key="native-STA/plane/2";

    public Task<IReadOnlyList<PatternCameraStaReadyPlaneEntry>> BorrowReadyStaPlaneEntriesAsync(
        SkyAppliedSourceCloudSelection selection)
    {ValidateCurrent(selection);return BorrowReadyStaPlaneEntriesAsync();}
    internal async Task<IReadOnlyList<PatternCameraStaReadyPlaneEntry>> BorrowReadyStaPlaneEntriesAsync()
    {
        var inputs=await GetInputsAsync();
        staEntry2??=await textures.GetInputAsync(2,StaEntry2Key);
        Sky2DeviceInput[] descriptors=[inputs[6],inputs[5],staEntry2,inputs[4]];
        var entries=new PatternCameraStaReadyPlaneEntry[4];
        for(int index=0;index<entries.Length;index++)
            entries[index]=new(sourceGroups.RequireAtmospherePlane().Entries[index],descriptors[index],
                TextureSet.StaInputs[index].NativeViewFormat,RequireCurrentOwner);
        return Array.AsReadOnly(entries);
    }
    public ValueTask DisposeAsync(){retiring=true;return textures.DisposeAsync();}
}
