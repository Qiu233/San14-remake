using System.Buffers.Binary;
using San14.Data;

// Numeric optics belongs to the existing season publisher; post textures,
// exposure and GPU histories remain with the native NormalPostProcess owner.
internal sealed record D3D11CurrentPostOpticsSnapshot(MapCameraConstructionPacket Camera,
    MapPostEffect3ApplyDerivedScalarPlan Dimensions,MapPostEffect3ControlledOpticsPreparation Optics,
    MapPostEffect3ParameterState State,bool ApertureChanged,int Blades,int Diaphragm,float[] Values)
{
    internal const int ByteLength=68;
    internal const uint Signature=0x314f4450;
    internal void Write(Span<byte> bytes)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes,Signature);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[4..],ApertureChanged?1u:0u);
        BinaryPrimitives.WriteInt32LittleEndian(bytes[8..],Blades);BinaryPrimitives.WriteInt32LittleEndian(bytes[12..],Diaphragm);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[16..],(uint)Dimensions.Dimensions.DeviceWidth);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[20..],(uint)Dimensions.Dimensions.DeviceHeight);
        for(int i=0;i<Values.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(bytes[(24+i*4)..],Values[i]);
    }
}

internal sealed class D3D11CurrentPostOpticsPublisher
{
    private readonly MapPostEffect3OwnedControlledCameraSources sources;
    private readonly MapPostEffect3AllocationPlan allocation;
    private D3D11CurrentPostOpticsSnapshot accepted;
    internal D3D11CurrentPostOpticsPublisher(D3D11NormalPostInitialConstants initial)
    {
        sources=initial.Sources;allocation=initial.Allocation;
        var camera=initial.Frame.CaptureAcceptedCamera();
        accepted=Capture(camera,initial.Frame.Derived,initial.Frame.Optics,initial.Frame.State,true);
    }
    internal D3D11CurrentPostOpticsSnapshot Prepare(D3D11CurrentSeasonFrame frame)
    {
        var camera=frame.Present.CapturePreparedCamera();
        if(camera.FrameNumber==accepted.Camera.FrameNumber)return accepted;
        var dimensions=accepted.Dimensions with{FrameNumber=camera.FrameNumber,
            Dimensions=accepted.Dimensions.Dimensions with{FrameNumber=camera.FrameNumber}};
        var state=accepted.State.CloneForNewFrame(camera.FrameNumber);
        var optics=sources.Optics.PrepareRetainedCurrentCamera(camera,accepted.Camera,dimensions,
            accepted.Dimensions,accepted.Optics,state,allocation,NativeLogDispatch.Sse);
        if(optics.ActualDirtyAfBeforePreparation)optics.ApplyRetainedToCurrentState(state,allocation);
        var before=accepted.Camera.Optics;var now=camera.Optics;
        bool aperture=now.LensSimulation.FStop!=before.LensSimulation.FStop||
            now.LensSimulation.AnamorphicFlag!=before.LensSimulation.AnamorphicFlag||
            now.LensEffects.DiaphragmBladeCounts!=before.LensEffects.DiaphragmBladeCounts||
            ((now.DepthOfField.Flags^before.DepthOfField.Flags)&8)!=0;
        return Capture(camera,dimensions,optics,state,aperture);
    }
    internal void Accept(D3D11CurrentPostOpticsSnapshot current)=>accepted=current;
    private D3D11CurrentPostOpticsSnapshot Capture(MapCameraConstructionPacket camera,
        MapPostEffect3ApplyDerivedScalarPlan dimensions,MapPostEffect3ControlledOpticsPreparation optics,
        MapPostEffect3ParameterState state,bool aperture)
    {
        float F(string name,int lane=0)=>BinaryPrimitives.ReadSingleLittleEndian(state.RequireValue(name).Span[(lane*4)..]);
        float gather=sources.Camera.PrepareCurrentGatherMaximum((int)(camera.Optics.DepthOfField.Flags&7),dimensions.EffectiveHeight84);
        float[] values=[gather,F("FocasuedDistance"),F("DofLensCoeff"),F("ScreenWhAspect"),F("AnamorphicRateXY"),F("AnamorphicRateXY",1),
            F("StopPolyRate"),F("LimbDarkenningInfo"),F("LimbDarkenningInfo",1),F("LimbDarkenningInfo",2),F("LimbDarkenningLinFade")];
        return new(camera,dimensions,optics,state,aperture,unchecked((int)camera.Optics.LensEffects.DiaphragmBladeCounts),
            (int)((camera.Optics.DepthOfField.Flags>>3)&1),values);
    }
}
