using San14.Data;

/// <summary>Integrates the shared CPU skin transport with the selected shadow
/// program. Device creation stays with the model shader owner.</summary>
public static class DeferredEarlyShadowArmyVertexTransport
{
    public static ModelShaderProgramPreparation Adapt(
        ModelShaderProgramPreparation prepared,ArmySkinVertexProfile profile)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        var transport=AdaptRaw(prepared,prepared.VertexTranslation,profile);
        var viewport=VertexShaderAdapter.AdaptPositionInput(transport.Bytes);
        var bindings=TranslatedShaderBindingReader.Read(
            prepared.Source.ReadResourceDefinitions(prepared.VertexProgramIndex),viewport.Bytes);
        return prepared with { VertexAdapter=viewport,VertexBindings=bindings,
            VertexDeviceTransportSignatures=ArmySkinVertexAdapter.DeviceTransportSignatures(prepared.VertexSignatures,profile) };
    }

    public static void RequireCurrentGates(TranslatedShaderBindings actualBindings,
        ArmySkinVertexProfile profile,ModelStageInputs actualVertex)
    {
        var buffer=actualBindings.SourceDefinitions.Buffers.Single();
        var skin=buffer.Variables.Single(v=>v.Name.Text=="bSkin");
        if(buffer.ByteSize is not (6256 or 14448)||skin.StartOffset!=6208||skin.ByteSize!=36||
            !actualVertex.Constants.TryGetValue(0,out var cb)||cb.Length!=buffer.ByteSize)
            throw new InvalidDataException("Early19 Army input transport needs the exact selected VS CB0 and actual skin fields.");
        ArmySkinVertexAdapter.ValidateGates(cb.Span[..6256],profile);
    }

    public static ArmySkinVertexAdaptation AdaptRaw(ModelShaderProgramPreparation prepared,
        ShaderSpirv translation, ArmySkinVertexProfile profile)
        => ArmySkinVertexAdapter.Adapt(translation, prepared.VertexSignatures, profile);
}
