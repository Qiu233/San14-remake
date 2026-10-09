
namespace San14.Data;

/// <summary>The explicit original default allocator route used by a fresh
/// RTScalar bank. Virtual30 allocates and then clears the entire requested
/// block; virtual28 alone is not this contract. Custom allocators and retained
/// banks require their own producer.</summary>
public sealed class LoadedOriginalMapPostEffect3ScalarZeroAllocationSource
{
    internal LoadedOriginalMapPostEffect3ScalarZeroAllocationSource(string locale,string evidence)
        =>(Locale,SourceEvidence)=(locale,evidence);
    public string Locale{get;}
    public string RuleVersion=>RenderRuleIdentity.Version;
    public string SourceEvidence{get;}
    public OriginalMapPostEffect3ParameterState CreateFreshState(
        OriginalMapPostEffect3Container module,long frameNumber,string ownerIdentity)
    {
        if(frameNumber<=0)throw new InvalidDataException("A fresh default-allocator scalar bank requires an owned frame.");
        var state=OriginalMapPostEffect3ParameterState.CreateUninitialized(module,frameNumber,ownerIdentity);
        if(state.Declarations.Select(d=>d.NameHash).Distinct().Count()!=state.Declarations.Count)
            throw new NotSupportedException("Fresh scalar zero allocation requires the actual unique source primary hashes.");
        foreach(var declaration in state.Declarations)
            if(!state.SetTypedByHash(declaration.NameHash,declaration.NativeValueType,declaration.ElementCount,
                new byte[declaration.ValueByteCount],SourceEvidence+";explicit-fresh-default-allocator-owner="+ownerIdentity+
                ";whole-allocation-cleared-before-record-metadata-and-handler-initializers"))
                throw new InvalidDataException("Original zero-allocation scalar declaration was not uniquely writable.");
        return state;
    }
}

public static class OriginalMapPostEffect3ScalarZeroAllocationSource
{
    public static LoadedOriginalMapPostEffect3ScalarZeroAllocationSource Load(OriginalGameRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new(locale,RenderRuleIdentity.For("fresh-scalar-zero-allocation",locale)+
            ";clear-whole-new-allocation;unique-typed-declarations;retained-and-custom-allocators-have-separate-owners");
    }
}
