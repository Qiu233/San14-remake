using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Verified backend reset recipe, not an observation of a running
/// game's sampler slots. Its default has no wrapper eight-byte key.</summary>
public sealed class OriginalWorldNativeSamplerResetSource
{
    private readonly byte[] descriptor;
    internal OriginalWorldNativeSamplerResetSource(LoadedOriginalDeferredSamplerNativeSource native,
        byte[] defaultDescriptor, string evidence)
        => (Native, descriptor, SourceEvidence) = (native, defaultDescriptor, evidence);
    public LoadedOriginalDeferredSamplerNativeSource Native { get; }
    public byte[] DefaultDescriptor => descriptor.ToArray();
    public string SourceEvidence { get; }
    public int StageCount => 6;
    public int RegisterCount => 16;
    public ulong ResetCachedKey => ulong.MaxValue;
}

public static partial class OriginalDeferredSamplerSources
{
    /// <summary>BF93D0 initialization submits opcode1 at BFBC1B. The handler
    /// 957AD0 calls962E90, which installs backend28A0 at all six by sixteen
    /// sampler slots. This recipe must still be joined to an owned fresh
    /// context and an exact later writer prefix; it is not current-slot proof.</summary>
    public static OriginalWorldNativeSamplerResetSource ReadWorldNativeSamplerReset(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(native);
        byte[] exe = File.ReadAllBytes(native.PhysicalPath);
        VerifyHash(exe, native.ExecutableSha256, "World sampler reset executable");
        var image = new NativeImage(exe);
        byte[] text = image.DecodeText(out int start);
        VerifyHash(text, native.DecodedTextSha256, "World sampler reset decoded text");
        (int Rva,int Bytes,string Sha)[] windows; (int Rva,int Bytes,string Sha)[] tables;
        int pointerSlot,resetHandler,resetSlots,initialSubmit,dispatcher,transfer;
        switch(native.Locale)
        {
            case "SC":
                windows=[
                    (0xBFD0DD,19,"77CFA283E538057F5D137E57CC15B7C0D0708C45199BB73E1F7E3CB6EA5C4641"),
                    (0x95BAA0,290,"B4BE10422BE0061EB70134BC4101B2772463298B1F9B08BEA22B9002C48D4C0A"),
                    (0x958BA0,27,"2CC3E2F134CA7FFB381B867C7DD6B64C3343D094AA3E1859331493922BD3FB3F"),
                    (0x963F40,444,"887DEDC3618EE6A56CEA0561B06BBD97FE9CF263908F52EF4ADD9B07176C823B"),
                    (0x966CD3,20,"BB022B20151301FC37E45D10F1291FA4EF663463E5187B0A1DED94EAD091B0D6"),
                ];
                tables=[
                    (0x14BF540,48,"F7906834891C286514DD41C274D24CAB2B36FA96FB79545367BCC03830EDF7E6"),
                    (0x14BF510,48,"B18D1220CC0FDDE511A1ACEC95F44AA2DA1F05C64EC434EC637E36FC26A267E9"),
                    (0x14BF118,8,"15C9874E41164EED26D65856167E96D9A82D0857B2937BBCDE0E1BFD513951A9"),
                ];
                pointerSlot=0x14BF118;resetHandler=0x958BA0;resetSlots=0x963F40;
                initialSubmit=0xBFD0DD;dispatcher=0x95BAA0;transfer=0x966CD3;
                break;
            case "TC":
                windows=[
                    (0xBFBC0D,19,"7A52D90ABD6398131C23D4AFCAF84EABAF01ABDE983263EA4EE61CD0E81182EC"),
                    (0x95A9D0,290,"0A49C816D18DC280C642334D7488624E7B30DE3001DE7791920968BD9E512D15"),
                    (0x957AD0,27,"6AF21C2426E0C30F38FD98627C660417449B4F817BA0B311D7B206A83E91D3A1"),
                    (0x962E90,444,"635FA4315A529EA35643A39195675F0ABC392805F7C0E401650619594FD7F258"),
                    (0x965B53,20,"BB022B20151301FC37E45D10F1291FA4EF663463E5187B0A1DED94EAD091B0D6"),
                ];
                tables=[
                    (0x14BE660,48,"F0A704DB1ADEC355D1019033B0AC70D6D8AE5218BF97D004D64F8588F4C37C9E"),
                    (0x14BE630,48,"789EC6247D11601D1A35BC9690AACB4110B29E6030D9598105252363A4825FA7"),
                    (0x14BE238,8,"46AD4F6365E93C554E0312D4C73DBFF3F1C4E663C0EB226991BC063CFA7E3147"),
                ];
                pointerSlot=0x14BE238;resetHandler=0x957AD0;resetSlots=0x962E90;
                initialSubmit=0xBFBC0D;dispatcher=0x95A9D0;transfer=0x965B53;
                break;
            default:throw new NotSupportedException("World native sampler reset source requires SC/TC.");
        }
        foreach(var w in windows)VerifyHash(text.AsSpan(w.Rva-start,w.Bytes),w.Sha,"World sampler reset source window");
        foreach(var table in tables)VerifyHash(image.Raw(table.Rva,table.Bytes),table.Sha,"World sampler reset actual callback table");
        if(BinaryPrimitives.ReadUInt64LittleEndian(image.Raw(pointerSlot,8))!=0x140000000UL+(uint)resetHandler)
            throw new InvalidDataException("Native opcode1 dispatch no longer selects this locale sampler reset.");
        return new(native,BackendDefaultDescriptor(native),native.ResourceUri+
            $";{native.Locale}{initialSubmit:X}->{dispatcher:X}/opcode1->{resetHandler:X}/{resetSlots:X};6x16/default28A0;"+
            $"{transfer:X}:renderer3168->backend28A0;actual-six-stage-callback-tables;owned-reset-recipe-not-original-current-slots");
    }
}
