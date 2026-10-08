using System.Collections.ObjectModel;

namespace San14.Data;

/// <summary>The original working-wrapper40/42 versus module40/44 comparison.
/// This result determines Apply local5 / controller argument4. It records a CPU
/// request; it does not acknowledge allocation or device completion.</summary>
public sealed record OriginalMapPostEffect3ActualExtentTransition(long FrameNumber,
    string OwnerIdentity,uint PriorModuleWidth40,uint PriorModuleHeight44,
    ushort WorkingWrapperWidth40,ushort WorkingWrapperHeight42,byte ControllerArgument4R9B,
    string SourceEvidence)
{
    public bool RequiresModuleResize=>ControllerArgument4R9B!=0;
}

/// <summary>Loaded complete paired native retained resize and optics boundaries.
/// FC/FD0 preserves controller construction dimensions and does not dirty AF.
/// Actual module resizing separately drives IsFirst and DOF's Count reset.</summary>
public sealed class LoadedOriginalMapPostEffect3RetainedResizeSource
{
    internal LoadedOriginalMapPostEffect3RetainedResizeSource(string locale,string exeHash,string textHash,
        Dictionary<string,string> windows)
    {Locale=locale;ExecutableSha256=exeHash;DecodedTextSha256=textHash;
        SourceWindows=new ReadOnlyDictionary<string,string>(windows);}
    public string Locale{get;}
    public string ExecutableSha256{get;}
    public string DecodedTextSha256{get;}
    public IReadOnlyDictionary<string,string> SourceWindows{get;}
    public string SourceEvidence=>$"{Locale}@{ExecutableSha256};retained-E22590/EB5D50/1145D00/1147830/11468D0;"+
        "controller-E22860-IsFirst->factory7-1155140/1157230;load-only-source-acceptance";

    public OriginalMapPostEffect3ActualExtentTransition ReadActualExtentTransition(long frameNumber,string owner,
        uint actualPriorModuleWidth40,uint actualPriorModuleHeight44,int actualWorkingWrapperWidth40,int actualWorkingWrapperHeight42)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if(frameNumber<=0||actualPriorModuleWidth40==0||actualPriorModuleHeight44==0||
            actualWorkingWrapperWidth40<=0||actualWorkingWrapperHeight42<=0||
            actualWorkingWrapperWidth40>ushort.MaxValue||actualWorkingWrapperHeight42>ushort.MaxValue)
            throw new InvalidDataException("Retained resize needs the actual module and positive wrapper extents.");
        ushort w=(ushort)actualWorkingWrapperWidth40,h=(ushort)actualWorkingWrapperHeight42;
        byte argument4=(byte)(actualPriorModuleWidth40!=w||actualPriorModuleHeight44!=h?1:0);
        return new(frameNumber,owner,actualPriorModuleWidth40,actualPriorModuleHeight44,w,h,argument4,
            SourceEvidence+";EB68F3..EB6982-actual-before-resize-extent-comparison;EB7F5E-r9b");
    }

    /// <summary>Normal retained Apply for the closed FC=FD=0 profile. The
    /// controller's88/8C/94/98 and effective80/84/F0/F4 survive actual resize.
    /// Other changed optics still require their actual dirty setter.</summary>
    public OriginalMapPostEffect3ApplyDerivedScalarPlan PrepareRetainedCurrentDerived(
        OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3ApplyFreshCameraScalars currentTail,
        OriginalMapPostEffect3ApplyFreshCameraScalars previousTail,OriginalMapPostEffect3ApplyDerivedScalarPlan previous,
        int actualWorkingSceneWidth,int actualWorkingSceneHeight,OriginalSkyWeatherNativePowerSource power)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(currentTail);
        ArgumentNullException.ThrowIfNull(previousTail);ArgumentNullException.ThrowIfNull(previous);ArgumentNullException.ThrowIfNull(power);
        if(camera.ExecutableLocale!=Locale||power.Locale!=Locale||previous.ExecutableLocale!=Locale||
            camera.FrameNumber!=previous.FrameNumber+1)
            throw new InvalidDataException("Retained derived Apply needs the same controller's next current Camera.");
        if((currentTail.Word(0x1320)&0x30)!=0||(previousTail.Word(0x1320)&0x30)!=0)
            throw new NotSupportedException("Retained FC/FD changes or enabled extent overrides require their actual E22590 AF writer.");
        var plan=OriginalMapPostEffect3ApplyDerivedScalars.PrepareRetainedExtentDisabledSource(
            camera,currentTail,previous,actualWorkingSceneWidth,actualWorkingSceneHeight,power);
        return plan with {SourceEvidence=plan.SourceEvidence+";"+SourceEvidence+
            ";E225F2-FC0-returns-without80/84/F0/F4/AF-writes;retain-original88/8C/94/98"};
    }
}

public static class OriginalMapPostEffect3RetainedResizeSource
{
    public static LoadedOriginalMapPostEffect3RetainedResizeSource Load(OriginalGameRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadPostEffect3RetainedResizeSource(repository.SourceDirectory,locale);
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3RetainedResizeSource LoadPostEffect3RetainedResizeSource(string directory,string locale)
    {
        var profile=Profile(locale);
        byte[] exe=File.ReadAllBytes(Path.Combine(directory,$"SAN14CE_{locale}.exe"));
        VerifyHash(exe,profile.ExeHash,"retained post-effect resize executable");
        var image=new NativeImage(exe);byte[] code=image.DecodeText(out int textRva);
        VerifyHash(code,profile.TextHash,"retained post-effect resize decoded text");
        var windows=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var (name,tcRva,scRva,length,tcHash,scHash) in new (string,int,int,int,string,string)[]{
            ("ExtentSetter", 0x00E22590, 0x00E23410, 586, "C194AA816504D95888A50BCDE8D6D1124EFC4523F3F57C8DED189D4CF2522945", "575BEA20D1B711019495CAA2B0BA86C7356844437A921FF1876FE9F586DD0347"),
            ("ControllerDimensionsCtor", 0x00E24A80, 0x00E25A40, 581, "F8665C3F4C21FE21A559A5655FF40714190F3B016F4EE0B766E9CFE1EF602221", "249A953C17FF2F6A6BE9CCE077E43E60E9404142B3BB460A2D31BF82091670AF"),
            ("OpticsAfPrepare", 0x00D7B4D0, 0x00D7C290, 69, "7D9D9828C35B068E26EAFEC15FBB28B91198CB992AEEE26EBDAD7B1416680E0C", "483BDBD7F2C2BB0DF585FBEBACCCA5325D0D2780F7E65934B769F3EA8148EAC4"),
            ("ModuleAllocator", 0x011468D0, 0x01147950, 2015, "351394E38BFCDFD6FC27AE285CF46244DCAEA49FD2502E0505EA9BFB3BAAF5B2", "E69ECA4DC3BD526D4BC6F6A5DF6EB0D3A6F268E1A146F19B38FE49935158DC86"),
            ("CallerApply", 0x00EB5D50, 0x00EB6D40, 16412, "D188F62871633D524939D157A34B77232EF9A24DFEAD1061F516E561EFFF939E", "663C8EB067A6B114671EC9C325E25795F68282F80B0BFBBAAFC6BF572F825F79"),
            ("ModuleResizeEntry", 0x01145D00, 0x01146D80, 21, "A64B00FC081D2D128395540B07D9B3192DBD71A9FEC978C7E0AEB00673ACA9C4", "A64B00FC081D2D128395540B07D9B3192DBD71A9FEC978C7E0AEB00673ACA9C4"),
            ("ModuleResize", 0x01147830, 0x011488B0, 86, "8205F3A6B68B9861EF2064AB24657762C30461DCDB2AD4A2F8FF0DE58213472B", "8205F3A6B68B9861EF2064AB24657762C30461DCDB2AD4A2F8FF0DE58213472B"),
            ("ModuleResetFactoryPreparation", 0x01147890, 0x01148910, 114, "08D9C76F26DBC6DFF369DB57BDC7FEC874F650581991B61D470AD8DD636B0917", "2C5FD15D788EA337A022FC1ECA4561401251BF3141DF9456B1AC90C7E58407F3"),
            ("ModuleDofInit", 0x01155140, 0x011561F0, 144, "A91075936307414BFC7CCC80E0AF09450E55A4C81447BC6003FF874558461F4D", "D3BCE501CEA5D84528789BCCD7DB59A8A3D33B19F65BE4DF5CD8343CB22793CF"),
            ("DofInit", 0x01157230, 0x011582E0, 989, "9316214803F0B784502ECDE7A1DE9FD1E0B0332CD88FC71C2345A4A05962896C", "2E04696CAD6FCF7BFD2F30A53E0F12039E2D816ADC57E3F0E63181A0461F6A05"),
            ("ControllerExecute", 0x00E22860, 0x00E23790, 1107, "725BF0BA2FDECED538B0BCCBFCE380097FCD760EBB0F330DD17CE42FD36C0C11", "43FC21A65E6C4BC3958B4991B93CCD5406C0C652C88A52150801979E8741CF96")})
        {
            VerifyHash(code.AsSpan((locale=="TC"?tcRva:scRva)-textRva,length),locale=="TC"?tcHash:scHash,name);
            windows.Add(name,locale=="TC"?tcHash:scHash);
        }
        return new(locale,profile.ExeHash,profile.TextHash,windows);
    }
}
