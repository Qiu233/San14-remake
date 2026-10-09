using System.Buffers.Binary;

namespace San14.Data;

public enum AuxiliaryCameraFilterScope
{
    SourceLoadedCameraNoLaterFilterWriter
}

/// <summary>The two source-loaded late Camera words consumed by C9AB00 and
/// C9AAE0. This is a bounded receipt, not a fabricated complete Camera tail.
/// Later explicit filter setters require a different current owner.</summary>
public sealed class DeferredAuxiliaryCameraFilterSource
{
    private readonly LoadedMapCameraConstructionSource source;
    private readonly byte[]? prefix;
    private DeferredAuxiliaryCameraFilterSource(LoadedMapCameraConstructionSource source,
        int resolution,int depth,long? frame,ReadOnlySpan<byte> prefix)
    {this.source=source;CameraTransparencyResolutionED8=resolution;CameraDepthFilterTypeEDC=depth;
        FrameNumber=frame;this.prefix=frame.HasValue?prefix.ToArray():null;}
    public int CameraTransparencyResolutionED8 { get; }
    public int CameraDepthFilterTypeEDC { get; }
    public long? FrameNumber { get; }
    public uint MainObjectId=>source.Camera.ObjectId;
    public string SourceProvenance=>source.ConstructorOrigin+
        ";TC-C53078/C53086:properties1040/1041->CPU-C48/C4C;TC-C42E1E..C42E30:Camera-ED8/EDC;"+
        "TC-E9EDE2:B992A0-current-Camera-copy;C9AB00/C9AAE0:Camera-overrides/CommonView-fallback;"+
        $"scope={AuxiliaryCameraFilterScope.SourceLoadedCameraNoLaterFilterWriter};frame={FrameNumber}";

    public static DeferredAuxiliaryCameraFilterSource ReadSourceLoadedMain(
        LoadedMapCameraConstructionSource source,AuxiliaryCameraFilterScope scope)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(scope!=AuxiliaryCameraFilterScope.SourceLoadedCameraNoLaterFilterWriter||
            source.Camera.ObjectId!=MainMapCameraAdapter.MainCameraObjectId)
            throw new InvalidDataException("Auxiliary late filter receipt requires actual source-loaded Main38D with no later filter writer.");
        var canonical=MapCameraConstructionSource.ReadPerspective(source.Resource,source.Camera.ObjectId);
        int Word(int ordinal,uint hash)
        {
            var p=canonical.Camera.Properties[ordinal];
            if(p.Index!=ordinal||p.NameHash!=hash||p.TypeCode!=4||p.ElementCount!=1||p.ElementSize!=4||p.RawValue.Length!=4)
                throw new InvalidDataException("Main Camera late filter property differs from its actual source loader.");
            return BinaryPrimitives.ReadInt32LittleEndian(p.RawValue);
        }
        return new(canonical,Word(1040,0xA29E490D),Word(1041,0x5692BB6E),null,default);
    }
    public static DeferredAuxiliaryCameraFilterSource CaptureSourceLoadedMain(
        MainMapCameraFrame main,AuxiliaryCameraFilterScope scope)
    {
        ArgumentNullException.ThrowIfNull(main);
        if(main.Camera.FrameNumber<=0||main.Camera.Prefix.Length!=0x3C0)
            throw new InvalidDataException("Auxiliary filter receipt needs the accepted same-frame Main Camera prefix.");
        var initial=ReadSourceLoadedMain(main.Camera.Source,scope);
        return new(initial.source,initial.CameraTransparencyResolutionED8,initial.CameraDepthFilterTypeEDC,
            main.Camera.FrameNumber,main.Camera.Prefix.Span);
    }
    public int ReadDepthFilterType(int? currentCommonView98)
    {ValidateSource();return CameraDepthFilterTypeEDC>=0?CameraDepthFilterTypeEDC:currentCommonView98??1;}
    public DeferredTransparencyImageSelection ReadTransparencySelection(int? currentCommonView8C)
    {ValidateSource();return new(CameraTransparencyResolutionED8,currentCommonView8C);}
    public void RequireSameMain(MainMapCameraFrame main)
    {
        ArgumentNullException.ThrowIfNull(main);ValidateSource();
        _=MapCameraConstructionSource.ReadPerspective(main.Camera.Source.Resource,main.Camera.Source.Camera.ObjectId);
        if(!FrameNumber.HasValue||FrameNumber!=main.Camera.FrameNumber||prefix is null||
            !prefix.AsSpan().SequenceEqual(main.Camera.Prefix.Span)||main.Camera.Source.Camera.ObjectId!=MainObjectId||
            main.Camera.Source.Resource.Source.ResourceUri!=source.Resource.Source.ResourceUri||
            main.Camera.Source.Resource.Source.DecodedSha256!=source.Resource.Source.DecodedSha256)
            throw new InvalidDataException("Auxiliary late Camera filter receipt belongs to another source/current Main frame.");
    }
    private void ValidateSource()
    {
        var current=ReadSourceLoadedMain(source,AuxiliaryCameraFilterScope.SourceLoadedCameraNoLaterFilterWriter);
        if(current.CameraDepthFilterTypeEDC!=CameraDepthFilterTypeEDC||current.CameraTransparencyResolutionED8!=CameraTransparencyResolutionED8)
            throw new InvalidDataException("Auxiliary late Camera filter source changed.");
    }
}
