using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Actual source identities already resolved by the current remake
/// pipeline. Reference50 is global27DDDE0 object's50 native wrapper; it must not
/// be inferred from a shader sampler name or silently substituted with Scene.</summary>
public sealed record OriginalMapPostEffect3ApplyFallbackBindingInputs(long FrameNumber,
    OriginalMapPostEffect3ApplyView WorkingScene,OriginalMapPostEffect3ApplyView ActualTarget,
    OriginalMapPostEffect3ApplyView? ReferenceTexture50,OriginalMapPostEffect3ApplyView? ToneShaderAuxF8,
    float ActualReferenceLuminance18,float ActualReciprocalGamma,string SourceEvidence);

public sealed record OriginalMapPostEffect3ApplyFallbackTextureBinding(int NativeSlot,
    OriginalMapPostEffect3ApplyView? Owner,uint CallSiteRva,string SourceEvidence);

public sealed record OriginalMapPostEffect3ApplyFallbackBindingPlan(long FrameNumber,
    bool UsesDirectTextureDrawPath,OriginalMapPostEffect3ApplyView ActualTarget,
    OriginalMapPostEffect3ApplyView? ToneShaderAuxF8,
    IReadOnlyList<OriginalMapPostEffect3ApplyFallbackTextureBinding> BeforeDraw,
    IReadOnlyList<OriginalMapPostEffect3ApplyFallbackTextureBinding> AfterDraw,
    IReadOnlyList<OriginalMapPostEffect3ApplyValueWrite> GenericShaderParameters,
    ReadOnlyMemory<byte> Native49Packet,string SourceEvidence)
{
    public int NativeShaderType=>UsesDirectTextureDrawPath?1:5;
    public uint DrawCallSiteRva=>0xEC39FD;
    /// <summary>Program selection remains the native shader-type/owner mapping;
    /// this receipt alone does not declare a SPK/G1S program index.</summary>
    public bool ContainsProgramSelection=>false;
}

public static class OriginalMapPostEffect3ApplyFallbackBindings
{
    public static OriginalMapPostEffect3ApplyFallbackBindingPlan Prepare(
        OriginalMapPostEffect3ApplyMode1Route route,OriginalMapPostEffect3ApplyFallbackViewportPlan viewport,
        OriginalMapPostEffect3ApplyFallbackBindingInputs input)
    {
        ArgumentNullException.ThrowIfNull(route);ArgumentNullException.ThrowIfNull(viewport);ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SourceEvidence);
        if(!route.ExecutesEc3360Fallback||input.FrameNumber!=route.FrameNumber||viewport.FrameNumber!=route.FrameNumber)
            throw new InvalidDataException("Fallback resources/viewport require the same actual mode1 owner frame.");
        foreach(var view in new[]{input.WorkingScene,input.ActualTarget,input.ReferenceTexture50,input.ToneShaderAuxF8})
            if(view is not null&&(view.FrameNumber!=input.FrameNumber||string.IsNullOrWhiteSpace(view.OwnerIdentity)))
                throw new InvalidDataException("Fallback native view is foreign or has no owned identity.");
        ArgumentNullException.ThrowIfNull(input.WorkingScene);ArgumentNullException.ThrowIfNull(input.ActualTarget);
        string evidence=route.SourceEvidence+";"+viewport.SourceEvidence+";"+input.SourceEvidence;
        var before=new List<OriginalMapPostEffect3ApplyFallbackTextureBinding>{new(0,input.WorkingScene,0xEC374C,evidence+";native876F30-op4-slot0-caller-r14")};
        var parameters=new List<OriginalMapPostEffect3ApplyValueWrite>();
        bool direct=viewport.UsesDirectTextureDrawPath;
        if(!direct)
        {
            if(input.ReferenceTexture50 is null||input.ToneShaderAuxF8 is null)
                throw new NotSupportedException("Tone fallback requires the actual reference50 texture and AuxF8 generic shader owners.");
            if(!float.IsFinite(input.ActualReferenceLuminance18)||input.ActualReferenceLuminance18<=0||
                !float.IsFinite(input.ActualReciprocalGamma))
                throw new InvalidDataException("Tone fallback requires finite current luminance and its actual source gamma result.");
            before.Add(new(1,input.ReferenceTexture50,0xEC37EB,evidence+";global27DDDE0+50-wrapper+10-native876F30-op4-slot1"));
            byte[] tone=new byte[16];BinaryPrimitives.WriteSingleLittleEndian(tone,D(1,M(input.ActualReferenceLuminance18,input.ActualReferenceLuminance18)));
            parameters.Add(new("vToneMapParams",0xEC3916,0xEC3360,tone,evidence+";EC38C2/EC38F3-generic-v20-name;binary32-square-then-reciprocal"));
            Add("fGamma",0xEC3948,input.ActualReciprocalGamma);
            Add("fToneCurveType",0xEC3994,viewport.FifthScalarXmm8);
        }
        // Native EC3A55..EC3A75 clears both texture slots even after type1 copy.
        var after=new[]{new OriginalMapPostEffect3ApplyFallbackTextureBinding(0,null,0xEC3A62,evidence),
            new OriginalMapPostEffect3ApplyFallbackTextureBinding(1,null,0xEC3A75,evidence)};
        return new(input.FrameNumber,direct,input.ActualTarget,direct?null:input.ToneShaderAuxF8,
            Array.AsReadOnly(before.ToArray()),Array.AsReadOnly(after),Array.AsReadOnly(parameters.ToArray()),viewport.Native49Packet.ToArray(),evidence);
        void Add(string name,uint at,float value)
        {byte[] bytes=new byte[4];BinaryPrimitives.WriteSingleLittleEndian(bytes,value);parameters.Add(new(name,at,0xEC3360,bytes,evidence+";generic-v20-key-index-native39-command"));}
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}
