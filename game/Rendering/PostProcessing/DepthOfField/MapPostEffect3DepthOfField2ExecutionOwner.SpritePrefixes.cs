using System.Buffers.Binary;
using Godot;
using San14.Data;

/// <summary>GPU append prefix from the original CS46. Count is read by its
/// owner from the actual B5 atomic counter, then joined to the same completed
/// dispatch and physical B6/B7 write revision. No whole-capacity claim.</summary>
public sealed class MapPostEffect3DepthOfField2SpritePrefix
{
    internal MapPostEffect3DepthOfField2SpritePrefix(MapPostEffect3DepthOfField2ExecutionOwner owner,
        MapPostEffect3ComputeCompletedPass completed,int parameter,int instance,Rid storage,long revision,
        uint count,uint capacity,Rid arguments,long argumentsRevision)
        =>(Owner,CompletedCS46,ParameterIndex,InstanceIndex,StorageRid,WriterRevision,ActualSpriteCount,CapacityByteCount,
            ArgumentsStorageRid,ArgumentsWriterRevision)=(owner,completed,parameter,instance,storage,revision,count,capacity,arguments,argumentsRevision);
    public MapPostEffect3DepthOfField2ExecutionOwner Owner{get;}
    public MapPostEffect3ComputeCompletedPass CompletedCS46{get;}
    public int ParameterIndex{get;}
    public int InstanceIndex{get;}
    public Rid StorageRid{get;}
    public long WriterRevision{get;}
    public uint ActualSpriteCount{get;}
    public uint ReadableByteCount=>checked(16*ActualSpriteCount);
    public uint CapacityByteCount{get;}
    public Rid ArgumentsStorageRid{get;}
    public long ArgumentsWriterRevision{get;}
    public long FrameNumber=>CompletedCS46.FrameNumber;
    public string ContextOwnerIdentity=>CompletedCS46.ContextOwnerIdentity;
    public string SourceEvidence=>Owner.Prepared.SourceEvidence+";original-CS46-imm_atomic_iadd-u0-at"+
        (ParameterIndex==7?"4/rear":"20/fore")+";two-original-float4-RGBA16F-texels-per-instance/16B;actual-GPU-counter="+
        ActualSpriteCount+";same-dispatch-and-storage-writer-revision;only-append-prefix-readable";
    public void RequireCurrent()=>Owner.RequireSpritePrefix(this);
}

public sealed partial class MapPostEffect3DepthOfField2ExecutionOwner
{
    private readonly Dictionary<int,MapPostEffect3DepthOfField2SpritePrefix> spritePrefixes=[];

    internal void RequireSpritePrefix(MapPostEffect3DepthOfField2SpritePrefix prefix)
    {
        RequireCurrent();ObjectDisposedException.ThrowIf(retiring,this);
        if(!ReferenceEquals(prefix.Owner,this)||!ReferenceEquals(Compute.LastCompletedPass,prefix.CompletedCS46)||
            !spritePrefixes.TryGetValue(prefix.ParameterIndex,out var current)||!ReferenceEquals(current,prefix)||
            prefix.CompletedCS46.SourcePassIndex!=58||prefix.CompletedCS46.ProgramSha256!=
                "04CB59EB2DDD04998EB07CAC1721512B4866A4863DB48B3559AB7B3C345D9149")
            throw new InvalidDataException("DOF2 sprite prefix is not this owner's actual original CS46 completed publication.");
        var textureBuffer=Bank.RequireBuffer(prefix.ParameterIndex,prefix.InstanceIndex);
        var argumentsWrite=prefix.CompletedCS46.BufferWrites.Single(w=>w.ParameterIndex==5);
        var arguments=Bank.RequireBuffer(5,argumentsWrite.InstanceIndex);
        if(textureBuffer.StorageRid!=prefix.StorageRid||textureBuffer.UploadRevision!=prefix.WriterRevision||
            textureBuffer.LastShaderWriteFrameNumber!=prefix.FrameNumber||textureBuffer.LastShaderWritePassIndex!=58||
            textureBuffer.Source.ByteCount!=prefix.CapacityByteCount||prefix.ReadableByteCount>prefix.CapacityByteCount||
            textureBuffer.Shape.ViewFormat!=10||textureBuffer.Source.ByteCount%16!=0||
            arguments.StorageRid!=prefix.ArgumentsStorageRid||arguments.UploadRevision!=prefix.ArgumentsWriterRevision||
            arguments.LastShaderWriteFrameNumber!=prefix.FrameNumber||arguments.LastShaderWritePassIndex!=58)
            throw new InvalidDataException("DOF2 sprite prefix/counter physical owners, capacities or completed writer revisions changed.");
    }

    private async Task CaptureSpritePrefixesAsync(MapPostEffect3ComputeCompletedPass completed)
    {
        RequireCurrent();
        if(!ReferenceEquals(Compute.LastCompletedPass,completed)||completed.SourcePassIndex!=58||
            completed.ProgramSha256!="04CB59EB2DDD04998EB07CAC1721512B4866A4863DB48B3559AB7B3C345D9149"||
            completed.BufferWrites.Select(w=>w.ParameterIndex).Order().ToArray() is not[5,6,7])
            throw new InvalidDataException("DOF2 sprite prefix capture requires its actual selected CS46 output buffers5/6/7.");
        var argumentsWrite=completed.BufferWrites.Single(w=>w.ParameterIndex==5);
        var arguments=Bank.RequireBuffer(5,argumentsWrite.InstanceIndex);
        if(arguments.Source.ByteCount!=32||arguments.StorageRid!=argumentsWrite.StorageRid||arguments.UploadRevision!=argumentsWrite.WriterRevision)
            throw new InvalidDataException("DOF2 original GPU counters need their actual completed raw32B argument owner.");
        byte[] bytes=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>rd.BufferGetData(arguments.StorageRid,0,32));
        if(bytes.Length!=32||BinaryPrimitives.ReadUInt32LittleEndian(bytes)!=4||BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16))!=4||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8))!=0||BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12))!=0||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(24))!=0||BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(28))!=0)
            throw new InvalidDataException("DOF2 indirect arguments lost the original CS45 vertexCount4/startVertex0/startInstance0 words.");
        uint rear=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)),fore=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(20));
        foreach(int parameter in new[]{6,7})
        {
            var write=completed.BufferWrites.Single(w=>w.ParameterIndex==parameter);var buffer=Bank.RequireBuffer(parameter,write.InstanceIndex);
            uint count=parameter==7?rear:fore;
            if((ulong)count*16>buffer.Source.ByteCount||!write.SelectedShaderDeclaresWrite||
                write.StorageRid!=buffer.StorageRid||write.WriterRevision!=buffer.UploadRevision)
                throw new InvalidDataException("DOF2 real GPU append prefix exceeds its original typed-buffer capacity or changed physical revision.");
            spritePrefixes[parameter]=new(this,completed,parameter,write.InstanceIndex,buffer.StorageRid,write.WriterRevision,count,
                buffer.Source.ByteCount,arguments.StorageRid,argumentsWrite.WriterRevision);
            spritePrefixes[parameter].RequireCurrent();
        }
    }
}
