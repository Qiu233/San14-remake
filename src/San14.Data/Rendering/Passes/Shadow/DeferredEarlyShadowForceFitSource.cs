using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The actual small field group written by the source fresh base
/// constructor. It belongs to the same current Compact preparation used by
/// v78, work, v48 and caster stages, not to an arbitrary reused native CPU.</summary>
public sealed class DeferredEarlyShadowCurrentForceFitFields
{
    private readonly byte[] constructorBytes;
    internal DeferredEarlyShadowCurrentForceFitFields(
        LoadedDeferredEarlyShadowForceFitSource source,
        DeferredEarlyShadowCurrentCompactFields compact, byte[] constructorBytes)
        => (Source,Compact,this.constructorBytes)=(source,compact,constructorBytes);
    public LoadedDeferredEarlyShadowForceFitSource Source { get; }
    public DeferredEarlyShadowCurrentCompactFields Compact { get; }
    public ReadOnlyMemory<byte> ConstructorBytes4A4To4AB => constructorBytes;
    public uint ForceFit4A8 => BinaryPrimitives.ReadUInt32LittleEndian(constructorBytes.AsSpan(4,4));
    /// <summary>1058260/10581E0 compare4A8 against0 and SETNE, then write
    /// that normalized DWORD to each actual controller unit48.</summary>
    public uint UnitForceFit48 => ForceFit4A8 == 0 ? 0u : 1u;
    public string SourceEvidence => Source.SourceEvidence+
        ";fresh-owned-4A4qword-before-v78/work/v48/caster;sameCompact;unit48-SETNE-from-retained4A8";
}

/// <summary>Loaded-once exact base constructor fragment. This finite scope
/// is valid for the normal caller's fresh Compact owner. Native pool reuse
/// skips the constructor, so it must retain its actual old field instead.</summary>
public sealed class LoadedDeferredEarlyShadowForceFitSource
{
    private readonly ulong sourceConstructorQword;
    internal LoadedDeferredEarlyShadowForceFitSource(string locale,string evidence,ulong constructorQword)
        => (Locale,SourceEvidence,sourceConstructorQword)=(locale,evidence,constructorQword);
    public string Locale { get; }
    public string SourceEvidence { get; }
    public static LoadedDeferredEarlyShadowForceFitSource Load(GameResourceRepository repository,string locale)
        => RenderSamplerSourceAdapters.LoadEarlyShadowForceFitSource(repository.SourceDirectory,locale);

    /// <summary>Call immediately after the actual current Compact fields
    /// are created, before reset/work/v48/caster. Only eight source bytes
    /// are owned here; no complete Shadow CPU or later setter is synthesized.</summary>
    public DeferredEarlyShadowCurrentForceFitFields PrepareFreshCurrent(
        DeferredEarlyShadowCurrentCompactFields actualCompact)
    {
        ArgumentNullException.ThrowIfNull(actualCompact);
        var own=actualCompact.OwnCamera;
        DeferredEarlyShadowCurrentMainFields.RequireActualMain(own.Main.ActualMainInputs,own.Main.PreparedMain);
        byte[] owned=new byte[8];
        ApplyOwnedConstructorFragment(owned);
        return new(this,actualCompact,owned);
    }
    /// <summary>The actual qword store overwrites all4A4..4AB bytes. This
    /// method is a fresh-construction operation; it is not a per-frame reset
    /// for a reused or externally mutated original Shadow CPU.</summary>
    public void ApplyOwnedConstructorFragment(Span<byte> actualOwned4A4To4AB)
    {
        if(actualOwned4A4To4AB.Length!=8)
            throw new ArgumentException("The source Shadow constructor fragment owns exactly4A4..4AB.");
        BinaryPrimitives.WriteUInt64LittleEndian(actualOwned4A4To4AB,sourceConstructorQword);
    }
}
