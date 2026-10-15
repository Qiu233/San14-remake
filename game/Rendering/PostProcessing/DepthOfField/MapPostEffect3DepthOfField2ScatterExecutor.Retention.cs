using System.Buffers.Binary;
using System.Security.Cryptography;
using Godot;
using San14.Data;

public sealed partial class MapPostEffect3DepthOfField2ScatterExecutor
{
    // Each source side holds its last real GPU copy, independently of later
    // writes to B5 or the other staging side. No CPU generation creates bytes.
    private readonly MapPostEffect3DepthOfField2StagingCopyReceipt?[] stagingContents=new MapPostEffect3DepthOfField2StagingCopyReceipt?[2];
    private void RecordActualStagingCopy(MapPostEffect3DepthOfField2ScatterInputs input,uint side,int pass)
    {
        var arguments=input.ActualArguments;
        long revision=checked((stagingContents[side]?.ActualStagingWriteRevision??0)+1);
        var copied=new MapPostEffect3DepthOfField2StagingCopyReceipt(input.Prepared.FrameNumber,pass,side,
            arguments.StorageRid,staging[side],32,input.Before.SourceEvidence+
            ";staging-copy-before-indirect;actual-source-side28;physical-copy-retained")
            {ActualArgumentsPhysicalSide=arguments.InstanceIndex,ActualArgumentsWriterRevision=arguments.UploadRevision,
                ActualStagingWriteRevision=revision};
        stagingContents[side]=copied;LastStagingCopy=copied;
    }
    public IReadOnlyList<MapPostEffect3DepthOfField2StagingCopyReceipt> CaptureRetainedStagingCopies()
    {
        ObjectDisposedException.ThrowIf(retiring,this);
        var copies=stagingContents.OfType<MapPostEffect3DepthOfField2StagingCopyReceipt>().ToArray();
        foreach(var copy in copies)RequireStagingCopy(copy);
        return Array.AsReadOnly(copies);
    }
    internal void RequireStagingCopy(MapPostEffect3DepthOfField2StagingCopyReceipt copy)
    {
        ObjectDisposedException.ThrowIf(retiring,this);
        if(copy.SourceSide28>1||!ReferenceEquals(stagingContents[copy.SourceSide28],copy)||
            staging[copy.SourceSide28]!=copy.ActualStagingStorageRid||!copy.ActualStagingStorageRid.IsValid||
            copy.ByteCount!=32||copy.ActualStagingWriteRevision<=0||copy.ActualArgumentsWriterRevision<=0||
            copy.SourcePassIndex is not(62 or 63))
            throw new InvalidDataException("DOF2 staging bytes lack the same actual physical copy and contents revision.");
    }
    internal async Task<MapPostEffect3DepthOfField2StagingReadbackReceipt> ReadRetainedStagingAsync(
        MapPostEffect3ParameterDepthOfField2Update prepared,Action requireCurrentSource)
    {
        await serial.WaitAsync();IDisposable? bankScope=null;
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);await DrainPendingDrawCoreAsync();requireCurrentSource();
            uint side=prepared.NextController.SpriteSide28;
            if(!prepared.RequiresSpriteCountReadback||side>1||prepared.ConstantBuffer.OwnerIdentity!=Bank.Source.ContextOwnerIdentity)
                throw new InvalidDataException("DOF2 retained staging map requires actual source Before57/count0/current side28.");
            var copy=stagingContents[side]??throw new NotSupportedException("DOF2 source staging side has no actual earlier GPU copy; allocation does not provide native map contents.");
            RequireStagingCopy(copy);
            if(copy.FrameNumber>=prepared.FrameNumber)
                throw new InvalidDataException("DOF2 retained Before57 must read the earlier contents selected by source side28.");
            bankScope=await Bank.AcquireBufferExecutionScopeAsync();
            byte[] bytes=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                requireCurrentSource();RequireStagingCopy(copy);
                return rd.BufferGetData(copy.ActualStagingStorageRid,0,32);
            });
            requireCurrentSource();RequireStagingCopy(copy);
            if(bytes.Length!=32||BinaryPrimitives.ReadUInt32LittleEndian(bytes)!=4||
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16))!=4||
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8))!=0||BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12))!=0||
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(24))!=0||BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(28))!=0)
                throw new InvalidDataException("DOF2 actual retained staging read does not have original two DrawInstancedIndirect32B counter groups.");
            uint rear=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
            uint fore=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(20));
            if(rear>129600||fore>129600)throw new InvalidDataException("DOF2 retained GPU count exceeds original two-texel sprite capacity.");
            var sourceRead=new MapPostEffect3DepthOfField2SpriteCountReadback(prepared.FrameNumber,
                Bank.Source.ContextOwnerIdentity,side,false,rear,fore,
                copy.SourceEvidence+$";actual-GPU-staging-map32B;copyFrame={copy.FrameNumber};copyRevision={copy.ActualStagingWriteRevision};sha256={Convert.ToHexString(SHA256.HashData(bytes))}");
            return new(this,copy,sourceRead,requireCurrentSource);
        }
        finally{bankScope?.Dispose();serial.Release();}
    }
}

/// <summary>Closed device readback for one consumer frame. A successful Godot
/// read supplies a non-null source map; failures cannot manufacture native null.</summary>
public sealed class MapPostEffect3DepthOfField2StagingReadbackReceipt
{
    private readonly MapPostEffect3DepthOfField2ScatterExecutor owner;
    private readonly Action requireCurrentSource;
    internal MapPostEffect3DepthOfField2StagingReadbackReceipt(MapPostEffect3DepthOfField2ScatterExecutor owner,
        MapPostEffect3DepthOfField2StagingCopyReceipt copy,MapPostEffect3DepthOfField2SpriteCountReadback read,
        Action requireCurrentSource)=>(this.owner,ActualCopy,SourceReadback,this.requireCurrentSource)=(owner,copy,read,requireCurrentSource);
    public MapPostEffect3DepthOfField2StagingCopyReceipt ActualCopy{get;}
    public MapPostEffect3DepthOfField2SpriteCountReadback SourceReadback{get;}
    public bool CommitsFunctorHistory=>false;
    public void RequireCurrent(){requireCurrentSource();owner.RequireStagingCopy(ActualCopy);}
}
