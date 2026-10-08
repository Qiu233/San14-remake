using System.Buffers.Binary;

namespace San14.Data;

public enum OriginalDirectionalProviderCallerScope
{
    FreshOwnedCameraClearedLateControlsNoLaterSelectorWriter
}

/// <summary>Finite source construction and current Main Eye projection. This
/// scope explicitly excludes later Camera pair/override and Aux control writers;
/// it does not reconstruct an arbitrary running native Camera's late tail.</summary>
public sealed class OriginalDirectionalProviderCallerProjection
{
    private readonly byte[] eye;
    internal OriginalDirectionalProviderCallerProjection(OriginalMainMapCameraFrame main,
        OriginalMapFreshCommonResourceSnapshot common,uint flags,byte checkType,int forceset,string provenance)
    {
        FrameNumber=main.Camera.FrameNumber;MainSourceUri=main.Camera.Source.Resource.Source.ResourceUri;
        MainSourceSha256=main.Camera.Source.Resource.Source.DecodedSha256;MainObjectId=main.Camera.Source.Camera.ObjectId;
        eye=main.Camera.Prefix.Span.Slice(0x10,12).ToArray();Common=common;
        AuxFlags8=flags;CheckLocationType=checkType;ForceSetIndex=forceset;SourceProvenance=provenance;
        // C3E1AD..C3E1D7 clears18C0; C3F000/AB5990 leave these late fields
        // unchanged. Pair0.bit31 clear forces D1A4B0 into its selection branch.
        Controls=new(forceset,0,0,-1,-1,F(0),F(8),true,false);
    }
    private float F(int offset)=>BinaryPrimitives.ReadSingleLittleEndian(eye.AsSpan(offset,4));
    public long FrameNumber{get;}
    public string MainSourceUri{get;}
    public string MainSourceSha256{get;}
    public uint MainObjectId{get;}
    public OriginalMapFreshCommonResourceSnapshot Common{get;}
    public uint AuxFlags8{get;}
    public byte CheckLocationType{get;}
    public int ForceSetIndex{get;}
    public float PairWeight=>0;
    public bool InvocationGate=>true;
    public OriginalDirectionalProviderControls Controls{get;}
    public string SourceProvenance{get;}
    public void RequireSameMain(OriginalMainMapCameraFrame main)
    {
        if(main.Camera.FrameNumber!=FrameNumber||main.Camera.Source.Resource.Source.ResourceUri!=MainSourceUri||
            main.Camera.Source.Resource.Source.DecodedSha256!=MainSourceSha256||main.Camera.Source.Camera.ObjectId!=MainObjectId||
            !main.Camera.Prefix.Span.Slice(0x10,12).SequenceEqual(eye))
            throw new InvalidDataException("Directional provider caller belongs to a different same-frame Main Camera Eye/source.");
    }
}

public static class OriginalDirectionalLightProviderCaller
{
    public static OriginalDirectionalProviderCallerProjection CaptureFreshOwnedCaller(OriginalGameRepository repository,
        OriginalMainMapCameraFrame main,OriginalMapFreshCommonResourceSnapshot common,OriginalDirectionalProviderCallerScope scope)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(main);ArgumentNullException.ThrowIfNull(common);
        if(scope!=OriginalDirectionalProviderCallerScope.FreshOwnedCameraClearedLateControlsNoLaterSelectorWriter||
            common.Scope!=OriginalMapFreshCommonResourceScope.SourceCreatedNoCallbackBeforeAmbientPublication||
            common.FrameNumber!=main.Camera.FrameNumber||main.Camera.FrameNumber<=0||main.Camera.Prefix.Length!=0x3C0||
            main.Camera.Source.Camera.ObjectId!=OriginalMainMapCameraAdapter.MainCameraObjectId)
            throw new InvalidDataException("Fresh Directional caller requires the actual same-frame Main and owned no-callback Aux scope.");
        var camera=OriginalMapCameraConstructionSource.ReadPerspective(main.Camera.Source.Resource,main.Camera.Source.Camera.ObjectId);
        var selector=camera.Camera.Properties[1050];
        if(selector.NameHash!=0x2D4CC9AF||selector.TypeCode!=5||selector.ElementCount!=1||selector.ElementSize!=4||
            selector.RawValue.Length!=4||BinaryPrimitives.ReadUInt32LittleEndian(selector.RawValue)!=0)
            throw new NotSupportedException("Fresh Main external selector requires another actual current override owner.");
        var commonObjects=repository.LoadObjects("KIDSSystemResource",OriginalMapFreshCommonResources.SourceFileId);
        var source=OriginalMapFreshCommonResources.ReadSourceTemplate(commonObjects);
        if(common.CommonViewFlags8!=source.Flags8||!common.ProducerProvenance.Contains(source.SourceIdentity,StringComparison.Ordinal))
            throw new InvalidDataException("Fresh provider Aux differs from its actual saved source generation.");
        var declaration=commonObjects.Document.Objects.Single(o=>o.ObjectId==OriginalMapFreshCommonResources.SourceObjectId);
        uint Word(int ordinal,uint hash)
        {
            var p=declaration.Properties[ordinal];
            if(p.Index!=ordinal||p.NameHash!=hash||p.TypeCode!=4||p.ElementCount!=1||p.ElementSize!=4||p.RawValue.Length!=4)
                throw new InvalidDataException("Directional provider Aux source descriptor differs from its exact loader.");
            return BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue);
        }
        uint type=Word(23,0x6AEA4989),force=Word(24,0x00D3B791);
        if(type!=0||force!=uint.MaxValue)
            throw new NotSupportedException("Default Directional caller requires another actual Aux selector branch.");
        return new(main,common,source.Flags8,(byte)type,unchecked((int)force),
            $"{source.SourceIdentity};Main={camera.Resource.Source.ResourceUri}#{camera.Camera.ObjectId:X8}@{camera.Resource.Source.DecodedSha256};"+
            $"frame={main.Camera.FrameNumber};explicit-scope={scope};D1A4B0-copy-B992A0;late15BC/15C0/15C4/15C8=construction-cleared;pair0/0-weight0;scalar=-1/-1;Eye=current-Main10");
    }
}

public sealed class OriginalDirectionalProviderCallerCurrentFrame
{
    private readonly byte[] camera,bytes;
    internal OriginalDirectionalProviderCallerCurrentFrame(OriginalDirectionalLightConstructionPacket root,
        OriginalDirectionalProviderCallerProjection caller,ReadOnlySpan<byte> preparedCamera)
    {
        RegisteredRoot=root;Caller=caller;camera=preparedCamera.ToArray();
        Temporary=OriginalDirectionalLightConstruction.ConstructProviderRootZeroPairTemporary(root);
        var current=Temporary.ApplyCurrentCamera(camera);bytes=current.Bytes.ToArray();
        Applied=current.Applied;SourceHighBitSwappedRows=current.SourceHighBitSwappedRows;
    }
    public OriginalDirectionalLightConstructionPacket RegisteredRoot{get;}
    public OriginalDirectionalProviderCallerProjection Caller{get;}
    public OriginalDirectionalLightProviderTemporary Temporary{get;}
    public ReadOnlyMemory<byte> Bytes=>bytes.ToArray();
    public bool Applied{get;}
    public bool SourceHighBitSwappedRows{get;}
    public string SourceProvenance=>Caller.SourceProvenance+";"+Temporary.SourceProvenance;
    /// <summary>Explicit numerical replay for diagnostics. Ordinary consumers
    /// bind the accepted current Camera with RequireCamera.</summary>
    public void ValidateCurrentSourceFrame()
    {
        Temporary.ValidateSourceTemporary();var expected=Temporary.ApplyCurrentCamera(camera);
        if(expected.Applied!=Applied||expected.SourceHighBitSwappedRows!=SourceHighBitSwappedRows||!expected.Bytes.AsSpan().SequenceEqual(bytes))
            throw new InvalidDataException("Directional provider temporary current Camera projection changed.");
    }
    public void RequireCamera(ReadOnlySpan<byte> sameFramePreparedCamera)
    {if(!sameFramePreparedCamera.SequenceEqual(camera))throw new InvalidDataException("Provider frame has another current prepared Camera generation.");}
}

public sealed partial class OriginalMapDirectionalLightProviderOwner
{
    public OriginalDirectionalProviderCallerCurrentFrame PrepareFreshCallerFrame(OriginalDirectionalLightConstructionPacket root,
        OriginalDirectionalProviderCallerProjection caller,OriginalMainMapCameraFrame main,ReadOnlySpan<byte> preparedCamera3C0)
    {
        ArgumentNullException.ThrowIfNull(root);ArgumentNullException.ThrowIfNull(caller);
        ValidateRootBinding(root);caller.RequireSameMain(main);
        if(caller.Controls.FirstIndex!=-1||caller.Controls.PairFirstIndex!=0||caller.Controls.PairSecondIndex!=0||caller.PairWeight!=0||
            caller.Controls.ExternalCameraOverridePresent||!caller.InvocationGate)
            throw new InvalidDataException("Fresh provider caller is outside actual zero-pair temporary scope.");
        return new(root,caller,preparedCamera3C0);
    }
}
