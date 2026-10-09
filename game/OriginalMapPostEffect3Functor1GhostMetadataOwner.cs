using System.Security.Cryptography;
using San14.Data;

/// <summary>Actual current controller CPU producer and temporary registry
/// metadata identity, for Select's unconsumed B4 setter. No GPU buffer/SRV is
/// fabricated. A selected B4 read remains unsupported.</summary>
internal sealed class OriginalMapPostEffect3Functor1GhostMetadataOwner
{
    private readonly OriginalMapPostEffect3WorldControlledFactoryFrame frame;
    private readonly OriginalMapPostEffect3ControlledOpticsPreparation producer;
    private readonly byte[] initializedPrefix,currentPrefix;
    internal readonly object Controller828Identity;
    internal readonly object Registry4Identity=new();
    internal string SourceEvidence{get;}
    internal OriginalMapPostEffect3Functor1GhostMetadataOwner(OriginalMapPostEffect3WorldControlledFactoryFrame frame,
        LoadedOriginalMapPostEffect3Functor1FrontendLifetimeSource native)
    {
        this.frame=frame;producer=frame.Camera.Optics;producer.ValidateCurrent(frame.Camera.State,frame.Camera.Allocation);
        Controller828Identity=frame.ModuleLifetime?.GhostIdentity??new object();
        native.ValidateFor(frame.Camera.Allocation);
        initializedPrefix=producer.InitialGhostRecords640.ToArray();currentPrefix=producer.SelectedGhostRecords640.ToArray();
        if(producer.NativeGhostAllocationByteCount!=native.ControllerGhostBufferByteCapacity||producer.NativeGhostStride!=28||
            initializedPrefix.Length!=448||currentPrefix.Length!=producer.GhostSpriteCount*28||currentPrefix.Length>448)
            throw new InvalidDataException("Controller828 metadata lacks its actual fresh448B/stride28 CPU producers.");
        SourceEvidence=native.SourceEvidence+";"+producer.SourceEvidence+
            ";controller-448B-stride28;initial-16rows;current-prefix;"+
            "temporary-registry4-logical-retain;managed-metadata-only/no-device-SRV/no-readiness;"+
            "initialSHA="+Hash(initializedPrefix)+";currentSHA="+Hash(currentPrefix);
    }
    internal void RequireCurrent()
    {
        frame.RequireCurrent();producer.ValidateCurrent(frame.Camera.State,frame.Camera.Allocation);
        frame.ModuleLifetime?.RequireGhostCurrent(frame.Camera);
        if(!ReferenceEquals(producer,frame.Camera.Optics)||!initializedPrefix.AsSpan().SequenceEqual(producer.InitialGhostRecords640.Span)||
            !currentPrefix.AsSpan().SequenceEqual(producer.SelectedGhostRecords640.Span))
            throw new InvalidDataException("Actual controller ghost CPU producer changed after temporary registry metadata assignment.");
    }
    private static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b));
}
