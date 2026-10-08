using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The accepted prepared Camera and both native gamma predicates.
/// CurrentTargetIsSrgb is the resolved target record +60==1, not a guessed
/// shader name. SourceContextLinearHdrOutput is its exact virtual80 result.</summary>
public sealed record OriginalMapFinalOutputCameraInputs(long FrameNumber,
    ReadOnlyMemory<byte> PreparedCamera,bool CurrentTargetIsSrgb,
    bool SourceContextLinearHdrOutput,int ResolvedFxaaMode,string NativeContextSource);
public sealed record OriginalMapFinalOutputConstantPacket(byte[] Bytes,
    IReadOnlyList<string> WrittenFields,IReadOnlyList<string> RetainedFields,
    long FrameNumber,string NativeContextSource);

/// <summary>Recovered E6F580 named writers into the exact original PS28 CB.
/// The caller supplies a complete retained native template. This preserves
/// all other fields and padding; it does not fill unknown effect state with
/// source declarations, zero buffers, or guessed camera defaults.</summary>
public static class OriginalMapFinalOutputConstants
{
    public const string PixelSha256="362F68F149219C2E26A388C385365455A21A4ED6E43B0950389A2C1B52D1F573";
    /// <summary>Use the actual source-created accepted Camera packet. Its11
    /// extra scalar copies are native loader/copier joins outside the3C0 prefix.
    /// The local sparse staging array only feeds the recovered17 named writers;
    /// it is not presented as a full native Camera or a complete final CB.</summary>
    public static OriginalMapFinalOutputConstantPacket ApplySourceCreatedCamera(
        OriginalMapFinalOutputProgramSource program,ReadOnlySpan<byte> completeRetainedTemplate,
        OriginalMapCameraConstructionPacket camera,bool currentTargetIsSrgb,
        bool sourceContextLinearHdrOutput,int resolvedFxaaMode)
    {
        ArgumentNullException.ThrowIfNull(camera);
        byte[] fields=new byte[0x13C0];camera.Prefix.Span.CopyTo(fields);var extra=camera.ExtraScalars;
        void F(int at,float value)=>BinaryPrimitives.WriteSingleLittleEndian(fields.AsSpan(at,4),value);
        void I(int at,int value)=>BinaryPrimitives.WriteInt32LittleEndian(fields.AsSpan(at,4),value);
        F(0x404,extra.FxaaEdgeSensitivity404);F(0x408,extra.FxaaEdgeSensitivityMin408);F(0x40C,extra.FxaaSharpness40C);F(0x410,extra.FxaaPixelRange410);
        F(0x7B0,extra.LimbDarkeningWeight7B0);F(0xE78,extra.DistantBlurZThresholdE78);F(0xE7C,extra.DistantBlurIntensityE7C);
        I(0x13B0,extra.Lut1Type13B0);I(0x13B4,extra.Lut2Type13B4);F(0x13B8,extra.LutInterpolation13B8);F(0x13BC,extra.LutIntensity13BC);
        return ApplyPreparedCamera(program,completeRetainedTemplate,new(camera.FrameNumber,fields,currentTargetIsSrgb,
            sourceContextLinearHdrOutput,resolvedFxaaMode,camera.FrameProvenance));
    }
    public static OriginalMapFinalOutputConstantPacket ApplyPreparedCamera(
        OriginalMapFinalOutputProgramSource program,ReadOnlySpan<byte> completeRetainedTemplate,
        OriginalMapFinalOutputCameraInputs input)
    {
        ArgumentNullException.ThrowIfNull(program);ArgumentNullException.ThrowIfNull(input);
        if(program.PixelSha256!=PixelSha256||Convert.ToHexString(SHA256.HashData(program.PixelBytes.Span))!=PixelSha256||
            completeRetainedTemplate.Length!=240||input.PreparedCamera.Length<0x13C0||input.FrameNumber<=0||string.IsNullOrWhiteSpace(input.NativeContextSource))
            throw new InvalidDataException("Final camera writers require exact source PS28, a complete240B CB, accepted Camera and native predicates.");
        var definition=DxbcResourceDefinitionReader.Read(program.PixelBytes.Span);
        var buffers=definition.Buffers.Where(b=>b.ByteSize==240&&b.Name.Text=="$Globals").ToArray();
        if(buffers.Length!=1||definition.Bindings.Count(b=>b.InputType==0&&b.BindPoint==0)!=1)
            throw new InvalidDataException("Final camera writers require the exact source PS28 cb0 layout.");
        var variables=buffers[0].Variables.ToDictionary(v=>v.Name.Text,StringComparer.Ordinal);
        var snapshot=input.PreparedCamera.ToArray();var bytes=completeRetainedTemplate.ToArray();var writes=new List<string>();
        void Write(string name,ReadOnlySpan<byte> value)
        {
            if(!variables.TryGetValue(name,out var field)||field.ByteSize!=value.Length)
                throw new InvalidDataException("Final native writer does not match its exact source field "+name+".");
            value.CopyTo(bytes.AsSpan(field.StartOffset,field.ByteSize));writes.Add(name);
        }
        void Copy(string name,int cameraOffset,int size)=>Write(name,snapshot.AsSpan(cameraOffset,size));
        void Float(string name,float value){Span<byte> data=stackalloc byte[4];BinaryPrimitives.WriteSingleLittleEndian(data,value);Write(name,data);}
        Copy("fBloomWeight",0x180,4);Copy("fStarWeight",0x184,4);Copy("fLensFlareWeight",0x188,4);
        Copy("vColorScale",0x18C,12);Copy("vSaturationScale",0x198,12);
        Copy("fDistantBlurZThreshold",0xE78,4);Copy("fDistantBlurIntensity",0xE7C,4);
        Copy("fLimbDarkeningWeight",0x7B0,4);
        Copy("fColorCorrectionInterpolationRate",0x13B8,4);Copy("fColorCorrectionIntensity",0x13BC,4);
        Float("fColorCorrectionLut1",BinaryPrimitives.ReadInt32LittleEndian(snapshot.AsSpan(0x13B0,4)));
        Float("fColorCorrectionLut2",BinaryPrimitives.ReadInt32LittleEndian(snapshot.AsSpan(0x13B4,4)));
        Copy("fFXAAEdgeThreshold",0x404,4);Copy("fFXAAEdgeSharpness",0x40C,4);Copy("fFXAAPixelRange",0x410,4);
        // E6FD83 tests the resolved mode argument, not Camera+414 directly.
        // ViewAux and Camera contribute to that argument before this writer.
        if(input.ResolvedFxaaMode==1)Copy("fFXAAEdgeThresholdMin",0x408,4);
        else Float("fFXAAEdgeThresholdMin",float.MaxValue);
        float gamma=BinaryPrimitives.ReadSingleLittleEndian(snapshot.AsSpan(0x174,4));
        if(!float.IsFinite(gamma)||gamma<=0)throw new InvalidDataException("Final native Camera gamma must be positive and finite.");
        if(input.CurrentTargetIsSrgb||input.SourceContextLinearHdrOutput)gamma=Divide(gamma,BitConverter.UInt32BitsToSingle(0x400CCCCD));
        Float("fGamma",Divide(1,gamma));
        return new(bytes,writes.AsReadOnly(),Array.AsReadOnly(variables.Keys.Where(k=>!writes.Contains(k)).ToArray()),input.FrameNumber,input.NativeContextSource);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Divide(float a,float b)=>a/b;
}
