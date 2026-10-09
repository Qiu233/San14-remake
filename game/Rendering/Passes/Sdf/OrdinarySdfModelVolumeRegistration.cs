using San14.Data;

/// <summary>The actual ready SDF record from ModelCPU D0. MaterialCPU D0 and
/// ordinary texture-group records have a different domain. Header and
/// resource20 must come from the real source model SDF resource producer.</summary>
public interface IOrdinarySdfReadyVolumeRecord
{
    ReadOnlyMemory<byte> ReadActualSourceHeader28();
    object? ActualVolumeResource20 { get; }
}

/// <summary>Finite DB7DCB/D69D70 Model-SDF volume registration. Invoke after the
/// actual enclosing model/caller eligibility gates and for each actual selected
/// View bank. It does not stand in for those outer gates, View selection, source
/// cull, graph2 or the atlas/update backend.</summary>
public static class OrdinarySdfModelVolumeRegistration
{
    public static bool RegisterCurrentReadyModelVolume(
        LoadedOrdinarySdfPopulationSource source,
        IOrdinarySdfReadyVolumeRecord? actualCurrentModelSdfD0,
        Func<OrdinarySdfVolumeTransform> readActualCurrentWorld,
        OrdinarySdfOperation1Bank actualCurrentViewBank,
        Func<OrdinarySdfVolumeViewBox,bool> testActualCurrentView)
    {
        // Original DB7DCB null ModelSdfD0 does not read description/World/cull.
        if (actualCurrentModelSdfD0 is null) return false;
        var shape = actualCurrentModelSdfD0;
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(readActualCurrentWorld);
        ArgumentNullException.ThrowIfNull(actualCurrentViewBank); ArgumentNullException.ThrowIfNull(testActualCurrentView);
        var header = OrdinarySdfVolumeHeader.ReadActual(shape.ReadActualSourceHeader28().Span);
        var descriptor = source.DescribeCurrentVolume(header, readActualCurrentWorld(), shape.ActualVolumeResource20);
        return actualCurrentViewBank.RegisterCurrentViewVolume(descriptor,testActualCurrentView);
    }
}
