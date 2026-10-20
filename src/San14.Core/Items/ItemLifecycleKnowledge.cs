using System.Text.Json.Serialization;

namespace San14.Core;

public enum ItemStatusKnowledgeKind { ExactSourceState, KnownNotListEligible }

/// <summary>Exact source state or the weaker fact that its list predicate is
/// false. Missing knowledge never supplies a made-up source code.</summary>
public sealed record ItemStatusKnowledge
{
    public ItemStatusKnowledgeKind Kind { get; }
    public byte? Code { get; }
    [JsonConstructor]
    public ItemStatusKnowledge(ItemStatusKnowledgeKind kind, byte? code)
    {
        if (!Enum.IsDefined(kind) || (kind == ItemStatusKnowledgeKind.ExactSourceState) != code.HasValue)
            throw new InvalidDataException("Item status knowledge has an invalid union.");
        Kind = kind; Code = code;
    }
    [JsonIgnore]
    public bool IsListEligible => Kind == ItemStatusKnowledgeKind.ExactSourceState && Code == 1;
    public static ItemStatusKnowledge Exact(byte code) => new(ItemStatusKnowledgeKind.ExactSourceState, code);
    public static ItemStatusKnowledge NotListEligible { get; } = new(ItemStatusKnowledgeKind.KnownNotListEligible, null);
}

/// <summary>Item-owned current lifecycle facts. A held item can retain an old
/// location code; this is not a claim that it currently occupies that place.</summary>
public sealed record ItemLifecycleKnowledge(ItemStatusKnowledge? Status,
    ushort? RetainedLocationCode, byte? RetainedFlags);
