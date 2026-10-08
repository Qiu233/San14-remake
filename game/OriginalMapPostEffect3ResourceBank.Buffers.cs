using System.Security.Cryptography;
using Godot;
using San14.Data;

public sealed partial record OriginalMapPostEffect3DeviceBuffer
{
    public bool HasCompleteWriterContents{get;internal set;}
    public long? CompleteFrameNumber{get;internal set;}
    public uint? WriterRva{get;internal set;}
    public string? CompleteBytesSha256{get;internal set;}
    public string? CompleteSourceEvidence{get;internal set;}
    public long UploadRevision{get;internal set;}
    public long? LastShaderWriteFrameNumber{get;internal set;}
    public int? LastShaderWritePassIndex{get;internal set;}
}

public interface IOriginalMapPostEffect3ComputeBufferLease
{
    long FrameNumber{get;}
    string ContextOwnerIdentity{get;}
    int ParameterIndex{get;}
    int InstanceIndex{get;}
    Rid StorageRid{get;}
    Rid ShaderReadRid{get;}
    RenderingDevice.DataFormat DeviceTexelFormat{get;}
    uint ByteCount{get;}
    long UploadRevision{get;}
    string SourceEvidence{get;}
    void RequireCurrent();
    void RequireReadRange(uint byteOffset,uint byteCount);
}

public sealed partial class OriginalMapPostEffect3ResourceBank
{
    private sealed record CurrentBufferLease(OriginalMapPostEffect3ResourceBank Bank,long FrameNumber,
        string ContextOwnerIdentity,int ParameterIndex,int InstanceIndex,Rid StorageRid,
        Rid ShaderReadRid,RenderingDevice.DataFormat DeviceTexelFormat,uint ByteCount,long UploadRevision,string SourceEvidence):IOriginalMapPostEffect3ComputeBufferLease
    {
        public void RequireCurrent()
        {var b=Bank.RequireCurrentBuffer(ParameterIndex,InstanceIndex,FrameNumber);
         if(Bank.Source.ContextOwnerIdentity!=ContextOwnerIdentity||b.StorageRid!=StorageRid||b.ShaderReadRid!=ShaderReadRid||b.DeviceTexelFormat!=DeviceTexelFormat||
            b.Source.ByteCount!=ByteCount||b.UploadRevision!=UploadRevision)
             throw new InvalidDataException("Actual current Lens buffer owner/revision changed.");}
        public void RequireReadRange(uint byteOffset,uint byteCount)
        {RequireCurrent();if(byteCount==0||(ulong)byteOffset+byteCount>ByteCount)
            throw new InvalidDataException("Actual complete buffer lease does not cover the selected read range.");}
    }
    public IOriginalMapPostEffect3ComputeBufferLease CaptureCurrentBufferLease(int index,int instance,long frame)
    {
        lock(lifetime)
        {var b=RequireCurrentBuffer(index,instance,frame);return new CurrentBufferLease(this,frame,Source.ContextOwnerIdentity,index,instance,
            b.StorageRid,b.ShaderReadRid,b.DeviceTexelFormat,b.Source.ByteCount,b.UploadRevision,b.CompleteSourceEvidence+
            $";actual-complete-buffer-writer={b.WriterRva:X};sha={b.CompleteBytesSha256};revision={b.UploadRevision}");}
    }

    // Dispatch shares the bank upload serialization and keeps all bank RIDs
    // alive until its accepted command and cleanup finish.
    internal async Task<IDisposable> AcquireBufferExecutionScopeAsync()
    {
        lock(lifetime){ObjectDisposedException.ThrowIf(retiring,this);accepted++;}
        try{await serial.WaitAsync();return new BufferExecutionScope(this);}
        catch{lock(lifetime)if(--accepted==0&&retiring)drained?.TrySetResult();throw;}
    }
    private sealed class BufferExecutionScope(OriginalMapPostEffect3ResourceBank bank):IDisposable
    {
        private bool disposed;
        public void Dispose()
        {if(disposed)return;disposed=true;bank.serial.Release();lock(bank.lifetime)if(--bank.accepted==0&&bank.retiring)bank.drained?.TrySetResult();}
    }
    internal long InvalidateBufferAfterShaderWrite(int index,int instance,long frame,int pass)
    {
        lock(lifetime)
        {var b=Require(Buffers,index,instance,"written buffer");b.UploadRevision=checked(b.UploadRevision+1);
         b.HasCompleteWriterContents=false;b.CompleteFrameNumber=null;b.WriterRva=null;b.CompleteBytesSha256=null;b.CompleteSourceEvidence=null;b.PreparedShaderReadRid=default;
         b.LastShaderWriteFrameNumber=frame;b.LastShaderWritePassIndex=pass;return b.UploadRevision;}
    }
    /// <summary>Joins source coverage with this executor's actual last successful
    /// command, exact physical storage/side and still-current writer revision.
    /// Coverage by itself is never a GPU completion or indirect capability.</summary>
    public IOriginalMapPostEffect3ComputeBufferLease PublishLensBufferCoverage(
        OriginalMapPostEffect3LensWriteCoverage coverage,OriginalMapPostEffect3ComputeExecutor actualExecutor,
        OriginalMapPostEffect3ComputeCompletedPass completed)
    {
        ArgumentNullException.ThrowIfNull(coverage);ArgumentNullException.ThrowIfNull(actualExecutor);ArgumentNullException.ThrowIfNull(completed);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!ReferenceEquals(actualExecutor.Bank,this)||!ReferenceEquals(actualExecutor.LastCompletedPass,completed)||
                coverage.Table!=OriginalMapPostEffect3BindingTable.BufferParameters||
                !((coverage.ParameterIndex==2&&coverage.SourcePassIndex==30)||(coverage.ParameterIndex==3&&coverage.SourcePassIndex==31))||
                coverage.SourceSha256!=Source.Source.Sha256||coverage.ContextOwnerIdentity!=Source.ContextOwnerIdentity||
                completed.ContextOwnerIdentity!=Source.ContextOwnerIdentity||completed.FrameNumber!=coverage.FrameNumber||
                completed.SourcePassIndex!=coverage.SourcePassIndex||completed.ProgramSha256!=coverage.ProgramSha256||
                completed.Dispatch!=coverage.ActualDispatch||completed.ActualShaderLocalSize!=new Vector3I(
                    checked((int)coverage.LocalSize.X),checked((int)coverage.LocalSize.Y),checked((int)coverage.LocalSize.Z)))
                throw new InvalidDataException("Lens full buffer coverage is not this actual source executor/frame/program command.");
            var outputs=completed.BufferWrites.Where(w=>w.ParameterIndex==coverage.ParameterIndex&&w.SelectedShaderDeclaresWrite).ToArray();
            if(outputs.Length!=1)throw new InvalidDataException("Actual selected Lens shader did not submit exactly this buffer write.");
            var write=outputs[0];var b=Require(Buffers,write.ParameterIndex,write.InstanceIndex,"covered Lens buffer");
            if(write.StorageRid!=b.StorageRid||write.ByteCount!=b.Source.ByteCount||write.WriterRevision!=b.UploadRevision||
                b.LastShaderWriteFrameNumber!=coverage.FrameNumber||b.LastShaderWritePassIndex!=coverage.SourcePassIndex||
                coverage.ByteOffset!=0||coverage.ByteCount!=b.Source.ByteCount)
                throw new InvalidDataException("Lens source coverage belongs to another physical buffer/side/revision or incomplete capacity.");
            string? contentHash=null;
            if(coverage.SourcePassIndex==30)
            {
                if(b.Shape.Dimension!=OriginalMapPostEffect3BufferDimension.Structured||coverage.ByteCount!=448||
                    coverage.RequiredConstantBufferParameterIndex!=6||string.IsNullOrWhiteSpace(coverage.RequiredConstantBufferBytesSha256)||
                    !coverage.RequiredInputBufferBytesSha256.Keys.Order().SequenceEqual(new[]{0,1}))
                    throw new InvalidDataException("GhostSprite full coverage lost its exact CB6/input0/input1 source dependencies.");
                var cbReads=completed.ConstantBufferReads.Where(c=>c.ParameterIndex==6).ToArray();
                if(cbReads.Length!=1||!cbReads[0].HasCompleteWriterContents||cbReads[0].CompleteBytesSha256!=coverage.RequiredConstantBufferBytesSha256)
                    throw new InvalidDataException("Actual GhostSprite shader did not consume the exact complete Init CB6 counter16.");
                var cb=RequireCurrentConstantBuffer(6,cbReads[0].InstanceIndex,coverage.FrameNumber);
                if(cb.StorageRid!=cbReads[0].StorageRid||cb.UploadRevision!=cbReads[0].UploadRevision||cb.CompleteBytesSha256!=coverage.RequiredConstantBufferBytesSha256)
                    throw new InvalidDataException("GhostSprite coverage CB6 physical owner/revision changed after execution.");
                foreach(var requirement in coverage.RequiredInputBufferBytesSha256)
                {
                    var inputReads=completed.BufferReads.Where(r=>r.ParameterIndex==requirement.Key).ToArray();
                    if(inputReads.Length!=1||inputReads[0].CompleteBytesSha256!=requirement.Value)
                        throw new InvalidDataException("Actual GhostSprite shader did not consume the complete source Lens arrays.");
                    var input=RequireCurrentBuffer(requirement.Key,inputReads[0].InstanceIndex,coverage.FrameNumber);
                    if(input.StorageRid!=inputReads[0].StorageRid||input.UploadRevision!=inputReads[0].UploadRevision||
                        input.Source.ByteCount!=inputReads[0].ByteCount||input.CompleteBytesSha256!=requirement.Value)
                        throw new InvalidDataException("GhostSprite source array owner/revision changed after execution.");
                }
                // Source addresses/coverage are closed; result values depend on
                // GPU inputs and no readback occurred. Their byte hash is unknown.
            }
            else
            {
                if(b.Shape.Dimension!=OriginalMapPostEffect3BufferDimension.Raw||coverage.ByteCount!=16)
                    throw new InvalidDataException("GhostArguments reset full raw16B shape differs.");
                byte[] bytes=new byte[16];System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes,4);
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4),4);
                contentHash=Convert.ToHexString(SHA256.HashData(bytes));
            }
            b.HasCompleteWriterContents=true;b.CompleteFrameNumber=coverage.FrameNumber;b.WriterRva=0x1163170;
            b.CompleteBytesSha256=contentHash;
            b.CompleteSourceEvidence=coverage.SourceEvidence+";"+completed.SourceEvidence+";actual-physical-storage-and-still-current-writer-revision";
            return CaptureCurrentBufferLease(write.ParameterIndex,write.InstanceIndex,coverage.FrameNumber);
        }
    }
    public OriginalMapPostEffect3DeviceBuffer RequireCurrentBuffer(int index,int instance,long frame)
    {
        lock(lifetime)
        {ObjectDisposedException.ThrowIf(retiring,this);var target=Require(Buffers,index,instance,"buffer");
         if(!target.HasCompleteWriterContents||target.CompleteFrameNumber!=frame||target.UploadRevision<=0)
             throw new InvalidDataException("3SPK buffer has no complete same-frame source writer contents.");return target;}
    }

    public Task UploadCompleteLensBufferAsync(OriginalMapPostEffect3LensBufferUpload packet,int instance)
    {
        ArgumentNullException.ThrowIfNull(packet);byte[] bytes=packet.Bytes.ToArray();
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);var target=Require(Buffers,packet.Declaration.Parameter.Index,instance,"Lens buffer");
            if(packet.SourceSha256!=Source.Source.Sha256||packet.OwnerIdentity!=Source.ContextOwnerIdentity||packet.FrameNumber<=0||
                packet.WriterRva!=OriginalMapPostEffect3ParameterLensFlare.NativeInitializeWriterRva(packet.Locale)||bytes.Length!=target.Source.ByteCount||
                packet.Declaration.ByteCount!=target.Source.ByteCount||
                !packet.Declaration.Parameter.RawBytes.Span.SequenceEqual(target.Source.Parameter.RawBytes.Span)||
                target.Shape.Dimension!=OriginalMapPostEffect3BufferDimension.Structured||target.RequiresIndirectArgumentAdapter||target.NativeAllowsUnorderedAccess)
                throw new InvalidDataException("3SPK complete Lens buffer upload source/extent/owned declaration differs.");
            accepted++;return UploadLensBufferAcceptedAsync(packet,bytes,target);
        }
    }
    private async Task UploadLensBufferAcceptedAsync(OriginalMapPostEffect3LensBufferUpload packet,byte[] bytes,
        OriginalMapPostEffect3DeviceBuffer target)
    {
        await serial.WaitAsync();
        try
        {
            // Another accepted upload may have replaced this side while this
            // packet waited. Always resolve the current physical side here.
            lock(lifetime)target=Require(Buffers,packet.Declaration.Parameter.Index,target.InstanceIndex,"Lens buffer");
            if((target.CompleteFrameNumber is{} previous&&packet.FrameNumber<previous)||
                (target.LastShaderWriteFrameNumber is{} written&&packet.FrameNumber<written))
                throw new InvalidDataException("Lens buffer upload cannot replace a newer source frame.");
            long nextRevision=checked(target.UploadRevision+1);
            string hash=Convert.ToHexString(SHA256.HashData(bytes));
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                // Godot4.7's _get_buffer_from_owner explicitly cannot resolve
                // TextureBuffer RIDs for BufferUpdate. Supplying complete bytes
                // to a new TextureBufferCreate is the supported CPU upload.
                Rid replacement=rd.TextureBufferCreate(target.TexelCount,target.DeviceTexelFormat,bytes);
                if(!replacement.IsValid)throw new InvalidDataException("Device rejected complete original Lens TextureBufferCreate bytes.");
                bool installed=false;
                try
                {
                    var next=target with{StorageRid=replacement,CompleteFrameNumber=packet.FrameNumber,WriterRva=packet.WriterRva,
                        CompleteBytesSha256=hash,UploadRevision=nextRevision,HasCompleteWriterContents=true,
                        CompleteSourceEvidence=packet.SourceEvidence+";Godot4.7-TextureBufferCreate-full-initialdata;serialized-physical-side-replacement",
                        LastShaderWriteFrameNumber=null,LastShaderWritePassIndex=null};
                    lock(lifetime)
                    {
                        if(!ReferenceEquals(Require(Buffers,packet.Declaration.Parameter.Index,target.InstanceIndex,"Lens buffer"),target))
                            throw new InvalidDataException("Accepted Lens buffer physical side changed outside its serialized execution scope.");
                        int ownerSlot=owned.IndexOf(target.StorageRid);
                        if(ownerSlot<0)throw new InvalidDataException("Lens buffer physical RID is not this bank's live owned allocation.");
                        var sides=bufferRows[packet.Declaration.Parameter.Index].ToArray();sides[target.InstanceIndex]=next;
                        bufferRows[packet.Declaration.Parameter.Index]=Array.AsReadOnly(sides);
                        owned[ownerSlot]=replacement;OwnedRidCount=owned.Count;installed=true;
                        target.HasCompleteWriterContents=false;target.CompleteFrameNumber=null;target.CompleteBytesSha256=null;
                        target.CompleteSourceEvidence=null;target.UploadRevision=nextRevision;
                    }
                    // Descriptor sets that borrowed this RID are automatically
                    // invalidated by Godot; prior leases reject the new RID/rev.
                    // The old RID was removed from owned before this free, so
                    // bank retirement cannot free it a second time.
                    rd.FreeRid(target.StorageRid);return true;
                }
                finally{if(!installed)rd.FreeRid(replacement);}
            });
        }
        finally{serial.Release();lock(lifetime)if(--accepted==0&&retiring)drained?.TrySetResult();}
    }
}
