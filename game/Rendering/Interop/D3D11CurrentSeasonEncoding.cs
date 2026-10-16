using System.Buffers.Binary;
using San14.Data;

public readonly record struct D3D11PreparedMainWord(int Offset, uint Bits);

/// <summary>Current season transport for NativeApi. Layout constants and raw
/// compatibility light fields belong to this boundary.</summary>
internal static class D3D11CurrentSeasonEncoding
{
    private const int HeaderBytes = 32;
    private const int MainPrefixBytes = 960;
    private const int MainWordBytes = 8;
    private const int LightHeaderBytes = 16;
    private const int LightNumericBytes = 0x328;

    internal static byte[] WriteNative(D3D11CurrentSeasonFrame frame, D3D11CurrentHdrCatalog? hdr = null)
    {
        // Header32, Main3C0, sorted (int32 offset,uint32 bits) writes,
        // then ordered light headers (object,mask,terrain scalar,shadow modes,first-caster flags,reserved) and numeric light data,
        // Atmosphere316, current selected Exposure32, OUM1 texture material,
        // OBM1 bubble author settings, CRP3 Common policy, HFO1 layers, MSS1 stars,
        // SRC1 ordered per-light receiver bias, OPC1 camera and Pattern modifiers
        // optional HLC1 typed lighting inputs and OCC1 contact occlusion inputs,
        // without shader layouts.
        byte[] hdrBytes = hdr is null ? [] : D3D11HdrFrameEncoding.Write(frame, hdr);
        byte[] occlusionBytes = D3D11ContactOcclusionEncoding.Write(frame);
        byte[] result = new byte[HeaderBytes + MainPrefixBytes + frame.PreparedWords.Count * MainWordBytes + frame.Lights.Count * (LightHeaderBytes + LightNumericBytes) + D3D11CurrentAtmosphereFrame.ByteLength + D3D11CurrentExposureEncoding.ByteLength + OceanTextureMaterialEncoding.ByteLength + OceanBubbleMaterialEncoding.ByteLength + CommonRenderPolicyEncoding.ByteLength + HeightFogSettingsEncoding.ByteLength + SkyStarCurrentEncoding.ByteLength + 4 + frame.Lights.Count * ShadowReceiverBiasEncoding.ByteLength + 4 + PatternCurrentEncoding.CameraByteLength + frame.Lights.Count * PatternCurrentEncoding.ModifierByteLength + hdrBytes.Length + occlusionBytes.Length];
        var bytes = result.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)frame.MainMode);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[4..], (uint)frame.SeasonIndex);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[8..], (uint)frame.PreparedWords.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[12..], (uint)frame.Lights.Count);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[16..], frame.Mode.AcceptedMainFrame.Camera.FrameNumber);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[24..], frame.WorldSeason.ProcessedNativeOrdinal);
        frame.PreparedMain.Span.CopyTo(bytes[HeaderBytes..]);
        int at = HeaderBytes + MainPrefixBytes;
        foreach (var word in frame.PreparedWords)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes[at..], word.Offset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[(at + 4)..], word.Bits); at += MainWordBytes;
        }
        foreach (var light in frame.Lights)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..], light.ObjectId);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[(at + 4)..], light.GroupMask);
            BinaryPrimitives.WriteSingleLittleEndian(bytes[(at + 8)..], light.ReceiverBias.Scale);
            bytes[at + 12] = light.Lighting.ShadowModes.Receive;
            bytes[at + 13] = light.Lighting.ShadowModes.Local;
            bytes[at + 14] = light.State.HeaderCasterPlane;
            light.WriteNumericPrefix(bytes.Slice(at + LightHeaderBytes, LightNumericBytes));
            at += LightHeaderBytes + LightNumericBytes;
        }
        frame.Atmosphere.NumericBytes.Span.CopyTo(bytes[at..]);
        at += D3D11CurrentAtmosphereFrame.ByteLength;
        D3D11CurrentExposureEncoding.Write(frame.ExposureLight, bytes[at..]);
        at += D3D11CurrentExposureEncoding.ByteLength;
        OceanTextureMaterialEncoding.Write(frame.OceanTextureMaterial, bytes[at..]);
        at += OceanTextureMaterialEncoding.ByteLength;
        OceanBubbleMaterialEncoding.Write(frame.OceanBubbleMaterial, bytes[at..]);
        at += OceanBubbleMaterialEncoding.ByteLength;
        CommonRenderPolicyEncoding.Write(frame.CommonPolicy, bytes[at..]);
        at += CommonRenderPolicyEncoding.ByteLength;
        HeightFogSettingsEncoding.Write(frame.HeightFog, bytes[at..]);
        at += HeightFogSettingsEncoding.ByteLength;
        SkyStarCurrentEncoding.Write(frame.Stars, frame.Atmosphere.Controller.MatrixF0,
            frame.Atmosphere.Controller.Controller.Scalar38, bytes[at..]);
        at += SkyStarCurrentEncoding.ByteLength;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..], ShadowReceiverBiasEncoding.PacketSignature);
        at += 4;
        foreach (var light in frame.Lights)
        {
            ShadowReceiverBiasEncoding.Write(light.ReceiverBias, bytes[at..]);
            at += ShadowReceiverBiasEncoding.ByteLength;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..], PatternCurrentEncoding.PacketSignature);
        at += 4;
        PatternCurrentEncoding.WriteCamera(frame.PatternSettings, bytes[at..]);
        at += PatternCurrentEncoding.CameraByteLength;
        foreach (var light in frame.Lights)
        {
            PatternCurrentEncoding.WriteModifier(light.PatternModifier, bytes[at..]);
            at += PatternCurrentEncoding.ModifierByteLength;
        }
        hdrBytes.CopyTo(bytes[at..]);
        at += hdrBytes.Length;
        occlusionBytes.CopyTo(bytes[at..]);
        return result;
    }

    internal static D3D11PreparedMainWord[] MainWrites(MapSeasonCameraPostEffectScalarWriteIdentity actual,
        CameraViewSettings selectedViewDefaults, OceanWaveSettings oceanWaves, OceanSurfaceSettings oceanSurface, OceanOpticsSettings oceanOptics,
        SkyCloudShaderSettings cloudShaderSettings)
    {
        var words = actual.Scalars.EncodedWrites.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var pair in CameraViewSettingsEncoding.ShadowWords(selectedViewDefaults)) words.TryAdd(pair.Key, pair.Value);
        foreach (var pair in OceanWaveSettingsEncoding.MainWords(oceanWaves)) words[pair.Key] = pair.Value;
        foreach (var pair in OceanSurfaceSettingsEncoding.MainWords(oceanSurface)) words[pair.Key] = pair.Value;
        foreach (var pair in OceanOpticsSettingsEncoding.MainWords(oceanOptics)) words[pair.Key] = pair.Value;
        words[0x678] = cloudShaderSettings.RayExponent;
        words[0xE40] = cloudShaderSettings.SkyDomeLightGroupId;
        if (actual.PreparedSpecialRectangleFlag1510 is uint flag) words[0x1510] = flag;
        return words.OrderBy(pair => pair.Key).Select(pair => new D3D11PreparedMainWord(pair.Key, pair.Value)).ToArray();
    }
}
