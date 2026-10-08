using San14.Core;

namespace San14.Data;

/// <summary>Semantic decoders are separate from fixed-record parsing and must explicitly resolve their gaps.</summary>
public interface IOriginalTacticRuntimeDecoder
{
    string Revision { get; }
    PlayableTacticDefinition? ResolveDeclaration(GameState state, OriginalTacticSourceRow source);
    TacticUseEligibility CanUse(GameState state, UnitState caster, OriginalTacticSourceRow source,
        PlayableTacticDefinition declaration);
    TacticTargetResolution ResolveEffects(GameState state, UnitState caster, UnitState target,
        OriginalTacticSourceRow source, PlayableTacticDefinition declaration);
}

/// <summary>Connects an explicitly resolved original semantic decoder to the independent queue.
/// A different fixed resource revision cannot silently reinterpret a saved world's tactics.</summary>
public sealed class OriginalTacticSourceRules : IPlayableTacticRules
{
    private readonly OriginalTacticSourceCatalog _catalog;
    private readonly IOriginalTacticRuntimeDecoder _decoder;

    public OriginalTacticSourceRules(OriginalTacticSourceCatalog catalog, IOriginalTacticRuntimeDecoder decoder)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        if (string.IsNullOrWhiteSpace(decoder.Revision))
            throw new InvalidDataException("Tactic semantic decoder revision is missing.");
    }

    public PlayableTacticDefinition? Resolve(GameState state, int tacticId)
    {
        OriginalTacticSourceRow? row = SourceRow(state, tacticId);
        if (row is null) return null;
        PlayableTacticDefinition? definition = _decoder.ResolveDeclaration(state, row);
        if (definition is null) return null;
        if (definition.TacticId != tacticId)
            throw new InvalidDataException("Original tactic decoder returned a different identity.");
        definition = definition with
        {
            ProfileRevision = $"{_decoder.Revision}:{row.Source.Sha256}:{row.DecodedSha256}:{definition.ProfileRevision}",
            Effects = definition.Effects?.ToArray()!
        };
        PlayableTacticStateValidation.ValidateDefinition(definition);
        return definition;
    }

    public TacticUseEligibility CanUse(GameState state, UnitState unit, PlayableTacticDefinition definition)
    {
        OriginalTacticSourceRow? row = SourceRow(state, definition.TacticId);
        return row is null ? new(null, "Original fixed-data source differs from this world.", "tactic.source_revision")
            : _decoder.CanUse(state, unit, row, definition);
    }

    public TacticTargetResolution ResolveEffects(GameState state, UnitState caster, UnitState target,
        PlayableTacticDefinition definition)
    {
        OriginalTacticSourceRow row = SourceRow(state, definition.TacticId)
            ?? throw new InvalidDataException("Original tactic source revision changed during settlement.");
        return _decoder.ResolveEffects(state, caster, target, row, definition);
    }

    private OriginalTacticSourceRow? SourceRow(GameState state, int tacticId)
    {
        if (tacticId < 0 || tacticId >= _catalog.Rows.Count ||
            !state.ContentHashes.TryGetValue(_catalog.Source.ResourceUri, out string? hash) ||
            !hash.Equals(_catalog.Source.Sha256, StringComparison.OrdinalIgnoreCase)) return null;
        OriginalTacticSourceRow row = _catalog.Rows[tacticId];
        return row.Slot == tacticId ? row : throw new InvalidDataException("Original tactic catalog slot changed.");
    }
}
