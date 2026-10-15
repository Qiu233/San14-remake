using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public enum DirectionalProviderCallerScope
{
    FreshOwnedCameraClearedLateControlsNoLaterSelectorWriter
}

/// <summary>Finite source construction and current Main Eye projection. This
/// scope explicitly excludes later Camera pair/override and Aux control writers;
/// it does not reconstruct an arbitrary running native Camera's late tail.</summary>
public sealed class DirectionalProviderCallerProjection
{
    private readonly Vector3 eye;
    internal DirectionalProviderCallerProjection(MainMapCameraFrame main,
        MapCommonRenderState common,uint flags,byte checkType,int forceset,string provenance)
    {
        FrameNumber=main.Camera.FrameNumber;MainSourceUri=main.Camera.Source.Resource.Source.ResourceUri;
        MainSourceSha256=main.Camera.Source.Resource.Source.DecodedSha256;MainObjectId=main.Camera.Source.Camera.ObjectId;
        eye=ReadEye(main);Common=common;
        AuxFlags8=flags;CheckLocationType=checkType;ForceSetIndex=forceset;SourceProvenance=provenance;
        // C3E1AD..C3E1D7 clears18C0; C3F000/AB5990 leave these late fields
        // unchanged. Pair0.bit31 clear forces D1A4B0 into its selection branch.
        Controls=new(forceset,0,0,-1,-1,eye.X,eye.Z,true,false);
    }
    private static Vector3 ReadEye(MainMapCameraFrame main)
    {
        var bytes=main.Camera.Prefix.Span.Slice(0x10,12);
        return new(BinaryPrimitives.ReadSingleLittleEndian(bytes),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[4..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[8..]));
    }
    private static bool SameBits(Vector3 first, Vector3 second) =>
        BitConverter.SingleToUInt32Bits(first.X)==BitConverter.SingleToUInt32Bits(second.X) &&
        BitConverter.SingleToUInt32Bits(first.Y)==BitConverter.SingleToUInt32Bits(second.Y) &&
        BitConverter.SingleToUInt32Bits(first.Z)==BitConverter.SingleToUInt32Bits(second.Z);
    public long FrameNumber{get;}
    public string MainSourceUri{get;}
    public string MainSourceSha256{get;}
    public uint MainObjectId{get;}
    public MapCommonRenderState Common{get;}
    public uint AuxFlags8{get;}
    public byte CheckLocationType{get;}
    public int ForceSetIndex{get;}
    public float PairWeight=>0;
    public bool InvocationGate=>true;
    public DirectionalProviderControls Controls{get;}
    public string SourceProvenance{get;}
    public void RequireSameMain(MainMapCameraFrame main)
    {
        if(main.Camera.FrameNumber!=FrameNumber||main.Camera.Source.Resource.Source.ResourceUri!=MainSourceUri||
            main.Camera.Source.Resource.Source.DecodedSha256!=MainSourceSha256||main.Camera.Source.Camera.ObjectId!=MainObjectId||
            !SameBits(ReadEye(main),eye))
            throw new InvalidDataException("Directional provider caller belongs to a different same-frame Main Camera Eye/source.");
    }
}

public static class DirectionalLightProviderCaller
{
    public static void RequireFreshDefaultCaller(MainMapCameraFrame main,MapCommonRenderState common)
    {
        ArgumentNullException.ThrowIfNull(main);ArgumentNullException.ThrowIfNull(common);
        if(common.Scope!=CommonRenderPublicationKind.InitialSettings||common.InitialSettings is null||
            common.FrameNumber!=main.Camera.FrameNumber||main.Camera.FrameNumber<=0||main.Camera.Prefix.Length!=0x3C0||
            main.Camera.Source.Camera.ObjectId!=MainMapCameraAdapter.MainCameraObjectId)
            throw new InvalidDataException("Fresh Directional caller requires the actual same-frame Main and owned no-callback Aux scope.");
        if(main.Camera.Source.Declaration.ExternalDirectionalSelectorKey is not 0)
            throw new NotSupportedException("Fresh Main external selector requires another actual current override owner.");
        var source=common.InitialSettings;
        if(source.DirectionalLocationCheckType is not 0||source.DirectionalForceSetIndex is not -1)
            throw new NotSupportedException("Default Directional caller requires another actual Aux selector branch.");
    }

    public static DirectionalProviderCallerProjection CaptureFreshOwnedCaller(
        MainMapCameraFrame main,MapCommonRenderState common,DirectionalProviderCallerScope scope)
    {
        if(scope!=DirectionalProviderCallerScope.FreshOwnedCameraClearedLateControlsNoLaterSelectorWriter)
            throw new InvalidDataException("Unsupported fresh Directional caller scope.");
        RequireFreshDefaultCaller(main,common);
        var source=common.InitialSettings!;
        var camera=main.Camera.Source;
        return new(main,common,source.Flags8,(byte)source.DirectionalLocationCheckType!.Value,source.DirectionalForceSetIndex!.Value,
            $"{source.SourceIdentity};Main={camera.Resource.Source.ResourceUri}#{camera.Camera.ObjectId:X8}@{camera.Resource.Source.DecodedSha256};"+
            $"frame={main.Camera.FrameNumber};explicit-scope={scope};D1A4B0-copy-B992A0;late15BC/15C0/15C4/15C8=construction-cleared;pair0/0-weight0;scalar=-1/-1;Eye=current-Main10");
    }
}

public sealed class DirectionalProviderCallerCurrentFrame
{
    private readonly byte[] camera,bytes;
    internal DirectionalProviderCallerCurrentFrame(DirectionalLightConstructionPacket root,
        DirectionalProviderCallerProjection caller,ReadOnlySpan<byte> preparedCamera)
    {
        RegisteredRoot=root;Caller=caller;camera=preparedCamera.ToArray();
        Temporary=DirectionalLightConstruction.ConstructProviderRootZeroPairTemporary(root);
        var current=Temporary.ApplyCurrentCamera(camera);bytes=current.Bytes;
        Applied=current.Applied;SourceHighBitSwappedRows=current.SourceHighBitSwappedRows;
    }
    public DirectionalLightConstructionPacket RegisteredRoot{get;}
    public DirectionalProviderCallerProjection Caller{get;}
    public DirectionalLightProviderTemporary Temporary{get;}
    public ReadOnlyMemory<byte> Bytes=>bytes.ToArray();
    internal ReadOnlySpan<byte> PreparedBytes=>bytes;
    public bool Applied{get;}
    public bool SourceHighBitSwappedRows{get;}
    public string SourceProvenance=>Caller.SourceProvenance+";"+Temporary.SourceProvenance;
    public void RequireCamera(ReadOnlySpan<byte> sameFramePreparedCamera)
    {if(!sameFramePreparedCamera.SequenceEqual(camera))throw new InvalidDataException("Provider frame has another current prepared Camera generation.");}
}

public sealed partial class MapDirectionalLightProviderOwner
{
    public DirectionalProviderCallerCurrentFrame PrepareFreshCallerFrame(DirectionalLightConstructionPacket root,
        DirectionalProviderCallerProjection caller,MainMapCameraFrame main,ReadOnlySpan<byte> preparedCamera3C0)
    {
        ArgumentNullException.ThrowIfNull(root);ArgumentNullException.ThrowIfNull(caller);
        ValidateRootBinding(root);caller.RequireSameMain(main);
        if(caller.Controls.FirstIndex!=-1||caller.Controls.PairFirstIndex!=0||caller.Controls.PairSecondIndex!=0||caller.PairWeight!=0||
            caller.Controls.ExternalCameraOverridePresent||!caller.InvocationGate)
            throw new InvalidDataException("Fresh provider caller is outside actual zero-pair temporary scope.");
        return new(root,caller,preparedCamera3C0);
    }
}
