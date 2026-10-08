using System.Security.Cryptography;

namespace San14.Data;

/// <summary>The source fresh default Apply factory registered by1092E0.
/// E6BBC0 supplies its pair(0,1); E7BB60/E6B9F0 copies it into Apply1A4/1A8.
/// Alternative109370 external-pair registration is a separate unsupported
/// owner, not a silent override of this fresh-default receipt.</summary>
public sealed class OriginalMapPostEffect3ApplyFunctorSelection
{
    private readonly OriginalMapPostEffect3Container container;
    private OriginalMapPostEffect3ApplyFunctorSelection(OriginalMapPostEffect3Container source,
        long frame,string factoryOwner,string evidence)
    {container=source;FrameNumber=frame;FactoryOwnerIdentity=factoryOwner;SourceEvidence=evidence;}
    public long FrameNumber{get;}
    public string FactoryOwnerIdentity{get;}
    public string SourceEvidence{get;}
    public int WithoutControllerIndex1A4=>0;
    public int WithControllerIndex1A8=>1;
    public OriginalMapPostEffect3Functor SelectForControllerPresence(bool actualControllerPresent)
        =>container.Functors[actualControllerPresent?WithControllerIndex1A8:WithoutControllerIndex1A4];
    public IReadOnlyList<OriginalMapPostEffect3Pass> ReadSelectedPasses(bool actualControllerPresent)
        =>container.ReadFunctorPasses(actualControllerPresent?WithControllerIndex1A8:WithoutControllerIndex1A4);

    /// <summary>The caller deliberately creates its own default source factory
    /// registry. This does not assert the original process used that registry,
    /// nor infer controller presence from the module's existence.</summary>
    public static OriginalMapPostEffect3ApplyFunctorSelection ConstructFreshDefault(
        OriginalMapFinalOutputPostEffect3Source module,OriginalMapPostEffect3Container parsed,
        long frameNumber,string actualDefaultFactoryOwner,string callerRegistryEvidence)
    {
        ArgumentNullException.ThrowIfNull(module);ArgumentNullException.ThrowIfNull(parsed);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualDefaultFactoryOwner);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerRegistryEvidence);
        if(frameNumber<=0||module.SelectedTemplateMode3C0!=1||
            Convert.ToHexString(SHA256.HashData(module.Payload.Span))!=OriginalMapFinalOutputPostEffect3Source.PayloadSha256||
            parsed.Sha256!=OriginalMapFinalOutputPostEffect3Source.PayloadSha256||
            Convert.ToHexString(SHA256.HashData(parsed.Bytes.Span))!=parsed.Sha256)
            throw new InvalidDataException("Fresh Apply selection requires this mode1 module and owned factory frame.");
        // Independently own/reparse the hash-pinned payload, so the caller's
        // mutable list/byte backing cannot later change a selected pass set.
        var snapshot=OriginalMapPostEffect3ContainerReader.Read(parsed.Bytes.Span);
        if(snapshot.Functors.Count!=2||snapshot.Functors[0].Index!=0||snapshot.Functors[1].Index!=1)
            throw new InvalidDataException("Fresh default Apply selectors require both actual source functors.");
        return new(snapshot,frameNumber,actualDefaultFactoryOwner,
            "TC:1092E0/109332:E6BBC0-default-pair0,1;109357:B2D30063;109364:B3C3F0-register;"+
            "E7C3FF:prototype+8;E6BB6E:Apply1A4;E6BB78:Apply1A8;"+
            "EB805C:without-controller;EB7F6B/E22C1D:with-controller;"+
            "explicit-owned-default-registry;"+actualDefaultFactoryOwner+";"+callerRegistryEvidence);
    }
}
