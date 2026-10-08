using System.Buffers.Binary;
using System.Security.Cryptography;
using Godot;
using San14.Data;

public sealed partial class OriginalMapPostEffect3ResourceBank
{
    private readonly Dictionary<(int Side,long Frame,long Revision),OriginalMapPostEffect3ComputeCompletedPass> dofArgumentDomains=[];

    /// <summary>Full original32B argument domain only: CS45 writes both four
    /// words and CS46 changes just initialized counters4/20 atomically.
    /// Sprite buffer append prefixes are published by their actual owner.</summary>
    public IOriginalMapPostEffect3ComputeBufferLease PublishDepthOfField2ArgumentCoverage(
        OriginalMapPostEffect3ComputeExecutor actualCompute,OriginalMapPostEffect3ComputeCompletedPass completed,
        OriginalMapPostEffect3DepthOfField2BeforeBindings actualBefore)
    {
        ArgumentNullException.ThrowIfNull(actualCompute);ArgumentNullException.ThrowIfNull(completed);
        ArgumentNullException.ThrowIfNull(actualBefore);actualBefore.RequireCurrent();
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            int pass=completed.SourcePassIndex;bool clear=pass==57;
            if(actualBefore.SourcePassIndex!=pass||actualBefore.FrameNumber!=completed.FrameNumber||
                actualBefore.ContextOwnerIdentity!=Source.ContextOwnerIdentity||!actualBefore.RequiresGenericComputeDispatch)
                throw new InvalidDataException("DOF2 argument writer needs its actual same-command native Before source.");
            string expected=clear?OriginalMapPostEffect3DofSpriteStorageAdapter.ClearSourceSha256:OriginalMapPostEffect3DofSpriteStorageAdapter.BuildSourceSha256;
            if(!ReferenceEquals(actualCompute.Bank,this)||!ReferenceEquals(actualCompute.LastCompletedPass,completed)||pass is not(57 or 58)||
                completed.ProgramSha256!=expected||completed.ContextOwnerIdentity!=Source.ContextOwnerIdentity||completed.FrameNumber<=0||
                completed.Dispatch!=Source.Passes[pass].DispatchExtent||
                completed.ActualShaderLocalSize!=(clear?new Vector3I(1,1,1):new Vector3I(16,16,1))||completed.Writes.Count!=0||
                !completed.BufferWrites.Select(w=>w.ParameterIndex).Order().SequenceEqual(clear?new[]{5}:new[]{5,6,7}))
                throw new InvalidDataException("DOF2 argument coverage needs the exact actual original CS45/46 command and output roster.");
            var write=completed.BufferWrites.Single(w=>w.ParameterIndex==5);var target=RequireBuffer(5,write.InstanceIndex);
            if(!IsOriginalDofSpriteIndirectAllocation(target.Source,target.Shape)||!target.UsesStorageBuffer||!target.SupportsIndirectArgumentBinding||
                !write.SelectedShaderDeclaresWrite||write.ByteCount!=32||write.StorageRid!=target.StorageRid||write.WriterRevision!=target.UploadRevision||
                target.LastShaderWriteFrameNumber!=completed.FrameNumber||target.LastShaderWritePassIndex!=pass)
                throw new InvalidDataException("DOF2 argument coverage no longer owns this actual paired32B write revision.");
            var expectedBindings=Source.Passes[pass].ActiveBindings.Where(b=>(b.StageMask&1)!=0).ToArray();
            if(!completed.OrderedEffectiveBindings.Select(b=>b.SourceRowIndex).SequenceEqual(expectedBindings.Select(b=>b.SourceRowIndex))||
                completed.OrderedEffectiveBindings.Zip(expectedBindings).Any(p=>!p.First.RawBytes.Span.SequenceEqual(p.Second.RawBytes.Span)))
                throw new InvalidDataException("DOF2 actual argument command replaced an original source binding.");
            byte[] initial=new byte[32];BinaryPrimitives.WriteUInt32LittleEndian(initial,4);BinaryPrimitives.WriteUInt32LittleEndian(initial.AsSpan(16),4);
            string? known=clear?Convert.ToHexString(SHA256.HashData(initial)):null;
            if(clear)
            {
                // Native counted CB setters remain present even though CS45
                // does not declare/read a shader CB. Their actual upload
                // receipts are not an output-value dependency of this reset.
                if(completed.BufferReads.Count!=0)
                    throw new InvalidDataException("Original CS45 reset has no prior-buffer read dependency.");
                // This reset replaces the side's complete argument domain.
                // CS46 below reads only the same-frame current revision.
                foreach(var key in dofArgumentDomains.Keys.Where(k=>k.Side==write.InstanceIndex).ToArray())
                    dofArgumentDomains.Remove(key);
            }
            else
            {
                var prior=completed.BufferReads.SingleOrDefault(r=>r.ParameterIndex==5);
                if(prior is null||completed.BufferReads.Count!=1||prior.InstanceIndex!=write.InstanceIndex||prior.StorageRid!=write.StorageRid||
                    prior.ByteCount!=32||checked(prior.UploadRevision+1)!=write.WriterRevision||
                    !dofArgumentDomains.TryGetValue((write.InstanceIndex,completed.FrameNumber,prior.UploadRevision),out var domain)||
                    domain.SourcePassIndex is not(57 or 58)||domain.BufferWrites.Single(w=>w.ParameterIndex==5).StorageRid!=write.StorageRid)
                    throw new InvalidDataException("CS46 atomic counters need their actual completed same-frame prior32B CS45/46 domain.");
                var cb=completed.ConstantBufferReads.SingleOrDefault(c=>c.ParameterIndex==18);
                if(cb is null||!cb.HasCompleteWriterContents||string.IsNullOrWhiteSpace(cb.CompleteBytesSha256))
                    throw new InvalidDataException("CS46 requires its actual complete original CB18.");
                var current=RequireCurrentConstantBuffer(18,cb.InstanceIndex,completed.FrameNumber);
                if(current.StorageRid!=cb.StorageRid||current.UploadRevision!=cb.UploadRevision||current.CompleteBytesSha256!=cb.CompleteBytesSha256)
                    throw new InvalidDataException("CS46 current CB18 writer owner/revision changed before publication.");
            }
            target.HasCompleteWriterContents=true;target.CompleteFrameNumber=completed.FrameNumber;target.WriterRva=actualBefore.NativeGenericCommandRva;
            target.CompleteBytesSha256=known;target.CompleteSourceEvidence=completed.SourceEvidence+
                ";exact-original-DOF2-CS45-two32B-argument-groups-or-CS46-prior-initialized-two-atomic-counters;full32B-not-sprite-capacity";
            dofArgumentDomains[(write.InstanceIndex,completed.FrameNumber,write.WriterRevision)]=completed;
            return CaptureCurrentBufferLease(5,write.InstanceIndex,completed.FrameNumber);
        }
    }

    /// <summary>Copy actual source bytes to a readonly half-float texel view.
    /// Only the owner-verified append prefix becomes readable; unknown tail
    /// bytes are copied as storage and receive no initialized-content claim.</summary>
    public async Task<IOriginalMapPostEffect3ComputeBufferLease> CaptureDepthOfField2SpriteBufferReadAsync(
        OriginalMapPostEffect3DepthOfField2SpritePrefix prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);prefix.RequireCurrent();
        using var scope=await AcquireBufferExecutionScopeAsync();
        var buffer=RequireBuffer(prefix.ParameterIndex,prefix.InstanceIndex);
        if(!ReferenceEquals(prefix.Owner.Bank,this)||prefix.ContextOwnerIdentity!=Source.ContextOwnerIdentity||
            !IsOriginalDofSpriteStorageAllocation(buffer.Source,buffer.Shape)||!buffer.UsesStorageBuffer||
            buffer.StorageRid!=prefix.StorageRid||buffer.UploadRevision!=prefix.WriterRevision||
            prefix.CapacityByteCount!=buffer.Source.ByteCount||prefix.ReadableByteCount>buffer.Source.ByteCount)
            throw new InvalidDataException("DOF2 sprite SRV bridge requires this exact current source half-float append-prefix owner.");
        var key=(prefix.ParameterIndex,prefix.InstanceIndex);
        bool cached;
        lock(lifetime)cached=bufferShaderReadViews.TryGetValue(key,out var view)&&view.Revision==prefix.WriterRevision&&buffer.PreparedShaderReadRid==view.View;
        if(!cached)
        {
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                prefix.RequireCurrent();byte[] bytes=rd.BufferGetData(buffer.StorageRid,0,buffer.Source.ByteCount);
                if(bytes.Length!=buffer.Source.ByteCount)throw new InvalidDataException("DOF2 actual completed storage readback extent differs from its source allocation.");
                Rid read=rd.TextureBufferCreate(buffer.TexelCount,buffer.DeviceTexelFormat,bytes);
                if(!read.IsValid)throw new InvalidDataException("Device rejected original DOF2 readonly RGBA16F texel view.");
                Rid old=default;bool installed=false;
                try
                {
                    prefix.RequireCurrent();
                    lock(lifetime)
                    {
                        if(!ReferenceEquals(RequireBuffer(prefix.ParameterIndex,prefix.InstanceIndex),buffer)||buffer.UploadRevision!=prefix.WriterRevision)
                            throw new InvalidDataException("DOF2 sprite storage changed during its readonly texel-view bridge.");
                        if(bufferShaderReadViews.TryGetValue(key,out var previous))old=previous.View;
                        if(old.IsValid&&!owned.Remove(old))throw new InvalidDataException("DOF2 readonly sprite view lost bank ownership.");
                        owned.Add(read);OwnedRidCount=owned.Count;bufferShaderReadViews[key]=(read,prefix.WriterRevision);
                        buffer.PreparedShaderReadRid=read;installed=true;
                    }
                    if(old.IsValid)rd.FreeRid(old);return true;
                }
                finally{if(!installed)rd.FreeRid(read);}
            });
        }
        prefix.RequireCurrent();return new DofSpritePrefixBufferRead(this,prefix,buffer.PreparedShaderReadRid);
    }

    private sealed record DofSpritePrefixBufferRead(OriginalMapPostEffect3ResourceBank Bank,
        OriginalMapPostEffect3DepthOfField2SpritePrefix Prefix,Rid ShaderReadRid):IOriginalMapPostEffect3ComputeBufferLease
    {
        public long FrameNumber=>Prefix.FrameNumber;public string ContextOwnerIdentity=>Prefix.ContextOwnerIdentity;
        public int ParameterIndex=>Prefix.ParameterIndex;public int InstanceIndex=>Prefix.InstanceIndex;
        public Rid StorageRid=>Prefix.StorageRid;public RenderingDevice.DataFormat DeviceTexelFormat=>RenderingDevice.DataFormat.R16G16B16A16Sfloat;
        public uint ByteCount=>Prefix.CapacityByteCount;public long UploadRevision=>Prefix.WriterRevision;
        public string SourceEvidence=>Prefix.SourceEvidence+";actual-uintSSBO-half4-bytes-to-readonly-typed-SRV;readable-prefix-only";
        public void RequireCurrent()
        {
            Prefix.RequireCurrent();lock(Bank.lifetime)
            {
                ObjectDisposedException.ThrowIf(Bank.retiring,Bank);
                var b=Bank.RequireBuffer(ParameterIndex,InstanceIndex);
                if(!ShaderReadRid.IsValid||b.StorageRid!=StorageRid||b.UploadRevision!=UploadRevision||b.PreparedShaderReadRid!=ShaderReadRid||
                    !Bank.bufferShaderReadViews.TryGetValue((ParameterIndex,InstanceIndex),out var v)||v.View!=ShaderReadRid||v.Revision!=UploadRevision)
                    throw new InvalidDataException("DOF2 sprite prefix SRV owner/revision was retired or replaced.");
            }
        }
        public void RequireReadRange(uint byteOffset,uint byteCount)
        {RequireCurrent();if(byteCount==0||(ulong)byteOffset+byteCount>Prefix.ReadableByteCount)
            throw new InvalidDataException("DOF2 sprite read exceeds the actual GPU append prefix; allocation tail is uninitialized.");}
    }

    private static bool IsOriginalDofSpriteStorageAllocation(OriginalMapPostEffect3BufferAllocation allocation,
        OriginalMapPostEffect3BufferShape shape)
    {
        var p=allocation.Parameter;
        if(p.Index is not(6 or 7))return false;
        string expected=p.Index==6?
            "FFFFFFFF8AEF643F000000000000010080F4030001000000060000008800000000000000":
            "FFFFFFFFB27004DB000000000000010080F4030001000000060000008800000000000000";
        if(!p.RawBytes.Span.SequenceEqual(Convert.FromHexString(expected))||shape.Dimension!=OriginalMapPostEffect3BufferDimension.Typed||
            shape.ViewFormat!=10||shape.ByteCount!=2073600||shape.ViewElementCount!=259200||
            !shape.CreatesShaderResourceView||!shape.CreatesUnorderedAccessView||allocation.InstanceCount!=1)
            throw new InvalidDataException("DOF2 sprite storage differs from the exact original RGBA16F259200-element declaration.");
        return true;
    }

    private static bool IsOriginalDofSpriteIndirectAllocation(OriginalMapPostEffect3BufferAllocation allocation,
        OriginalMapPostEffect3BufferShape shape)
    {
        var p=allocation.Parameter;
        if(p.Index!=5)return false;
        if(!p.RawBytes.Span.SequenceEqual(Convert.FromHexString("FFFFFFFFA39EA8980200000000000D000200000001000000100000008800000000000000"))||
            shape.Dimension!=OriginalMapPostEffect3BufferDimension.Raw||shape.ByteCount!=32||shape.ViewElementCount!=8||
            shape.MiscFlags!=0x30||!shape.CreatesShaderResourceView||!shape.CreatesUnorderedAccessView||allocation.InstanceCount!=2)
            throw new InvalidDataException("DOF2 indirect storage differs from the exact original raw32B paired DRAWINDIRECT_ARGS declaration.");
        return true;
    }
}
