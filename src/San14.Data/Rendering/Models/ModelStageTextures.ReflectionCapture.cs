using San14.Data.Formats;

namespace San14.Data;

public static partial class ModelStageTextures
{
    /// <summary>Actual state12 PB2 profile: ordinary registered material images
    /// plus the separately owned AmbientDif Cube at t5/s5. The Cube is not a
    /// ModelData texture slot and must be borrowed from the current light owner.</summary>
    public static ModelStageTexturePlan ResolveReflectionCapture(LoadedModelTextureResources model, int drawIndex,
        NamedModelShader provider, LoadedShaderContainer shader, ReadOnlySpan<uint> actualSevenIndices,
        uint actualPass, ModelStageTextureRuntime runtime)
    {
        if(actualSevenIndices.Length!=7)throw Error("capture requires all actual selected shader indices");
        int combination=shader.ReadG1Bindings().ResolveCombination(actualSevenIndices,actualPass).Index;
        var selectedBases=G2ShaderTextureBaseReader.ReadSelectedRows(shader.Source.Bytes,actualSevenIndices[0],actualSevenIndices[4]);
        return ResolveCore(model,drawIndex,provider,shader,combination,runtime,true,selectedBases);
    }

    /// <summary>Native state12 keeps the actual null SRV. Its selected PB2
    /// metadata admits the real three-material t4 and four-material t5 Cube
    /// variants; it does not emulate null with a replacement image.</summary>
    public static ModelStageTexturePlan ResolveNativeReflectionCapture(LoadedModelTextureResources model, int drawIndex,
        NamedModelShader provider, LoadedShaderContainer shader, ReadOnlySpan<uint> actualSevenIndices,
        uint actualPass, ModelStageTextureRuntime runtime)
    {
        if (actualSevenIndices.Length != 7) throw Error("capture requires all actual selected shader indices");
        int combination = shader.ReadG1Bindings().ResolveCombination(actualSevenIndices, actualPass).Index;
        var selectedBases = G2ShaderTextureBaseReader.ReadSelectedRows(shader.Source.Bytes, actualSevenIndices[0], actualSevenIndices[4]);
        return ResolveCore(model, drawIndex, provider, shader, combination, runtime, true, selectedBases,
            nativeReflectionNullSrv: true);
    }

    private static uint CaptureAmbientCubeRegister(G1ShaderResourceBindings metadata, bool nativeNullSrv)
    {
        _ = CaptureMaterialRegistrations(metadata, nativeNullSrv);
        return checked((uint)metadata.Entries.Single(value => value.RawAuxiliary == 8 && value.PrimaryEncoded == 26).SlotIndex);
    }

    private static IReadOnlyList<G1ShaderResourceBinding> CaptureMaterialRegistrations(G1ShaderResourceBindings metadata,
        bool nativeNullSrv)
    {
        var external=metadata.Entries.Where(value=>value.RawAuxiliary!=0).OrderBy(value=>value.SlotIndex).ToArray();
        bool fourMaterials = external is [
            {Name:"sGxAmbLut",SlotIndex:4,RawAuxiliary:8,PrimaryEncoded:54,SecondarySamplerEncoded:0},
            {Name:"sAmbDif",SlotIndex:5,RawAuxiliary:8,PrimaryEncoded:26,SecondarySamplerEncoded:0},
            {Name:"sAmbSpc",SlotIndex:6,RawAuxiliary:8,PrimaryEncoded:27,SecondarySamplerEncoded:0}];
        bool threeMaterials = nativeNullSrv && external is [
            {Name:"sGxAmbLut",SlotIndex:3,RawAuxiliary:8,PrimaryEncoded:54,SecondarySamplerEncoded:0},
            {Name:"sAmbDif",SlotIndex:4,RawAuxiliary:8,PrimaryEncoded:26,SecondarySamplerEncoded:0},
            {Name:"sAmbSpc",SlotIndex:5,RawAuxiliary:8,PrimaryEncoded:27,SecondarySamplerEncoded:0}];
        if (!fourMaterials && !threeMaterials)
            throw Error("capture profile requires its three actual external environment registrations");
        return metadata.Entries.Where(value=>value.RawAuxiliary==0).ToArray();
    }

    private static void RequireReflectionCaptureCube(LoadedShaderContainer shader, DxbcResourceDefinitions definitions,
        uint actualCubeRegister)
    {
        if (!shader.ReadConfigurations().Records.Any(value => value.Name == "ReflectionProbeGBuffer"))
            throw Error("capture overload requires its actual ReflectionProbeGBuffer module");
        var cube = definitions.Bindings.Where(value => value.InputType == 2 && value.BindPoint == actualCubeRegister).ToArray();
        var sampler = definitions.Bindings.Where(value => value.InputType == 3 && value.BindPoint == actualCubeRegister).ToArray();
        if (cube is not [{ Name.Text: "sAmbDif", BindCount: 1, Dimension: 9, ReturnType: 5 }] ||
            sampler is not [{ Name.Text: "__smpsAmbDif", BindCount: 1 }])
            throw Error("capture profile requires its exact separately supplied AmbientDif Cube and sampler");
    }
}
