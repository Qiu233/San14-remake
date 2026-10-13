using System.Collections.ObjectModel;
using San14.Data.Formats;

namespace San14.Data;

public enum DeferredEarlyLitSelectionPath { PreparedLocal88, CameraOverride1888, HostPalette, CameraSpecialFallback1890 }

/// <summary>The actual Host.v1A8 result. The four entries are wrapper references,
/// not graph texture names or physical shader views.</summary>
public sealed record DeferredEarlyLitPalette<TWrapper>(uint PackedSelector168,
    IReadOnlyList<TWrapper?> Wrappers40) where TWrapper : class;

public sealed record DeferredEarlyLitWrapperSelection<TWrapper>(TWrapper? Wrapper,
    DeferredEarlyLitSelectionPath Path) where TWrapper : class;

/// <summary>Actual pixel-stage4 texture/sampler binding. A source null retains
/// the native all-ones key and zero resource/view command; it supplies no white
/// image and does not authorize any null shader specialization.</summary>
public sealed record DeferredEarlyLitPixelBinding(uint Register,
    ulong PhysicalResource, ulong ShaderView, byte[] NativeSamplerKey,
    G1TextureSamplerRecipe? Sampler, byte[]? NullBackendSamplerDescriptor,
    bool ActualMetadataPresent)
{
    public int NativeStage => 4;
    public bool ProvesGpuCompletion => false;
}

/// <summary>Imported early68 wrapper selection, actual PatternMap setter7 and
/// pixel binding source. Current wrappers and their CPU.v28 metadata still
/// belong to the caller's actual resource owner.</summary>
public sealed class LoadedDeferredEarlyLitSource
{
    private readonly Dictionary<DeferredEarlyIndirectProgram,uint> registers = new();
    internal LoadedDeferredEarlyLitSource(string locale, string nativeEvidence,
        LoadedDeferredEarlyIndirectSource programs, LoadedDeferredSamplerNativeSource sampler)
    {
        Locale=locale;NativeSourceEvidence=nativeEvidence;Programs=programs;NativeSampler=sampler;
        foreach(var provider in programs.Programs.GroupBy(p=>p.ProviderKind))
        {
            var shader=provider.First().Shader;
            var names=shader.ReadConfigurations().Records.Select(r=>r.Name).ToArray();
            string[] actualExpected=provider.Key==DeferredEarlyIndirectProvider.Primary
                ? ["GBufferLayout","SubShadowMaps","PatternMap","ShadowReceiverBias"]
                : ["GBufferLayout","PatternMap","ShadowReceiverBias"];
            if(!names.SequenceEqual(actualExpected))
                throw new InvalidDataException("Early68 actual plugin chain differs from its imported setter7 source.");
            foreach(var program in provider)
            {
                var lit=program.Resources.Entries.Single(e=>e.Name=="sLit");
                if(lit.PrimaryEncoded!=7||lit.SecondarySamplerEncoded!=7||lit.SlotIndex<0||lit.SlotIndex>=16||
                    program.Textures.Single(t=>t.Name=="sLit") is not { Dimension:4,ReturnType:5 } texture||
                    texture.PixelRegister!=(uint)lit.SlotIndex||
                    program.Samplers.Count(s=>s.PixelRegister==(uint)lit.SlotIndex&&s.BindCount==1)!=1)
                    throw new InvalidDataException("Actual early68 texture7 and its same-register sampler do not agree.");
                registers.Add(program,(uint)lit.SlotIndex);
            }
        }
    }
    public string Locale { get; }
    public string NativeSourceEvidence { get; }
    public LoadedDeferredEarlyIndirectSource Programs { get; }
    public LoadedDeferredSamplerNativeSource NativeSampler { get; }
    public string SourceEvidence=>NativeSourceEvidence+";EBCD70-actual-Camera/Host/local88-selection;"+
        "actual2939-v168/v118;PatternMap-v58/setter7-v48;876F30-key38-and-selected-SRV;no-current-pixel-claim";

    /// <summary>Exact EBD510..EBD5B3 selection. The Host callback is invoked only
    /// on the original Camera1680-present / Camera3AC-not5 branch. Nullable
    /// selected wrappers stay null; fallback does not revisit local88.</summary>
    public DeferredEarlyLitWrapperSelection<TWrapper> SelectActualWrapper<TWrapper>(
        bool actualCamera1680Present, uint actualCameraKind3AC, TWrapper? actualCamera1888,
        int actualCamera544, TWrapper? actualCamera1890, TWrapper? actualPreparedLocal88,
        Func<DeferredEarlyLitPalette<TWrapper>?> readActualHostPalette) where TWrapper:class
    {
        ArgumentNullException.ThrowIfNull(readActualHostPalette);
        if(actualCamera1680Present&&actualCameraKind3AC!=5)
        {
            var palette=readActualHostPalette();
            if(palette is not null)
            {
                if(palette.Wrappers40.Count!=4)
                    throw new InvalidDataException("Actual Host palette needs its four wrapper40 entries.");
                uint index=(palette.PackedSelector168>>10)&7;
                if(index<4&&palette.Wrappers40[(int)index] is {} wrapper)
                    return new(wrapper,DeferredEarlyLitSelectionPath.HostPalette);
            }
            return new(actualCamera1890,DeferredEarlyLitSelectionPath.CameraSpecialFallback1890);
        }
        return actualCamera1888 is not null&&actualCamera544!=0
            ? new(actualCamera1888,DeferredEarlyLitSelectionPath.CameraOverride1888)
            : new(actualPreparedLocal88,DeferredEarlyLitSelectionPath.PreparedLocal88);
    }

    /// <summary>Consume the descriptor returned by this selected wrapper's
    /// actual Data20 CPU.v28. PatternMap retains the descriptor, then places it
    /// at texture7's current source register; this finite plugin chain makes no
    /// sampler-key modification. Metadata absence is the original null command.</summary>
    public DeferredEarlyLitPixelBinding PrepareActualPixelBinding(
        DeferredEarlyIndirectProgram actualProgram, DeferredEarlyRender2DMetadata? actualMetadata)
    {
        Programs.RequireProgram(actualProgram);
        uint register=registers[actualProgram];
        if(actualMetadata is null)
            return new(register,0,0,Enumerable.Repeat((byte)255,8).ToArray(),null,
                RenderSamplerSourceAdapters.BackendDefaultDescriptor(NativeSampler),false);
        if(actualMetadata.SamplerKey38.Length!=8||actualMetadata.Raw72.Length!=72)
            throw new InvalidDataException("Actual early68 wrapper metadata requires complete72B/key38.");
        byte[] key=actualMetadata.SamplerKey38.ToArray();
        var sampler=RenderSamplerSourceAdapters.DecodeKey(NativeSampler,key);
        ulong srv=(actualMetadata.PackedFlags36&0x8000)!=0?actualMetadata.ShaderView10:actualMetadata.ShaderView8;
        return new(register,actualMetadata.PhysicalResource0,srv,key,sampler,null,true);
    }
}

public static class DeferredEarlyLitSource
{
    /// <summary>Load/import checks happen once. Reuse the imported program and
    /// sampler owners; normal selection and binding do not reread source files.</summary>
    public static LoadedDeferredEarlyLitSource Load(GameResourceRepository repository,string locale,
        LoadedDeferredEarlyIndirectSource actualPrograms,LoadedDeferredSamplerNativeSource actualSampler)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(actualPrograms);
        ArgumentNullException.ThrowIfNull(actualSampler);
        if(actualSampler.Locale!=locale||!StringComparer.OrdinalIgnoreCase.Equals(
            actualSampler.SourceDirectory,Path.GetFullPath(repository.SourceDirectory)))
            throw new InvalidDataException("Early68 sampler and native source import must share their actual locale/source directory.");
        return RenderSamplerSourceAdapters.LoadEarlyLitSource(repository.SourceDirectory,locale,actualPrograms,actualSampler);
    }
}
