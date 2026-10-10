using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Original resource and compatibility encoding boundary.</summary>
public static class HdrShaderCodec
{
    public static HdrFeatureKey ReadFeature(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 48) throw new InvalidDataException("HDR feature requires twelve fields.");
        Span<uint> w = stackalloc uint[12];
        for (int i = 0; i < w.Length; i++) w[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(i * 4, 4));
        return new(w[0],w[1],w[2],w[3],w[4],w[5],w[6],w[7],w[8],w[9],w[10],w[11]);
    }

    internal static HdrVariantKey ReadVariant(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 96) throw new InvalidDataException("HDR variant requires twenty-four fields.");
        Span<uint> w = stackalloc uint[24];
        for (int i = 0; i < w.Length; i++) w[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(i * 4, 4));
        return new(w[0],w[1],w[2],w[3],w[4],w[5],w[6],w[7],w[8],w[9],w[10],w[11],
            w[12],w[13],w[14],(HdrReflectionMode)w[15],w[16],w[17],w[18],w[19],w[20],w[21],w[22],w[23]);
    }

    public static byte[] WriteFeature(HdrFeatureKey key) => Encode([key.Flags,key.DirectionalLights,
        key.PointLights,key.SpotLights,key.HemisphereLights,key.DirectionalMode,key.FogMode,
        key.FogQuality,key.ShadowMode,key.ShadowQuality,key.LocalShadowMode,key.MaterialMode]);
    public static byte[] WriteVariant(HdrVariantKey key) => Encode([key.BaseMode,key.SpecularOcclusion,
        key.SharedTargets,key.FirstLighting,key.SecondLighting,key.SharedAuxiliary,key.Constant6,
        key.Constant7,key.Constant8,key.LightPair,key.LightPairAuxiliary,key.CombinedLighting,
        key.LocalLightQueriesEmpty,key.VolumetricFog,key.RefractionResources,(uint)key.Reflection,
        key.RefractionSource,key.SharedDistance,key.Constant18,key.Constant19,key.Constant20,
        key.Constant21,key.Constant22,key.Constant23]);

    public static HdrSelectionSettings ReadSettings(DeferredF103CurrentVariantInputs current,
        uint cameraSpecularOcclusion, bool localLightQueriesEmpty = false, bool preRendererPresent = false)
    {
        uint flags = current.ContextFlags3F0;
        return new((HdrColorMode)current.NativeColorMode,cameraSpecularOcclusion,
            new((flags&0x4000)==0,(flags&0x1800)==0,(flags&0xC0000)!=0,
                (flags&0x8000)==0,(flags&0x10000)==0,(flags&0x20000)==0,
                (flags&0x100000)!=0,(flags&0x40000000)==0),
            current.ReflectionQueryD57E20 != 0 && current.SelectedCamera1808Present,
            (current.CommonFlags&0x200)!=0 && current.AmbientSelectionPresent &&
                (current.AmbientMapFirstAvailable || current.AmbientMapSecondAvailable || current.AmbientMapThirdAvailable),
            localLightQueriesEmpty,preRendererPresent);
    }

    internal static uint ReadField(HdrVariantKey key, HdrVariantField field) => field switch
    {
        HdrVariantField.SpecularOcclusion=>key.SpecularOcclusion,
        HdrVariantField.SharedTargets=>key.SharedTargets,
        HdrVariantField.FirstLighting=>key.FirstLighting,
        HdrVariantField.SecondLighting=>key.SecondLighting,
        HdrVariantField.SharedAuxiliary=>key.SharedAuxiliary,
        HdrVariantField.LightPair=>key.LightPair,
        HdrVariantField.LightPairAuxiliary=>key.LightPairAuxiliary,
        HdrVariantField.CombinedLighting=>key.CombinedLighting,
        HdrVariantField.LocalLightQueriesEmpty=>key.LocalLightQueriesEmpty,
        HdrVariantField.VolumetricFog=>key.VolumetricFog,
        HdrVariantField.RefractionResources=>key.RefractionResources,
        HdrVariantField.Reflection=>(uint)key.Reflection,
        HdrVariantField.RefractionSource=>key.RefractionSource,
        HdrVariantField.SharedDistance=>key.SharedDistance,
        _=>throw new ArgumentOutOfRangeException(nameof(field))
    };

    internal static G2ShaderCombination CopyCombination(G2ShaderCombination combination)
        => combination with
        {
            RawRow=combination.RawRow.ToArray(),
            Stages=Array.AsReadOnly(combination.Stages.Select(stage=>stage with
            {Entry=stage.Entry is {} entry ? entry with {RawTableRow=entry.RawTableRow.ToArray()} : null}).ToArray())
        };

    internal static DxbcResourceDefinitions FreezeDefinitions(DxbcResourceDefinitions definitions)
        =>definitions with
        {
            Buffers=Array.AsReadOnly(definitions.Buffers.Select(buffer=>buffer with
                {Variables=Array.AsReadOnly(buffer.Variables.ToArray())}).ToArray()),
            Bindings=Array.AsReadOnly(definitions.Bindings.ToArray()),
            Types=Array.AsReadOnly(definitions.Types.Select(type=>type with
                {Members=Array.AsReadOnly(type.Members.ToArray())}).ToArray())
        };
    private static byte[] Encode(ReadOnlySpan<uint> words)
    {
        byte[] bytes = new byte[words.Length * 4];
        for(int i=0;i<words.Length;i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*4,4),words[i]);
        return bytes;
    }
}
