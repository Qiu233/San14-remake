using System.Buffers.Binary;
using Godot;
using San14.Data.Formats;

/// <summary>Finite adaptation of the original 52-byte D3D11 sampler descriptor.
/// Descriptor values are supplied explicitly; no texture role determines the state.</summary>
public static class DeviceSampler
{
    public static Rid Create(RenderingDevice rd, G1TextureSamplerRecipe recipe)
        => Create(rd, recipe.EncodeD3D11Descriptor());

    public static Rid Create(RenderingDevice rd, ReadOnlySpan<byte> originalDescriptor)
    {
        ArgumentNullException.ThrowIfNull(rd);
        if (originalDescriptor.Length != 52) throw new InvalidDataException("Original sampler descriptor must contain52 bytes.");
        uint[] words = new uint[13];
        for (int i=0;i<13;i++) words[i]=BinaryPrimitives.ReadUInt32LittleEndian(originalDescriptor.Slice(i*4,4));
        uint filter=words[0],baseFilter=filter&~0x80u;
        bool comparison=(filter&0x80u)!=0,point=baseFilter==0,anisotropic=baseFilter==0x55;
        float bias=BitConverter.UInt32BitsToSingle(words[4]), minimum=BitConverter.UInt32BitsToSingle(words[11]),
            maximum=BitConverter.UInt32BitsToSingle(words[12]);
        if (baseFilter is not (0 or 0x14 or 0x15 or 0x55) ||
            (comparison ? words[6] is <1 or >8 : words[6]!=4) || words[5] is <1 or >16 ||
            !float.IsFinite(bias) || !float.IsFinite(minimum) || !float.IsFinite(maximum) || minimum>maximum ||
            words.Skip(7).Take(4).Any(w=>w!=0))
            throw new NotSupportedException("Original sampler filter/comparison/border/LOD recipe is outside the verified device adapter.");
        RenderingDevice.SamplerRepeatMode Repeat(uint mode) => mode switch {
            1=>RenderingDevice.SamplerRepeatMode.Repeat, 2=>RenderingDevice.SamplerRepeatMode.MirroredRepeat,
            3=>RenderingDevice.SamplerRepeatMode.ClampToEdge, 4=>RenderingDevice.SamplerRepeatMode.ClampToBorder,
            _=>throw new InvalidDataException("Unknown original sampler address mode.") };
        RenderingDevice.CompareOperator Compare(uint operation)=>operation switch {
            1=>RenderingDevice.CompareOperator.Never,2=>RenderingDevice.CompareOperator.Less,
            3=>RenderingDevice.CompareOperator.Equal,4=>RenderingDevice.CompareOperator.LessOrEqual,
            5=>RenderingDevice.CompareOperator.Greater,6=>RenderingDevice.CompareOperator.NotEqual,
            7=>RenderingDevice.CompareOperator.GreaterOrEqual,8=>RenderingDevice.CompareOperator.Always,
            _=>throw new InvalidDataException("Unknown D3D11 sampler comparison function.") };
        Rid sampler=default;
        try
        {
            using var state=new RDSamplerState {
                MagFilter=point?RenderingDevice.SamplerFilter.Nearest:RenderingDevice.SamplerFilter.Linear,
                MinFilter=point?RenderingDevice.SamplerFilter.Nearest:RenderingDevice.SamplerFilter.Linear,
                MipFilter=baseFilter is 0x15 or 0x55?RenderingDevice.SamplerFilter.Linear:RenderingDevice.SamplerFilter.Nearest,
                RepeatU=Repeat(words[1]),RepeatV=Repeat(words[2]),RepeatW=Repeat(words[3]),
                LodBias=bias,MinLod=minimum,MaxLod=maximum,UseAnisotropy=anisotropic,AnisotropyMax=words[5],
                EnableCompare=comparison,CompareOp=Compare(words[6]),
                BorderColor=RenderingDevice.SamplerBorderColor.FloatTransparentBlack,UnnormalizedUvw=false };
            sampler=rd.SamplerCreate(state);
            if(!sampler.IsValid)throw new InvalidDataException("Original sampler device creation failed.");
            return sampler;
        }
        catch(Exception failure)
        {
            if(sampler.IsValid)
            {
                try{rd.FreeRid(sampler);}
                catch(Exception retirementFailure)
                {
                    var partial=new NativeMapBorrowerRetirementPackage("device-sampler-partial/"+Guid.NewGuid().ToString("N"),
                        new NativeMapOwnedSamplerRetirement(sampler));
                    partial.RetainFailure();
                    throw new NativeMapBorrowerConstructionException(failure,retirementFailure,partial);
                }
            }
            throw;
        }
    }
}
