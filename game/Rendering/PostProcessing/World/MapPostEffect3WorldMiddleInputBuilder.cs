using San14.Data;
using San14.Data.Formats;

/// <summary>The actual native compute sampler state after this World's Shaft
/// family. Source-disabled passes advance source order without fabricating a
/// dispatch; stage2 stock draws do not provide stage0 sampler capabilities.</summary>
public interface IMapPostEffect3MiddleNativeSamplerScope
{
    long FrameNumber{get;}
    string ContextOwnerIdentity{get;}
    string SourceEvidence{get;}
    void RequireCurrent();
    IReadOnlyList<IMapPostEffect3ComputeSamplerLease> LeaseForPass(int sourcePassIndex,
        IEnumerable<uint> actuallyDeclaredSamplerRegisters);
    void AcceptCompleted(MapPostEffect3ComputeCompletedPass completed);
    void AcceptSkipped(MapPostEffect3MiddleExecutionRecipe sourceDisabledRecipe);
}

/// <summary>Same-frame actual World inputs for source passes8..19. Borrows the
/// source middle executor, completed Shaft family and native sampler state.
/// It creates no replacement texture or sampler and recopies no native alias.</summary>
public sealed class MapPostEffect3WorldMiddleInputBuilder
{
    private readonly MapPostEffect3MiddleExecutor middle;
    private readonly MapPostEffect3FirstWorldPreparedPrefix prefix;
    private readonly MapPostEffect3ShaftExecutionOwner shaft;
    private readonly MapPostEffect3ShaftCompletedFamily family;
    private readonly IMapPostEffect3MiddleNativeSamplerScope samplers;
    private readonly SemaphoreSlim serial=new(1,1);
    private bool failed;
    private MapPostEffect3WorldMiddleInputBuilder(MapPostEffect3MiddleExecutor execution,
        MapPostEffect3FirstWorldPreparedPrefix world,MapPostEffect3ShaftExecutionOwner sourceShaft,
        MapPostEffect3ShaftCompletedFamily actualFamily,IMapPostEffect3MiddleNativeSamplerScope samplerScope)
        =>(middle,prefix,shaft,family,samplers)=(execution,world,sourceShaft,actualFamily,samplerScope);

    public static MapPostEffect3WorldMiddleInputBuilder Create(MapPostEffect3MiddleExecutor middle,
        MapPostEffect3FirstWorldPreparedPrefix prefix,MapPostEffect3ShaftExecutionOwner actualShaft,
        IMapPostEffect3MiddleNativeSamplerScope actualNativeSamplers)
    {
        ArgumentNullException.ThrowIfNull(middle);ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(actualShaft);ArgumentNullException.ThrowIfNull(actualNativeSamplers);
        var family=actualShaft.LastCompletedFamily??throw new InvalidDataException("Middle inputs must follow the actual completed or source-skipped Shaft family.");
        var owner=new MapPostEffect3WorldMiddleInputBuilder(middle,prefix,actualShaft,family,actualNativeSamplers);
        owner.RequireCurrent();
        if(middle.NextSourcePassIndex!=8)throw new InvalidDataException("World middle input owner starts at source pass8.");
        return owner;
    }

    public string SourceEvidence=>prefix.Begin.SourceEvidence+";"+samplers.SourceEvidence+
        ";actual-World-source-Middle8..19;complete-physical-alias/Scene/E13/source-noise-inputs;no-fixture-samplers";
    public void RequireCurrent()
    {
        prefix.RequireCurrent();samplers.RequireCurrent();
        if(!ReferenceEquals(shaft.LastCompletedFamily,family)||!ReferenceEquals(family.ComputeChain.Prefix,prefix)||
            !ReferenceEquals(shaft.Bank,middle.Compute.Bank)||!ReferenceEquals(middle.Parameters,prefix.Parameters)||
            !ReferenceEquals(middle.Textures,prefix.Textures)||samplers.FrameNumber!=prefix.Context.FrameNumber||
            samplers.ContextOwnerIdentity!=middle.Parameters.OwnerIdentity)
            throw new InvalidDataException("Middle source inputs lost their actual ordered World/Shaft/module/sampler generation.");
        middle.AllocationSides.ValidateFor(middle.Compute.Bank.Source);
    }

    /// <summary>Source false branches return null, including no sampler leases.
    /// Every required texture in a true branch retains its actual current alias
    /// and completed producer. Known native-null slots are left to the actual
    /// executor's source-null receipt; unknown slots still fail explicitly.</summary>
    public MapPostEffect3ComputeFrameInputs? BuildCurrentInputs()
    {
        if(failed)throw new InvalidOperationException("Discard this failed World middle attempt.");
        RequireCurrent();int pass=middle.NextSourcePassIndex;
        var recipe=MapPostEffect3MiddleExecutionRecipe.Prepare(middle.Parameters,pass);
        if(!recipe.ExecutesPass)return null;
        var allocation=middle.Compute.Bank.Source;
        var program=allocation.Source.Passes[pass].ProgramReferences.Where(p=>p.StageIndex is not null).ToArray();
        if(program is not [{StageCode:5}]||program[0].StageIndex is not int index)
            throw new InvalidDataException("Actual middle pass is not its counted source compute program.");
        var rdef=DxbcResourceDefinitionReader.Read(allocation.Source.GetProgram(5,index).Bytes.Span);
        var declared=rdef.Bindings.Where(b=>b.InputType==3).ToArray();
        if(declared.Any(b=>b.BindCount!=1||b.BindPoint is not (7 or 8)))
            throw new NotSupportedException("Middle has a shader-visible sampled-wrapper sampler outside its closed native CS7/8 continuation.");
        // Pinned CS6..17 actually declare no s0..6. Counted wrapper setters
        // may update those slots, but they do not authorize invented GPU inputs.
        var selectedSamplers=samplers.LeaseForPass(pass,declared.Select(b=>b.BindPoint));
        if(!selectedSamplers.Select(s=>s.SourceRegister).Order().SequenceEqual(declared.Select(b=>b.BindPoint).Order())||
            selectedSamplers.Any(s=>s.NativeStageOrdinal!=0||s.FrameNumber!=recipe.FrameNumber||s.ContextOwnerIdentity!=recipe.ContextOwnerIdentity))
            throw new InvalidDataException("Middle source sampler continuation does not match its exact declared stage/register set.");
        foreach(var sampler in selectedSamplers)sampler.RequireCurrent();
        var native=allocation.Passes[pass].ActiveBindings.ToArray();
        if(native.Any(b=>b.Table==MapPostEffect3BindingTable.BufferParameters))
            throw new NotSupportedException("Actual middle input builder has no native buffer producer for this source pass.");
        var reads=native.Where(b=>b.Table==MapPostEffect3BindingTable.TextureParameters&&
            b.Role==MapPostEffect3BindingRole.ShaderResourceRead).Select(b=>b.ParameterIndex).Distinct().ToArray();
        // Source null is a real initialized pointer value, including the
        // native arg12=0 velocity slot. The executor captures these exact
        // same-table null receipts and applies its closed null-SRV adapter.
        // An unknown pointer remains an error; it is never converted to null.
        var textures=reads.Where(parameter=>prefix.Textures.ReadKnownNullablePointer(parameter) is not null)
            .Select(parameter=>ReadActualTexture(parameter,native)).ToArray();
        var cbs=recipe.ConstantBufferBindings.Select(b=>b.ConstantBuffer.Declaration.Index).Distinct()
            .ToDictionary(i=>i,i=>middle.AllocationSides.ConstantBufferInstances[i]);
        var outputs=native.Where(b=>b.Table==MapPostEffect3BindingTable.TextureParameters&&
            b.Role==MapPostEffect3BindingRole.UnorderedAccessReadWrite).Select(b=>b.ParameterIndex).Distinct()
            .ToDictionary(i=>i,i=>prefix.Textures.Require(i).Reference.InstanceIndex);
        recipe.RequireCurrent();RequireCurrent();
        return new(recipe.FrameNumber,Array.AsReadOnly(textures),Array.AsReadOnly(selectedSamplers.ToArray()),cbs,outputs)
            {TextureParameters=prefix.Textures};
    }

    public async Task<MapPostEffect3MiddlePassResult> ExecuteNextAsync()
    {
        await serial.WaitAsync();
        try
        {
            var inputs=BuildCurrentInputs();
            var result=await middle.ExecuteNextAsync(inputs);
            if(result.ActualDispatch is{} completed)samplers.AcceptCompleted(completed);
            else samplers.AcceptSkipped(result.Recipe);
            RequireCurrent();return result;
        }
        catch{failed=true;throw;}
        finally{serial.Release();}
    }

    private IMapPostEffect3ComputeTextureLease ReadActualTexture(int parameter,
        IReadOnlyList<MapPostEffect3Binding> native)
    {
        var bank=middle.Compute.Bank;var accepted=prefix.Textures.Require(parameter);var reference=accepted.Reference;
        if(reference.PhysicalAllocationParameterIndex is int physical)
        {
            var allocation=bank.RequireTexture(physical,reference.InstanceIndex);
            var rows=native.Where(b=>b.Table==MapPostEffect3BindingTable.TextureParameters&&
                b.Role==MapPostEffect3BindingRole.ShaderResourceRead&&b.ParameterIndex==parameter).ToArray();
            if(rows.Any(b=>b.SliceSelector is not (-1 or 0)))
                throw new NotSupportedException("Middle sampled resource needs another source array/slice producer.");
            uint first,count;
            if(rows.Any(b=>b.MipSelector==-1)){first=0;count=(uint)allocation.Shape.MipLevels;}
            else
            {
                int[] levels=rows.Select(b=>(int)b.MipSelector).Distinct().Order().ToArray();
                if(levels.Length==0||levels[0]<0||!levels.SequenceEqual(Enumerable.Range(levels[0],levels.Length)))
                    throw new NotSupportedException("Middle resource has an unclosed disjoint source mip footprint.");
                first=(uint)levels[0];count=(uint)levels.Length;
            }
            return bank.CaptureSourceWrittenTextureAliasLease(prefix.Textures,parameter,prefix.Context.FrameNumber,first,count);
        }
        var scene=prefix.Context.OutputFrame.Textures[0x064B746B].EffectiveSource;
        if(reference.ProducerOwnerIdentity==scene.OwnerIdentity)return prefix.WorldStockScope.LeaseWorkingScene(parameter);
        var depth=prefix.Context.TerrainFrame.Textures[0xE13DDB4F].EffectiveSource;
        if(reference.ProducerOwnerIdentity==depth.OwnerIdentity)return prefix.WorldStockScope.LeaseInputDepth(prefix.InputDepthSource,parameter);
        if(reference.ProducerOwnerIdentity==prefix.InputNoiseSource.OwnerIdentity)
        {
            var noise=prefix.InputNoiseSource.Lease();
            if(noise.ParameterIndex!=parameter)
                throw new NotSupportedException("Middle native Noise alias requires its actual copied logical slot capability.");
            return noise;
        }
        throw new NotSupportedException($"Middle source texture{parameter} requires its actual external producer '{reference.ProducerOwnerIdentity}'.");
    }
}
