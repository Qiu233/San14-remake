using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual D11190 arguments: R9D, entrySP28, entrySP30 and
/// entrySP38. These are not Scene View offsets1950/1958/1960.</summary>
public readonly record struct DeferredEarlyShadowPreparationArguments(
    uint FirstR9, uint SecondEntry28, float DirectionEntry30, float DirectionEntry38);

/// <summary>Only D01580's four source context stores. Other CommonContext
/// bytes and later scheduling/publication remain owned by their actual writer.</summary>
public readonly record struct DeferredEarlyShadowContextConstructorFields(
    uint Common353C, uint Common3540, float Common3550, float Common3554)
{
    public void WriteOwnedContextNumericFields(Span<byte> actualOwnedContext)
    {
        if (actualOwnedContext.Length < 0x3558)
            throw new ArgumentException("Shadow context source stores require the actual context through3554.", nameof(actualOwnedContext));
        BinaryPrimitives.WriteUInt32LittleEndian(actualOwnedContext[0x353C..], Common353C);
        BinaryPrimitives.WriteUInt32LittleEndian(actualOwnedContext[0x3540..], Common3540);
        BinaryPrimitives.WriteSingleLittleEndian(actualOwnedContext[0x3550..], Common3550);
        BinaryPrimitives.WriteSingleLittleEndian(actualOwnedContext[0x3554..], Common3554);
    }
}

public sealed class DeferredEarlyShadowCurrentPreparationArguments
{
    internal DeferredEarlyShadowCurrentPreparationArguments(MapFreshCommonResourceSnapshot common,
        MapSourceCreatedViewCameraFields camera, uint quality,
        DeferredEarlyShadowPreparationArguments values, string evidence)
        => (Common, Camera, CameraA88, Values, SourceEvidence) = (common, camera, quality, values, evidence);
    public MapFreshCommonResourceSnapshot Common { get; }
    public MapSourceCreatedViewCameraFields Camera { get; }
    public uint CameraA88 { get; }
    public DeferredEarlyShadowPreparationArguments Values { get; }
    public string SourceEvidence { get; }
}

/// <summary>One imported View0 constructor fragment. D20D50 loads actual
/// properties11/12/23; D01580 writes353C/3540/3550/3554. Original quality
/// selection is reused without rereading resources in a current request.</summary>
public sealed class LoadedDeferredEarlyShadowPreparationArgumentsSource
{
    internal LoadedDeferredEarlyShadowPreparationArgumentsSource(
        DeferredEarlyShadowContextConstructorFields fields, uint mainQuality, string evidence)
        => (ConstructorFields, SourceRetainedMainCameraA88, SourceEvidence) = (fields, mainQuality, evidence);
    public DeferredEarlyShadowContextConstructorFields ConstructorFields { get; }
    public uint SourceRetainedMainCameraA88 { get; }
    public string SourceEvidence { get; }

    public DeferredEarlyShadowPreparationArguments SelectCurrentQuality(uint actualCameraA88)
        => SelectCurrentQuality(ConstructorFields, actualCameraA88);

    /// <summary>Complete D4BBC3..D4BC98 quality table. The uint decrement
    /// and JA gate preserve source defaults for0 or any value outside1..18.</summary>
    public static DeferredEarlyShadowPreparationArguments SelectCurrentQuality(
        DeferredEarlyShadowContextConstructorFields actualContext, uint actualCameraA88)
    {
        (uint first, uint second) = actualCameraA88 switch
        {
            1 or 6 => (1024u, 1024u),
            2 or 7 => (2048u, 2048u),
            3 or 8 => (4096u, 4096u),
            4 or 9 => (1024u, 2048u),
            5 or 10 => (2048u, 4096u),
            11 or 15 => (256u, 256u),
            12 or 16 => (512u, 512u),
            13 or 17 => (256u, 512u),
            14 or 18 => (512u, 1024u),
            _ => (actualContext.Common353C, actualContext.Common3540)
        };
        return new(first, second, actualContext.Common3550, actualContext.Common3554);
    }

    /// <summary>Finite fresh source-context/Main-tail branch. The caller's
    /// existing retained Camera scope excludes a later quality writer; a
    /// current overlay or application quality callback needs its actual
    /// CommonContext values/CameraA88 and SelectCurrentQuality instead.</summary>
    public DeferredEarlyShadowCurrentPreparationArguments PrepareSourceCreatedRetainedMain(
        MapFreshCommonResourceSnapshot actualFreshCommon,
        MapSourceCreatedViewCameraFields actualRetainedCamera,
        MapCameraConstructionPacket actualCamera)
    {
        ArgumentNullException.ThrowIfNull(actualFreshCommon); ArgumentNullException.ThrowIfNull(actualRetainedCamera);
        ArgumentNullException.ThrowIfNull(actualCamera);
        if (actualFreshCommon.Scope != MapFreshCommonResourceScope.SourceCreatedNoCallbackBeforeAmbientPublication ||
            actualFreshCommon.FrameNumber != actualRetainedCamera.FrameNumber ||
            actualRetainedCamera.CameraObjectId != DefaultView0Source.MainCameraObjectId)
            throw new InvalidDataException("Retained source shadow arguments require the actual fresh context and same-frame Main tail scope.");
        actualRetainedCamera.Validate(actualCamera);
        return new(actualFreshCommon, actualRetainedCamera, SourceRetainedMainCameraA88,
            SelectCurrentQuality(SourceRetainedMainCameraA88), SourceEvidence + ";source-context-constructor;fresh-retained-Main-A88;not-full-SceneView/not-ShadowCPU");
    }
}

public static class DeferredEarlyShadowPreparationArgumentsSource
{
    /// <summary>Import once from the already admitted canonical View0 source.
    /// Parsing and descriptor checks are confined to this resource-load stage.</summary>
    public static LoadedDeferredEarlyShadowPreparationArgumentsSource Read(
        LoadedDefaultView0Source actualLoadedView0)
    {
        ArgumentNullException.ThrowIfNull(actualLoadedView0);
        var view = actualLoadedView0.CopyViewDefinitions().Document.Objects.Single(o => o.ObjectId == actualLoadedView0.ViewObjectId);
        var camera = actualLoadedView0.CopyCameraDefinitions().Document.Objects.Single(o => o.ObjectId == actualLoadedView0.MainCameraObjectId);
        var first = Field(view, 11, 0x69749B4A, 4, 1);
        var second = Field(view, 12, 0xF7ABF8FD, 4, 1);
        var directions = Field(view, 23, 0x87EC2FBE, 8, 2);
        var quality = Field(camera, 975, 0x3E757721, 4, 1);
        var fields = new DeferredEarlyShadowContextConstructorFields(
            BinaryPrimitives.ReadUInt32LittleEndian(first.RawValue), BinaryPrimitives.ReadUInt32LittleEndian(second.RawValue),
            BinaryPrimitives.ReadSingleLittleEndian(directions.RawValue), BinaryPrimitives.ReadSingleLittleEndian(directions.RawValue.AsSpan(4)));
        return new(fields, BinaryPrimitives.ReadUInt32LittleEndian(quality.RawValue), actualLoadedView0.SourceIdentity +
            ";TC:D20D50-p11/12/23->D01580-353C/3540/3550/3554;Main-p975->raw7FC->preparedA88;" +
            "D4BBC3/D4BC98-quality->D11190:R9/entrySP28/30/38;RBP=entrySP-1928");
    }

    private static DokProperty Field(DokObject owner, int ordinal, uint hash, uint type, int count)
    {
        var value = owner.Properties[ordinal];
        if (value.Index != ordinal || value.NameHash != hash || value.TypeCode != type ||
            value.ElementCount != count || value.ElementSize != 4 || value.RawValue.Length != count * 4)
            throw new InvalidDataException("Shadow preparation declaration differs from its exact source-loader binding.");
        return value;
    }
}
