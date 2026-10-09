using Godot;
using San14.Data;

public sealed partial class MapPostEffect3LensExecutionOwner
{
    private sealed record ManagerSelection(MapPostEffect3LensExecutionOwner Owner,
        MapPostEffect3LensExecutionRecipe Recipe,int NativeStageOrdinal,uint SourceRegister,
        MapPostEffect3ManagerSamplerKind Kind):IMapPostEffect3ManagerSamplerSelection
    {
        public long FrameNumber=>Recipe.FrameNumber;
        public string ContextOwnerIdentity=>Recipe.ContextOwnerIdentity;
        public string SourceEvidence=>Recipe.SourceEvidence+";native-Before-manager"+
            (Kind==MapPostEffect3ManagerSamplerKind.Linear?"30":"38")+"-sampler-selection";
        public void RequireCurrent()
        {
            lock(Owner.lifetime)ObjectDisposedException.ThrowIf(Owner.retiring&&Owner.accepted==0,Owner);
            int offset=Kind==MapPostEffect3ManagerSamplerKind.Linear?0x30:0x38;
            if(!Recipe.ExecutesPass||Recipe.Locale!=Owner.locale||Recipe.DescriptorTextureSubmissions.Count(s=>s.NativeStageCode==NativeStageOrdinal&&
                s.RegisterIndex==SourceRegister&&s.DescriptorTextureOffset==offset&&s.Locale==Owner.locale&&
                s.Setter==MapPostEffect3ResourceSetter.ShaderResource)!=1)
                throw new InvalidDataException("Current Lens Before did not install this actual manager descriptor/sampler slot.");
        }
    }

    private IMapPostEffect3ComputeSamplerLease Sampler(MapPostEffect3LensExecutionFrame frame,
        MapPostEffect3LensExecutionRecipe recipe,int stage,uint register)
    {
        var native=recipe.DescriptorTextureSubmissions.SingleOrDefault(s=>s.NativeStageCode==stage&&s.RegisterIndex==register);
        if(native?.DescriptorTextureOffset is 0x30 or 0x38)
            return LeaseCurrentManagerSampler(recipe,stage,register);
        var actual=frame.SourceInputs.RequireWrapperSampler(recipe,stage,register);
        actual.RequireCurrent();
        if(actual.FrameNumber!=recipe.FrameNumber||actual.ContextOwnerIdentity!=recipe.ContextOwnerIdentity||
            actual.SourceRegister!=register||actual.NativeStageOrdinal!=stage)
            throw new InvalidDataException("Lens sampled-wrapper sampler producer returned another frame/stage/register.");
        return actual;
    }

    public IMapPostEffect3ComputeSamplerLease LeaseCurrentManagerSampler(
        MapPostEffect3LensExecutionRecipe recipe,int nativeStageOrdinal,uint sourceRegister)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if(recipe.SourceSha256!=Bank.Source.Source.Sha256||recipe.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity)
            throw new InvalidDataException("Lens manager sampler requires its actual source/module owner.");
        var native=recipe.DescriptorTextureSubmissions.Single(s=>s.NativeStageCode==nativeStageOrdinal&&s.RegisterIndex==sourceRegister);
        if(native.DescriptorTextureOffset is not(0x30 or 0x38))
            throw new NotSupportedException("Lens shader register is not selected from either actual manager wrapper.");
        return Samplers.LeaseSelected(new ManagerSelection(this,recipe,nativeStageOrdinal,sourceRegister,
            native.DescriptorTextureOffset==0x30?MapPostEffect3ManagerSamplerKind.Linear:MapPostEffect3ManagerSamplerKind.Point),nativeStageOrdinal);
    }

    private IMapPostEffect3ComputeTextureLease ReadTexture(MapPostEffect3LensExecutionFrame frame,
        MapPostEffect3LensExecutionRecipe recipe,int parameter)
    {
        var reference=frame.TextureParameters.Require(parameter);
        // A source-written bank view retains the logical pointer alias and its
        // actual physical mip readiness. An allocation by itself cannot pass.
        if(reference.Reference.PhysicalAllocationParameterIndex is int physical)
        {
            var texture=Bank.RequireTexture(physical,reference.Reference.InstanceIndex);
            try
            {
                if(recipe.SourcePassIndex is >=20 and <=24 or >=35 and <=40)
                {
                    var footprints=recipe.OrderedEffectiveBindings.Where(r=>
                        r.Table==MapPostEffect3BindingTable.TextureParameters&&
                        r.Role==MapPostEffect3BindingRole.ShaderResourceRead&&(r.StageMask&1)!=0)
                        .GroupBy(r=>r.RegisterIndex).Select(g=>g.Last()).Where(r=>r.ParameterIndex==parameter)
                        .Select(r=>MapPostEffect3LensTextureReadFootprint.Prepare(Bank.Source,recipe,r.RegisterIndex)).ToArray();
                    return Bank.CaptureLensSourceMipReadLease(Array.AsReadOnly(footprints),frame.TextureParameters);
                }
                return Bank.CaptureSourceWrittenTextureAliasLease(frame.TextureParameters,parameter,
                    recipe.FrameNumber,0,checked((uint)texture.Shape.MipLevels));
            }
            catch(Exception error)when(error is InvalidDataException or NotSupportedException)
            {
                // The source owner may provide a prior real producer or a
                // shader-specific read domain (e.g. retained ToneMap texels).
                // It must independently prove its actual current contents.
            }
        }
        var actual=frame.SourceInputs.RequireReadableTexture(recipe,parameter,reference);
        actual.RequireCurrent();frame.TextureParameters.ValidateCurrent(reference);
        if(actual.FrameNumber!=recipe.FrameNumber||actual.ContextOwnerIdentity!=recipe.ContextOwnerIdentity||
            actual.ParameterIndex!=parameter||actual.InstanceIndex!=reference.Reference.InstanceIndex)
            throw new InvalidDataException("Lens source readable texture has another logical pointer/frame/side.");
        return actual;
    }

    private MapPostEffect3ComputeFrameInputs ComputeInputs(MapPostEffect3LensExecutionFrame frame,
        MapPostEffect3LensExecutionRecipe recipe,MapPostEffect3CompiledPass compiled)
    {
        var rows=recipe.OrderedEffectiveBindings.Where(r=>(r.StageMask&1)!=0).ToArray();
        var inputTextures=rows.Where(r=>r.Table==MapPostEffect3BindingTable.TextureParameters&&
            r.Role==MapPostEffect3BindingRole.ShaderResourceRead).Select(r=>r.ParameterIndex).Distinct()
            .Select(i=>ReadTexture(frame,recipe,i)).ToArray();
        var inputBuffers=rows.Where(r=>r.Table==MapPostEffect3BindingTable.BufferParameters&&
            r.Role==MapPostEffect3BindingRole.ShaderResourceRead).Select(r=>r.ParameterIndex).Distinct()
            .Select(i=>Bank.CaptureCurrentBufferLease(i,frame.ActualBufferInstances[i],recipe.FrameNumber)).ToArray();
        var samplers=compiled.Stages.Single().Bindings.Bindings.Where(d=>d.DescriptorKind=="Sampler")
            .Select(d=>Sampler(frame,recipe,0,d.Source.BindPoint)).ToArray();
        return new(recipe.FrameNumber,inputTextures,samplers,
            rows.Where(r=>r.Role==MapPostEffect3BindingRole.ConstantBufferRead).Select(r=>r.ParameterIndex).Distinct()
                .ToDictionary(i=>i,i=>frame.ActualConstantBufferInstances[i]),
            rows.Where(r=>r.Table==MapPostEffect3BindingTable.TextureParameters&&r.Role==MapPostEffect3BindingRole.UnorderedAccessReadWrite)
                .Select(r=>r.ParameterIndex).Distinct().ToDictionary(i=>i,i=>frame.TextureParameters.Require(i).Reference.InstanceIndex)){
            TextureParameters=frame.TextureParameters,CurrentBuffers=inputBuffers,
            NativeRegisterResetSource=NativeRegisterResetSource,
            ToneInfoSourceValueInfluence=recipe.SourcePassIndex==41?
                MapPostEffect3ToneInfoRegionWriteCoverage.Prepare(Bank.Source,recipe):null,
            OutputBufferInstances=rows.Where(r=>r.Table==MapPostEffect3BindingTable.BufferParameters&&
                r.Role==MapPostEffect3BindingRole.UnorderedAccessReadWrite).Select(r=>r.ParameterIndex).Distinct()
                .ToDictionary(i=>i,i=>frame.ActualBufferInstances[i]),LensExecution=recipe};
    }
}
