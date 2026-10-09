namespace San14.Data;

/// <summary>The four original SC164180/TC1643D0 byte stores. Quality is the signed
/// Configs/CG_QUALITY value read by the actual option18 getter, not a light type or scenario
/// difficulty. This projection neither selects a CPU nor reads native state.</summary>
public readonly record struct OriginalMapGameLightQualityWrites(byte Light10B1,
    byte Light10B2, byte Light10B5, byte ReceiveShadowF1D);

public static class OriginalMapGameLightQualityOverlay
{
    public const int CompleteLightDataBytes=0x10C0;
    public const string ConfigSection="Configs";
    public const string ConfigQualityName="CG_QUALITY";

    public static OriginalMapGameLightQualityWrites ReadWrites(int currentCgQualityOption18,int currentMainMode288)
    {
        //SC1641AE/TC1643FE JGE is signed; the following mode branch
        //independently selects B5 for mode>=3.
        byte selected=currentCgQualityOption18>=1?(byte)2:(byte)8;
        return new(currentCgQualityOption18>=1?(byte)2:(byte)6,selected,
            currentMainMode288>=3||currentCgQualityOption18>=1?(byte)2:(byte)0,selected);
    }

    /// <summary>Apply only after the caller has selected the CPU returned by
    /// CC7480. The source callback occurs before actor v430/v420 and provider
    /// temporary interpolation. Applying this to every prepared GPU record is
    /// not an equivalent operation.</summary>
    public static OriginalMapGameLightQualityOverlayPacket Apply(ReadOnlySpan<byte> completeLightData,
        int currentCgQualityOption18,int currentMainMode288)
    {
        if(completeLightData.Length!=CompleteLightDataBytes)
            throw new InvalidDataException("Game Light quality writer requires one complete selected CPU LightData10C0.");
        return new(completeLightData,currentCgQualityOption18,currentMainMode288);
    }

    internal static void Write(Span<byte> bytes,OriginalMapGameLightQualityWrites values)
    {
        bytes[0x10B1]=values.Light10B1;bytes[0x10B2]=values.Light10B2;
        bytes[0x10B5]=values.Light10B5;bytes[0xF1D]=values.ReceiveShadowF1D;
    }
}

/// <summary>Immutable projection of the admitted four stores; not a receipt
/// of the active original CPU, source caller selection or runtime setting.</summary>
public sealed class OriginalMapGameLightQualityOverlayPacket
{
    private readonly byte[] input,output;
    internal OriginalMapGameLightQualityOverlayPacket(ReadOnlySpan<byte> seed,int quality,int mode)
    {
        input=seed.ToArray();output=input.ToArray();QualityOption18=quality;MainMode288=mode;
        Writes=OriginalMapGameLightQualityOverlay.ReadWrites(quality,mode);
        OriginalMapGameLightQualityOverlay.Write(output,Writes);
    }
    public int QualityOption18{get;}
    public int MainMode288{get;}
    public OriginalMapGameLightQualityWrites Writes{get;}
    public ReadOnlyMemory<byte> Bytes=>output.ToArray();
    public void ValidateProjection()
    {
        byte[] expected=input.ToArray();
        OriginalMapGameLightQualityOverlay.Write(expected,
            OriginalMapGameLightQualityOverlay.ReadWrites(QualityOption18,MainMode288));
        if(!expected.AsSpan().SequenceEqual(output))
            throw new InvalidDataException("Selected Game LightData differs from the original four quality stores.");
    }
}

/// <summary>Independent remake current graphics setting. Fresh quality0 is
/// original SC3E61C0/TC3E5BC0 primary64 initialization, strictly before a saved configuration is
/// applied. Explicit later edits are owned settings, not original observations.</summary>
public sealed class OriginalMapGameLightQualityOwner
{
    private readonly LoadedOriginalMapGameLightQualitySource source;
    private OriginalMapGameLightQualityOwner(string identity,LoadedOriginalMapGameLightQualitySource source)
    {
        this.source=source;OwnedEnvironmentIdentity=identity;RuleVersion=source.RuleVersion;
        CurrentQualityOption18=source.FreshQuality;SourceProvenance=source.SourceProvenance+";owner="+identity;
    }
    public string OwnedEnvironmentIdentity{get;}
    public string RuleVersion{get;}
    public string Locale=>source.Locale;
    public int CurrentQualityOption18{get;private set;}
    public bool SavedConfigurationOrOwnedEditApplied{get;private set;}
    public string SourceProvenance{get;private set;}
    public static OriginalMapGameLightQualityOwner CreateFreshPrimary(OriginalGameRepository repository,
        string locale,string ownedEnvironmentIdentity)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentException.ThrowIfNullOrWhiteSpace(ownedEnvironmentIdentity);
        var source=OriginalDeferredSamplerSources.LoadGameLightQualitySource(repository.SourceDirectory,locale);
        return new(ownedEnvironmentIdentity,source);
    }
    public void SetOwnedCurrentQuality(int quality,string settingsProvenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsProvenance);
        //SC3EB110/TC3EAB20 index exactly [3,3,1] when applying settings.
        if(quality is <0 or >2)throw new InvalidDataException("Owned CG_QUALITY has no admitted three-entry settings application.");
        CurrentQualityOption18=quality;SavedConfigurationOrOwnedEditApplied=true;
        SourceProvenance=source.SourceProvenance+$";owned-Configs/CG_QUALITY={quality};{settingsProvenance};owner={OwnedEnvironmentIdentity}";
    }
    public OriginalMapGameLightQualityOverlayPacket ApplySelectedCpu(ReadOnlySpan<byte> completeLightData,
        OriginalMainMapCameraModeOwner actualMainModeOwner,OriginalMainMapCameraModePacket currentMainMode)
    {
        ArgumentNullException.ThrowIfNull(currentMainMode);
        currentMainMode.ValidateCurrent(actualMainModeOwner,currentMainMode.AcceptedMainFrame);
        return OriginalMapGameLightQualityOverlay.Apply(completeLightData,CurrentQualityOption18,currentMainMode.Mode288);
    }
}
