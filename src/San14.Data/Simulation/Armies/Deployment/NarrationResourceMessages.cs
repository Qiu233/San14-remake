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
    int ArmyValue(int argument, int field) => throw new InvalidDataException("narration.army_argument");
    string ArmyText(int argument, int field) => throw new InvalidDataException("narration.army_argument");
    int StringValue(int argument, int field) => throw new InvalidDataException("narration.string_argument");
    string StringText(int argument, int field) => throw new InvalidDataException("narration.string_argument");
    int IntegerValue(int argument) => throw new InvalidDataException("narration.integer_argument");
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

    private static readonly HashSet<int> LifespanNotificationMessages =
    [
        0x0097, 0x0098, 0x0099, 0x009A, 0x009B, 0x009C, 0x009D, 0x009E, 0x009F, 0x00A0, 0x00A1, 0x00A2,
        0x00A3, 0x00A4, 0x00A5, 0x00A6, 0x00A7, 0x00B8, 0x00B9, 0x00BA, 0x00BB, 0x00BC, 0x00BD, 0x00BE,
        0x00BF, 0x00C0, 0x00C1, 0x00C2, 0x00C3, 0x00C4, 0x00C5, 0x00C6, 0x00C7, 0x00C8, 0x00C9, 0x00CA,
        0x00CB, 0x00CC, 0x00CD, 0x00CE, 0x00CF, 0x00D0, 0x00D1, 0x00D2, 0x00D3, 0x00D4, 0x00D5, 0x00D7,
        0x00D8, 0x00D9, 0x00DA, 0x00DB, 0x00DC, 0x00DD, 0x00DE, 0x00DF, 0x00E0, 0x00E1, 0x00E2, 0x00E3,
        0x00E4, 0x00E5, 0x00E6, 0x00E7, 0x00E8, 0x00E9, 0x00EA, 0x00EB, 0x00EC, 0x00ED, 0x00EE, 0x00EF,
        0x00F0, 0x00F1, 0x00F2, 0x00F3, 0x00F4, 0x00F5, 0x00F6, 0x00F7, 0x00F8, 0x00F9, 0x00FA, 0x00FB,
        0x00FC, 0x00FD, 0x00FE, 0x00FF, 0x0100, 0x0101, 0x0119, 0x5D50, 0x5D51, 0x5D52, 0x5D53, 0x5D54,
        0x5D55, 0x5D56, 0x5D57, 0x5D58, 0x5D59, 0x5D5A, 0x5D5B, 0x5D5C, 0x5D5D, 0x5D5E, 0x5D5F, 0x5D60,
        0x5D61, 0x5D62, 0x5D63, 0x5D64, 0x5D65, 0x5D66, 0x5D67, 0x5D68, 0x5D69, 0x5D6A, 0x5D6B, 0x5D6C,
        0x5D6D, 0x5D6E, 0x5D6F, 0x5D70, 0x5D71, 0x5D72, 0x5D73, 0x5D74, 0x5D75, 0x5D76, 0x5D77, 0x5D78,
        0x5D79, 0x5D7A, 0x5D7B, 0x5D7C, 0x5D7D, 0x5D7E,
    ];
    internal string ResolvePersonLifespanNotification(INarrationCurrentContext people, Func<int, int> drawApp32)
    {
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(drawApp32);
        _prefix = "annual-person-lifespan-notification.text";
        string text = ExecuteMessage(people, drawApp32, 0x5D50, Get(0x5D50, true),
            _ => false, true, null, LifespanNotificationMessages.Contains,
            (group, code) => (group, code) switch
            {
                ('C', 'J') => "^02",
                ('E', 'K') => string.Empty,
                _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
            }, true).Text;
        // Existing complete short-BMP save support; no native buffer claim or truncation.
        if (text.Length > 338 || text.Any(c => c == '\0' || char.IsSurrogate(c)))
            throw Failure("complete_short_bmp_profile");
        return text;
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

    private static readonly HashSet<int> ReportMessages =
    [
        0x0600, 0x0601, 0x0602, 0x0603, 0x0604, 0x0605, 0x0606, 0x0607, 0x0608, 0x0609,
        0x060A, 0x2A3B, 0x3225, 0x3228, 0x3229, 0x322D, 0x3239, 0x323A, 0x3452, 0x349D, 0x53D4,
        0x53D5, 0x53D6, 0x53D7, 0x53D8, 0x53D9, 0x53DA, 0x5F58, 0x5F6A, 0x5F6D, 0x5F7B,
        0x5F7C, 0x5F7D, 0x5F80, 0xC356, 0xC357, 0xC358, 0xC359, 0xC35A, 0xC360, 0xC96A,
        0xC96B, 0xC96F,
    ];
    private bool _report;
    private readonly Dictionary<int, Program> _reportPrograms = [];
    internal string ResolveHistoryReport(INarrationCurrentContext context, Func<int,int> drawApp32, int root)
    {
        if (root is not (0x5F6A or 0x5F58 or 0x5F6D or 0x5F80 or 0xC96A or 0xC96B or
            0xC356 or 0xC357 or 0xC358 or 0xC359 or 0xC35A or 0xC96F or 0xC360 or
            0x3228 or 0x3229 or 0x3225 or 0x322D or 0x3239 or 0x323A or
            0x5F7B or 0x5F7C or 0x5F7D or 0x349D or 0x53D4 or 0x3452 or 0x2A3B))
            throw Failure("history_report_root");
        _prefix = "campaign-history.text";
        bool previous = _report; _report = true;
        try
        {
            string text = ExecuteMessage(context, drawApp32, root, Get(root), _ => false, true, null, ReportMessages.Contains,
                escapeText: (group, code) => (group, code) switch
                { ('C','J') => "^02", ('C','L') => "^05", ('E','K') or ('E','M') => string.Empty,
                  _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}") }, allowOpaqueEscapes: true).Text;
            if (text.Length > 338 || text.Any(c => c == '\0' || char.IsSurrogate(c)))
                throw Failure("complete_short_bmp_profile");
            return text;
        }
        finally { _report = previous; }
    }

    private bool _history;
    private readonly Dictionary<int, Program> _historyPrograms = [];
    private static readonly HashSet<int> HistoryMessages =
    [
        0x0097, 0x0098, 0x0099, 0x009A, 0x009B, 0x009C, 0x009D, 0x009E, 0x009F, 0x00A0, 0x00A1, 0x00A2,
        0x00A3, 0x00A4, 0x00A5, 0x00A6, 0x00A7, 0x00B8, 0x00B9, 0x00BA, 0x00BB, 0x00BC, 0x00BD, 0x00BE,
        0x00BF, 0x00C0, 0x00C1, 0x00C2, 0x00C3, 0x00C4, 0x00C5, 0x00C6, 0x00C7, 0x00C8, 0x00C9, 0x00CA,
        0x00CB, 0x00CC, 0x00CD, 0x00CE, 0x00CF, 0x00D0, 0x00D1, 0x00D2, 0x00D3, 0x00D4, 0x00D5, 0x00D7,
        0x00D8, 0x00D9, 0x00DA, 0x00DB, 0x00DC, 0x00DD, 0x00DE, 0x00DF, 0x00E0, 0x00E1, 0x00E2, 0x00E3,
        0x00E4, 0x00E5, 0x00E6, 0x00E7, 0x00E8, 0x00E9, 0x00EA, 0x00EB, 0x00EC, 0x00ED, 0x00EE, 0x00EF,
        0x00F0, 0x00F1, 0x00F2, 0x00F3, 0x00F4, 0x00F5, 0x00F6, 0x00F7, 0x00F8, 0x00F9, 0x00FA, 0x00FB,
        0x00FC, 0x00FD, 0x00FE, 0x00FF, 0x0100, 0x0101, 0x0102, 0x0103, 0x0104, 0x0105, 0x0106, 0x0107,
        0x0108, 0x0109, 0x010A, 0x010B, 0x010C, 0x010D, 0x010E, 0x010F, 0x0110, 0x0111, 0x0112, 0x0113,
        0x0114, 0x0115, 0x0116, 0x0117, 0x0118, 0x0119, 0x011A, 0x011B, 0x011C, 0x011D, 0x011E, 0x011F,
        0x0120, 0x0121, 0x0122, 0x0123, 0x0124, 0x0125, 0x0127, 0x0128, 0x017C, 0x017E, 0x017F, 0x0180,
        0x0181, 0x0182, 0x0183, 0x0184, 0x0185, 0x0186, 0x0187, 0x0188, 0x0189, 0x018A, 0x018B, 0x018C,
        0x018D, 0x018E, 0x018F, 0x0190, 0x0191, 0x0192, 0x0193, 0x0194, 0x0195, 0x0196, 0x01F3, 0x01F4,
        0x01F5, 0x01F6, 0x01F7, 0x01F8, 0x01F9, 0x01FA, 0x01FB, 0x01FC, 0x01FD, 0x01FE, 0x01FF, 0x0200,
        0x0201, 0x0202, 0x0203, 0x0204, 0x0205, 0x0206, 0x0207, 0x0209, 0x020A, 0x020B, 0x020E, 0x020F,
        0x0210, 0x0211, 0x0212, 0x0213, 0x0214, 0x0215, 0x0216, 0x0217, 0x0218, 0x0219, 0x021A, 0x021B,
        0x021C, 0x021D, 0x021E, 0x021F, 0x0220, 0x0221, 0x0222, 0x0223, 0x0224, 0x0225, 0x0226, 0x0227,
        0x0228, 0x0229, 0x022A, 0x022B, 0x022C, 0x022F, 0x0230, 0x0231, 0x0232, 0x0233, 0x0234, 0x0235,
        0x0236, 0x0237, 0x2A2E, 0x2A32, 0x528F, 0x5290, 0x5291, 0x5293, 0x5294, 0x5296, 0x5297, 0x5299,
        0x529A, 0x529C, 0x529D, 0x529E, 0x529F, 0x52A0, 0x52A1, 0x52A2, 0x52A3, 0x52A4, 0x52A5, 0x52A6,
        0x52A7, 0x52A8, 0x52A9,
    ];

    internal string ResolvePersonDeathHistory(INarrationCurrentContext people,
        Func<int, int> drawApp32, int root)
    {
        if (root is not (0x2A32 or 0x2A2E or 0x528F))
            throw new InvalidDataException("person-death.history.root");
        _prefix = "person-death.history-text";
        bool previous = _history;
        _history = true;
        try
        {
            string text = ExecuteMessage(people, drawApp32, root, Get(root),
                _ => false, true, null, HistoryMessages.Contains).Text;
            if (text.Length > 338 || text.Any(c => c == '\0' || char.IsSurrogate(c)))
                throw Failure("complete_short_bmp_profile");
            return text;
        }
        finally { _history = previous; }
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
            ReportInteger argument => people.IntegerValue(argument.Index),
            Person p => people.PersonValue(p.Argument, p.Field),
            Army a => people.ArmyValue(a.Argument, a.Field),
            StringArgument a => people.StringValue(a.Argument, a.Field),
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
          Army a => people.ArmyText(a.Argument, a.Field),
          StringArgument a => _report ? people.StringText(a.Argument, a.Field) :
              string0?.Invoke() ?? throw Failure("string_argument"),
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
        var cache = _report ? _reportPrograms : _history ? _historyPrograms : _programs;
        if (cache.TryGetValue(id, out var program)) return program;
        var parser = new Parser(ReadRecord(id), _prefix, _appearance || _report || allowOpaqueEscapes,
            fourPersonArguments: _history || _report, reportArguments: _report);
        program = parser.ReadProgram();
        parser.RequireEnd();
        cache.Add(id, program);
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
    private sealed record ReportInteger(int Index) : Expression;
    private sealed record Person(int Argument, int Field) : Expression;
    private sealed record Area(int Argument, int Field) : Expression;
    private sealed record City(int Argument, int Field) : Expression;
    private sealed record Force(int Argument, int Field) : Expression;
    private sealed record Army(int Argument, int Field) : Expression;
    private sealed record StringArgument(int Argument = 0, int Field = 0) : Expression;
    private sealed record Function(int Field, Expression[] Arguments) : Expression;
    private sealed record Comparison(Expression Left, int Operator, Expression? Right);
    private sealed record Condition(Comparison[] Terms, int[] Operators);

    private sealed class Parser(byte[] bytes, string prefix, bool allowOpaqueEscapes = false,
        Func<int, int>? readInteger = null, bool fourPersonArguments = false, bool reportArguments = false)
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
            if (reportArguments && selector is >= '2' and <= ';') return new ReportInteger(selector - '2');
            if (readInteger is not null && selector is '2' or '3')
                return new IntegerArgument(selector - '2', readInteger);
            if (selector is 'F' or 'G' || fourPersonArguments && selector is 'H' or 'I')
            {
                int field = Peek() >= 0x20 ? Read() : 0;
                return new Person(selector - 'F', field);
            }
            if (selector == 'x')
            { int field = Peek() >= 0x20 ? Read() : 0; return new City(0, field); }
            if (selector == 'P')
            { int field = Peek() >= 0x20 ? Read() : 0; return new Force(0, field); }
            if (reportArguments && selector is 'n' or 'o')
            { int field = Peek() >= 0x20 ? Read() : 0; return new Army(selector - 'n', field); }
            if (selector == '<' || reportArguments && selector is '=' or '>')
            { int field = reportArguments && Peek() >= 0x20 ? Read() : 0;
              return new StringArgument(selector - '<', field); }
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

