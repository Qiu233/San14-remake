using System.Buffers.Binary;
using System.Collections.ObjectModel;

namespace San14.Data;

/// <summary>Actual direct V468 package writes to Main CPU1048..1078, followed
/// by C42070 preparation into Camera12FC..1328. These are source-selected
/// current words, separate from Main38D's original declaration defaults.
/// The same selected source also copies Main EB8/EBC HDR/LDR raw lookup keys.
/// Limb51C/520/524 and brightness10B8/10BC follow their direct V468 copies
/// into prepared7A8/7AC/7B0 and136C/1370.
/// Geometry projection, pose and clip planes retain their own producers.</summary>
public sealed partial class OriginalMapSeasonCameraPostEffectScalars
{
    private readonly IReadOnlyDictionary<int,uint> words;
    private readonly string selectionEvidence;
    private OriginalMapSeasonCameraPostEffectScalars(OriginalMapCameraConstructionPacket camera,
        IReadOnlyDictionary<int,uint> values,string evidence)
    {
        FrameNumber=camera.FrameNumber;MainCameraObjectId=camera.Source.Camera.ObjectId;
        ExecutableLocale=camera.ExecutableLocale;
        MainResourceUri=camera.Source.Resource.Source.ResourceUri;
        MainResourceSha256=camera.Source.Resource.Source.DecodedSha256;
        words=values;SourceEvidence=evidence;selectionEvidence=evidence;
    }
    private OriginalMapSeasonCameraPostEffectScalars(OriginalMapSeasonCameraPostEffectScalars prior,
        long frame,string evidence)
    {
        FrameNumber=frame;MainCameraObjectId=prior.MainCameraObjectId;
        ExecutableLocale=prior.ExecutableLocale;
        MainResourceUri=prior.MainResourceUri;MainResourceSha256=prior.MainResourceSha256;
        words=prior.words;SourceEvidence=prior.SourceEvidence+";"+evidence;selectionEvidence=prior.selectionEvidence;
    }
    public long FrameNumber{get;}
    public uint MainCameraObjectId{get;}
    public string ExecutableLocale{get;}
    public string MainResourceUri{get;}
    public string MainResourceSha256{get;}
    public string SourceEvidence{get;}
    public IReadOnlyList<int> PreparedCameraOffsets=>Array.AsReadOnly(words.Keys.Order().ToArray());
    public uint HdrLutKey932=>words[0xEB8];
    public uint LdrLutKey933=>words[0xEBC];
    public bool TryReadWord(int preparedCameraOffset,out uint value)=>words.TryGetValue(preparedCameraOffset,out value);

    public static OriginalMapSeasonCameraPostEffectScalars CaptureDirectSelection(
        OriginalMapSeasonCameraSelection selection,OriginalMapCameraConstructionPacket acceptedMain)
    {
        ArgumentNullException.ThrowIfNull(selection);ArgumentNullException.ThrowIfNull(acceptedMain);
        selection.Validate();
        if(acceptedMain.ExecutableLocale is not ("TC" or "SC")||acceptedMain.FrameNumber<1||
            acceptedMain.Source.Camera.ObjectId!=OriginalMainMapCameraAdapter.MainCameraObjectId||
            selection.SecondPackage is not null)
            throw new NotSupportedException("Current season optics require the recovered direct package application to an accepted Main38D frame.");
        var values=new Dictionary<int,uint>();var camera=selection.FirstPackage.Camera;
        foreach(var b in Bindings)
        {
            var property=camera.Properties[b.Ordinal];
            if(property.Index!=b.Ordinal||property.NameHash!=b.Hash||property.TypeCode!=b.Type||
                property.ElementCount!=1||property.ElementSize!=4||property.RawValue.Length!=4||
                camera.Properties.Count(p=>p.NameHash==b.Hash)!=1)
                throw new InvalidDataException("Selected season Camera optics differ from their exact source loader property.");
            uint value=BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
            if(b.Type==8&&!float.IsFinite(BitConverter.UInt32BitsToSingle(value)))
                throw new NotSupportedException("Selected season Camera optics leave the finite recovered source domain.");
            values.Add(b.Prepared,value);
        }
        return new(acceptedMain,new ReadOnlyDictionary<int,uint>(values),selection.SourceEvidence+
            ";TC:CB364E/CB3693-direct-C358A0(context,target,selected,provider,position);"+
            "C3890B..C38947-selectedCPU1048..1058-raw-copies;C38977..C3899B-selectedCPU106C..1074-raw-copies;"+
            "C367F5..C36819-selectedCPU51C/520/524;C4191B..C4193F-prepared7A8/7AC/7B0;"+
            "C38A4F..C38A67-selectedCPU10B8/10BC;C43B4A..C43B62-prepared136C/1370;"+
            "C3845E/C38464-selectedCPUEB8-to-MainEB8-HDR-key932;C3846A/C38470-selectedCPUEBC-to-MainEBC-LDR-key933;"+
            "same-first-selected-R8/RDI-source-no-LUT-key-interpolation;SC-paired:C39B0E..C39B26;"+
            "SC-paired:C39FBB..C39FF7/C3A027..C3A04B/C450B6..C450E6/C45116..C45146/C45A00..C45A27;"+
            "direct-C358A0-preserves-MainCPU1078-pose-distance-flag-and-12E8/12EC-lens-inputs;"+
            "C44350..C44377-prepared12FC-source1048-or-pose-distance;C43A06..C43A96-prepared1300..132C;"+
            "actual-selected-season-optics-and-LUT-raw-keys/current-Main-source-declarations-retained;no-resource-lookup-or-GPU-completion");
    }
    public void ValidateForCamera(OriginalMapCameraConstructionPacket camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if(camera.FrameNumber!=FrameNumber||camera.ExecutableLocale!=ExecutableLocale||
            camera.Source.Camera.ObjectId!=MainCameraObjectId||
            camera.Source.Resource.Source.ResourceUri!=MainResourceUri||
            camera.Source.Resource.Source.DecodedSha256!=MainResourceSha256)
            throw new InvalidDataException("Season optics belong to another Main Camera frame/source.");
    }
    internal void RequireEquivalentWrites(OriginalMapSeasonCameraPostEffectScalars actual)
    {
        if(FrameNumber!=actual.FrameNumber||ExecutableLocale!=actual.ExecutableLocale||
            MainCameraObjectId!=actual.MainCameraObjectId||MainResourceUri!=actual.MainResourceUri||
            MainResourceSha256!=actual.MainResourceSha256||selectionEvidence!=actual.selectionEvidence||
            words.Count!=actual.words.Count||words.Any(w=>!actual.words.TryGetValue(w.Key,out uint value)||value!=w.Value))
            throw new InvalidDataException("Accepted Camera has another current season optics writer.");
    }
    internal OriginalMapSeasonCameraPostEffectScalars ForOwnedFrame(long frame,string evidence)
        =>new(this,frame,evidence+";copied-already-accepted-season-optics/no-new-V468");
    internal OriginalMapSeasonCameraPostEffectScalars WithMainPostSelection(
        OriginalMapMainCameraPostSelectionPacket post,OriginalMapCameraConstructionPacket camera)
    {
        ValidateForCamera(camera);post.ValidateSameMain(camera);
        var values=words.ToDictionary(pair=>pair.Key,pair=>pair.Value);
        foreach(var pair in post.PreparedWords)values[pair.Key]=pair.Value;
        return new(camera,new ReadOnlyDictionary<int,uint>(values),selectionEvidence+";"+post.SourceEvidence);
    }
    internal OriginalMapSeasonCameraPostEffectScalars WithActualMainPhase(
        OriginalSkySeasonMainCameraInputs inputs,OriginalMapCameraConstructionPacket camera)
    {
        ValidateForCamera(camera);
        var values=words.ToDictionary(pair=>pair.Key,pair=>pair.Value);
        foreach(int at in new[]{0x1C4,0x1C8,0x1D0})
            values[at]=BinaryPrimitives.ReadUInt32LittleEndian(inputs.BeforeFinalPreparationPrefix.Span.Slice(at,4));
        return new(camera,new ReadOnlyDictionary<int,uint>(values),selectionEvidence+";"+inputs.Phase.SourceProvenance+
            ";actual-Main-adaptation1C4/1C8-and-Cloudy1D0/not-controller-clock");
    }
    /// <summary>Attach this proven writer without changing independently
    /// prepared matrices, lens or source declaration arrays.</summary>
    public OriginalMapCameraConstructionPacket ApplyToSameAcceptedCamera(OriginalMapCameraConstructionPacket camera)
    {
        ValidateForCamera(camera);
        return new(camera.Prefix.ToArray(),camera.Source,camera.CurrentPose,camera.Lens,camera.Matrices,
            camera.ImageBase,camera.ExecutableLocale,camera.FrameNumber,camera.FrameProvenance+";"+SourceEvidence,this);
    }
    private readonly record struct Binding(int Prepared,int Ordinal,uint Hash,uint Type);
    private static readonly Binding[] Bindings=
    [
        new(0x7A8,49,0x4226C411,8),new(0x7AC,50,0x468E5E0A,8),new(0x7B0,51,0xD66B33B7,8),
        new(0x12FC,274,0xEE9D93F9,8),new(0x1300,275,0x995A7B0E,8),
        new(0x1304,276,0x04CFDD5D,8),new(0x1308,277,0x6934EE7C,8),
        new(0x130C,278,0xF2B45C7F,8),new(0x1320,929,0x03C0227E,4),
        new(0x1324,955,0x8CC2593A,8),new(0x1328,984,0x182ED000,4),
        new(0x136C,943,0x3F80B302,8),new(0x1370,944,0x67E49830,8),
        new(0xEB8,932,0x2572A4E9,5),new(0xEBC,933,0xDF7EBEED,5)
    ];
}
