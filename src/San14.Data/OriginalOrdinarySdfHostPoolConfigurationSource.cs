namespace San14.Data;

/// <summary>Three DWORDs copied by the Main Host constructor into Q724/728/72C.
/// The last word belongs to Frame420, not the two SDF operation0 capacities.</summary>
public readonly record struct OriginalOrdinarySdfHostPoolFields(
    uint Host724, uint Host728, uint Host72C)
{
    public bool ConfiguresOperation0Pool => Host724 != 0 || Host728 != 0;
}

/// <summary>One actual Common frame's finite Frame400 pool. D019B0 writes the
/// initial capacities and both list pointers. It is not a full Common frame,
/// a SDF manager or an operation0 ring/count publication.</summary>
public sealed class OriginalOrdinarySdfSourceCreatedOperation0Pool
{
    private readonly List<OriginalOrdinarySdfOperation0Bank> allocated = new();
    internal OriginalOrdinarySdfSourceCreatedOperation0Pool(object actualCommonOwner,
        int commonFrameSide, string evidence)
        => (ActualCommonOwner, CommonFrameSide, SourceEvidence) =
            (actualCommonOwner, commonFrameSide, evidence);

    public object ActualCommonOwner { get; }
    public int CommonFrameSide { get; }
    public string SourceEvidence { get; }
    public OriginalOrdinarySdfOperation0PoolFields CurrentFields { get; private set; }

    /// <summary>D5F170 skips D1B260 when BOTH Host words are zero. A skipped
    /// call preserves an existing pool, including a previous nonzero setup.</summary>
    public bool ApplyCurrentHostConfiguration(OriginalOrdinarySdfHostPoolFields actualHost)
    {
        if (!actualHost.ConfiguresOperation0Pool) return false;
        // D1B260 retires its all-list then clears both lists before the stores.
        allocated.Clear();
        CurrentFields = new(actualHost.Host724, actualHost.Host728);
        return true;
    }

    /// <summary>The source-created scope has no later free-list publisher.
    /// D0C2E0 then requires BOTH capacities before constructing a new bank.
    /// Normal startup's proved0/0 returns null without inspecting model kind,
    /// bone names, palette or profile. This is not a general released-bank pool.</summary>
    public OriginalOrdinarySdfOperation0Bank? AcquireCurrentViewBank(
        LoadedOriginalOrdinarySdfPopulationSource actualPopulationSource)
    {
        ArgumentNullException.ThrowIfNull(actualPopulationSource);
        if (CurrentFields.SphereCapacity4 == 0 || CurrentFields.DualCapacity8 == 0)
            return null;
        var bank = actualPopulationSource.CreateOperation0ViewBank(CurrentFields)!;
        allocated.Add(bank);
        return bank;
    }

}

/// <summary>Loaded once. Normal startup overrides the creator's optional-null
/// fallback256/256/0 with0/0/256. Later Host settings need their actual writer;
/// this does not reread declarations or reset pools on every render request.</summary>
public sealed class LoadedOriginalOrdinarySdfHostPoolConfigurationSource
{
    internal LoadedOriginalOrdinarySdfHostPoolConfigurationSource(string locale,
        OriginalOrdinarySdfHostPoolFields startup, OriginalOrdinarySdfHostPoolFields fallback,
        string evidence)
        => (Locale, NormalStartupHostFields, CreatorAbsentOverrideFields, SourceEvidence) =
            (locale, startup, fallback, evidence);
    public string Locale { get; }
    public OriginalOrdinarySdfHostPoolFields NormalStartupHostFields { get; }
    public OriginalOrdinarySdfHostPoolFields CreatorAbsentOverrideFields { get; }
    public string SourceEvidence { get; }

    /// <summary>The real D01580 recipe constructs four D019B0 frames at
    /// Common210 + side*800. This publishes ONLY their Frame400 pool fields.
    /// Pass the program's already owned Common lifetime, not a selected package.</summary>
    public OriginalOrdinarySdfSourceCreatedOperation0Pool[] ConstructFreshNormalCommonPools(
        object actualCommonOwner, OriginalMainMapContextOwnerSource actualFreshHostSource)
    {
        ArgumentNullException.ThrowIfNull(actualCommonOwner);
        ArgumentNullException.ThrowIfNull(actualFreshHostSource);
        if (actualFreshHostSource.Locale != Locale)
            throw new ArgumentException("SDF startup and actual Main Host constructor use different source locales.", nameof(actualFreshHostSource));
        var pools = new OriginalOrdinarySdfSourceCreatedOperation0Pool[4];
        for (int side = 0; side < pools.Length; side++)
        {
            pools[side] = new(actualCommonOwner, side, SourceEvidence);
            pools[side].ApplyCurrentHostConfiguration(NormalStartupHostFields);
        }
        return pools;
    }
}

public static class OriginalOrdinarySdfHostPoolConfigurationSource
{
    public static LoadedOriginalOrdinarySdfHostPoolConfigurationSource Load(
        OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadOrdinarySdfHostPoolSource(repository.SourceDirectory, locale);
    }
}
