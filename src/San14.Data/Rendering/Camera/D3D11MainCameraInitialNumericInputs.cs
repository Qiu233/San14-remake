using System.Text;

namespace San14.Data;

public readonly record struct D3D11MainCameraInitialWord(int Offset, uint Bits);

/// <summary>Only the numeric fields consumed by the native renderer at startup.
/// This is not a full prepared Camera, a named Data registry or a normal V460
/// result. Unread gaps and source pointer metadata are never serialized.</summary>
public sealed class D3D11MainCameraInitialNumericInputs
{
    private readonly byte[] prefix;
    private readonly D3D11MainCameraInitialWord[] words;
    private D3D11MainCameraInitialNumericInputs(MainMapCameraFrame initialMain,byte[] prefix,
        CameraStartupSettings settings, string sourceEvidence)
    {
        InitialMain=initialMain; this.prefix=prefix; Settings=settings;
        words=CameraStartupEncoding.Words(settings); SourceEvidence=sourceEvidence;
        NumericWords=Array.AsReadOnly(words);
    }

    public const int PrefixOffset=8;
    public const int PrefixByteLength=MapCameraConstruction.PrefixByteLength-PrefixOffset;
    public const string NativeDirectory="main-camera-initial-input";
    public const string NativeFilename="profile.bin";
    public string SourceEvidence { get; }
    public MainMapCameraFrame InitialMain { get; }
    public CameraStartupSettings Settings { get; }
    public IReadOnlyList<D3D11MainCameraInitialWord> NumericWords { get; }

    /// <summary>Prepare the consumed startup settings from the imported camera
    /// declaration. Current prepared values take precedence over the declaration;
    /// the current frame owns exposure rectangle flags.</summary>
    public static D3D11MainCameraInitialNumericInputs Create(
        GameResourceRepository repository, MainMapCameraFrame actualMain)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(actualMain);
        var camera=actualMain.Camera;
        if(camera.Source.Camera.ObjectId!=MainMapCameraAdapter.MainCameraObjectId ||
            camera.Prefix.Length!=MapCameraConstruction.PrefixByteLength)
            throw new NotSupportedException("Native initial fields require the actual owned Main38D frame.");
        var settings=CameraStartupEncoding.ApplyPreparedOverrides(CameraStartupImport.Load(camera.Source),
            camera.CurrentSeasonPostEffectScalars,camera.SourceSpecialRectangleFlag1510);
        return new(actualMain,camera.Prefix.Slice(PrefixOffset,PrefixByteLength).ToArray(),settings,
            RenderRuleIdentity.For("main-camera-initial-consumed-numerics",camera.ExecutableLocale)+";"+
            camera.Source.Resource.Source.ResourceUri+";owned-frame="+camera.FrameNumber);
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
