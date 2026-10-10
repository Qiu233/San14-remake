using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The accepted prepared Camera and both native gamma predicates.
/// CurrentTargetIsSrgb is the resolved target record +60==1, not a guessed
/// shader name. SourceContextLinearHdrOutput is its exact virtual80 result.</summary>
public sealed record MapFinalOutputCameraInputs(long FrameNumber,
    ReadOnlyMemory<byte> PreparedCamera,bool CurrentTargetIsSrgb,
    bool SourceContextLinearHdrOutput,int ResolvedFxaaMode,string NativeContextSource);
public sealed record MapFinalOutputConstantPacket(byte[] Bytes,
    IReadOnlyList<string> WrittenFields,IReadOnlyList<string> RetainedFields,
    long FrameNumber,string NativeContextSource);

/// <summary>Recovered E6F580 named writers into the exact original PS28 CB.
/// The caller supplies a complete retained native template. This preserves
/// all other fields and padding; it does not fill unknown effect state with
/// source declarations, zero buffers, or guessed camera defaults.</summary>
public static class MapFinalOutputConstants
{
    public const string PixelSha256="362F68F149219C2E26A388C385365455A21A4ED6E43B0950389A2C1B52D1F573";
    /// <summary>Write final shader constants from this camera phase's optical settings.</summary>
    public static MapFinalOutputConstantPacket ApplySourceCreatedCamera(
        MapFinalOutputProgramSource program,ReadOnlySpan<byte> completeRetainedTemplate,
        MapCameraConstructionPacket camera,bool currentTargetIsSrgb,
        bool sourceContextLinearHdrOutput,int resolvedFxaaMode)
    {
        ArgumentNullException.ThrowIfNull(camera);
        return ApplyOptics(program, completeRetainedTemplate, camera.Optics, camera.FrameNumber,
            currentTargetIsSrgb, sourceContextLinearHdrOutput, resolvedFxaaMode, camera.FrameProvenance);
    }

    public static MapFinalOutputConstantPacket ApplyPreparedCamera(
        MapFinalOutputProgramSource program,ReadOnlySpan<byte> completeRetainedTemplate,
        MapFinalOutputCameraInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.PreparedCamera.Length < 0x13C0) throw new InvalidDataException("Encoded final camera input is incomplete.");
        return ApplyOptics(program, completeRetainedTemplate, CameraOpticsEncoding.DecodeFinalOutput(input.PreparedCamera.Span),
            input.FrameNumber, input.CurrentTargetIsSrgb, input.SourceContextLinearHdrOutput, input.ResolvedFxaaMode, input.NativeContextSource);
    }
    private static MapFinalOutputConstantPacket ApplyOptics(MapFinalOutputProgramSource program,
        ReadOnlySpan<byte> completeRetainedTemplate, CameraOpticsSettings optics, long frameNumber,
        bool currentTargetIsSrgb, bool sourceContextLinearHdrOutput, int resolvedFxaaMode, string source)
    {
        ArgumentNullException.ThrowIfNull(program);
        if(program.PixelSha256!=PixelSha256||Convert.ToHexString(SHA256.HashData(program.PixelBytes.Span))!=PixelSha256||
            completeRetainedTemplate.Length!=240||frameNumber<=0||string.IsNullOrWhiteSpace(source))
            throw new InvalidDataException("Final camera writers require the source PS28, a retained constant buffer and an accepted camera.");
        var definition=DxbcResourceDefinitionReader.Read(program.PixelBytes.Span);
        var buffers=definition.Buffers.Where(b=>b.ByteSize==240&&b.Name.Text=="$Globals").ToArray();
        if(buffers.Length!=1||definition.Bindings.Count(b=>b.InputType==0&&b.BindPoint==0)!=1)
            throw new InvalidDataException("Final camera writers require the exact source PS28 cb0 layout.");
        var variables=buffers[0].Variables.ToDictionary(v=>v.Name.Text,StringComparer.Ordinal);
        var bytes=completeRetainedTemplate.ToArray();var writes=new List<string>();
        void Write(string name,ReadOnlySpan<byte> value)
        {
            if(!variables.TryGetValue(name,out var field)||field.ByteSize!=value.Length)
                throw new InvalidDataException("Final native writer does not match its exact source field "+name+".");
            value.CopyTo(bytes.AsSpan(field.StartOffset,field.ByteSize));writes.Add(name);
        }
        void Vector(string name, float x, float y, float z)
        {
            Span<byte> data=stackalloc byte[12];
            BinaryPrimitives.WriteSingleLittleEndian(data,x);BinaryPrimitives.WriteSingleLittleEndian(data[4..],y);BinaryPrimitives.WriteSingleLittleEndian(data[8..],z);
            Write(name,data);
        }
        void Float(string name,float value){Span<byte> data=stackalloc byte[4];BinaryPrimitives.WriteSingleLittleEndian(data,value);Write(name,data);}
        Float("fBloomWeight",optics.Output.BloomIntensity);Float("fStarWeight",optics.Output.StarIntensity);Float("fLensFlareWeight",optics.Output.LensFlareIntensity);
        Vector("vColorScale",optics.Output.ColorScaleX,optics.Output.ColorScaleY,optics.Output.ColorScaleZ);
        Vector("vSaturationScale",optics.Output.SaturationScaleX,optics.Output.SaturationScaleY,optics.Output.SaturationScaleZ);
        Float("fDistantBlurZThreshold",optics.DepthOfField.DistantBlurZThreshold);Float("fDistantBlurIntensity",optics.DepthOfField.DistantBlurIntensity);
        Float("fLimbDarkeningWeight",optics.Limb.Weight);
        Float("fColorCorrectionInterpolationRate",optics.ColorGrading.InterpolationRate);Float("fColorCorrectionIntensity",optics.ColorGrading.Intensity);
        Float("fColorCorrectionLut1",unchecked((int)optics.ColorGrading.Lut1));Float("fColorCorrectionLut2",unchecked((int)optics.ColorGrading.Lut2));
        Float("fFXAAEdgeThreshold",optics.Fxaa.EdgeThreshold);Float("fFXAAEdgeSharpness",optics.Fxaa.EdgeSharpness);Float("fFXAAPixelRange",optics.Fxaa.PixelRange);
        // E6FD83 tests the resolved mode argument, not Camera+414 directly.
        // ViewAux and Camera contribute to that argument before this writer.
        if(resolvedFxaaMode==1)Float("fFXAAEdgeThresholdMin",optics.Fxaa.EdgeThresholdMin);
        else Float("fFXAAEdgeThresholdMin",float.MaxValue);
        float gamma=optics.Exposure.Gamma;
        if(!float.IsFinite(gamma)||gamma<=0)throw new InvalidDataException("Final native Camera gamma must be positive and finite.");
        if(currentTargetIsSrgb||sourceContextLinearHdrOutput)gamma=Divide(gamma,BitConverter.UInt32BitsToSingle(0x400CCCCD));
        Float("fGamma",Divide(1,gamma));
        return new(bytes,writes.AsReadOnly(),Array.AsReadOnly(variables.Keys.Where(k=>!writes.Contains(k)).ToArray()),frameNumber,source);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Divide(float a,float b)=>a/b;
}
