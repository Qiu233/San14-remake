using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Actual source identities already resolved by the current remake
/// pipeline. Reference50 is global object's50 native wrapper; it must not
/// be inferred from a shader sampler name or silently substituted with Scene.</summary>
public sealed record MapPostEffect3ApplyFallbackBindingInputs(long FrameNumber,
    MapPostEffect3ApplyView WorkingScene,MapPostEffect3ApplyView ActualTarget,
    MapPostEffect3ApplyView? ReferenceTexture50,MapPostEffect3ApplyView? ToneShaderAuxF8,
    float ActualReferenceLuminance18,float ActualReciprocalGamma,string SourceEvidence);

public sealed record MapPostEffect3ApplyFallbackTextureBinding(int NativeSlot,
    MapPostEffect3ApplyView? Owner,string SourceEvidence);

public sealed record MapPostEffect3ApplyFallbackBindingPlan(long FrameNumber,
    bool UsesDirectTextureDrawPath,MapPostEffect3ApplyView ActualTarget,
    MapPostEffect3ApplyView? ToneShaderAuxF8,
    IReadOnlyList<MapPostEffect3ApplyFallbackTextureBinding> BeforeDraw,
    IReadOnlyList<MapPostEffect3ApplyFallbackTextureBinding> AfterDraw,
    IReadOnlyList<MapPostEffect3ApplyValueWrite> GenericShaderParameters,
    ReadOnlyMemory<byte> Native49Packet,string SourceEvidence)
{
    public int NativeShaderType=>UsesDirectTextureDrawPath?1:5;

    /// <summary>Program selection remains the native shader-type/owner mapping;
    /// this receipt alone does not declare a SPK/G1S program index.</summary>
    public bool ContainsProgramSelection=>false;
}

public static class MapPostEffect3ApplyFallbackBindings
{
    public static MapPostEffect3ApplyFallbackBindingPlan Prepare(
        MapPostEffect3ApplyMode1Route route,MapPostEffect3ApplyFallbackViewportPlan viewport,
        MapPostEffect3ApplyFallbackBindingInputs input)
    {
        ArgumentNullException.ThrowIfNull(route);ArgumentNullException.ThrowIfNull(viewport);ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SourceEvidence);
        if(!route.ExecutesToneFallback||input.FrameNumber!=route.FrameNumber||viewport.FrameNumber!=route.FrameNumber)
            throw new InvalidDataException("Fallback resources/viewport require the same actual mode1 owner frame.");
        foreach(var view in new[]{input.WorkingScene,input.ActualTarget,input.ReferenceTexture50,input.ToneShaderAuxF8})
            if(view is not null&&(view.FrameNumber!=input.FrameNumber||string.IsNullOrWhiteSpace(view.OwnerIdentity)))
                throw new InvalidDataException("Fallback native view is foreign or has no owned identity.");
        ArgumentNullException.ThrowIfNull(input.WorkingScene);ArgumentNullException.ThrowIfNull(input.ActualTarget);
        string evidence=route.SourceEvidence+";"+viewport.SourceEvidence+";"+input.SourceEvidence;
        var before=new List<MapPostEffect3ApplyFallbackTextureBinding>{new(0,input.WorkingScene,evidence+";native-op4-slot0-caller-r14")};
        var parameters=new List<MapPostEffect3ApplyValueWrite>();
        bool direct=viewport.UsesDirectTextureDrawPath;
        if(!direct)
        {
            if(input.ReferenceTexture50 is null||input.ToneShaderAuxF8 is null)
                throw new NotSupportedException("Tone fallback requires the actual reference50 texture and AuxF8 generic shader owners.");
            if(!float.IsFinite(input.ActualReferenceLuminance18)||input.ActualReferenceLuminance18<=0||
                !float.IsFinite(input.ActualReciprocalGamma))
                throw new InvalidDataException("Tone fallback requires finite current luminance and its actual source gamma result.");
            before.Add(new(1,input.ReferenceTexture50,evidence+"global+50-wrapper+10-native-op4-slot1"));
            byte[] tone=new byte[16];BinaryPrimitives.WriteSingleLittleEndian(tone,D(1,M(input.ActualReferenceLuminance18,input.ActualReferenceLuminance18)));
            parameters.Add(new("vToneMapParams",MapPostEffect3WriteStage.FallbackvToneMapParams,tone,evidence+"-generic-v20-name;binary32-square-then-reciprocal"));
            Add("fGamma",MapPostEffect3WriteStage.FallbackfGamma,input.ActualReciprocalGamma);
            Add("fToneCurveType",MapPostEffect3WriteStage.FallbackfToneCurveType,viewport.FifthScalarXmm8);
        }
        // Native .. clears both texture slots even after type1 copy.
        var after=new[]{new MapPostEffect3ApplyFallbackTextureBinding(0,null,evidence),
            new MapPostEffect3ApplyFallbackTextureBinding(1,null,evidence)};
        return new(input.FrameNumber,direct,input.ActualTarget,direct?null:input.ToneShaderAuxF8,
            Array.AsReadOnly(before.ToArray()),Array.AsReadOnly(after),Array.AsReadOnly(parameters.ToArray()),viewport.Native49Packet.ToArray(),evidence);
        void Add(string name,MapPostEffect3WriteStage stage,float value)
        {byte[] bytes=new byte[4];BinaryPrimitives.WriteSingleLittleEndian(bytes,value);parameters.Add(new(name,stage,bytes,evidence+";generic-v20-key-index-native39-command"));}
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}
