using System.Collections.Immutable;
using System.Linq.Expressions;

namespace San14.Data.AI.Original;

public enum OriginalAiConditionRecordKind : byte { All, Any, Query }
public sealed record OriginalAiConditionRecord(int NodeIndex, int WordIndex,
    OriginalAiConditionRecordKind Kind, bool Negate, uint QueryId, int ChildCount,
    ImmutableArray<uint> Parameters);
public abstract record OriginalAiConditionClause;
public sealed record OriginalAiConditionQuery(OriginalAiConditionRecord Record) : OriginalAiConditionClause;
public sealed record OriginalAiConditionGroup(OriginalAiConditionRecord Record,
    ImmutableArray<OriginalAiConditionQuery> Children) : OriginalAiConditionClause;
public sealed record OriginalAiConditionDefinition(bool InvertResult, bool RequireAll,
    OriginalAiConditionMode Mode, ImmutableArray<OriginalAiConditionRecord> Records,
    ImmutableArray<OriginalAiConditionClause> Clauses);

/// <summary>Typed bindings for the three scalar predicates. Missing or differently typed values
/// follow the source's false/fallback behavior. ObjectKeysEqual must resolve both object keys
/// and compare their stable domain identities; it returns false if either key is unavailable.</summary>
public interface IOriginalAiConditionScalarValues
{
    bool TryReadInteger(uint key, out int value);
    bool ObjectKeysEqual(uint leftKey, uint rightKey);
}

public static class OriginalAiScriptConditions
{
    public const uint DecoratorTypeId = 0x2252B2CB;

    public static OriginalAiConditionDefinition Read(ImmutableArray<uint> arguments, int nodeIndex)
    {
        if (arguments.IsDefault || arguments.Length < 4) throw Error(nodeIndex, 0, "Missing condition header");
        if (arguments[2] > 2) throw Error(nodeIndex, 2, $"Unbound decorator mode {arguments[2]}");
        int count = Count(arguments[3], nodeIndex, 3, 127);
        int position = 4;
        var records = ImmutableArray.CreateBuilder<OriginalAiConditionRecord>(count);
        for (int i = 0; i < count; ++i)
        {
            int at = position;
            if (position > arguments.Length - 5) throw Error(nodeIndex, at, "Truncated condition record");
            uint kind = arguments[position++], negate = arguments[position++], query = arguments[position++];
            if (kind > 2) throw Error(nodeIndex, at, $"Unbound record kind {kind}");
            // The original stores these fields as bytes, then uses signed byte counts.
            if (negate > byte.MaxValue) throw Error(nodeIndex, at + 1, "Negation field exceeds its stored byte");
            int children = Count(arguments[position++], nodeIndex, at + 3, 127);
            int parameterCount = Count(arguments[position++], nodeIndex, at + 4, 64);
            if (position > arguments.Length - parameterCount) throw Error(nodeIndex, position, "Truncated query parameters");
            records.Add(new(nodeIndex, at, (OriginalAiConditionRecordKind)kind, negate != 0, query, children,
                arguments.AsSpan(position, parameterCount).ToArray().ToImmutableArray()));
            position += parameterCount;
        }
        if (position != arguments.Length) throw Error(nodeIndex, position, "Unparsed condition words");
        ImmutableArray<OriginalAiConditionRecord> parsed = records.MoveToImmutable();
        var clauses = ImmutableArray.CreateBuilder<OriginalAiConditionClause>();
        for (int i = 0; i < parsed.Length; ++i)
        {
            OriginalAiConditionRecord record = parsed[i];
            if (record.Kind == OriginalAiConditionRecordKind.Query)
            {
                if (record.ChildCount != 0) throw Error(nodeIndex, record.WordIndex + 3, "Leaf has a group child count");
                clauses.Add(new OriginalAiConditionQuery(record));
                continue;
            }
            if (record.ChildCount > parsed.Length - i - 1)
                throw Error(nodeIndex, record.WordIndex + 3, "Condition group exceeds the record list");
            var children = ImmutableArray.CreateBuilder<OriginalAiConditionQuery>(record.ChildCount);
            for (int j = 0; j < record.ChildCount; ++j)
            {
                OriginalAiConditionRecord child = parsed[++i];
                if (child.Kind != OriginalAiConditionRecordKind.Query || child.ChildCount != 0)
                    throw Error(nodeIndex, child.WordIndex, "Nested groups are not supported by the original flat group evaluator");
                children.Add(new(child));
            }
            // Group query/negate/parameter metadata is preserved above. The source uses only its
            // Kind and ChildCount when folding this group, and evaluates the following leaf records.
            clauses.Add(new OriginalAiConditionGroup(record, children.MoveToImmutable()));
        }
        return new(arguments[0] != 0, arguments[1] != 0, (OriginalAiConditionMode)arguments[2],
            parsed, clauses.ToImmutable());
    }

    /// <summary>Load-time lowering only. Bind every query to a raw bool predicate; negation is
    /// applied here exactly once. Query order and AND/OR short circuit follow the source.</summary>
    public static Expression Lower(OriginalAiConditionDefinition definition,
        Func<OriginalAiConditionQuery, Expression> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        Expression result = Expression.Constant(definition.RequireAll);
        foreach (OriginalAiConditionClause clause in definition.Clauses)
        {
            Expression next = clause switch
            {
                OriginalAiConditionQuery query => Bind(query, bind),
                OriginalAiConditionGroup group => Fold(group.Children,
                    group.Record.Kind == OriginalAiConditionRecordKind.All, bind),
                _ => throw new InvalidDataException("Unbound original AI condition clause.")
            };
            result = definition.RequireAll ? Expression.AndAlso(result, next) : Expression.OrElse(result, next);
        }
        return definition.InvertResult ? Expression.Not(result) : result;
    }

    /// <summary>Bind source query 20, 21 or 23 to the typed scalar host, once at load time.
    /// Other query IDs remain the policy host's explicit responsibility.</summary>
    public static Expression LowerScalarQuery(OriginalAiConditionQuery query, Expression values)
    {
        var record = query.Record;
        if (record.QueryId is not (20 or 21 or 23))
            throw Error(record.NodeIndex, record.WordIndex + 2, $"Query {record.QueryId} is not a scalar binding");
        if (record.Parameters.Length != 2)
            throw Error(record.NodeIndex, record.WordIndex + 4, $"Query {record.QueryId} needs two parameters");
        string method = record.QueryId switch { 20 => nameof(GreaterThan), 21 => nameof(AtLeast), _ => nameof(EqualKeys) };
        Expression right = record.QueryId == 23
            ? Expression.Constant(record.Parameters[1])
            : Expression.Constant(unchecked((int)record.Parameters[1]));
        return Expression.Call(typeof(OriginalAiScriptConditions), method, Type.EmptyTypes,
            values, Expression.Constant(record.Parameters[0]), right);
    }

    public static bool GreaterThan(IOriginalAiConditionScalarValues values, uint key, int literal)
        => values.TryReadInteger(key, out int value) && value > literal;
    public static bool AtLeast(IOriginalAiConditionScalarValues values, uint key, int literal)
        => values.TryReadInteger(key, out int value) && value >= literal;
    public static bool EqualKeys(IOriginalAiConditionScalarValues values, uint leftKey, uint rightKey)
    {
        bool leftPresent = values.TryReadInteger(leftKey, out int left);
        bool rightPresent = values.TryReadInteger(rightKey, out int right);
        return leftPresent && rightPresent ? left == right : values.ObjectKeysEqual(leftKey, rightKey);
    }

    private static Expression Fold(ImmutableArray<OriginalAiConditionQuery> children, bool all,
        Func<OriginalAiConditionQuery, Expression> bind)
    {
        Expression expression = Expression.Constant(all);
        foreach (OriginalAiConditionQuery query in children)
            expression = all ? Expression.AndAlso(expression, Bind(query, bind)) : Expression.OrElse(expression, Bind(query, bind));
        return expression;
    }
    private static Expression Bind(OriginalAiConditionQuery query, Func<OriginalAiConditionQuery, Expression> bind)
    {
        Expression expression = bind(query);
        if (expression.Type != typeof(bool)) throw Error(query.Record.NodeIndex, query.Record.WordIndex, "Query binding is not bool");
        return query.Record.Negate ? Expression.Not(expression) : expression;
    }
    private static int Count(uint value, int node, int word, int maximum) => value <= maximum
        ? (int)value : throw Error(node, word, $"Count {value} exceeds the source storage bound {maximum}");
    private static InvalidDataException Error(int node, int word, string message)
        => new($"Original AI condition node {node}, argument word {word}: {message}.");
}
