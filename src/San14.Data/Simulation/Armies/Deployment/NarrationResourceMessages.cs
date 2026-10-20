using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

internal interface INarrationCurrentContext
{
    int PersonValue(int argument, int field);
    string PersonText(int argument, int field);
    int Relationship(int field, int firstPersonId, int secondPersonId);
    // The actual current owner resolves the first pointer before evaluating
    // the second expression. Synthetic legacy contexts need no pointer owner.
    int Personality(int firstPersonId, Func<int> readTraitKey) => Relationship('L', firstPersonId, readTraitKey());
    string AreaText(int argument, int field);
    string CityText(int argument, int field) => throw new InvalidDataException("narration.city_argument");
    string ForceText(int argument, int field) => throw new InvalidDataException("narration.force_argument");
}

internal enum NarrationResourceFamily { Deployment, AreaDismissal, AreaAppointment }

internal sealed record NarrationResourceText(int RootMessageId, int MessageId,
    IReadOnlyList<NarrationMessageChoice> Choices, string Text, IReadOnlyList<string> Pages);

/// <summary>The finite deployment, region-handover and officer-speech dialect. It reads game
/// resources, never executable instructions. Expressions are parsed before
/// evaluation; unreached operands do not query current people or draw randoms.</summary>
internal sealed class NarrationResourceMessages
{
    private readonly MessageDocument _messages;
    private readonly Dictionary<int, Program> _programs = [];
    public NarrationResourceMessages(MessageDocument messages) => _messages = messages;

    public NarrationResourceText Resolve(INarrationCurrentContext people,
        Func<int, int> presentationRandom, NarrationResourceFamily family)
    {
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(presentationRandom);
        _prefix = family == NarrationResourceFamily.Deployment ? "deployment.narration" : "area-draft.handover";
        int root = family switch { NarrationResourceFamily.Deployment => 0x5668,
            NarrationResourceFamily.AreaDismissal => 0x573C, NarrationResourceFamily.AreaAppointment => 0x5742,
            _ => throw Failure("family") };
        int firstLeaf = root + 1;
        int lastLeaf = family switch { NarrationResourceFamily.Deployment => 0x567E,
            NarrationResourceFamily.AreaDismissal => 0x5741, _ => 0x5747 };
        return ExecuteMessage(people, presentationRandom, root, ReadTextFamily(root, family),
            id => id >= firstLeaf && id <= lastLeaf, false, null);
    }

    internal NarrationResourceText ResolveCity(INarrationCurrentContext people,
        Func<int, int> presentationRandom, int root)
    {
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(presentationRandom);
        _prefix = "city-domestic.handover";
        int last = root switch
        {
            0x5A80 or 0x5A88 or 0x5A90 or 0x5A98 => root + 7,
            0x5763 or 0x577B => root + 12,
            0x575D or 0x5775 => root + 5,
            _ => throw Failure("root")
        };
        var program = root is 0x575D or 0x5775 ? Get(root) : ReadVoiceText(root, 33);
        return ExecuteMessage(people, presentationRandom, root, program,
            id => id > root && id <= last, false, null);
    }

    internal string ResolveAssociationRemoval(INarrationCurrentContext people)
    {
        ArgumentNullException.ThrowIfNull(people);
        _prefix = "city-domestic.association";
        var program = Get(0x3452);
        if (program.Statements is not [Literal, Display { Expression: Force { Argument: 0, Field: '4' } },
            Literal, Display { Expression: Person { Argument: 0, Field: '3' } }, Literal])
            throw Failure("resource_profile");
        return ExecuteMessage(people, _ => throw Failure("random"), 0x3452, program,
            _ => false, true, null).Text;
    }

    internal string ResolvePersonAppearance(INarrationCurrentContext people,
        Func<int, int> presentationRandom, int root)
    {
        if (root is not (0x5D27 or 0x5D28 or 0x5D4F)) throw new InvalidDataException("person-appearance.text.root");
        _prefix = "person-appearance.text";
        bool previous = _appearance;
        _appearance = true;
        try
        {
            var program = root == 0x5D28 ? ReadVoiceText(root, 40) : Get(root);
            //Both voice40 alternatives are the exact same J5D29 program. Its
            //reached F2 operand still precedes that program; no availability is invented.
            if (root == 0x5D28) _ = people.PersonValue(0, '2');
            string text = ExecuteMessage(people, presentationRandom, root, program,
                _ => false, true, null).Text;
            if (text.Length > 338 || text.Any(c => c == '\0' || char.IsSurrogate(c)))
                throw Failure("complete_short_bmp_profile");
            return text;
        }
        finally { _appearance = previous; }
    }

    internal string ResolvePersonDeathInitial(INarrationCurrentContext people,
        Func<int, int> drawApp32)
    {
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(drawApp32);
        _prefix = "person-death.text";
        var program = ReadVoiceText(0x2A14, 46);
        //The actual F2 read still precedes the identical voice branches. This
        //does not invent availability or query App admission before formatting.
        _ = people.PersonValue(0, '2');
        string text = ExecuteMessage(people, drawApp32, 0x2A14, program,
            _ => false, true, null,
            id => id is >= 0x2A15 and <= 0x2A2C or >= 0x0097 and <= 0x00A7,
            DeathEscape, allowOpaqueEscapes: true).Text;
        //The existing complete short-BMP save domain, not a native buffer size.
        if (text.Length > 338 || text.Any(c => c == '\0' || char.IsSurrogate(c)))
            throw Failure("complete_short_bmp_profile");
        return text;

        string DeathEscape(int group, int code) => (group, code) switch
        {
            ('C', 'J') => "^02",
            ('E', 'K') => string.Empty,
            _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
        };
    }

    internal string ResolvePersonDeathSecond(INarrationCurrentContext people,
        Func<int, int> readInteger, Func<string> readString0, Func<int, int> drawApp32)
    {
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(readInteger);
        ArgumentNullException.ThrowIfNull(readString0);
        ArgumentNullException.ThrowIfNull(drawApp32);
        _prefix = "person-death.second-text";
        //This capability is local to2A2D, never cached under the shared id key.
        var parser = new Parser(ReadRecord(0x2A2D), _prefix, true, readInteger);
        var program = parser.ReadProgram();
        parser.RequireEnd();
        string text = ExecuteMessage(people, drawApp32, 0x2A2D, program,
            _ => false, true, readString0, _ => false, SecondEscape, true).Text;
        //The existing saved complete-short-BMP domain; never truncate.
        if (text.Length > 338 || text.Any(c => c == '\0' || char.IsSurrogate(c)))
            throw Failure("complete_short_bmp_profile");
        return text;

        string SecondEscape(int group, int code) => (group, code) switch
        {
            ('C', 'L') => "^05",
            //The parser consumed the fresh E parameter M at this statement.
            ('E', 'M') => string.Empty,
            _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
        };
    }

    private bool _appearance;
    private static bool AppearanceMessage(int id) => id is 0x5D27 or 0x5D28 or 0x5D4F or 0x017A or
        >= 0x017E and <= 0x0196 or >= 0x5D29 and <= 0x5D4E or 0x0097 or 0x00B8 or 0x0143 or
        0x0098 or 0x009C or 0x009D or 0x009E or 0x00B9 or 0x00CE or 0x00CF or 0x00D5 or 0x0144 or 0x0153 or
        0x00C6 or 0x00D0 or 0x00D2 or 0x00D3 or 0x00D4 or 0x00BF or 0x00D1 or 0x0119;

    internal NarrationResourceText ResolveOfficerSpeech(INarrationCurrentContext people,
        Func<int,int> presentationRandom, int root, Func<string> string0)
    {
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(presentationRandom);
        ArgumentNullException.ThrowIfNull(string0);
        _prefix = "area-draft.speech";
        if (root is not (0x56D9 or 0x56DE or 0x56E3 or 0x56E8 or 0x56ED or 0x56F2 or 0x573B or
            0x56FB or 0x5700 or 0x5705 or 0x570A or 0x570F or 0x5714 or 0x571A or 0x5720 or
            0x5726 or 0x572C or 0x5731 or 0x5736)) throw Failure("root");
        return ExecuteMessage(people, presentationRandom, root, Get(root),
            id => id is >= 0x56DA and <= 0x573A && id is not (0x56DE or 0x56E3 or 0x56E8 or
                0x56ED or 0x56F2 or 0x56FB or 0x5700 or 0x5705 or 0x570A or 0x570F or 0x5714 or
                0x571A or 0x5720 or 0x5726 or 0x572C or 0x5731 or 0x5736), true, string0);
    }

    private NarrationResourceText ExecuteMessage(INarrationCurrentContext people,
        Func<int,int> presentationRandom, int root, Program rootProgram, Func<int,bool> isLeaf,
        bool singleParagraph, Func<string>? string0,
        Func<int, bool>? referenceAllowed = null, Func<int, int, string>? escapeText = null,
        bool allowOpaqueEscapes = false)
    {
        int leaf = -1;
        var choices = new List<NarrationMessageChoice>();
        var text = new StringBuilder();
        int random(int bound)
        {
            if (bound < 2) return 0;
            int result = presentationRandom(bound);
            if ((uint)result >= (uint)bound) throw Failure("presentation_random_range");
            return result;
        }
        Execute(rootProgram, root, 0);
        if (!singleParagraph && (leaf < 0 || text.Length == 0)) throw Failure("empty_or_unproved_variant");
        string complete = text.ToString();
        if (singleParagraph) return new(root, leaf, choices.AsReadOnly(), complete, Array.Empty<string>());
        var pages = new List<string>();
        int start = 0, lines = 0;
        for (int index = 0; index < complete.Length; index++)
            if (complete[index] == '\n' && ++lines == 3)
            { pages.Add(complete[start..index]); start = index + 1; lines = 0; }
        //845660 keeps a final nonempty paragraph; an exact trailing separator
        //does not create an additional invisible page.
        if (start < complete.Length) pages.Add(complete[start..]);
        return new(root, leaf, choices.AsReadOnly(), complete, pages.AsReadOnly());

        void Execute(Program program, int messageId, int depth)
        {
            if (depth > 64) throw Failure("reference_depth");
            foreach (var statement in program.Statements)
            {
                switch (statement)
                {
                    case Literal literal: text.Append(literal.Text); break;
                    case Display display: text.Append(DisplayValue(display.Expression)); break;
                    case OpaqueEscape escape:
                        if (escapeText is null) throw Failure($"unresolved_escape_{escape.Group:X2}_{escape.Code:X2}");
                        text.Append(escapeText(escape.Group, escape.Code)); break;
                    case Reference reference:
                        if (referenceAllowed is not null && !referenceAllowed(reference.Id))
                            throw Failure($"unresolved_reference_{reference.Id:X4}");
                        if (_appearance && !AppearanceMessage(reference.Id))
                            throw Failure($"unresolved_reference_{reference.Id:X4}");
                        if (isLeaf(reference.Id)) leaf = reference.Id;
                        Execute(Get(reference.Id, allowOpaqueEscapes), reference.Id, depth + 1);
                        if (reference.Jump) return;
                        break;
                    case Conditional conditional:
                        if (Test(conditional.Condition)) { Execute(conditional.Body, messageId, depth + 1); return; }
                        break;
                    case Choice choice:
                        int selected = Eval(choice.Expression);
                        selected = Math.Min(selected, choice.Candidates.Length - 1);
                        if (selected < 0) throw Failure("negative_choice");
                        choices.Add(new(messageId, choice.Candidates.Length, selected));
                        Execute(choice.Candidates[selected], messageId, depth + 1); return;
                    default: throw Failure("statement");
                }
            }
        }
        int Eval(Expression expression) => expression switch
        {
            Number n => n.Value,
            IntegerArgument argument => argument.Read(argument.Index),
            Person p => people.PersonValue(p.Argument, p.Field),
            Function f when f.Field == '2' => Percentage(Eval(f.Arguments[0])),
            Function f when f.Field == '3' => random(Eval(f.Arguments[0])),
            Function f when f.Field == 'L' => people.Personality(Eval(f.Arguments[0]), () => Eval(f.Arguments[1])),
            Function f => people.Relationship(f.Field, Eval(f.Arguments[0]), f.Arguments.Length > 1 ? Eval(f.Arguments[1]) : 0),
            _ => throw Failure("expression")
        };
        int Percentage(int percent) => random(100) < percent ? 1 : 0;
        string DisplayValue(Expression expression) => expression switch
        { Person p => people.PersonText(p.Argument, p.Field), Area a => people.AreaText(a.Argument, a.Field),
          City c => people.CityText(c.Argument, c.Field),
          Force f => people.ForceText(f.Argument, f.Field),
          StringArgument => string0?.Invoke() ?? throw Failure("string_argument"),
          _ => Eval(expression).ToString(CultureInfo.InvariantCulture) };
        bool Test(Condition condition)
        {
            bool result = Compare(condition.Terms[0]);
            for (int index = 1; index < condition.Terms.Length; index++)
            {
                var comparison = condition.Terms[index];
                //The native evaluator folds in resource order, without
                //conventional && precedence over ||.
                if (condition.Operators[index - 1] == '&') result = result && Compare(comparison);
                else result = result || Compare(comparison);
            }
            return result;
        }
        bool Compare(Comparison comparison)
        {
            int left = Eval(comparison.Left);
            if (comparison.Right is null) return left != 0;
            int right = Eval(comparison.Right);
            return comparison.Operator switch
            { '=' => left == right, '!' => left != right, '<' => left < right, '>' => left > right,
              '{' => left <= right, '}' => left >= right, _ => throw Failure("comparison") };
        }
    }

    private string _prefix = "deployment.narration";
    private InvalidDataException Failure(string field) => new(_prefix + "." + field);

    private Program ReadTextFamily(int root, NarrationResourceFamily family)
    {
        //The root's audio-availability branches are text-identical. Prove
        //this against the actual current resource rather than pretending
        //that unknown voice availability has a known boolean value.
        if (family == NarrationResourceFamily.AreaDismissal) return Get(root);
        return ReadVoiceText(root, family == NarrationResourceFamily.Deployment ? (byte)37 : (byte)33);
    }

    private Program ReadVoiceText(int root, byte voice)
    {
        byte[] bytes = ReadRecord(root);
        ReadOnlySpan<byte> voice37 = [5,5,4,2,0xC8,0x34,2,(byte)'F',(byte)'2',(byte)'%',
            (byte)('0' + voice / 10), (byte)('0' + voice % 10)];
        int remaining = bytes.Length - voice37.Length - 3;
        if (remaining < 0 || remaining % 2 != 0 || !bytes.AsSpan(0, Math.Min(bytes.Length,voice37.Length)).SequenceEqual(voice37))
            throw Failure("resource.voice_text_profile");
        int count = remaining / 2, split = voice37.Length + count;
        if (!bytes.AsSpan(split,3).SequenceEqual(new byte[]{5,5,8}) ||
            !bytes.AsSpan(voice37.Length,count).SequenceEqual(bytes.AsSpan(split+3,count)))
            throw Failure("resource.voice_text_branches_differ");
        var parser = new Parser(bytes.AsSpan(voice37.Length,count).ToArray(), _prefix);
        var program = parser.ReadProgram(); parser.RequireEnd();
        return program;
    }

    private Program Get(int id, bool allowOpaqueEscapes = false)
    {
        if (_programs.TryGetValue(id, out var program)) return program;
        var parser = new Parser(ReadRecord(id), _prefix, _appearance || allowOpaqueEscapes);
        program = parser.ReadProgram();
        parser.RequireEnd();
        _programs.Add(id, program);
        return program;
    }
    private byte[] ReadRecord(int id)
    {
        int section = id / 10000, index = id % 10000;
        if ((uint)section >= (uint)_messages.Sections.Count || (uint)index >= (uint)_messages.Sections[section].Records.Count)
            throw Failure("message_reference");
        return _messages.Sections[section].Records[index].RawBytes;
    }

    private sealed record Program(Statement[] Statements);
    private abstract record Statement;
    private sealed record Literal(string Text) : Statement;
    private sealed record OpaqueEscape(int Group, int Code) : Statement;
    private sealed record Display(Expression Expression) : Statement;
    private sealed record Reference(int Id, bool Jump) : Statement;
    private sealed record Conditional(Condition Condition, Program Body) : Statement;
    private sealed record Choice(Expression Expression, Program[] Candidates) : Statement;
    private abstract record Expression;
    private sealed record Number(int Value) : Expression;
    private sealed record IntegerArgument(int Index, Func<int, int> Read) : Expression;
    private sealed record Person(int Argument, int Field) : Expression;
    private sealed record Area(int Argument, int Field) : Expression;
    private sealed record City(int Argument, int Field) : Expression;
    private sealed record Force(int Argument, int Field) : Expression;
    private sealed record StringArgument : Expression;
    private sealed record Function(int Field, Expression[] Arguments) : Expression;
    private sealed record Comparison(Expression Left, int Operator, Expression? Right);
    private sealed record Condition(Comparison[] Terms, int[] Operators);

    private sealed class Parser(byte[] bytes, string prefix, bool allowOpaqueEscapes = false,
        Func<int, int>? readInteger = null)
    {
        private int _at;
        public Program ReadProgram()
        {
            var statements = new List<Statement>();
            while (_at < bytes.Length)
            {
                if (Has(5, 5, 5)) { _at += 3; return new(statements.ToArray()); }
                if (Has(5, 5, 8)) { _at += 3; continue; }
                if (Has(5, 5, 4))
                { _at += 3; var condition = ReadCondition(); statements.Add(new Conditional(condition, ReadProgram())); continue; }
                if (Has(7, 7, 1))
                {
                    _at += 3; int start = _at;
                    while (!Has(7, 7, 2)) { _at += 2; if (_at > bytes.Length - 3) throw Bad("literal_end"); }
                    statements.Add(new Literal(new UnicodeEncoding(false, false, true).GetString(bytes, start, _at - start)));
                    _at += 3; continue;
                }
                if (Peek() == 0x1B)
                {
                    _at++;
                    int group = Read();
                    int colour = Read();
                    if (allowOpaqueEscapes && (group == 'C' && colour is 'J' or 'L' ||
                        group == 'E' && colour is 'K' or 'M'))
                    { statements.Add(new OpaqueEscape(group, colour)); continue; }
                    if (group != 'C') throw Bad("escape");
                    statements.Add(new Literal(colour switch { '4' => "^05", 'Z' => "^00", _ => throw Bad("escape_colour") }));
                    continue;
                }
                int opcode = Read();
                if (opcode == 2) { _at--; statements.Add(new Display(ReadExpression())); continue; }
                if (opcode != 1) throw Bad("opcode");
                int command = Read();
                if (command is 'J' or 'C')
                {
                    if (_at > bytes.Length - 4) throw Bad("reference");
                    int id = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(_at, 4)); _at += 4;
                    statements.Add(new Reference(id, command == 'J')); continue;
                }
                if (command == 'S')
                {
                    if (Read() != '%') throw Bad("choice_count");
                    int count = ReadDigits();
                    if (count is not (2 or 3 or 4 or 5 or 6 or 7 or 8)) throw Bad("choice_arity");
                    var choice = ReadExpression(); var candidates = new Program[count];
                    for (int index = 0; index < count; index++) candidates[index] = ReadProgram();
                    statements.Add(new Choice(choice, candidates));
                    //Each candidate returns to its caller; no extra return
                    //is appended after the final candidate in these records.
                    return new(statements.ToArray());
                }
                throw Bad("command");
            }
            throw Bad("return");
        }
        private Condition ReadCondition()
        {
            var terms = new List<Comparison>(); var operators = new List<int>();
            do
            {
                var left = ReadExpression(); int comparison = 0; Expression? right = null;
                if (Peek() == 4) { _at++; comparison = Read(); right = ReadExpression(); }
                terms.Add(new(left, comparison, right));
                if (Peek() != 6) return new(terms.ToArray(), operators.ToArray());
                _at++; int boolean = Read();
                if (boolean is not ('&' or '|')) throw Bad("boolean");
                operators.Add(boolean);
            } while (true);
        }
        private Expression ReadExpression()
        {
            if (Peek() == '%') { _at++; return new Number(ReadDigits()); }
            if (Peek() is >= '0' and <= '9') return new Number(ReadDigits());
            if (Read() != 2) throw Bad("expression_tag");
            int selector = Read();
            if (readInteger is not null && selector is '2' or '3')
                return new IntegerArgument(selector - '2', readInteger);
            if (selector is 'F' or 'G')
            {
                int field = Peek() >= 0x20 ? Read() : 0;
                return new Person(selector - 'F', field);
            }
            if (selector == 'x')
            { int field = Peek() >= 0x20 ? Read() : 0; return new City(0, field); }
            if (selector == 'P')
            { int field = Peek() >= 0x20 ? Read() : 0; return new Force(0, field); }
            if (selector == '<') return new StringArgument();
            if (selector == 0x96)
            { int field = Peek() >= 0x20 ? Read() : 0; return new Area(0, field); }
            if (selector != 0xC8) throw Bad("selector");
            int function = Read();
            int arity = function is '2' or '3' or '<' ? 1 : function is >= '=' and <= 'L' ? 2 : throw Bad("function");
            var arguments = new Expression[arity];
            for (int i = 0; i < arity; i++) arguments[i] = ReadExpression();
            return new Function(function, arguments);
        }
        private int ReadDigits()
        {
            int start = _at, number = 0;
            while (Peek() is >= '0' and <= '9') number = checked(number * 10 + Read() - '0');
            if (start == _at) throw Bad("number");
            return number;
        }
        private bool Has(int a, int b, int c) => _at <= bytes.Length - 3 && bytes[_at] == a && bytes[_at + 1] == b && bytes[_at + 2] == c;
        private int Peek() => _at < bytes.Length ? bytes[_at] : -1;
        private int Read() => _at < bytes.Length ? bytes[_at++] : throw Bad("end");
        public void RequireEnd() { if (_at != bytes.Length) throw Bad("trailing"); }
        private InvalidDataException Bad(string field) => new($"{prefix}.resource.{field}@{_at}");
    }
}

