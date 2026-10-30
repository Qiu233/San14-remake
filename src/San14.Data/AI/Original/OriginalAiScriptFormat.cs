using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace San14.Data.AI.Original;

public enum OriginalAiScriptBinaryOperation { Add, Subtract, Multiply, Divide }

// All values in this particular resource language are signed 32-bit integers.
public abstract record OriginalAiScriptExpression(int SourceOffset);
public sealed record OriginalAiScriptConstant(int SourceOffset, int Value)
    : OriginalAiScriptExpression(SourceOffset);
public sealed record OriginalAiScriptArgument(int SourceOffset, OriginalAiScriptExpression Index)
    : OriginalAiScriptExpression(SourceOffset);
public sealed record OriginalAiScriptBinary(int SourceOffset, OriginalAiScriptBinaryOperation Operation,
    OriginalAiScriptExpression Left, OriginalAiScriptExpression Right)
    : OriginalAiScriptExpression(SourceOffset);

public sealed record OriginalAiScript(int Id, int SourceOffset, int TerminatorOffset,
    int WordCount, OriginalAiScriptExpression Expression);

/// <summary>The seven integer inputs published by the actual CallScript node, per invocation.</summary>
public readonly record struct OriginalAiScriptArguments(int Argument0, int Argument1, int Argument2,
    int Argument3, int Argument4, int Argument5, int Argument6)
{
    public int Get(int index) => index switch
    {
        0 => Argument0, 1 => Argument1, 2 => Argument2, 3 => Argument3,
        4 => Argument4, 5 => Argument5, 6 => Argument6,
        _ => throw new ArgumentOutOfRangeException(nameof(index), index,
            "The original CallScript binding supplies seven integer arguments.")
    };
}

public sealed class OriginalAiScriptLibrary
{
    private readonly Lazy<CompiledOriginalAiScriptLibrary> _compiled;
    public ImmutableArray<OriginalAiScript> Scripts { get; }
    internal OriginalAiScriptLibrary(ImmutableArray<OriginalAiScript> scripts)
    {
        Scripts = scripts;
        _compiled = new Lazy<CompiledOriginalAiScriptLibrary>(CompileCore);
    }

    /// <summary>Call once when loading the resource; retain the returned immutable library.</summary>
    public CompiledOriginalAiScriptLibrary Compile() => _compiled.Value;

    private CompiledOriginalAiScriptLibrary CompileCore()
    {
        if (!RuntimeFeature.IsDynamicCodeSupported || !RuntimeFeature.IsDynamicCodeCompiled)
            throw new PlatformNotSupportedException("Original AI scripts require the configured JIT expression backend.");
        var compiled = ImmutableDictionary.CreateBuilder<int, Func<OriginalAiScriptArguments, int>>();
        foreach (OriginalAiScript script in Scripts)
        {
            ParameterExpression arguments = Expression.Parameter(typeof(OriginalAiScriptArguments), "arguments");
            compiled.Add(script.Id, Expression.Lambda<Func<OriginalAiScriptArguments, int>>(
                Lower(script.Expression, arguments), arguments).Compile());
        }
        return new CompiledOriginalAiScriptLibrary(compiled.ToImmutable());
    }

    private static Expression Lower(OriginalAiScriptExpression node, ParameterExpression arguments)
    {
        switch (node)
        {
            case OriginalAiScriptConstant constant: return Expression.Constant(constant.Value);
            case OriginalAiScriptArgument argument:
                return Expression.Call(arguments, nameof(OriginalAiScriptArguments.Get), Type.EmptyTypes,
                    Lower(argument.Index, arguments));
            case OriginalAiScriptBinary binary:
            {
                Expression left = Lower(binary.Left, arguments), right = Lower(binary.Right, arguments);
                // Add/Subtract/Multiply (without Checked) retain the source's low 32 bits.
                if (binary.Operation == OriginalAiScriptBinaryOperation.Add) return Expression.Add(left, right);
                if (binary.Operation == OriginalAiScriptBinaryOperation.Subtract) return Expression.Subtract(left, right);
                if (binary.Operation == OriginalAiScriptBinaryOperation.Multiply) return Expression.Multiply(left, right);
                if (binary.Operation != OriginalAiScriptBinaryOperation.Divide)
                    throw new InvalidDataException($"Unbound script operation at 0x{node.SourceOffset:X}.");
                // Source evaluates the divisor first, returns 1 at zero without evaluating the dividend,
                // and otherwise uses signed 64-bit division followed by low-32-bit truncation.
                ParameterExpression divisor = Expression.Variable(typeof(int), "divisor");
                return Expression.Block([divisor], Expression.Assign(divisor, right),
                    Expression.Condition(Expression.Equal(divisor, Expression.Constant(0)), Expression.Constant(1),
                        Expression.Convert(Expression.Divide(Expression.Convert(left, typeof(long)),
                            Expression.Convert(divisor, typeof(long))), typeof(int))));
            }
            default: throw new InvalidDataException($"Unbound script expression at 0x{node.SourceOffset:X}.");
        }
    }
}

public sealed class CompiledOriginalAiScriptLibrary
{
    private readonly ImmutableDictionary<int, Func<OriginalAiScriptArguments, int>> _scripts;
    internal CompiledOriginalAiScriptLibrary(ImmutableDictionary<int, Func<OriginalAiScriptArguments, int>> scripts)
        => _scripts = scripts;
    public IEnumerable<int> ScriptIds => _scripts.Keys;
    public Func<OriginalAiScriptArguments, int> Get(int id) => _scripts.TryGetValue(id, out var script)
        ? script : throw new KeyNotFoundException($"Original AI script {id} is not defined in this resource.");
    public int Evaluate(int id, OriginalAiScriptArguments arguments) => Get(id)(arguments);
}

public static class OriginalAiScriptFormat
{
    private const uint ScriptEnd = 0x40000014;
    private const uint LibraryEnd = 0x7FFFFFFF;
    // This is the capacity passed to the original script manager, not a sampled script count.
    private const int ScriptCapacity = 0x380;

    /// <summary>Reads the original normal param_s.bin container, not an executable or execution cache.</summary>
    public static OriginalAiScriptLibrary Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < 16 || (source.Length & 3) != 0)
            throw Error(0, "Truncated or unaligned script container");
        if (Word(source, 0) != 1) throw Error(0, "Expected the one-section param_s container");
        int start = CheckedOffset(Word(source, 4), 4);
        int size = CheckedOffset(Word(source, 8), 8);
        if (start < 12 || (start & 3) != 0 || size < 4 || (size & 3) != 0 ||
            (long)start + size != source.Length)
            throw Error(4, "Script section range does not match the container");

        int position = start;
        var scripts = ImmutableArray.CreateBuilder<OriginalAiScript>();
        var ids = new HashSet<int>();
        while (true)
        {
            int recordAt = position;
            uint rawId = Next(source, ref position);
            if (rawId == LibraryEnd)
            {
                if (position != source.Length) throw Error(position, "Bytes follow the library terminator");
                return new OriginalAiScriptLibrary(scripts.ToImmutable());
            }
            if (rawId >= ScriptCapacity) throw Error(recordAt, $"Script ID {rawId} exceeds manager capacity");
            int id = (int)rawId;
            if (!ids.Add(id)) throw Error(recordAt, $"Duplicate script ID {id}");
            var words = new List<(uint Value, int Offset)>();
            int endAt;
            while (true)
            {
                endAt = position;
                uint word = Next(source, ref position);
                if (word == ScriptEnd) break;
                if (words.Count == 79) throw Error(endAt, $"Script {id} exceeds the original 80-word buffer including terminator");
                words.Add((word, endAt));
            }
            if (words.Count == 0) throw Error(endAt, $"Script {id} has no expression");
            int cursor = 0;
            OriginalAiScriptExpression expression = ReadExpression(words, ref cursor, id);
            if (cursor != words.Count) throw Error(words[cursor].Offset, $"Script {id} has trailing expression words");
            scripts.Add(new(id, recordAt, endAt, words.Count, expression));
        }
    }

    private static OriginalAiScriptExpression ReadExpression(List<(uint Value, int Offset)> words,
        ref int cursor, int scriptId)
    {
        if (cursor == words.Count)
            throw Error(words[^1].Offset + 4, $"Script {scriptId} is missing an operand");
        (uint value, int at) = words[cursor++];
        // Bit 31 denotes a negative literal and takes precedence over both dispatch bits.
        if ((int)value < 0 || (value & 0x60000000) == 0)
            return new OriginalAiScriptConstant(at, unchecked((int)value));
        if ((value & 0x40000000) != 0)
        {
            OriginalAiScriptBinaryOperation operation = value switch
            {
                0x40000001 => OriginalAiScriptBinaryOperation.Add,
                0x40000002 => OriginalAiScriptBinaryOperation.Subtract,
                0x40000003 => OriginalAiScriptBinaryOperation.Multiply,
                0x40000004 => OriginalAiScriptBinaryOperation.Divide,
                _ => throw Error(at, $"Script {scriptId} has unbound operator 0x{value:X8}")
            };
            OriginalAiScriptExpression left = ReadExpression(words, ref cursor, scriptId);
            OriginalAiScriptExpression right = ReadExpression(words, ref cursor, scriptId);
            return new OriginalAiScriptBinary(at, operation, left, right);
        }
        if (value != 0x20000000)
            throw Error(at, $"Script {scriptId} has unbound host getter 0x{value:X8}");
        OriginalAiScriptExpression index = ReadExpression(words, ref cursor, scriptId);
        if (index is OriginalAiScriptConstant constant && constant.Value is < 0 or > 6)
            throw Error(index.SourceOffset, $"Script {scriptId} requests argument {constant.Value}, outside CallScript's seven inputs");
        return new OriginalAiScriptArgument(at, index);
    }

    private static uint Next(ReadOnlySpan<byte> source, ref int position)
    {
        if (position > source.Length - 4) throw Error(position, "Unexpected end of script resource");
        uint value = Word(source, position); position += 4; return value;
    }
    private static uint Word(ReadOnlySpan<byte> source, int at)
        => BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(at, 4));
    private static int CheckedOffset(uint value, int at) => value <= int.MaxValue
        ? (int)value : throw Error(at, "Section offset or length exceeds supported span size");
    private static InvalidDataException Error(int at, string message)
        => new($"param_s.bin at byte 0x{at:X}: {message}.");
}
