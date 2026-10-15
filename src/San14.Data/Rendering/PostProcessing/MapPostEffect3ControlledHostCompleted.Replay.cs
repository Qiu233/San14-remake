namespace San14.Data;

public sealed partial class MapOwnedGraphicsColorMaskHistorySource
{
    public void ValidateCompletedSourceHostOutput(MapOwnedGraphicsMaskPrefix prefix,
        MapPostEffect3ControlledHostCompleted actualCompleted)
    {
        ArgumentNullException.ThrowIfNull(actualCompleted);RequireMutableSourceBoundary();RequirePrefix(prefix,true);
        actualCompleted.RequireCompletedSourceHostWriterScope();
        if(actualCompleted.FrameNumber!=prefix.FrameNumber||actualCompleted.Camera.ExecutableLocale!=Locale||
            !ReferenceEquals(actualCompleted.CurrentStock.Prefix,prefix)||actualCompleted.ReturnedRt0ColorWriteMask!=prefix.ColorWriteMask)
            throw new InvalidDataException("Controlled Host mask acknowledgement is foreign to this exact source attempt.");
    }
    /// <summary>Commit last, after the same closed actual returned output was
    /// validated by Clip and Stock. Does not create device or pixel authority.</summary>
    public void ReplayCompletedSourceHostOutput(MapOwnedGraphicsMaskPrefix prefix,
        MapPostEffect3ControlledHostCompleted actualCompleted)
    {
        ValidateCompletedSourceHostOutput(prefix,actualCompleted);
        CompletedSourceFrameNumber=prefix.FrameNumber;lastCompletedPrefix=prefix;pending=null;started=false;
        ReleaseAcceptedSourceSnapshot();
    }
}

public sealed partial class MapCopy97CurrentStockSource
{
    public void ValidateCompletedSourceHostOutput(MapCopy97CurrentStockPacket packet,
        MapPostEffect3ControlledHostCompleted actualCompleted)
    {
        ArgumentNullException.ThrowIfNull(packet);ArgumentNullException.ThrowIfNull(actualCompleted);
        ValidateCurrent(packet);actualCompleted.RequireCompletedSourceHostWriterScope();
        var camera=actualCompleted.Camera;
        if(!ReferenceEquals(actualCompleted.CurrentStock,packet)||!ReferenceEquals(actualCompleted.Context,packet.Frame.Context)||
            actualCompleted.FrameNumber!=packet.FrameNumber||camera.Source.Camera.ObjectId!=packet.Frame.Camera.Source.Camera.ObjectId||
            camera.Source.Resource.Source.ResourceUri!=packet.Frame.Camera.Source.Resource.Source.ResourceUri||
            camera.Source.Resource.Source.DecodedSha256!=packet.Frame.Camera.Source.Resource.Source.DecodedSha256||
            camera.CurrentPose!=packet.Frame.Camera.CurrentPose||
            !camera.Prefix.Span.SequenceEqual(packet.Frame.Lighting.PreparedCameraPrefix.Span))
            throw new InvalidDataException("Controlled Host stock acknowledgement is foreign to this actual Camera/Q/lighting/frame.");
        frontend.ValidateCompletedSourceHostOutput(packet.Prefix,actualCompleted);
    }
    public void ReplayCompletedSourceHostOutput(MapCopy97CurrentStockPacket packet,
        MapPostEffect3ControlledHostCompleted actualCompleted)
    {
        ValidateCompletedSourceHostOutput(packet,actualCompleted);
        // F1 shaders and the admitted caller copy have no six-word setter;
        // stock B0 was source-null on entry and ScreenEnd restores its scope.
        completedScreenScale=packet.CurrentScreenScale29E0;completedColorScale=packet.CurrentColorScale29F0;
        completedCustomVsAbsent=packet.CurrentCustomVertexShaderIsAbsent;
        CompletedSourceFrameNumber=packet.FrameNumber;prepared=null;acceptedMaskSources=null;
    }
}

public sealed partial class SkyCloudSourceClipPlane
{
    public void ValidateCompletedSourceHostOutput(SkyCloudSourceClipPlanePacket packet,
        MapCopy97CurrentStockPacket actualStock,MapPostEffect3ControlledHostCompleted actualCompleted)
    {
        ArgumentNullException.ThrowIfNull(actualCompleted);ValidateSubmittedCurrentFamily(packet,actualStock);
        actualStock.Source.ValidateCompletedSourceHostOutput(actualStock,actualCompleted);
    }
    public void ReplayCompletedSourceHostOutput(SkyCloudSourceClipPlanePacket packet,
        MapCopy97CurrentStockPacket actualStock,MapPostEffect3ControlledHostCompleted actualCompleted)
    {
        ValidateCompletedSourceHostOutput(packet,actualStock,actualCompleted);
        CompletedSourceFrameNumber=packet.FrameNumber;completed=packet;
    }
}
