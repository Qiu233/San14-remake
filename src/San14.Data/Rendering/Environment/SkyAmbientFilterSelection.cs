using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The same original requested DWORD controls both the CPU sample
/// producer and the MakeSkyAmbient binding variant. Native passes stay 9/10/7;
/// their absolute source combinations depend on that DWORD.</summary>
public sealed class SkyAmbientFilterSelection
{
    public const string ShaderSha256 = "A23C9FCE094C7FA40D8E173D96A48D33D043A4D31D8D8A146DB965D44A31A102";
    public const string VertexSha256 = "9AC9FD5ECA4E735E43A773185BE5FCD8491748270E9411840F166FD67085D873";
    public const string CopyPixelSha256 = "76409178505ECED7451C9DE3A3824E2BA5EB8D49B9800EA2808DA3D2590F3F26";
    private static readonly string[] FilterPixelHashes =
    [
        "DE3E84E0E349F974CE04001C8978EADC260FB172B636E143D6D519053A5CD450",
        "AF47F317FA04884E2F72D5139EB97DEB43CA29C59AC2FF96C6DE58A1B5CD3BAF",
        "011EC6FD73A875A56099F9DA2075E4EA2D7C9CE435A0521EAD3420136C975EFF",
        "446131C52A529156746FE0293041461BE48795F934FAA379C3EB302E6016A9BD",
        "03B1E0E45C3D17F0CBF9805FEDEC5479B6C8B484AB1A3B18220DDA6EF8ACFEA1",
        "E86B0A27EFB79F7AC390CBDB555C4924085898670810C326DFAEB004AFD89902",
        "57FD678909BC24CDA7AA5F85AA8ED82A952A949DC085ACFAA6C2002E8FE0FC28",
        "3E85F48F1A8E1CDE5F2645B50977E1A4683D85CE0E9D84B4573D5C31A4645B11"
    ];

    private SkyAmbientFilterSelection(LoadedShaderContainer source, uint count,
        int variant, G2ShaderCombination copy, G2ShaderCombination filter, G2ShaderCombination blend)
    { Source=source; SampleCount=count; BindingVariantIndex=variant; CopyCombination=copy;
      FilterCombination=filter; BlendCombination=blend; }
    public LoadedShaderContainer Source { get; }
    public uint SampleCount { get; }
    public int BindingVariantIndex { get; }
    public G2ShaderCombination CopyCombination { get; }
    public G2ShaderCombination FilterCombination { get; }
    public G2ShaderCombination BlendCombination { get; }
    public string FilterPixelSha256 => FilterPixelHashes[BindingVariantIndex];
    public string SourceProvenance => Source.Source.ResourceUri+"@"+ShaderSha256+
        $";requested-count={SampleCount};binding-variant={BindingVariantIndex};"+
        "TC-D1F1A0/98D0A0->AACB40/981710/9806D0->1647C0/96E810;"+
        "same-count-11981C0-producer;explicit-owned-selection/not-original-GPU-observation";

    /// <summary>Retained for component fixtures with an explicit N. Unsupported
    /// source variants fail; no native prior-variant fallback is invented.</summary>
    public static SkyAmbientFilterSelection SelectExplicitSampleCount(
        LoadedShaderContainer source, uint sampleCount)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Source.OverrideSource is not null ||
            source.Source.ResourceUri!="san14://rdb/system/19DBE085" ||
            source.Source.DecodedSha256!=ShaderSha256 ||
            Convert.ToHexString(SHA256.HashData(source.Source.Bytes))!=ShaderSha256)
            throw new InvalidDataException("Sky filter selection requires the exact original MakeSkyAmbient source.");
        if(sampleCount is not (8 or 1024))
            throw new NotSupportedException("This WORK backend closes the existing explicit N8 component and current source N1024 only.");
        var tables=source.ReadG1Bindings();
        if (tables.PassCount!=13 || !tables.RawDimensionCounts.SequenceEqual(new uint[]{1,0,0,0,0,0,8}) ||
            tables.Features.Count!=1 || tables.Variants.RecordWidth!=4 || tables.Variants.Records.Count!=8 ||
            tables.Combinations.Rows.Count!=104)
            throw new InvalidDataException("Sky filter source dimensions or requested DWORD width changed.");
        Span<byte> requested=stackalloc byte[4];BinaryPrimitives.WriteUInt32LittleEndian(requested,sampleCount);
        var match=tables.Variants.Match(requested)??
            throw new NotSupportedException("Sky sample count has no original variant; retained native selection is not supplied.");
        uint[] dimensions=[0,0,0,0,0,0,(uint)match.Index];
        return new(source,sampleCount,match.Index,tables.ResolveCombination(dimensions,9),
            tables.ResolveCombination(dimensions,10),tables.ResolveCombination(dimensions,7));
    }

    /// <summary>Resolve a caller's explicit copy/filter combination through the
    /// actual source variant table. An arbitrary pass or combination is rejected.</summary>
    public static SkyAmbientFilterSelection FromExplicitCombination(
        LoadedShaderContainer source, int combinationIndex, bool filter)
    {
        ArgumentNullException.ThrowIfNull(source);
        var table=source.ReadG1Bindings();
        if(table.Variants.RecordWidth!=4)throw new InvalidDataException("Sky requested sample count must be one DWORD.");
        foreach(var row in table.Variants.Records)
        {
            uint count=BinaryPrimitives.ReadUInt32LittleEndian(row.RawBytes);
            if(count is not (8 or 1024))continue;
            var candidate=SelectExplicitSampleCount(source,count);
            if((filter?candidate.FilterCombination:candidate.CopyCombination).Index==combinationIndex)return candidate;
        }
        throw new NotSupportedException("Explicit Sky cube combination is not an original copy/filter sample variant.");
    }

    public void ValidatePrograms()
    {
        var canonical=SelectExplicitSampleCount(Source,SampleCount);
        if(canonical.BindingVariantIndex!=BindingVariantIndex || canonical.CopyCombination.Index!=CopyCombination.Index ||
            canonical.FilterCombination.Index!=FilterCombination.Index || canonical.BlendCombination.Index!=BlendCombination.Index)
            throw new InvalidDataException("Sky filter selection was replaced or its source changed.");
        foreach(var (combination,pixelHash) in new[]{(CopyCombination,CopyPixelSha256),(FilterCombination,FilterPixelSha256)})
        {
            var vs=combination.Stages.Single(s=>s.StageCode==1&&s.Entry is not null);
            var ps=combination.Stages.Single(s=>s.StageCode==0&&s.Entry is not null);
            if(Convert.ToHexString(SHA256.HashData(Source.Container.GetProgramBytes(Source.Source.Bytes,vs.Entry!.ProgramIndex)))!=VertexSha256 ||
                Convert.ToHexString(SHA256.HashData(Source.Container.GetProgramBytes(Source.Source.Bytes,ps.Entry!.ProgramIndex)))!=pixelHash)
                throw new InvalidDataException("Sky cube source programs differ from their exact original sample variant.");
        }
    }
}

/// <summary>Same-frame prepared Main3A4 provenance. It follows the actual
/// source CPU158 publication and verifies preservation through each accepted
/// season/RGB/weather continuation; a later unidentified writer is rejected.</summary>
public sealed class SkyAmbientCurrentFilterSelection
{
    private readonly MapSourceCreatedSeasonLightingFrame present;
    private SkyAmbientCurrentFilterSelection(MapSourceCreatedSeasonLightingFrame present,
        SkyAmbientFilterSelection selection)
    { this.present=present; Selection=selection; }
    public SkyAmbientFilterSelection Selection { get; }
    public long FrameNumber=>present.FrameNumber;
    public string SourceProvenance=>present.SourceProvenance+";"+Selection.SourceProvenance+
        ";Main-ordinal864/hash0B6C81F1/type4->CPU158->C40DC6/prepared3A4->B99861/EA3DF7-arg7;"+
        "accepted-current-prefix-preserved-through-season/RGB/weather;later-count-writer-unclosed";

    public static SkyAmbientCurrentFilterSelection CaptureCurrent(
        LoadedShaderContainer shader, MapSourceCreatedSeasonLightingFrame present)
    {
        ArgumentNullException.ThrowIfNull(present);
        var camera=present.AcceptedSeasonCamera.Inputs.MainFrame.Camera;
        if(camera.FrameNumber!=present.FrameNumber || camera.Source.Camera.ObjectId!=MainMapCameraAdapter.MainCameraObjectId)
            throw new InvalidDataException("Sky filter selection requires this same-frame actual Main Camera.");
        var property=camera.Source.Camera.Properties[864];
        if(property.Index!=864 || property.NameHash!=0x0B6C81F1 || property.TypeCode!=4 ||
            property.ElementCount!=1 || property.RawValue.Length!=4)
            throw new InvalidDataException("Main sample count differs from its exact source property/CPU binding.");
        uint count=BinaryPrimitives.ReadUInt32LittleEndian(present.PreparedCameraPrefix.Span.Slice(0x3A4,4));
        if(count!=camera.Source.CpuWord(0x158) || count!=BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue) ||
            count!=BinaryPrimitives.ReadUInt32LittleEndian(camera.Prefix.Span.Slice(0x3A4,4)) ||
            count!=BinaryPrimitives.ReadUInt32LittleEndian(present.AcceptedSeasonCamera.Inputs.BeforeFinalPreparationPrefix.Span.Slice(0x3A4,4)) ||
            count!=BinaryPrimitives.ReadUInt32LittleEndian(present.AcceptedSeasonCamera.SunMoonRgb.Prefix.Span.Slice(0x3A4,4)))
            throw new NotSupportedException("Current Sky sample count has a later writer outside the accepted Main publication scope.");
        var selected=SkyAmbientFilterSelection.SelectExplicitSampleCount(shader,count);selected.ValidatePrograms();
        return new(present,selected);
    }

    public void ValidateCurrent(MapSourceCreatedSeasonLightingFrame actualPresent)
    {
        if(!ReferenceEquals(present,actualPresent))throw new InvalidDataException("Sky filter receipt belongs to another accepted Camera frame.");
        var actual=CaptureCurrent(Selection.Source,actualPresent);
        if(actual.Selection.SampleCount!=Selection.SampleCount || actual.Selection.FilterCombination.Index!=Selection.FilterCombination.Index)
            throw new InvalidDataException("Accepted current Sky filter source changed after preparation.");
    }

    /// <summary>A compiled WORK owner cannot reuse N sample buffers/programs
    /// after the Camera selects another N. Its caller must retire and rebuild.</summary>
    public void RequireSameProgramSampleCount(SkyAmbientFilterSelection allocatedSelection)
    {
        ArgumentNullException.ThrowIfNull(allocatedSelection);Selection.ValidatePrograms();allocatedSelection.ValidatePrograms();
        if(Selection.SampleCount!=allocatedSelection.SampleCount ||
            Selection.FilterCombination.Index!=allocatedSelection.FilterCombination.Index)
            throw new NotSupportedException("Changed Camera Sky sample count requires retiring and rebuilding the original WORK programs/sample buffers.");
    }
}
