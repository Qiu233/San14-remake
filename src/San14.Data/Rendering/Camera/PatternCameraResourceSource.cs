using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A real retained named Data owner returned by the project's source
/// registry. Loading a DOK key or raw bytes is not this publication boundary.</summary>
public interface IPatternCameraCurrentNamedData
{
    uint ObjectKey { get; }
    uint FactoryClassId { get; }
    object CurrentDataOwner { get; }
    string SourceEvidence { get; }
    void RequireCurrent();
}
public interface IPatternCameraCurrentNamedRegistry
{
    /// <summary>Actual named lookup/handle-to-Data result. Unknown producer
    /// support must throw; null is an actual lookup/ready result.</summary>
    IPatternCameraCurrentNamedData? LookupCurrentData(uint objectKey);
    string SourceEvidence { get; }
    void RequireCurrent();
}
public sealed record PatternCameraResourceDeclaration(DokObject Camera,
    uint SkylightStaKey, uint MergedPatternKey, uint OfflinePatternKey, string SourceEvidence);
public sealed record PatternCameraResolvedResources(PatternCameraResourceDeclaration Declaration,
    uint ActualLoaderContextFlags3E4, IPatternCameraCurrentNamedData? SkylightSta,
    IPatternCameraCurrentNamedData? MergedPattern,
    IPatternCameraCurrentNamedData? OfflinePattern, bool OfflineFallbackUsed,
    IPatternCameraCurrentNamedRegistry Registry)
{
    public string SourceEvidence => Declaration.SourceEvidence + ";" + Registry.SourceEvidence +
        ";C4F6F0-current-registry-returned-Data/raw2D30-2F18-2F20;CAAEE0-to-K1680-1888-1890";
    public void RequireCurrent()
    {
        Registry.RequireCurrent(); SkylightSta?.RequireCurrent(); MergedPattern?.RequireCurrent(); OfflinePattern?.RequireCurrent();
    }
}

/// <summary>The loaded original Perspective loader and V468 resource-copy
/// contract. Its three resources remain typed owners, never invented native
/// pointers or a fake complete raw Camera allocation.</summary>
public sealed class LoadedPatternCameraResourceSource
{
    internal LoadedPatternCameraResourceSource(string locale, string evidence, uint fallback)
        => (Locale, SourceEvidence, OfflineFallbackKey) = (locale, evidence, fallback);
    public string Locale { get; }
    public string SourceEvidence { get; }
    public uint OfflineFallbackKey { get; }

    /// <summary>Resource/package load boundary. The declaration keys are
    /// retained separately from lookup results and current Data lifetime.</summary>
    public PatternCameraResourceDeclaration ReadDeclaration(DokObject actualCamera, string actualDeclarationEvidence)
    {
        ArgumentNullException.ThrowIfNull(actualCamera); ArgumentException.ThrowIfNullOrWhiteSpace(actualDeclarationEvidence);
        if (actualCamera.ClassId != MapCameraConstructionSource.PerspectiveCameraClassId)
            throw new NotSupportedException("Pattern resources require the actual Perspective loader declaration.");
        uint Key(int ordinal, uint hash)
        {
            var field = actualCamera.Properties[ordinal];
            if (field.Index != ordinal || field.NameHash != hash || field.TypeCode != 5 || field.ElementCount != 1 ||
                field.ElementSize != 4 || field.RawValue.Length != 4)
                throw new InvalidDataException("Pattern resource field has another original type/count/name.");
            return BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
        }
        return new(actualCamera, Key(825, 0x063D4BE7), Key(303, 0x3FAEAC18), Key(304, 0xFD361911),
            actualDeclarationEvidence + ";" + SourceEvidence);
    }

    /// <summary>Actual loader execution over an already owned registry. Source
    /// key0 bypasses the primary lookup; Offline still executes its literal
    /// fallback. Failed first handle/Data lookup also reaches that fallback.</summary>
    public PatternCameraResolvedResources BindFreshResources(PatternCameraResourceDeclaration declaration,
        IPatternCameraCurrentNamedRegistry actualRegistry, uint actualContextFlags3E4)
    {
        ArgumentNullException.ThrowIfNull(declaration); ArgumentNullException.ThrowIfNull(actualRegistry);
        actualRegistry.RequireCurrent();
        IPatternCameraCurrentNamedData? Lookup(uint key)
        {
            if (key == 0) return null;
            var value = actualRegistry.LookupCurrentData(key);
            if (value is not null)
            {
                value.RequireCurrent();
                if (value.ObjectKey != key) throw new InvalidDataException("Pattern lookup returned another named Data owner.");
            }
            return value;
        }
        var sta = (actualContextFlags3E4 & 3) == 1 ? Lookup(declaration.SkylightStaKey) : null;
        var merged = Lookup(declaration.MergedPatternKey);
        var offline = Lookup(declaration.OfflinePatternKey);
        bool fallback = offline is null;
        if (fallback) offline = Lookup(OfflineFallbackKey);
        return new(declaration, actualContextFlags3E4, sta, merged, offline, fallback, actualRegistry);
    }
}

public static class PatternCameraResourceSource
{
    public static LoadedPatternCameraResourceSource Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return RenderSamplerSourceAdapters.LoadPatternCameraResourceSource(repository.SourceDirectory, locale);
    }
}
