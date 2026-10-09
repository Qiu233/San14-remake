namespace San14.Data;

/// <summary>Fresh numeric storage for the normal NoSub PS5/768B receiver.
/// Only the Pattern factory matrix is an initial semantic value. The first
/// four-pass current Shadow publication must write every other active field
/// before a draw; later smaller pass sets retain those same-session values.</summary>
public static class OriginalD3D11NormalShadowReceiverInitialInputs
{
    public const int ByteLength=768;
    public const uint ProviderObjectId=0xCDC8F8D5;
    public const int PixelProgramIndex=5;

    public static byte[] Create(OriginalGameRepository repository,
        OriginalD3D11MainCameraInitialNumericInputs initialMain,IReadOnlyDictionary<int,uint> actualPreparedMainWords)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(initialMain); ArgumentNullException.ThrowIfNull(actualPreparedMainWords);
        uint actualMainA3C=actualPreparedMainWords.TryGetValue(0xA3C,out uint currentMode)
            ? currentMode : initialMain.ReadWord(0xA3C);
        // PS5 computes all four matrices before cascade selection. A fresh
        // smaller pass set needs its own original module initialization path.
        if(actualMainA3C!=11)
            throw new NotSupportedException("Fresh normal receiver needs its actual initial four-pass publication before drawing.");
        var declared=OriginalModelShaderResources.ReadDeclaredShader(
            repository.LoadObjects("system",0x718CCB8B),ProviderObjectId);
        var shader=repository.LoadShaderContainer("system",declared.ShaderFileHash);
        var globals=shader.ReadResourceDefinitions(PixelProgramIndex).Buffers.Single(b=>b.Name.Text=="$Globals");
        var layout=new (string Name,int Offset,int Bytes)[]
        {
            ("ambLit",0,16),("mW2Pt",16,64),("fPtn",80,4),("mW2S",96,256),("vSMOf",352,64),
            ("vS2VZ",416,16),("vSMDF",432,16),("scSM",448,16),("nSSLoop",464,4),
            ("vLPlane",480,192),("sdplane",672,16),("mP2W",688,64),("fDOFade",752,4)
        };
        if(globals.ByteSize!=ByteLength || globals.Variables.Count!=layout.Length ||
            layout.Any(field=>!globals.Variables.Any(v=>v.Name.Text==field.Name &&
                v.StartOffset==field.Offset && v.ByteSize==field.Bytes)))
            throw new NotSupportedException("Normal receiver requires its actual NoSub PS5 physical layout.");
        var pattern=LoadedOriginalDeferredEarlyIndirectPatternMapSource.Load(repository,initialMain.InitialMain.Camera.ExecutableLocale)
            .ConstructAfterSourceFactoryPreparation();
        byte[] storage=new byte[ByteLength]; // Empty storage, not values for any missing current producer.
        pattern.WorldToPointMatrix.Span.CopyTo(storage.AsSpan(16,64));
        return storage;
    }

    public static void WriteFreshNativeInputs(OriginalGameRepository repository,
        OriginalD3D11MainCameraInitialNumericInputs initialMain,IReadOnlyDictionary<int,uint> actualPreparedMainWords,string inputRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputRoot);
        byte[] storage=Create(repository,initialMain,actualPreparedMainWords);
        foreach(string receiver in new[]{"rs68","rs119"})
        {
            string directory=Path.Combine(inputRoot,"normal-pattern-receiver-input","initial",receiver);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory,"ps-b0.bin"),storage);
        }
    }
}
