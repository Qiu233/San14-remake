using San14.Data;

/// <summary>The attached normal operation0 CPU ring with its actual device
/// transport. It consumes the finalized selected View bank, never draw counts.
/// Transport owns four buffers; publication alone does not certify GPU data.</summary>
public sealed class OrdinarySdfOperation0Owner : IOrdinarySdfOperation0
{
    private readonly IOrdinarySdfOperation0Transport transport;
    public OrdinarySdfOperation0Owner(LoadedOrdinarySdfPopulationSource source,
        uint sphereCapacity, uint dualCapacity, IOrdinarySdfOperation0Transport actualTransport)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(actualTransport);
        Source = source; transport = actualTransport;
        Ring = source.CreateOperation0Ring(sphereCapacity, dualCapacity);
    }
    public LoadedOrdinarySdfPopulationSource Source { get; }
    public OrdinarySdfOperation0Ring Ring { get; }
    public OrdinarySdfOperation0Publication? LastPublication { get; private set; }
    public OrdinarySdfOperation0Counts CaptureCurrentCounts() => Ring.CaptureCurrentCounts();
    public bool ApplyCurrentWorldPopulation(object actualSelectedViewOperation0Bank)
    {
        if (actualSelectedViewOperation0Bank is not OrdinarySdfOperation0Bank bank)
            throw new ArgumentException("Normal SDF operation0 needs its actual finalized View1C60 bank.", nameof(actualSelectedViewOperation0Bank));
        LastPublication = Ring.ApplyCurrentBank(bank, transport); return true;
    }
}
