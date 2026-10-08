using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalMapFinalOutputSamplerBinding(uint Register,int SpkSamplerIndex,
    uint SpkTextureIndex,uint NativeTextureKind,byte[] OriginalFinalKey,G1TextureSamplerRecipe Recipe);

/// <summary>PS28 original SPK sampler writes. Complete keys are supplied by
/// the actual texture producers; source INPUT kind5 retains them unchanged.
/// Other source textures receive exactly113B5EC..113B638's low-word writes.
/// No prior sampler bits or source texture identity are manufactured.</summary>
public static class OriginalMapFinalOutputSamplers
{
    private static readonly int[] Indices=[3,6,33,32,16,1,7,8,9,22,23,25,31];
    public static IReadOnlyList<OriginalMapFinalOutputSamplerBinding> PrepareFinal(
        LoadedOriginalMapFinalOutputResources source,
        IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> actualTextureProducerKeys,
        OriginalMapFinalOutputDeadColorCorrection? sourceDeadColorCorrection=null)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(actualTextureProducerKeys);
        var bytes=source.PostEffectObject.Properties.Single(p=>p.NameHash==0x7AE38475).RawValue;
        _=OriginalMapFinalOutputResources.ReadSpk(bytes);
        var registers=Enumerable.Range(0,13).Where(i=>sourceDeadColorCorrection is null||i!=2).ToArray();
        if(actualTextureProducerKeys.Count!=registers.Length||registers.Any(i=>!actualTextureProducerKeys.TryGetValue((uint)i,out var key)||key.Length!=8))
            throw new InvalidDataException($"Final SPK sampler preparation requires all{registers.Length} live complete native producer keys by physical PS register.");
        var result=new List<OriginalMapFinalOutputSamplerBinding>();
        for(uint register=0;register<13;register++)
        {
            if(sourceDeadColorCorrection is not null&&register==2)continue;
            int index=Indices[register],at=0x32C8+index*20;uint texture=U(bytes,at);
            if(texture>=32)throw new InvalidDataException("Final SPK sampler texture index exceeds the recovered source table.");
            uint kind=U(bytes,0x2A0C+(int)texture*24+16);
            byte[] key=actualTextureProducerKeys[register].ToArray();
            if(kind!=5)
            {
                uint min=U(bytes,at+8)&3,mag=U(bytes,at+4)&3,u=U(bytes,at+12)&3,v=U(bytes,at+16)&3;
                ushort retained=BinaryPrimitives.ReadUInt16LittleEndian(key);
                ushort address=(ushort)(((uint)retained&0xFFF0u)|u|(v<<2));
                ushort final=(ushort)(((uint)address&0x03FFu)|((min|(mag<<2))<<10));
                BinaryPrimitives.WriteUInt16LittleEndian(key,final);
            }
            var recipe=OriginalDeferredSamplerSources.DecodeKey(source.StockScreen.Native,key);
            result.Add(new(register,index,texture,kind,key,recipe));
        }
        return result.AsReadOnly();
    }
    private static uint U(byte[] data,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at,4));
}
