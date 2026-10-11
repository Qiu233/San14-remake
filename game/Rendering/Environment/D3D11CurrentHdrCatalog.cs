using System.Buffers.Binary;
using San14.Data;

/// <summary>Loaded scene lighting policy. Program selection and layouts belong
/// to the resource bank and the current native View.</summary>
public sealed class D3D11CurrentHdrCatalog
{
    public float AmbientMapsRate { get; }
    public uint CommonLightingOptions { get; }
    public uint CommonEffectMode { get; }
    public uint CameraReflectionDeclaration { get; }
    public bool SuppressAmbient => (CommonLightingOptions & 0x800) == 0;
    public bool FogOwnerEnabled { get; }
    public HdrShaderResources Resources { get; }

    private D3D11CurrentHdrCatalog(HdrShaderResources resources, float ambientMapsRate,
        uint lightingOptions, bool fogEnabled,uint cameraReflectionDeclaration,uint commonEffectMode)
    {
        Resources=resources; AmbientMapsRate=ambientMapsRate;
        CommonLightingOptions=lightingOptions; FogOwnerEnabled=fogEnabled;
        CameraReflectionDeclaration=cameraReflectionDeclaration;
        CommonEffectMode=commonEffectMode;
    }

    public static D3D11CurrentHdrCatalog Create(GameResourceRepository repository,
        MainMapCameraFrame initialMain, HdrShaderResources resources)
    {
        var commonSource=repository.LoadObjects("KIDSSystemResource",CommonRenderResources.SourceFileId);
        var common=CommonRenderResources.ImportSettings(commonSource);
        var ambient=initialMain.Camera.Source.Camera.Properties.Single(property=>property.NameHash==0x08367252);
        if(ambient.TypeCode!=8 || ambient.ElementCount!=1 || ambient.RawValue.Length!=4)
            throw new InvalidDataException("Main ambient maps rate requires a scalar float declaration.");
        var reflection=initialMain.Camera.Source.Camera.Properties.Single(property=>property.NameHash==0x053A9DF4);
        if(reflection.TypeCode!=5 || reflection.ElementCount!=1 || reflection.RawValue.Length!=4)
            throw new InvalidDataException("Main reflection map requires its scalar resource key.");
        return new(resources,BinaryPrimitives.ReadSingleLittleEndian(ambient.RawValue),
            (uint)common.InitialAuxiliary.LightingOptions,SkySceneSourceDefaults.SharedContextInitializer().FogEnabled120,
            BinaryPrimitives.ReadUInt32LittleEndian(reflection.RawValue),
            CommonRenderResources.ReadScalar(commonSource,0xA1BA4B90,5));
    }
}
