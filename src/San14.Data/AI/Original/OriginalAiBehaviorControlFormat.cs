using System.Collections.Immutable;

namespace San14.Data.AI.Original;

public enum OriginalAiChildOrder { Source = 0, EvaluationDescending = 1, Shuffled = 2 }
public enum OriginalAiSequentialPolicy { FirstSuccess = 0, RequireAllSuccess = 1, VisitAll = 2 }
public enum OriginalAiConditionMode { Guard = 0, RepeatWhileTrue = 1, RepeatWhileFalse = 2 }
public enum OriginalAiConditionTermKind { All = 0, Any = 1, Callback = 2 }

public abstract record OriginalAiControlDefinition;
public sealed record OriginalAiRootControl : OriginalAiControlDefinition;
public sealed record OriginalAiSequentialControl(OriginalAiChildOrder Order,
    OriginalAiSequentialPolicy Policy, bool ContinueInSameUpdate) : OriginalAiControlDefinition;
public sealed record OriginalAiConditionControl(bool Invert, bool RequireAll,
    OriginalAiConditionMode Mode, ImmutableArray<OriginalAiConditionTerm> Terms)
    : OriginalAiControlDefinition;

/// <summary>Source condition descriptors are flattened: an All/Any term names
/// the following NestedTermCount callback terms. Callback IDs refer to the AI
/// callback table, not automatically to param_s script indices.</summary>
public sealed record OriginalAiConditionTerm(OriginalAiConditionTermKind Kind,
    bool Invert, uint CallbackId, int NestedTermCount, ImmutableArray<uint> Arguments);

/// <summary>Decodes common control parameters at the resource format boundary.
/// It supplies definitions only; update timing, running state and callback
/// evaluation remain the engine's responsibility.</summary>
public static class OriginalAiBehaviorControlReader
{
    public static bool TryRead(OriginalAiBehaviorNode node, out OriginalAiControlDefinition? control)
    {
        control = null;
        if (node.IsNative) return false;
        var values = node.Arguments;
        switch (node.TypeId)
        {
            case 0xF3836309:
                Require(values.Length == 0, node, "Root has unexpected parameters.");
                control = new OriginalAiRootControl();
                return true;
            case 0x416D418C:
                Require(values.Length == 3 && values[0] <= 2 && values[1] <= 2, node,
                    "Unsupported sequential parameters.");
                control = new OriginalAiSequentialControl((OriginalAiChildOrder)values[0],
                    (OriginalAiSequentialPolicy)values[1], values[2] != 0);
                return true;
            case 0x2252B2CB:
                Require(values.Length >= 4 && values[2] <= 2 && values[3] <= 127, node,
                    "Unsupported condition parameters.");
                int count = (int)values[3];
                int cursor = 4;
                var terms = ImmutableArray.CreateBuilder<OriginalAiConditionTerm>(count);
                for (int index = 0; index < count; index++)
                {
                    Require(values.Length - cursor >= 5, node, "Truncated condition descriptor.");
                    uint kind = values[cursor++];
                    bool invert = values[cursor++] != 0;
                    uint callback = values[cursor++];
                    uint nested = values[cursor++];
                    uint arguments = values[cursor++];
                    Require(kind <= 2 && nested <= 127 && arguments <= 127 &&
                        arguments <= values.Length - cursor, node, "Invalid condition descriptor.");
                    var payload = values.Slice(cursor, (int)arguments);
                    cursor += (int)arguments;
                    terms.Add(new((OriginalAiConditionTermKind)kind, invert, callback, (int)nested, payload));
                }
                Require(cursor == values.Length, node, "Unparsed condition parameters.");
                for (int index = 0; index < count; index++)
                {
                    var term = terms[index];
                    if (term.Kind == OriginalAiConditionTermKind.Callback) continue;
                    Require(term.NestedTermCount <= count - index - 1, node,
                        "Condition group exceeds its descriptor list.");
                    // The source evaluator calls the immediate callback terms.
                    // Nested group semantics are not inferred here.
                    for (int child = 1; child <= term.NestedTermCount; child++)
                        Require(terms[index + child].Kind == OriginalAiConditionTermKind.Callback,
                            node, "Nested condition groups are unsupported.");
                    index += term.NestedTermCount;
                }
                control = new OriginalAiConditionControl(values[0] != 0, values[1] != 0,
                    (OriginalAiConditionMode)values[2], terms.MoveToImmutable());
                return true;
            default:
                return false;
        }
    }

    private static void Require(bool accepted, OriginalAiBehaviorNode node, string message)
    {
        if (!accepted) throw new InvalidDataException($"AI node {node.Index} ({node.TypeId:X8}): {message}");
    }
}
