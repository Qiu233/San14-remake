using System.Buffers.Binary;

namespace San14.Data;

public sealed record MapPostEffect3FactoryScalarInitialization(int FactoryIndex,bool NativeReturn,int? NativeCapability,IReadOnlyList<string> ExplicitAbsentLookups,
    string SourceEvidence);

/// <summary>Complete scalar allocation and all ten native factory initializers
/// for this successful fresh source module. Does not claim complete GPU resources,
/// native handler member storage, Apply writers or an original active frame.</summary>
public sealed record MapPostEffect3CompleteFreshScalarInitialization(
    MapPostEffect3ParameterState State,IReadOnlyList<MapPostEffect3FactoryScalarInitialization> Factories,
    string SourceEvidence)
{
    public bool IsCompleteFreshScalarInitialization=>true;
    public bool IsCompleteModuleState=>false;
}

/// <summary>Source →→→→ iterates all
/// ten type7 factories. Initializer order belongs to the module's type7 table,
/// regardless of selected Functor0/1. Only the typed default zero allocator and
/// successful original factory placement profile admit this fresh route.
/// Existing unknown/custom-allocator state is not accepted or rewritten.</summary>
public static class MapPostEffect3ApplyCompleteInitialScalars
{
    public static IReadOnlyList<MapPostEffect3ApplyInitialDefault> DepthOfField2Defaults=>Array.AsReadOnly(Dof);
    public static IReadOnlyList<MapPostEffect3ApplyInitialDefault> Composite2Defaults=>Array.AsReadOnly(Composite);

    public static MapPostEffect3CompleteFreshScalarInitialization CreateFresh(
        MapPostEffect3Container actualModule,long frameNumber,string ownerIdentity,
        LoadedMapPostEffect3ScalarZeroAllocationSource actualDefaultAllocator)
    {
        ArgumentNullException.ThrowIfNull(actualDefaultAllocator);ArgumentNullException.ThrowIfNull(actualModule);
        MapPostEffect3ParameterState.ValidateSource(actualModule);
        var rows=actualModule.Tables.Single(t=>t.ChunkKind==7).Rows;
        string locale=actualDefaultAllocator.Locale;
        var factories=MapPostEffect3PassStateReader.FactoriesForLocale(locale);
        if(rows.Count!=10||factories.Count!=10)
            throw new InvalidDataException("Complete fresh initialization requires the exact ten source factory placements.");
        for(int i=0;i<rows.Count;i++)
            if(BinaryPrimitives.ReadUInt32LittleEndian(rows[i].Span[4..])!=factories[i].NameHash)
                throw new InvalidDataException("Complete fresh initialization source factory order differs.");
        var state=MapPostEffect3ParameterState.CreateFreshWithSourceZeroAllocator(actualModule,frameNumber,ownerIdentity,actualDefaultAllocator);
        var receipts=new List<MapPostEffect3FactoryScalarInitialization>();
        for(int i=0;i<6;i++)receipts.Add(new(i,true,null,Array.Empty<string>(),
            "no scalar defaults;source factory row order"));
        var lens=MapPostEffect3ApplyInitialScalars.InitializeLens(actualModule,state,ownerIdentity+"/factory/6",locale);
        receipts.Add(new(6,lens.InitializerAllLookupsAccepted,null,
            lens.ExplicitlyAbsentNames,lens.SourceEvidence));
        receipts.Add(InitializeSupplement(state,Dof,7,2,locale));
        var exposure=MapPostEffect3ApplyInitialScalars.InitializeExposure(actualModule,state,ownerIdentity+"/factory/8",locale);
        receipts.Add(new(8,exposure.InitializerAllLookupsAccepted,
            exposure.ExposureCapability290,exposure.ExplicitlyAbsentNames,exposure.SourceEvidence));
        receipts.Add(InitializeSupplement(state,Composite,9,5,locale));
        if(state.WrittenCount!=state.Declarations.Count)
            throw new InvalidDataException("Complete fresh original scalar allocation lost known storage.");
        return new(state,receipts.AsReadOnly(),actualDefaultAllocator.SourceEvidence+
            "successful-pinned-type7-placement-profile;-count10;-descriptor38"+
            "-all-source-handler-defaults;source-order-0;9;future-Apply-writes-not-included");
    }

    private static MapPostEffect3FactoryScalarInitialization InitializeSupplement(
        MapPostEffect3ParameterState state,MapPostEffect3ApplyInitialDefault[] defaults,
        int index,int finalCapability,string locale)
    {
        // Every declaration in these actual source tables resolves primary.
        // Reject missing/type-different rows rather than selecting a different
        // optional capability profile or fabricating native cached pointers.
        foreach(var d in defaults)
        {
            int i=state.FindScalarIndex(d.PrimaryHash)??throw new NotSupportedException("Source initializer declaration absent: "+d.Name);
            var target=state.Declarations[i];
            if(target.NativeValueType!=d.NativeType||target.ElementCount!=d.ElementCount||target.ValueByteCount!=d.Value.Length)
                throw new InvalidDataException("Source initializer typed target differs: "+d.Name);
        }
        foreach(var d in defaults)
            if(!state.SetTypedByHash(d.PrimaryHash,d.NativeType,d.ElementCount,d.Value.Span,d.SourceEvidence+";actual-all-present-primary-initializer"))
                throw new InvalidDataException("Validated source initializer target changed.");
        return new(index,true,finalCapability,Array.Empty<string>(),
            $"{locale};source-types/counts/defaults;all{defaults.Length}-primary-lookups-present;factory{index}-capability={finalCapability};paired-SC/TC-source-table");
    }

    private static readonly MapPostEffect3ApplyInitialDefault[] Dof=
    [
        new("EnableAutoExposure",0x8767AFDEu,6u,1,0xB0,"01000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("UserExposureScale",0x5CE025BDu,1u,1,0xB8,"6F12033A","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("AnamorphicRateXY",0xFECE7930u,1u,2,0xC0,"0000803F0000803F","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("ScreenWhAspect",0x675BE908u,1u,1,0xC8,"398EE33F","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("StopBladeCount",0x84A28F6Cu,6u,1,0xD0,"06000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("StopPolyRate",0xA66BBAE1u,1u,1,0xD8,"476E6F3F","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("FocasuedDistance",0x9AC810E3u,1u,1,0xE0,"0000C842","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("DofLensCoeff",0xA6C53144u,1u,1,0xE8,"0000F044","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("CameraNearFarCm",0x7552DDEFu,1u,2,0xF0,"0000204100401C46","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("GatherMaxCoC",0xEA115B95u,1u,1,0xF8,"F5491F3C","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("GatherSizeType",0x6EEE901Du,6u,1,0x100,"02000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("SpriteDofThreshold",0x6E26C888u,1u,1,0x108,"0000003F","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("QualityLevel",0x7A1FA336u,6u,1,0x110,"07000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("EnableDof",0xD3729FADu,6u,1,0x118,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("ChangeFStop",0xACBFA86Bu,6u,1,0x120,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("EnableSpriteDof",0x6E82D592u,6u,1,0x128,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("DiaphragmType",0x414B6021u,6u,1,0x130,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("IsFirstExec",0xCEB30472u,6u,1,0x138,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("Enable4KResFix",0x93B076F4u,6u,1,0x140,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("EnableMultiResFix",0x15285BDAu,6u,1,0x148,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("DOF2HeatSimmerParams",0xEF3EFD34u,1u,3,0x150,"000000000000803F00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("Viewport",0x26F729DCu,4u,1,0x158,"00000000000000000000803F0000803F","TC;actual-stack-default-table;lookup/store114B0F0"),
    ];
    private static readonly MapPostEffect3ApplyInitialDefault[] Composite=
    [
        new("ScreenWhAspect",0x675BE908u,1u,1,0x68,"398EE33F","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("EnableAutoExposure",0x8767AFDEu,6u,1,0x70,"01000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("UserExposureScale",0x5CE025BDu,1u,1,0x78,"6F12033A","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("UserWhitePoint",0xB9C02407u,1u,1,0x80,"6F12033A","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("LensFlareProjPosFovScale",0xF866D4B0u,1u,4,0x88,"00000000000000000000000000000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("TinyChromaAberraAmount",0x6DFD8C5Fu,1u,1,0x90,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("TinyChromaAberraType",0x5AF0C641u,6u,1,0x98,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("LensFlareIntensity",0xD469F14Fu,1u,1,0xA0,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("LensFlareThreshold",0x1F845B47u,1u,1,0xA8,"5BB01C3F","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("EnableBloom",0xB344DE25u,6u,1,0xB0,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("BloomSampleScale",0xD40FA65Fu,1u,1,0xB8,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("BloomIntensity",0x1CB8F2D2u,1u,1,0xC0,"0000803F","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("LimbDarkenningInfo",0xEC3A2E7Fu,1u,3,0xC8,"4AA1C7428D27813F2B519E3D","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("LimbDarkenningLinFade",0xF4B89C44u,1u,1,0xD0,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("LdrLutIntensity",0xF5041EAAu,1u,1,0xD8,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("GammaValue",0xBE0D0DB1u,1u,1,0xE0,"0000803F","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("EnableFXAA",0xD08309DAu,6u,1,0xE8,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("FXAAEdgeThreshold",0x3072172Au,1u,1,0xF0,"CDCC4C3D","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("FXAAEdgeThresholdMin",0x77CBE89Cu,1u,1,0xF8,"CDCC4C3D","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("FXAAEdgeSharpness",0x350DC714u,1u,1,0x100,"00000041","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("FXAAPixelRange",0xE9D8242Fu,1u,1,0x108,"00000040","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("QualityLevel",0x7A1FA336u,6u,1,0x110,"07000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("EnableRadialBlur",0x3FE3E9EEu,6u,1,0x118,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("RadialBlurCenter",0x110C5C0Du,1u,2,0x120,"0000000000000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("RadialBlurAmount",0x40EF0210u,1u,1,0x128,"CDCCCC3D","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("RadialBlurDistanceExp",0xCCA6A0D0u,1u,1,0x130,"00004040","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("RadialBlurMaxSampleNum",0xD71E2F30u,6u,1,0x138,"10000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("RadialBlurWeightDecay",0x41360AD6u,1u,1,0x140,"0000803F","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("HdrTvScale",0x1C0EC13Au,1u,1,0x148,"0000803F","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("FXAAType",0x855F59D2u,6u,1,0x150,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("FXAAQualityEdgeThreshold",0x54A05D87u,1u,1,0x158,"E7FB293E","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("FXAAQualityEdgeThresholdMin",0xDF0F67D5u,1u,1,0x160,"3199AA3D","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("FXAAQualitySubpixSharpness",0xBEB97EFBu,1u,1,0x168,"0000403F","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("FXAAQualityLevel",0x4DCC7F5Du,6u,1,0x170,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("FXAAQualityPerformance",0x07D112C9u,6u,1,0x178,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("Viewport",0x26F729DCu,4u,1,0x180,"00000000000000000000803F0000803F","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("CompositeLastViewport",0x2FE660C0u,4u,1,0x188,"00000000000000000000803F0000803F","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("CrossFilterWeight",0xE5652573u,1u,1,0x190,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("CrossFilterLength",0xF82CBFC5u,1u,1,0x198,"3333333F","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("CrossFilterAttenuation",0xA6E4F5D5u,1u,1,0x1A0,"3333333F","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("CrossFilterInclination",0x0FE6EE0Du,1u,1,0x1A8,"0000A041","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("CrossFilterAberration",0x108CD87Au,1u,1,0x1B0,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("EnableSeparateHdrLut",0xCA828200u,6u,1,0x1B8,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("HdrTvNit",0xB6B4A409u,6u,1,0x1C0,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("BaseTvNit",0x1F2FDA7Cu,6u,1,0x1C8,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("TonemapType",0x87D299B0u,6u,1,0x1D0,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("ColorGamut",0x0F58CE72u,6u,1,0x1D8,"00000000","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("NeutralTonemapParams0",0xB6001D47u,4u,1,0x1E0,"CDCC4C3EA470BD3E8FC2753ECDCC4C3E","TC;actual-stack-default-table;lookup/store114B0F0"),
        new("NeutralTonemapParams1",0x3C85D7E6u,4u,1,0x1E8,"0AD7A33C9A99993E9A99A94000002041","TC;actual-stack-default-table;lookup/store114B0F0"),
    ];
}
