using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

public sealed record OriginalMapPostEffect3LensGhostDraw(uint VertexCount,uint InstanceCount,
    uint StartVertex,uint StartInstance,uint TemporaryBlendWord,bool TemporaryBlendEnabled,
    bool TemporaryDepthTestEnabled,int SpriteBufferParameterIndex,uint RequiredSpriteReadBytes,string SourceEvidence)
{
    /// <summary>0x40031 encodes primitive4 in its upper16 bits. The native
    /// conversion table maps4 to D3D11 topology5 (TriangleStrip).</summary>
    public uint NativePrimitiveCode=>4;
    public uint NativeD3DPrimitiveTopology=>5;
    public bool UsesIndirectArgumentBuffer=>false;
    public bool EstablishesCompleteTargetContents=>false;
}

/// <summary>Source-specific factory4 pass preparation. Native Select submits
/// counted resource bindings before Before mutates them. Commit consumes
/// that resulting table, not a fresh copy of the counted rows.</summary>
public sealed class OriginalMapPostEffect3LensExecutionRecipe
{
    private OriginalMapPostEffect3LensExecutionRecipe(OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3ParameterLensFlareInitialization init,OriginalMapPostEffect3LensFlareBeforePacket before,
        IReadOnlyList<OriginalMapPostEffect3Binding> bindings,OriginalMapPostEffect3LensGhostDraw? ghost)
        =>(FrameNumber,ContextOwnerIdentity,SourceSha256,SourcePassIndex,Before,OrderedEffectiveBindings,GhostDraw,Initialization)=
            (init.FrameNumber,init.OwnerIdentity,init.SourceSha256,before.PassIndex,before,bindings,ghost,init);
    internal OriginalMapPostEffect3ParameterLensFlareInitialization Initialization{get;}
    public string Locale=>Initialization.Locale;
    public long FrameNumber{get;}
    public string ContextOwnerIdentity{get;}
    public string SourceSha256{get;}
    public int SourcePassIndex{get;}
    public OriginalMapPostEffect3LensFlareBeforePacket Before{get;}
    public bool ExecutesPass=>Before.BeforeHookAllowsPass;
    public OriginalMapPostEffect3DispatchExtent ActualDispatch=>Before.Dispatch;
    public IReadOnlyList<OriginalMapPostEffect3Binding> OrderedEffectiveBindings{get;}
    public OriginalMapPostEffect3LensGhostDraw? GhostDraw{get;}
    public IReadOnlyList<OriginalMapPostEffect3LensFlareBeforeTextureSubmission> DescriptorTextureSubmissions=>
        Array.AsReadOnly(Before.OrderedTextureSubmissions.Where(s=>s.DescriptorTextureOffset is not null).ToArray());
    public bool CommitsFunctorHistory=>false;
    public bool EstablishesCompleteResourceContents=>false;
    public string SourceEvidence=>Before.SourceEvidence+
        "-Select-counted-bindings;-Before-overrides;-commit-result"+
        (GhostDraw?.SourceEvidence??"-non-GhostSprite-returns-true;-source-automatic-dispatch");

    public static OriginalMapPostEffect3LensExecutionRecipe Prepare(
        OriginalMapPostEffect3ParameterState currentState,OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3ParameterLensFlareInitialization init,
        OriginalMapPostEffect3LensPresetParameterOwner actualLens,int passIndex,
        OriginalMapPostEffect3LensFlareBeforeInputs currentBeforeInputs)
    {
        ArgumentNullException.ThrowIfNull(currentState);ArgumentNullException.ThrowIfNull(allocation);
        ArgumentNullException.ThrowIfNull(init);ArgumentNullException.ThrowIfNull(actualLens);
        OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);actualLens.ValidatePointerWrites(currentState);
        if(currentState.FrameNumber!=init.FrameNumber||currentState.FrameNumber!=actualLens.FrameNumber||
            currentState.SourceSha256!=init.SourceSha256||currentState.OwnerIdentity!=init.OwnerIdentity||
            actualLens.Source.ExecutableLocale!=init.Locale||
            allocation.ContextOwnerIdentity!=init.OwnerIdentity||passIndex is <20 or >43)
            throw new InvalidDataException("Lens execution requires the exact source module, owned current initialization and pinned Lens frame.");
        var before=OriginalMapPostEffect3ParameterLensFlareBefore.Prepare(allocation,init,passIndex,currentBeforeInputs);
        var ordered=allocation.Passes[passIndex].ActiveBindings.ToList();
        foreach(var s in before.OrderedTextureSubmissions.Where(s=>s.ParameterIndex is not null))
        {
            byte mask=s.NativeStageCode switch{0=>1,2=>4,_=>throw new NotSupportedException("Lens Before submits an unsupported native stage.")};
            short view=s.Role switch{OriginalMapPostEffect3BindingRole.ShaderResourceRead=>1,
                OriginalMapPostEffect3BindingRole.UnorderedAccessReadWrite=>0,_=>throw new NotSupportedException("Lens Before has an unsupported submission role.")};
            // -1 source row denotes the separately proven hook call, never a
            // counted table row or a made-up resource index.
            ordered.Add(new(-1,OriginalMapPostEffect3BindingTable.TextureParameters,s.ParameterIndex!.Value,view,
                checked((short)s.RegisterIndex),checked((sbyte)(s.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead?-1:s.MipSelector)),
                -1,mask,s.Role,0,ReadOnlyMemory<byte>.Empty));
        }
        OriginalMapPostEffect3LensGhostDraw? ghost=null;
        if(before.BeforeHookAllowsPass&&passIndex==34)
        {
            var pass=allocation.Source.Passes[34];var refs=pass.ProgramReferences.Where(r=>r.StageIndex is not null).ToArray();
            if(pass.NameHash!=OriginalMapPostEffect3ParameterState.PrimaryNameHash("GhostSprite")||
                refs.Length!=2||!refs.Any(r=>r.StageCode==1&&r.StageIndex==0)||!refs.Any(r=>r.StageCode==0&&r.StageIndex==2)||pass.RawBytes.Span[0x38]!=0)
                throw new InvalidDataException("Lens custom draw differs from its original GhostSprite VS0/PS2 source pass.");
            byte[] split=actualLens.SplitBytes.ToArray();uint count=BinaryPrimitives.ReadUInt32LittleEndian(split.AsSpan(0x58,4));
            if(count!=actualLens.Source.GhostCount)throw new InvalidDataException("Lens GhostCount differs from its typed source Split.");
            int buffer=allocation.BufferParameters.Single(b=>b.NameHash==OriginalMapPostEffect3ParameterState.PrimaryNameHash("GhostSprite")).Index;
            var shape=OriginalMapPostEffect3AllocationBufferShapeReader.Read(allocation.BufferAllocations.Single(a=>a.Parameter.Index==buffer));
            uint instances=Math.Min(count,14u);
            if(buffer!=2||shape.Dimension!=OriginalMapPostEffect3BufferDimension.Structured||shape.StructureByteStride!=28||
                shape.ByteCount<instances*28||!shape.CreatesShaderResourceView)
                throw new InvalidDataException("GhostSprite draw requires the original source structured buffer2 and its native stride28.");
            ghost=new(4,instances,0,0,0x00114400,true,false,buffer,instances*28,
                "TC;SC;packet40031=4,min(Split58,14),0,0"+
                "temporary-blend114400/enabled;temporary-depth1BF0;cullE8-inherited;restore-inherited-states;"+
                "opcode40031->;-primitive4->D3D5-TriangleStrip;no-indirect-call;SplitSha="+
                Convert.ToHexString(SHA256.HashData(split)));
        }
        // Buffer3's allocator flag exposes an indirect-capable view, but these
        // default passes use it as CS data. Buffer5 belongs to another functor.
        if(ordered.Any(b=>b.Table==OriginalMapPostEffect3BindingTable.BufferParameters&&b.ParameterIndex==5))
            throw new InvalidDataException("Functor0 Lens source unexpectedly references the other functor's buffer5.");
        return new(allocation,init,before,ordered.AsReadOnly(),ghost);
    }
}
