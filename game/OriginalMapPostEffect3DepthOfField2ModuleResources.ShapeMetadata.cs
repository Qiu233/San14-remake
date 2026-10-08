using San14.Data;

public sealed partial class OriginalMapPostEffect3DepthOfField2ModuleResources
{
    /// <summary>Before the new Prepare/Init or execution borrow, read only
    /// the retained wrapper dimensions from its actual texture creator.</summary>
    internal OriginalMapPostEffect3RetainedShapeWrapperMetadata CaptureCurrentShapeWrapperMetadata(
        OriginalMapPostEffect3Functor1ModuleInvocation invocation,OriginalMapPostEffect3TextureParameterState table)
    {
        ObjectDisposedException.ThrowIf(retiring||disposed,this);Bank.RequireTexture(59,0);
        if(CpuModule is null||active is not null||UnreturnedDeviceFailure is not null||
            !ReferenceEquals(table.Allocation,Bank.Source)||LastCompletedFrameNumber!=table.FrameNumber-1||
            invocation.Begin.Counter<=1)
            throw new InvalidDataException("Retained shape metadata needs this live module's preceding completed component before the current borrow.");
        invocation.RequireOwnedModule(CpuModule);invocation.RequireCurrentTextureTable(table);
        var shape=Shape??throw new InvalidDataException("Retained shape pointer has no module-owned physical creator.");
        if(!shape.Rid.IsValid)
            throw new InvalidDataException("Retained shape's actual physical allocation has been retired.");
        var extent=CaptureShapeWrapperExtent(shape,table);
        return new(extent,shape.Rid.Id,LastCompletedFrameNumber!.Value,shape.WriterFrameNumber,shape.Revision);
    }

    //The numeric/pointer join is separately testable without a GPU. Only the
    //method above joins real Bank/module/lifetime authority. Neither makes a
    //shape read lease or relabels its previous53 writer as current pixels.
    internal static OriginalMapPostEffect3DepthOfField2ExternalExtent CaptureShapeWrapperExtent(
        ShapeStorage shape,OriginalMapPostEffect3TextureParameterState table)
    {
        var pointer=table.Require(58);table.ValidateCurrent(pointer);
        if(!ReferenceEquals(shape.LastTable.Allocation,table.Allocation)||table.FrameNumber<=shape.LastTable.FrameNumber||
            !ReferenceEquals(pointer.Reference,shape.Reference)||shape.Reference.PhysicalAllocationParameterIndex is not null||
            shape.Reference.InstanceIndex!=0||shape.Reference.HasSourceSelectedMipView||shape.Side is 0 or >32767)
            throw new InvalidDataException("Current shape extent needs the same external58 wrapper/physical creator and later same-allocation table.");
        ushort side=checked((ushort)shape.Side);
        return new(pointer,side,side,shape.Reference.SourceEvidence+
            $";actual-module-shape-wrapper-metadata/physicalRID={shape.Rid.Id};tableFrame={table.FrameNumber}/slotRevision={pointer.SlotRevision};"+
            $"creator-width40/height42={side}/{side};nativeFormat3/DXGI10/M1;metadata-only/no-pixel-publication");
    }
}

internal sealed class OriginalMapPostEffect3RetainedShapeWrapperMetadata
{
    internal OriginalMapPostEffect3RetainedShapeWrapperMetadata(OriginalMapPostEffect3DepthOfField2ExternalExtent extent,
        ulong storage,long componentFrame,long writerFrame,long revision)
        =>(Extent,StorageRid,PreviousComponentFrame,ShapeWriterFrame,ShapeContentsRevision)=(extent,storage,componentFrame,writerFrame,revision);
    internal OriginalMapPostEffect3DepthOfField2ExternalExtent Extent{get;}
    internal ulong StorageRid{get;}
    internal long PreviousComponentFrame{get;}
    internal long ShapeWriterFrame{get;}
    internal long ShapeContentsRevision{get;}
    internal object CaptureEvidence()=>new{tableFrame=Extent.ActualTexture.FrameNumber,Extent.ActualTexture.ParameterIndex,
        Extent.ActualTexture.SlotRevision,Extent.ActualTexture.Reference.InstanceIndex,StorageRid,PreviousComponentFrame,
        ShapeWriterFrame,ShapeContentsRevision,Extent.Width40,Extent.Height42,Extent.SourceEvidence,
        metadataOnly=true,grantsCurrentPixels=false,sourceShapeRecreated=false};
}
