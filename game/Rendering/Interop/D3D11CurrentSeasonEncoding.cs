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
        // then ordered light headers (object,mask,terrain scalar,shadow modes,reserved) and numeric light data,
        // Atmosphere296, current selected Exposure32 and the HDR routes.
        byte[] hdrBytes = hdr is null ? [] : D3D11HdrFrameEncoding.Write(frame, hdr);
        byte[] result = new byte[HeaderBytes + MainPrefixBytes + frame.PreparedWords.Count * MainWordBytes + frame.Lights.Count * (LightHeaderBytes + LightNumericBytes) + D3D11CurrentAtmosphereFrame.ByteLength + D3D11CurrentExposureEncoding.ByteLength + hdrBytes.Length];
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
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[(at + 8)..], BinaryPrimitives.ReadUInt32LittleEndian(light.CompleteData.Span[0xF24..]));
            bytes[at + 12] = light.Lighting.ShadowModes.Receive;
            bytes[at + 13] = light.Lighting.ShadowModes.Local;
            light.CompleteData.Span[..LightNumericBytes].CopyTo(bytes[(at + LightHeaderBytes)..]);
            at += LightHeaderBytes + LightNumericBytes;
        }
        frame.Atmosphere.NumericBytes.Span.CopyTo(bytes[at..]);
        at += D3D11CurrentAtmosphereFrame.ByteLength;
        D3D11CurrentExposureEncoding.Write(frame.ExposureLight, bytes[at..]);
        hdrBytes.CopyTo(bytes[(at + D3D11CurrentExposureEncoding.ByteLength)..]);
        return result;
    }

    internal static D3D11PreparedMainWord[] MainWrites(MapSeasonCameraPostEffectScalarWriteIdentity actual,
        CameraViewSettings selectedViewDefaults)
    {
        var words = actual.Scalars.EncodedWrites.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var pair in CameraViewSettingsEncoding.ShadowWords(selectedViewDefaults)) words.TryAdd(pair.Key, pair.Value);
        if (actual.PreparedSpecialRectangleFlag1510 is uint flag) words[0x1510] = flag;
        return words.OrderBy(pair => pair.Key).Select(pair => new D3D11PreparedMainWord(pair.Key, pair.Value)).ToArray();
    }
}
