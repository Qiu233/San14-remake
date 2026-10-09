using System.Buffers.Binary;
using System.Text;

namespace San14.Data;

public readonly record struct OriginalD3D11MainCameraInitialWord(int Offset, uint Bits);

/// <summary>Only the numeric fields consumed by the native renderer at startup.
/// This is not a full prepared Camera, a named Data registry or a normal V460
/// result. Unread gaps and source pointer metadata are never serialized.</summary>
public sealed class OriginalD3D11MainCameraInitialNumericInputs
{
    private readonly byte[] prefix;
    private readonly OriginalD3D11MainCameraInitialWord[] words;
    private OriginalD3D11MainCameraInitialNumericInputs(OriginalMainMapCameraFrame initialMain,byte[] prefix,
        OriginalD3D11MainCameraInitialWord[] words, string sourceEvidence)
    {
        InitialMain=initialMain; this.prefix=prefix; this.words=words; SourceEvidence=sourceEvidence;
        NumericWords=Array.AsReadOnly(words);
    }

    public const int PrefixOffset=8;
    public const int PrefixByteLength=OriginalMapCameraConstruction.PrefixByteLength-PrefixOffset;
    public const string NativeDirectory="main-camera-initial-input";
    public const string NativeFilename="profile.bin";
    public string SourceEvidence { get; }
    public OriginalMainMapCameraFrame InitialMain { get; }
    public IReadOnlyList<OriginalD3D11MainCameraInitialWord> NumericWords { get; }

    // GroundDetails wind, held territory B1C, exposure1510, and the two
    // startup shadow families. Each tail word has an actual V418 transport.
    private static readonly int[] Offsets=
    [
        0x9EC,0x9F0,0x9F4,0x9F8,0x9FC,0xA00,0xA04,0xA08,0xA10,0xA18,0xA20,
        0xA28,0xA2C,0xA38,0xA3C,0xA40,0xA84,0xA88,
        0xB1C,0xDC0,0xDC4,0xDC8,0xDCC,0xDD0,0xDD4,0xDD8,0xDDC,0xDE0,0xDE4,0xDE8,0xDEC,
        0xDF4,0xDF8,0x141C,0x1438,0x143C,0x1510
    ];

    /// <summary>Prepare once from this actual owned Main frame and its loaded
    /// RDB declaration. The raw constructor and direct property loader feed
    /// the existing numeric transports; no Lens/STA/reference owner is invented
    /// for fields whose writers do not read those owners. Actual current scalar
    /// stores, when present on the frame, keep precedence over its declaration.</summary>
    public static OriginalD3D11MainCameraInitialNumericInputs Create(
        OriginalGameRepository repository, OriginalMainMapCameraFrame actualMain)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(actualMain);
        var camera=actualMain.Camera;
        if(camera.Source.Camera.ObjectId!=OriginalMainMapCameraAdapter.MainCameraObjectId ||
            camera.Prefix.Length!=OriginalMapCameraConstruction.PrefixByteLength)
            throw new NotSupportedException("Native initial fields require the actual owned Main38D frame.");
        var construction=OriginalMainMapRawCameraNumericConstructionSource.Load(repository,camera.ExecutableLocale);
        var declaration=construction.ReadStockPerspectiveDeclaration(camera.Source.Resource,camera.Source.Camera.ObjectId);
        var preparation=OriginalMainMapRawCameraPreparationSource.Load(repository,camera.ExecutableLocale);
        byte[] raw=new byte[LoadedOriginalMainMapRawCameraNumericConstructionSource.NativeAllocationByteLength];
        construction.ApplyFreshConstructor(raw);
        declaration.ApplyDirectCpuProperties(raw);
        preparation.ApplyCurrentNumericFields(raw);
        var words=new OriginalD3D11MainCameraInitialWord[Offsets.Length];
        for(int i=0;i<Offsets.Length;i++)
        {
            int offset=Offsets[i], rawOffset=LoadedOriginalMainMapRawCameraPreparationSource.EmbeddedCameraOffset+offset;
            uint bits=BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(rawOffset,4));
            if(camera.CurrentSeasonPostEffectScalars is{}current && current.TryReadWord(offset,out uint currentBits))
                bits=currentBits;
            if(offset==0x1510)bits=camera.SourceSpecialRectangleFlag1510;
            words[i]=new(offset,bits);
        }
        return new(actualMain,camera.Prefix.Slice(PrefixOffset,PrefixByteLength).ToArray(),words,
            RenderRuleIdentity.For("main-camera-initial-consumed-numerics",camera.ExecutableLocale)+";"+
            declaration.SourceEvidence+";owned-frame="+camera.FrameNumber+
            ";actual-prefix8..3C0;direct-V418-numeric-tail;not-full-Camera/not-V460/not-registry");
    }

    public uint ReadWord(int offset)
    {
        foreach(var word in words)if(word.Offset==offset)return word.Bits;
        throw new ArgumentOutOfRangeException(nameof(offset),"This startup packet does not own that field.");
    }

    public byte[] Encode()
    {
        using var output=new MemoryStream();
        using(var writer=new BinaryWriter(output,Encoding.ASCII,leaveOpen:true))
        {
            writer.Write(Encoding.ASCII.GetBytes("MAINNUM1"));
            writer.Write(PrefixOffset); writer.Write(PrefixByteLength); writer.Write(words.Length);
            writer.Write(prefix);
            foreach(var word in words){writer.Write(word.Offset);writer.Write(word.Bits);}
        }
        return output.ToArray();
    }

    public void WriteNativeInputs(string inputRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputRoot);
        string directory=Path.Combine(inputRoot,NativeDirectory);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory,NativeFilename),Encode());
    }
}
