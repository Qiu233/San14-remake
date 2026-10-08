using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

public readonly record struct OriginalMainMapRawCameraWordRange(
    int SourceCpuOffset, int DestinationCpuOffset, int WordCount);

/// <summary>The current Data at raw Camera2EC0, resolved by its owning
/// resource registry. A declared key does not create this value. A real Data
/// with no current CPU8 payload takes the native preserve-output branch.</summary>
public sealed record OriginalMainMapRawCameraCurrentLens(
    ulong DataHandle, object DataOwner,
    LoadedOriginalMapPostEffect3LensPresetSource? CurrentBuiltin0Payload,
    OriginalMapPostEffect3LensPresetIntermediateMathSources? MathSources);

/// <summary>V418's actual prepared values. This result does not create a raw
/// Camera, resolve its resource bank, or run the later V460 lighting writer.</summary>
public sealed record OriginalMainMapRawCameraPreparationResult(
    OriginalMapCameraCurrentPose CurrentGetters, OriginalCameraLens Lens,
    OriginalMapCameraConstructionMatrices Matrices, bool LensIntermediateWritten,
    string SourceEvidence);

/// <summary>The complete ordinary Perspective V418 field writer on the SAME
/// caller-owned raw CPU. This source plan is loaded once; each call reads the
/// current scalar fields, getters, Host flags and actual resolved Lens Data.
/// The source loader/retained2Cxx bank remain explicit caller responsibilities.</summary>
public sealed class LoadedOriginalMainMapRawCameraPreparationSource
{
    private readonly OriginalMainMapRawCameraWordRange[] ranges;
    internal LoadedOriginalMainMapRawCameraPreparationSource(string locale,string evidence,
        OriginalMainMapRawCameraWordRange[] ranges)
    {
        Locale=locale;SourceEvidence=evidence;this.ranges=ranges;
        NumericRanges=Array.AsReadOnly(ranges);
    }
    public const int RequiredCurrentCpuByteLength=0x2F48;
    public const int EmbeddedCameraOffset=0x1330;
    public const int EmbeddedCameraByteLength=0x18C0;
    public string Locale{get;}
    public string SourceEvidence{get;}
    public IReadOnlyList<OriginalMainMapRawCameraWordRange> NumericRanges{get;}
    public int OrdinaryNumericWordCount=>1202;

    /// <summary>Real C9AE30/C9B180/C9B230 fallback getters. An active2CA8
    /// transform has its own producer; it must supply the three actual getter
    /// results to ApplyCurrentPerspective instead of invoking this fallback.</summary>
    public OriginalMapCameraCurrentPose ReadCurrentUnboundGetters(ReadOnlySpan<byte> currentCpu)
    {
        RequireStorage(currentCpu.Length);
        if(BinaryPrimitives.ReadUInt64LittleEndian(currentCpu[0x2CA8..])!=0)
            throw new NotSupportedException("A bound raw Camera transform requires its actual80/88/90 getter results.");
        return new(new(F(currentCpu,0x12FC),F(currentCpu,0x1300),F(currentCpu,0x1304)),
            new(F(currentCpu,0x1308),F(currentCpu,0x130C),F(currentCpu,0x1310),F(currentCpu,0x1314)),Vector3.One);
    }

    /// <summary>1186 direct plus16 loop DWORD transports, the independent14FC sentinel
    /// and126C byte normalization. No zero defaults, resource resolution,
    /// geometry construction or later solar/history update is implied.</summary>
    public void ApplyCurrentNumericFields(Span<byte> sameCurrentCpu)
    {
        RequireStorage(sameCurrentCpu.Length);
        foreach(var range in ranges)
            sameCurrentCpu.Slice(range.SourceCpuOffset,range.WordCount*4)
                .CopyTo(sameCurrentCpu.Slice(range.DestinationCpuOffset,range.WordCount*4));
        BinaryPrimitives.WriteUInt32LittleEndian(sameCurrentCpu[0x14FC..],uint.MaxValue);
        // Original C44094 writes one byte, preserving the other three bytes.
        sameCurrentCpu[0x286C]=sameCurrentCpu[0x126C]==0?(byte)0:(byte)1;
    }

    /// <summary>Actual ordinary V418/C409C0/C44480 path. It retains the
    /// embedded header, lighting history and resource bank; Lens-null retains
    /// all previous92B. Serial managed owner supplies the source lock scope.
    /// This is not an initializer and does not clear Host.bit30.</summary>
    public OriginalMainMapRawCameraPreparationResult ApplyCurrentPerspective(
        Span<byte> sameCurrentCpu,OriginalMapCameraCurrentPose actualGetters,
        uint actualHostFlags3E4,uint nativeClipProfile,
        OriginalNativeCameraTangentDispatch tangentDispatch,
        OriginalMainMapRawCameraCurrentLens? actualCurrentLens)
    {
        RequireStorage(sameCurrentCpu.Length);
        if((U(sameCurrentCpu,0x1318)&1)!=0)
            throw new NotSupportedException("Raw V418 source currently implements its ordinary Perspective branch.");
        ulong lensHandle=BinaryPrimitives.ReadUInt64LittleEndian(sameCurrentCpu[0x2EC0..]);
        if(lensHandle!=0&&(actualCurrentLens is null||actualCurrentLens.DataHandle!=lensHandle))
            throw new InvalidDataException("Raw Camera2EC0 requires its actual current resolved Data, not a declaration key or guessed absence.");
        if(actualCurrentLens is{}resolved)
        {
            ArgumentNullException.ThrowIfNull(resolved.DataOwner);
            if(lensHandle!=resolved.DataHandle||lensHandle==0)
                throw new InvalidDataException("Lens Data is not the current retained raw Camera2EC0 handle.");
            if(resolved.CurrentBuiltin0Payload is{}payload&&
                (payload.ExecutableLocale!=Locale||resolved.MathSources is null||resolved.MathSources.Locale!=Locale))
                throw new InvalidDataException("Current Lens payload/math source belongs to another native locale.");
        }
        uint header=U(sameCurrentCpu,8);
        W(sameCurrentCpu,8,header|0x80000000);
        ApplyCurrentNumericFields(sameCurrentCpu);
        float near=OriginalNativeFloat.Multiply(actualGetters.Scale90.Z,F(sameCurrentCpu,0x10));
        float far=OriginalNativeFloat.Multiply(actualGetters.Scale90.Z,F(sameCurrentCpu,0x14));
        WF(sameCurrentCpu,0x1470,near);WF(sameCurrentCpu,0x1474,far);
        bool wroteLens=actualCurrentLens?.CurrentBuiltin0Payload is not null;
        if(wroteLens)
            WriteCurrentLensIntermediate(sameCurrentCpu,actualCurrentLens!,tangentDispatch);
        var pose=OriginalMapCameraConstructionMath.PoseFromQuaternionC45956(
            new(actualGetters.Position80,1),actualGetters.Quaternion88,F(sameCurrentCpu,0xC),actualGetters.Scale90.Z);
        pose.WriteEyeTargetUp(sameCurrentCpu[EmbeddedCameraOffset..]);
        // C44361/C4436F is after Lens/quaternion, independently of the bulk.
        if((sameCurrentCpu[0x1078]&1)==0)W(sameCurrentCpu,0x262C,U(sameCurrentCpu,0x1048));
        else WF(sameCurrentCpu,0x262C,pose.EffectiveLookDistance);
        W(sameCurrentCpu,0x1478,U(sameCurrentCpu,0x12E8));
        float aspect=OriginalNativeFloat.Multiply(
            Divide(actualGetters.Scale90.X,actualGetters.Scale90.Y),F(sameCurrentCpu,0x12EC));
        WF(sameCurrentCpu,0x147C,aspect);
        uint flags=U(sameCurrentCpu,0x1488);
        flags=(actualHostFlags3E4&(1u<<29))!=0?flags|2u:flags&~2u;
        W(sameCurrentCpu,0x1488,flags);
        var lens=OriginalCameraLens.DecodePrefix(sameCurrentCpu.Slice(EmbeddedCameraOffset,OriginalCameraLens.PrefixBytes));
        var matrices=OriginalMapCameraConstructionMath.CompleteAB6C60(
            pose.Eye10,pose.Target20,pose.Up30,lens,nativeClipProfile,tangentDispatch);
        matrices.WriteEarlyMatrices(sameCurrentCpu[EmbeddedCameraOffset..]);
        sameCurrentCpu[0x1490]=sameCurrentCpu[0x1491]=0;
        W(sameCurrentCpu,8,U(sameCurrentCpu,8)&0x7FFFFFFF);
        return new(actualGetters,lens,matrices,wroteLens,SourceEvidence+";same-retained-rawCPU;current1202-words;"+
            "actual80/88/90;actualHost3E4.bit29;Lens2EC0-currentData;retained-lighting/resource-bank;not-V460");
    }

    /// <summary>C58158..C581AA's fresh loader tail, after the actual V418.
    /// This branch requires current STA and Depending Data to be absent and
    /// current AutoDateTimeStep0. It neither resolves the raw resource bank
    /// nor performs normal View V460 or publishes a ready Data.</summary>
    public OriginalPreparedCameraLightingPacket CompleteFreshLoaderWithoutTables(
        Span<byte> sameCurrentCpu, OriginalCameraLightingController declaredLighting)
    {
        RequireStorage(sameCurrentCpu.Length);
        var camera = sameCurrentCpu.Slice(EmbeddedCameraOffset, EmbeddedCameraByteLength);
        if (BinaryPrimitives.ReadUInt64LittleEndian(camera[0x1680..]) != 0 ||
            BinaryPrimitives.ReadUInt64LittleEndian(sameCurrentCpu[0x2CC8..]) != 0)
            throw new NotSupportedException("Fresh Camera tail needs the actual present STA/Depending producers for these resolved Data.");
        if (BinaryPrimitives.ReadInt32LittleEndian(camera[0x3A0..]) != 0)
            throw new NotSupportedException("Fresh Camera tail needs its current nonzero AutoDateTimeStep writer.");
        // Numeric controller values come from this V418 target, while the
        // declaration key remains source metadata, not a current table pointer.
        var controller = declaredLighting with
        {
            TransitionSpan = F(camera, 0x288), TransitionSuppression = F(camera, 0x28C), SunMargin = F(camera, 0x290),
            SunHueSaturation = new(F(camera, 0x29C), F(camera, 0x2A0)),
            MoonHueSaturation = new(F(camera, 0x2A4), F(camera, 0x2A8)),
            TimeSpanPerDay = F(camera, 0x220), AutoDateTimeStepType = 0
        };
        var parameters = OriginalSkylightParameters.ReadSourceFields(camera.Slice(0x200, 28));
        BinaryPrimitives.WriteUInt64LittleEndian(camera[0x18A8..], 0); // Current raw2CC8 is null.
        W(camera, 0x3B0, uint.MaxValue);
        OriginalPreparedCameraWeather.ApplyCurrentFields(camera);
        var lighting = OriginalPreparedCameraLighting.PrepareWithoutTransmittanceTable(controller, parameters,
            camera[..OriginalPreparedCameraLighting.PrefixByteLength], 0);
        lighting.Prefix.Span.CopyTo(camera);
        // Loader markers are after C44ED0, not constructor defaults and not
        // current LUT transitions. All unlisted bytes retain the caller state.
        WF(camera, 0x138C, -1); WF(camera, 0x1390, -1);
        BinaryPrimitives.WriteUInt64LittleEndian(camera[0x15BC..], ulong.MaxValue);
        W(camera, 0x15C4, 0);
        return lighting;
    }

    private static void WriteCurrentLensIntermediate(Span<byte> cpu,
        OriginalMainMapRawCameraCurrentLens actual,OriginalNativeCameraTangentDispatch dispatch)
    {
        var preset=actual.CurrentBuiltin0Payload!;var math=actual.MathSources!;
        if(dispatch!=OriginalNativeCameraTangentDispatch.Sse||!Sse.IsSupported)
            throw new NotSupportedException("Raw Lens current producer uses the recovered native finite SSE math branch.");
        byte[] payload=preset.PrimitiveBytes.ToArray();
        float aspect=F(cpu,0x12EC),diagonal=Sse.SqrtScalar(Vector128.CreateScalar(
            OriginalNativeFloat.Add(OriginalNativeFloat.Multiply(aspect,aspect),1))).GetElement(0);
        // Original caller is32B:1048,104C,sqrt(aspect^2+1),aspect,1050,1054,74,78.
        float[] input=[F(cpu,0x1048),F(cpu,0x104C),diagonal,aspect,F(cpu,0x1050),F(cpu,0x1054),F(cpu,0x74),F(cpu,0x78)];
        float I(int at)=>input[at/4];float P(int at)=>F(payload,at);
        byte[] output=new byte[92];void O(int at,float value)=>WF(output,at,value);
        float A(float a,float b)=>OriginalNativeFloat.Add(a,b);
        float S(float a,float b)=>OriginalNativeFloat.Subtract(a,b);
        float M(float a,float b)=>OriginalNativeFloat.Multiply(a,b);
        float D(float a,float b)=>Divide(a,b);
        float Pow(float x,float y)=>OriginalSkyWeatherNativePower.PowSse(x,y,math.Power);
        float Atan(float x)=>OriginalNativeSkyAtan.Atan1032034(x,math.Atan);
        float magnification=D(I(0),S(I(0),I(4)));O(0,magnification);
        float power=Pow(magnification,P(0x40));O(4,power);
        float focal=M(power,I(4));O(8,focal);float twiceFocal=A(focal,focal);
        O(0x18,M(Atan(D(D(P(4),I(8)),twiceFocal)),2));
        float ratio=D(focal,magnification);O(0xC,ratio);
        float fieldOfView=M(Atan(D(P(4),twiceFocal)),2);O(0x10,fieldOfView);
        O(0x14,M(Atan(D(M(D(P(4),I(8)),I(0xC)),twiceFocal)),2));
        float range=D(S(ratio,P(0xC)),S(P(0x10),P(0xC)));O(0x1C,range);
        float multiplier=S(1,M(S(1,P(0x48)),S(1,range)));O(0x20,multiplier);O(0x24,M(multiplier,fieldOfView));
        float aperture=D(M(I(0),I(0x10)),S(I(0),ratio));O(0x28,aperture);
        float apertureRatio=D(P(0x18),aperture);O(0x2C,apertureRatio);
        float tangent=OriginalNativeCameraTangent.Tangent1020A90(M(fieldOfView,.5f),math.Tangent,dispatch);
        O(0x30,D(P(4),M(tangent,A(aperture,aperture))));
        float correction=A(D(S(focal,ratio),ratio),1),correctedStop=M(correction,P(0x4C));
        O(0x34,correctedStop);O(0x38,M(S(P(0x4C),P(0x50)),apertureRatio));O(0x3C,M(correctedStop,.5f));
        O(0x40,A(M(D(S(I(0x10),P(0x18)),S(P(0x1C),P(0x18))),S(P(0x24),P(0x20))),P(0x20)));
        O(0x44,M(P(0x84),I(0x18)));O(0x48,M(P(0x88),I(0x1C)));
        O(0x54,M(Pow(2,I(0x14)),math.ExposureMultiplier));
        O(0x4C,M(correction,correctedStop));O(0x50,M(M(S(correctedStop,P(0x50)),apertureRatio),.5f));
        //1148F00 owns92B only. Marker2958 and all other retained bytes survive.
        output.CopyTo(cpu[0x28FC..]);
    }
    private static void RequireStorage(int count)
    {if(count<RequiredCurrentCpuByteLength)throw new InvalidDataException("Raw Camera preparation requires existing2F48 CPU storage; a3C0 snapshot is not a raw Camera.");}
    private static uint U(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
    private static float F(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadSingleLittleEndian(b[at..]);
    private static void W(Span<byte> b,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(b[at..],value);
    private static void WF(Span<byte> b,int at,float value)=>BinaryPrimitives.WriteSingleLittleEndian(b[at..],value);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Divide(float a,float b)=>a/b;
}

public static class OriginalMainMapRawCameraPreparationSource
{
    public static LoadedOriginalMainMapRawCameraPreparationSource Load(OriginalGameRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadMainMapRawCameraPreparationSource(repository.SourceDirectory,locale);
    }
}
