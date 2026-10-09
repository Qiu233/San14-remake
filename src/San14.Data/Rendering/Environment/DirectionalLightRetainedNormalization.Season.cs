namespace San14.Data;

public sealed partial class DirectionalLightRetainedNormalization
{
    /// <summary>Import the next loaded season's constructors using the same
    /// already verified shader consumer directory. No current lighting packet,
    /// clock, GPU content or per-request result is carried into this candidate.</summary>
    public DirectionalLightRetainedNormalization CreateSeasonConstructionCandidate()
        => new(late,sky,BackendOwnerIdentity,earlyRequired);

    /// <summary>Install after the actual session's preceding request drained.
    /// The Host retains this consumer owner, while its current constructor
    /// directory changes to the newly loaded season. Old lighting packets do
    /// not become current merely because they share an object key.</summary>
    public void InstallSeasonConstructionCandidate(DirectionalLightRetainedNormalization candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if(ReferenceEquals(candidate,this)||!ReferenceEquals(candidate.late,late)||
            !ReferenceEquals(candidate.sky,sky)||candidate.BackendOwnerIdentity!=BackendOwnerIdentity||
            candidate.earlyRequired!=earlyRequired||candidate.constructed.Count!=5||
            !candidate.templates.Keys.Order().SequenceEqual(candidate.constructed.Keys.Order()))
            throw new InvalidDataException("Season constructor installation requires all five roots from this consumer owner's loaded shader directory.");
        templates.Clear();constructed.Clear();
        foreach(var row in candidate.templates)templates.Add(row.Key,row.Value);
        foreach(var row in candidate.constructed)constructed.Add(row.Key,row.Value);
    }
}
