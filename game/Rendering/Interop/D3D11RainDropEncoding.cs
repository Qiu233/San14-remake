using System.Buffers.Binary;
using San14.Data;

/// <summary>Project-owned RDP1 packet; declaration import and same-frame weather
/// are independent of the retained native manager and its accepted clock.</summary>
internal static class D3D11RainDropEncoding
{
    internal const int ByteLength = 120;
    internal static byte[] Write(D3D11CurrentSeasonFrame frame)
    {
        if (frame.RainProvider is not { } provider) return [];
        var bytes = new byte[ByteLength];
        int at = 0;
        void Word(uint value) { BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value); at += 4; }
        void Scalar(float value) => Word(BitConverter.SingleToUInt32Bits(value));
        var s = frame.RainSettings;
        Word(0x31504452); Word(provider.Owner.Object.ObjectId);
        Word(provider.RainShader.Object.ObjectId); Word(provider.SnowShader.Object.ObjectId);
        Word(s.Textures.Drop); Word(s.Textures.Splash); Word(s.Textures.HeightMask); Word(s.Textures.SnowFragment);
        Word(s.Group); Word(provider.CommonFlags); Scalar(provider.MinimumInterval);
        Scalar(s.RippleRateScale); Scalar(s.RippleMaximumPrecipitation);
        Scalar(s.RippleFadeInSeconds); Scalar(s.RippleFadeOutSeconds); Scalar(s.RippleFlowSpeedSlope);
        Scalar(s.DrawRadius); Scalar(s.HeightRange); Scalar(s.Bottom);
        Scalar(s.DeclaredPrecipitation); Scalar(s.DeclaredTemperature);
        Scalar(s.SplashLifeSeconds); Scalar(s.RippleLifeSeconds);
        Word(0); // Reserved packet word, not an original field.
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(at), provider.Owner.Scope); at += 8;
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(at), provider.RainDeclaration.Scope); at += 8;
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(at), provider.SnowDeclaration.Scope);
        return bytes;
    }
}
