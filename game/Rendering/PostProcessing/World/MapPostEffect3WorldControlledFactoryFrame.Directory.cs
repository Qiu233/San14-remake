using Godot;
using San14.Data;

public sealed partial class MapPostEffect3WorldControlledFactoryFrame
{
    public MapPostEffect3Functor1FrontendCursor? DirectoryCursor{get;private set;}
    internal MapPostEffect3Functor1EyeCloneCatalog? EyeCloneCatalog{get;private set;}
    /// <summary>Own the actual source shader catalog and metadata cache before
    /// any selected wrapper is entered. This creates no submission/End proof.</summary>
    public async Task<MapPostEffect3Functor1FrontendCursor> PrepareActualDirectoryAsync(
        GameResourceRepository repository,MapPostEffect3ProgramLibrary programs,Action<string>? checkpoint=null)
    {
        RequireCurrent();
        if(DirectoryCursor is not null||SubmissionLedger.HistoricalReceipts.Count!=0||CpuInvocation.RecordedCpuWrapperReturns.Count!=0)
            throw new InvalidOperationException("The actual frontend cursor must precede the first selected source wrapper.");
        var sources=ModuleLifetime?.StaticSources??throw new InvalidDataException("Directory lacks its module-owned static source generation.");
        sources.RequireRepository(repository);
        var source=sources.Cursor;
        var lifetime=sources.Lifetime;
        var catalog=await MapPostEffect3Functor1ShaderCatalog.CaptureCreatedAsync(this,programs,checkpoint);
        EyeCloneCatalog=await MapPostEffect3Functor1EyeCloneCatalog.CaptureAsync(this,programs,
            sources.Eye);
        checkpoint?.Invoke("controlled-source-Eye85/86-actual-array-clone-wrapper-catalog-created");
        DirectoryCursor=new(this,source,catalog,lifetime);Frontend.AttachDirectoryCursor(DirectoryCursor);return DirectoryCursor;
    }
    public void RequireActualDirectoryReturned(MapPostEffect3Functor1DirectoryReturnedReceipt actual)
    {
        if(CpuInvocation.IsControllerReturned){RequirePortReturned(actual);return;}
        ObjectDisposedException.ThrowIf(disposed,this);actual.RequireCurrent();preparation.RequireCurrent();
        CpuInvocation.RequireOwnedModule(CpuModule);CpuInvocation.RequireCurrentReturnedDirectory(actual.CpuDirectoryReturned,Textures);
        Camera.Invocation.RequirePending();preparation.ValidateControlledCamera(Camera);
        World.RequireCurrent();HdrRead.RequireCurrent();Lens.RequireCurrent();DepthOfField.RequireCurrent();
        Eye.RequireCurrent();EyeInputs.RequireCurrent();Composite.RequireCurrent();Carriers.RequireCurrent();
    }
    public MapPostEffect3WorldControlledPortReturnReceipt? PortReturn=>ModuleLifetime?.LastReturned is{} actual&&
        actual.FrameNumber==Camera.FrameNumber?actual:null;
    public MapPostEffect3Functor1DirectoryReturnedReceipt ActualDirectoryReturn=>
        DirectoryCursor?.ReturnedReceipt??throw new InvalidDataException("Actual directory not returned.");
    public void RequirePortReturned(MapPostEffect3Functor1DirectoryReturnedReceipt actual)
    {
        ObjectDisposedException.ThrowIf(disposed,this);preparation.RequireCurrent();actual.RequireCurrent();
        CpuInvocation.RequireOwnedModule(CpuModule);CpuInvocation.RequireCurrentExitedDirectory(actual.CpuDirectoryReturned,Textures);
        if(!ReferenceEquals(actual,ActualDirectoryReturn)||PortReturn is not{} returned)
            throw new InvalidDataException("Exit diagnostics require the exact closed port return and sealed directory.");
        returned.RequireHistoricalAuthority();preparation.ValidateControlledCamera(Camera);World.RequireCurrent();HdrRead.RequireCurrent();
    }
    internal MapPostEffect3Functor1FrontendCacheOwner.Metadata? CaptureDirectoryMetadata(MapPostEffect3Binding binding)
    {
        if(binding.Table==MapPostEffect3BindingTable.TextureParameters)return CaptureDirectoryTextureMetadata(binding.ParameterIndex);
        var bank=World.Bank;int index=binding.ParameterIndex;
        if(binding.Table==MapPostEffect3BindingTable.BufferParameters)
        {
            if(index==4)
            {
                var ghost=DirectoryCursor?.GhostMetadata??throw new InvalidDataException("Actual controller828 metadata owner is absent.");
                ghost.RequireCurrent();
                return new(ghost.Controller828Identity,ghost.Controller828Identity,ModuleLifetime?.GhostStorageRid??default,4,0,
                    ghost.SourceEvidence,ghost.RequireCurrent){PhysicalProducer=ghost};
            }
            if(!AllocationSides.BufferInstances.TryGetValue(index,out int side))
                throw new NotSupportedException("Select lacks this actual source buffer side: "+index);
            var buffer=bank.RequireBuffer(index,side);
            return new(buffer,buffer,buffer.StorageRid,index,side,"actual-bank-buffer-allocation/no-readiness",()=>
            {if(!ReferenceEquals(bank.RequireBuffer(index,side),buffer))throw new InvalidDataException("Select buffer allocation owner changed.");});
        }
        if(!AllocationSides.ConstantBufferInstances.TryGetValue(index,out int cbSide)||
            !bank.ConstantBuffers.TryGetValue(index,out var buffers)||(uint)cbSide>=buffers.Count)
            throw new NotSupportedException("Select lacks this actual native CB allocation side: "+index);
        var cb=buffers[cbSide];bank.RequireTexture(59,0); //actual bank lifetime, no CB contents claim.
        return new(cb,cb,cb.StorageRid,index,cbSide,"actual-bank-CB-allocation/no-writer-or-readiness",()=>
        {bank.RequireTexture(59,0);if(!ReferenceEquals(bank.ConstantBuffers[index][cbSide],cb))throw new InvalidDataException("Select CB owner changed.");});
    }
    internal MapPostEffect3Functor1FrontendCacheOwner.Metadata? CaptureDirectoryTextureMetadata(int index)
    {
        var reference=Textures.ReadKnownNullablePointer(index);if(reference is null)return null;
        var pointer=Textures.Require(index);Textures.ValidateCurrent(pointer);var bank=World.Bank;
        if(reference.PhysicalAllocationParameterIndex is{} physical)
        {
            if(reference.HasSourceSelectedMipView)
            {
                var clones=EyeCloneCatalog??throw new InvalidDataException("Source selected Eye mip pointer has no actual clone catalog.");
                return clones.Metadata(clones.RequireAlias(index),index);
            }
            var texture=bank.RequireTexture(physical,reference.InstanceIndex);
            if(reference.NativeFirstMipLevel is{} first&&(uint)first>=texture.Shape.MipLevels)
                throw new InvalidDataException("Source wrapper metadata points outside its actual physical texture.");
            return new(reference,reference,texture.StorageRid,index,reference.InstanceIndex,"actual-source-texture-wrapper/bank-allocation/no-pixels",()=>
            {if(!ReferenceEquals(bank.RequireTexture(physical,reference.InstanceIndex),texture))throw new InvalidDataException("Select texture physical owner changed.");}){PhysicalProducer=texture};
        }
        var scene=World.Context.OutputFrame.Textures[0x064B746B];
        if(reference.ProducerOwnerIdentity==scene.EffectiveSource.OwnerIdentity)
            return new(reference,reference,scene.StorageRid,index,reference.InstanceIndex,"actual-World-Scene-wrapper/metadata-only",World.RequireCurrent){PhysicalProducer=scene};
        var depth=World.Context.TerrainFrame.Textures[0xE13DDB4F];
        if(reference.ProducerOwnerIdentity==depth.EffectiveSource.OwnerIdentity)
            return new(reference,reference,depth.StorageRid,index,reference.InstanceIndex,"actual-World-depth-wrapper/metadata-only",World.RequireCurrent){PhysicalProducer=depth};
        if(reference.ProducerOwnerIdentity==HdrRead.ProducerOwnerIdentity)
            return new(reference,reference,HdrRead.StorageRid,index,reference.InstanceIndex,"actual-Main-selected-Static-HDR-wrapper/metadata-only",HdrRead.RequireCurrent){PhysicalProducer=HdrRead};
        if(depthOfFieldResources?.Shape is{} shape&&ReferenceEquals(reference,shape.Reference)&&shape.Rid.IsValid)
            return new(reference,reference,shape.Rid,index,reference.InstanceIndex,"actual-module-DOF-shape-wrapper/metadata-only",()=>
            {bank.RequireTexture(59,0);if(!ReferenceEquals(depthOfFieldResources.Shape,shape)||!shape.Rid.IsValid)
                throw new InvalidDataException("Select shape owner changed.");}){PhysicalProducer=shape};
        throw new NotSupportedException("Select external texture has no exact current producer metadata contract: "+index);
    }
    internal MapPostEffect3Functor1FrontendCacheOwner.Metadata CaptureDirectoryCarrier(bool linear)
    {
        var source=Carriers.CaptureCurrent();object pointer=linear?source.ActualLinearWrapper30:source.ActualPointWrapper38;
        return new(pointer,pointer,default,linear?7:8,0,source.SourceEvidence+";native-SRV-carrier-only/no-pixel-texture",Carriers.RequireCurrent){PhysicalProducer=Carriers};
    }
}
