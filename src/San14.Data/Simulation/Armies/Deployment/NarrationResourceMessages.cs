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
    int CityValue(int argument, int field) => throw new InvalidDataException("narration.city_numeric_argument");
    int BaseArgumentValidity(int raw) => throw new InvalidDataException("narration.base_argument_validity");
    string CityText(int argument, int field) => throw new InvalidDataException("narration.city_argument");
    string ForceText(int argument, int field) => throw new InvalidDataException("narration.force_argument");
    int ForceValue(int argument, int field) => throw new InvalidDataException("narration.force_numeric_argument");
    string DistrictText(int argument, int field) => throw new InvalidDataException("narration.district_argument");
}

internal enum NarrationResourceFamily { Deployment, AreaDismissal, AreaAppointment }

internal sealed record NarrationResourceText(int RootMessageId, int MessageId,
    IReadOnlyList<NarrationMessageChoice> Choices, string Text, IReadOnlyList<string> Pages);

/// <summary>The finite deployment, region-handover and officer-speech dialect. It reads game
/// resources, never executable instructions. Expressions are parsed before
/// evaluation; unreached operands do not query current people or draw randoms.</summary>
internal sealed partial class NarrationResourceMessages
{
    private readonly MessageDocument _messages;
    private readonly Dictionary<int, Program> _programs = [];
    public NarrationResourceMessages(MessageDocument messages) => _messages = messages;

    //8CF1F0 clears the formatter arguments and reads one selected explanation.
    //Use the ordinary resource parser/evaluator, including its global message
    //section addressing, without exposing a world or a presentation sampler.
    internal string ResolveInformationExplanation(int root)
    {
        _prefix = "information.destiny.explanation";
        if (root is < 0x34CD or > 0x34EB) throw Failure("message");
        return ExecuteMessage(InformationExplanationArguments.Instance,
            _ => throw Failure("random_argument"), root, Get(root, true), _ => false, true, null,
            id => id is >= 0x34CD and <= 0x34EB,
            (group, code) => (group, code) switch
            { ('C', 'J') => "^02", ('C', 'L') => "^05", _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}") }, true).Text;
    }

    private sealed class InformationExplanationArguments : INarrationCurrentContext
    {
        internal static readonly InformationExplanationArguments Instance = new();
        private static InvalidDataException Missing() => new("information.destiny.explanation_argument");
        public int PersonValue(int argument, int field) => throw Missing();
        public string PersonText(int argument, int field) => throw Missing();
        public int Relationship(int field, int firstPersonId, int secondPersonId) => throw Missing();
        public string AreaText(int argument, int field) => throw Missing();
    }

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

    internal NarrationResourceText ResolveCampaignHandover(INarrationCurrentContext people,
        Func<int, int> presentationRandom, string provinceName)
    {
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(presentationRandom);
        ArgumentNullException.ThrowIfNull(provinceName);
        _prefix = "campaign.expedition.narration";
        bool previous = _report; _report = true;
        try
        {
            //The selected TC/SC5810 resources have identical Voice37 text
            //arms. Preserve the reached Person lookup before the shared body.
            var program = ReadVoiceText(0x5810, 37);
            _ = people.PersonValue(0, '2');
            return ExecuteMessage(new CampaignProvinceNarration(people, provinceName), presentationRandom,
                0x5810, program, id => id is >= 0x5811 and <= 0x5826, true, null,
                id => id is >= 0x5811 and <= 0x5826 || TalentReportMessages.Contains(id),
                (group, code) => (group, code) switch
                {
                    ('C', 'J') => "^02", ('C', 'L') => "^05", ('E', 'K') or ('E', 'M') => string.Empty,
                    _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
                }, true);
        }
        finally { _report = previous; }
    }

    internal string ResolveCampaignConfirmation(INarrationCurrentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _prefix = "campaign.expedition.confirmation";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, _ => throw Failure("unexpected_random"), 0x580F,
                Get(0x580F, true), _ => false, true, null, _ => false,
                (group, code) => (group, code) switch
                {
                    ('C', 'J') => "^02", ('C', 'L') => "^05", ('E', 'K') or ('E', 'M') => string.Empty,
                    _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
                }, true).Text;
        }
        finally { _report = previous; }
    }

    private sealed class CampaignProvinceNarration(INarrationCurrentContext people, string name) : INarrationCurrentContext
    {
        public int PersonValue(int a, int f) => people.PersonValue(a, f);
        public string PersonText(int a, int f) => people.PersonText(a, f);
        public int Relationship(int f, int a, int b) => people.Relationship(f, a, b);
        public int Personality(int a, Func<int> f) => people.Personality(a, f);
        public string AreaText(int a, int f) => people.AreaText(a, f);
        public string StringText(int a, int f) => a == 0 && f is 0 or '2'
            ? CityDomesticMessageFormatting.FormatAbilityField(name)
            : a == 0 && f == '3' ? string.Empty : people.StringText(a, f);
        public int StringValue(int a, int f) => a == 0 && f is 0 or '2' or '3'
            ? f == '3' ? name.Length : CityDomesticMessageFormatting.FormatAbilityField(name).Length
            : people.StringValue(a, f);
    }

    internal NarrationResourceText ResolveTransport(INarrationCurrentContext people,
        Func<int, int> presentationRandom, int root)
    {
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(presentationRandom);
        if (root is not (0x57AD or 0x57BA)) throw new InvalidDataException("transport.narration.root");
        _prefix = "transport.narration";
        int first = root + 1, last = root + 12;
        // Both original voice38 branches contain the same complete text.
        // This proves text independently; it does not invent voice availability.
        return ExecuteMessage(people, presentationRandom, root, ReadVoiceText(root, 38),
            id => id >= first && id <= last, false, null,
            id => id >= first && id <= last || id is >= 0x0097 and <= 0x00A7);
    }

    internal NarrationResourceText ResolveCity(INarrationCurrentContext people,
        Func<int, int> presentationRandom, int root, bool executeVoiceRequest = false)
    {
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(presentationRandom);
        _prefix = "city-domestic.handover";
        int last = root switch
        {
            0x5A64 or 0x5A6C or 0x5A80 or 0x5A88 or 0x5A90 or 0x5A98 => root + 7,
            0x5763 or 0x577B => root + 12,
            0x575D or 0x5775 => root + 5,
            _ => throw Failure("root")
        };
        var program = (root is 0x575D or 0x5775) || executeVoiceRequest && (root is 0x5A64 or 0x5A6C)
            ? Get(root) : ReadVoiceText(root, (byte)(root is 0x5A64 or 0x5A6C ? 42 : 33));
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

    //Exact TC/SC normal Game resource closure for the actual talent roots.
    //No other report origin can enter this capability.
    private static readonly HashSet<int> TalentReportMessages =
    [
        //Office submission announcements use only the actual submitting Force.
        0x597C, 0x597D,
        //Policy-level announcement/confirmation and actual doctrine reply
        //leaves. Their only shared calls rejoin the existing0097 family.
        0x57EE, 0x3163, 0x3162, 0x3155, 0x3156, 0x3157, 0x3158, 0x3159,
        0x315A, 0x315B, 0x315C, 0x315D, 0x315E, 0x315F, 0x3160, 0x3161,
        //Normal scenery-discovery speech and its exact TC/SC leaves; these
        //call only the shared0097 family already admitted below.
        0xC970, 0xC971, 0xC972, 0xC973, 0xC974, 0xC975, 0xC976, 0xC977, 0xC978,
        0x5C74, 0x5C75, 0x5C76,
        //Normal TC/SC recruit conversation roots and their95-message closure.
        0xC366, 0xC367, 0xC368, 0xC369, 0xC36A, 0xC36B, 0xC36C, 0xC36D, 0xC36E, 0xC36F,
        0xC370, 0xC371, 0xC372, 0xC373, 0xC374, 0xC375, 0xC376, 0xC377, 0xC378, 0xC379,
        0xC37A, 0xC37B, 0xC37C, 0xC37D, 0xC37E, 0xC37F, 0xC380, 0xC381, 0xC382, 0xC383,
        0xC384, 0xC385, 0xC386, 0xC387, 0xC388, 0xC389, 0xC38A, 0xC38B, 0xC38C,
        0xC3B6, 0xC3B7, 0xC3B8, 0xC3B9, 0xC3BA, 0xC3BB, 0xC3BC, 0xC3BD, 0xC3BE, 0xC3BF,
        0xC3C0, 0xC3C1, 0xC3C2, 0xC3C3, 0xC3C5, 0xC3C6, 0xC3C7, 0xC3C8, 0xC3C9, 0xC3CA,
        0xC3CB, 0xC3CC, 0xC3CD, 0xC3CE, 0xC3CF, 0xC3D0, 0xC3D1, 0xC3D3, 0xC3D4, 0xC3D5,
        0xC3D6, 0xC3D7, 0xC3D8, 0xC3D9, 0xC3DA, 0xC3DB, 0xC3DC, 0xC3DD, 0xC3DE, 0xC3DF,
        0xC3E0, 0xC3E1, 0xC3E2, 0xC3E3, 0xC3E4, 0xC3E5, 0xC3E6, 0xC3E7, 0xC3E8, 0xC3E9,
        0xC3EA, 0xC3EB, 0xC3EC, 0xC3ED, 0xC3EE, 0xC3EF,
        //The real failure leaves call0129. Its finite normal TC/SC
        //address-term closure rejoins the admitted shared messages below.
        0x0129, 0x012A, 0x012B, 0x012C, 0x012D, 0x012E, 0x012F,
        0x0130, 0x0131, 0x0132, 0x0133, 0x0134, 0x0135,
        0x0097, 0x0098, 0x0099, 0x009A, 0x009B, 0x009C, 0x009D, 0x009E, 0x009F, 0x00A0, 0x00A1, 0x00A2,
        0x00A3, 0x00A4, 0x00A5, 0x00A6, 0x00A7, 0x00A8, 0x00A9, 0x00AA, 0x00AB, 0x00AC, 0x00AD, 0x00AE,
        0x00AF, 0x00B0, 0x00B1, 0x00B2, 0x00B3, 0x00B4, 0x00B5, 0x00B6, 0x00B7, 0x00B8, 0x00B9, 0x00BA,
        0x00BB, 0x00BC, 0x00BD, 0x00BE, 0x00BF, 0x00C0, 0x00C1, 0x00C2, 0x00C3, 0x00C4, 0x00C5, 0x00C6,
        0x00C7, 0x00C8, 0x00C9, 0x00CA, 0x00CB, 0x00CC, 0x00CD, 0x00CE, 0x00CF, 0x00D0, 0x00D1, 0x00D2,
        0x00D3, 0x00D4, 0x00D5, 0x00D7, 0x00D8, 0x00D9, 0x00DA, 0x00DB, 0x00DC, 0x00DD, 0x00DE, 0x00DF,
        0x00E0, 0x00E1, 0x00E2, 0x00E3, 0x00E4, 0x00E5, 0x00E6, 0x00E7, 0x00E8, 0x00E9, 0x00EA, 0x00EB,
        0x00EC, 0x00ED, 0x00EE, 0x00EF, 0x00F0, 0x00F1, 0x00F2, 0x00F3, 0x00F4, 0x00F5, 0x00F6, 0x00F7,
        0x00F8, 0x00F9, 0x00FA, 0x00FB, 0x00FC, 0x00FD, 0x00FE, 0x00FF, 0x0100, 0x0101, 0x0102, 0x0103,
        0x0104, 0x0105, 0x0106, 0x0107, 0x0108, 0x0109, 0x010A, 0x010B, 0x010C, 0x010D, 0x010E, 0x010F,
        0x0110, 0x0111, 0x0112, 0x0113, 0x0114, 0x0115, 0x0116, 0x0117, 0x0118, 0x0119, 0x011A, 0x011B,
        0x011C, 0x011D, 0x011E, 0x011F, 0x0120, 0x0121, 0x0122, 0x0123, 0x0124, 0x0125, 0x0127, 0x0128,
        0x01AC, 0x01AD, 0x01AE, 0x01AF, 0x01B0, 0x01B1, 0x01B2, 0x01B3, 0x01B4, 0x01B5, 0x01B6, 0x01B7,
        0x01B8, 0x01B9, 0x01BA, 0x01BB, 0x01BC, 0x01BD, 0x01BE, 0x01BF, 0x01C0, 0x01C2, 0x01C3, 0x01C4,
        0x01C7, 0x01C8, 0x01C9, 0x01CA, 0x01CB, 0x01CC, 0x01CD, 0x01CE, 0x01CF, 0x01D0, 0x01D1, 0x01D2,
        0x01D3, 0x01D4, 0x01D5, 0x01D6, 0x01D7, 0x01D8, 0x01D9, 0x01DA, 0x01DB, 0x01DC, 0x01DD, 0x01DE,
        0x01DF, 0x01E0, 0x01E1, 0x01E2, 0x01E3, 0x01E4, 0x01E5, 0x01E8, 0x01E9, 0x01EA, 0x01EB, 0x01EC,
        0x01ED, 0x01EE, 0x01EF, 0x01F0, 0x01F3, 0x01F4, 0x01F5, 0x01F6, 0x01F7, 0x01F8, 0x01F9, 0x01FA,
        0x01FB, 0x01FC, 0x01FD, 0x01FE, 0x01FF, 0x0200, 0x0201, 0x0202, 0x0203, 0x0204, 0x0205, 0x0206,
        0x0207, 0x0209, 0x020A, 0x020B, 0x020E, 0x020F, 0x0210, 0x0211, 0x0212, 0x0213, 0x0214, 0x0215,
        0x0216, 0x0217, 0x0218, 0x0219, 0x021A, 0x021B, 0x021C, 0x021D, 0x021E, 0x021F, 0x0220, 0x0221,
        0x0222, 0x0223, 0x0224, 0x0225, 0x0226, 0x0227, 0x0228, 0x0229, 0x022A, 0x022B, 0x022C, 0x022F,
        0x0230, 0x0231, 0x0232, 0x0233, 0x0234, 0x0235, 0x0236, 0x0237, 0x298F, 0x2990, 0x2C82, 0x2C83,
        0x2C84, 0x2C85, 0x2C86, 0x2C87, 0x2C88, 0x2C89, 0x2C8A, 0x2C8B, 0x2C8C, 0x2C8D, 0x2C8E, 0x2C8F,
        0x2C90, 0x2C91, 0x2C92, 0x2C93, 0x2C94, 0x2C95, 0x2C96, 0x2C97, 0x2C98, 0x2C99, 0x2C9A, 0x2C9B,
        0x2C9C, 0x2C9D, 0x2C9E, 0x2C9F, 0x2CA0, 0x2CA1, 0x2CA2, 0x2CA3, 0x2CA4, 0x2CA5, 0x2CA6, 0x2CA7,
        0x2CA8, 0x2CA9, 0x2CAA, 0x2CAB, 0x2CAC, 0x2CAD, 0x2CAE, 0x2CAF, 0x2CB0, 0x2CB1, 0x2CB2, 0x2CB3,
        0x2CB4, 0x2CB5, 0x2CB6, 0x2CB7, 0x2CB8, 0x2CB9, 0x2CBA, 0x2CBB, 0x2CBC, 0x2CBD, 0x2CBE, 0x2CBF,
        0x2CC0, 0x2CC1, 0x2CC2, 0x2CC3, 0x2CC4, 0x2CC5, 0x2CC6, 0x2CC7, 0x2CC8, 0x2CC9, 0x2CCA, 0x2CCB,
        0x2CCC, 0x2CCD, 0x2CCE, 0x2CCF, 0x2CD0, 0x2CD1, 0x2CD2, 0x31FC, 0x31FD, 0x31FE, 0x31FF, 0x34A0,
        0x51A0, 0x51A1, 0x51A2, 0x51A3, 0x51A4, 0x51A5, 0x51A6, 0x51A7, 0x51A8, 0x51A9, 0x51AA, 0x51AB,
        0x51AC, 0x51AD, 0x51AE, 0x51AF, 0x51B0, 0x51B1, 0x51B2, 0x51B3, 0x51B4, 0x51B5, 0x51B6, 0x51B7,
        0x51B8, 0x51B9, 0x51BA, 0x51BB, 0x51BC, 0x51BD, 0x51BE, 0x51BF, 0x51C0, 0x51C1, 0x51C2, 0x51C3,
        0x51C4, 0x51C5, 0x51C6, 0x51C7, 0x51C8, 0x51C9, 0x51CA, 0x51CB, 0x51CC, 0x51CD, 0x51CE, 0x51CF,
        0x51D0, 0x51D1, 0x51D2, 0x51D3, 0x51D4, 0x51D5, 0x51D6, 0x51D7, 0x51D8, 0x51D9, 0x51DA, 0x51DB,
        0x51DC, 0x51DD, 0x51DE, 0x51DF, 0x51E0, 0x51E1, 0x51E2, 0x51E3, 0x51E4, 0x51E5, 0x51E6, 0x51E7,
        0x51E8, 0x51E9, 0x51EA, 0x535B, 0x535C, 0x535D, 0x535E, 0x535F, 0x5360, 0x5361, 0x5362, 0x5363,
        0x5364, 0x53C5, 0x53C6, 0x53C7, 0x53C8, 0x53C9, 0x53CA, 0x53CB, 0x53CC, 0xC365, 0xC391, 0xC392,
        0xC39D, 0xC39F, 0xC3A9, 0xC3AB, 0xC3AC, 0xC3AD, 0xC3AE, 0xC3AF, 0xC3B0, 0xC3B1, 0xC3B2, 0xC3B3,
        0xC3B4, 0xC3B5, 0xC3F1, 0xC3F2, 0xC3F3, 0xC3F4, 0xC3F5, 0xC3F6, 0xC3F7, 0xC3F8, 0xC3F9, 0xC3FA,
        0xC3FB, 0xC3FC, 0xC3FD, 0xC3FE, 0xC3FF, 0xC400, 0xC401, 0xC402, 0xC403, 0xC404, 0xC405, 0xC406,
        0xC407, 0xC408, 0xC409, 0xC40A, 0xC40B, 0xC40C, 0xC40D, 0xC40E, 0xC40F, 0xC410, 0xC411, 0xC412,
        0xC413, 0xC414, 0xC415, 0xC416, 0xC417, 0xC97B, 0xC97C, 0xC97D, 0xC97E, 0xC97F, 0xC980, 0xC981, 0xC982, 0xC983, 0xC984, 0xC985, 0xC986,
    ];
    // TC/SC source62 resource closure: original personnel command reports and
    // their actual address/sex/personality references. Voice calls execute on
    // the source-time application stream; this is not a voice-disabled dialect.
    private static readonly HashSet<int> PersonnelAdministrationMessages =
    [
        0x578C, 0x5C0D, 0x5C0E, 0x5C0F, 0x5C14, 0x5C15, 0x5C16,
        0x5D21, 0x5D22, 0x5D23, 0x5D24,
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
        0x0120, 0x0121, 0x0122, 0x0123, 0x0124, 0x0125, 0x0127, 0x0128, 0x0600, 0x0601, 0x0602, 0x0603,
        0x0604, 0x0605, 0x0606, 0x0607, 0x0608, 0x0609, 0x060A, 0x2733, 0x57DA, 0x57E6, 0x585D, 0x585E,
        0x585F, 0x5860, 0x5861, 0x5862, 0x5863, 0x5864, 0x5865, 0x5866, 0x5867, 0x5868, 0x5884, 0x5885,
        0x5886, 0x5887, 0x5888, 0x5889, 0x588A, 0x588B, 0x588C, 0x588D, 0x588E, 0x588F, 0x5890, 0x5891,
        0x5892, 0x5893, 0x5894, 0x5895, 0x5896, 0x5897, 0x5898, 0x5899, 0x589A, 0x589B, 0x589C, 0x589D,
        0x589E, 0x589F, 0x598F, 0x5C4E, 0x5C51, 0x5C52, 0x5C53, 0x5C54, 0x5C55, 0x5C56, 0x5C57, 0x5C58,
        0x5C59, 0x5C5A, 0x5C5B, 0x5C5C, 0x5C5D, 0x5C5E, 0x5C5F, 0x5C6E, 0x5C72,
        0x00A8, 0x00A9, 0x00AA, 0x00AB, 0x00AC, 0x00AD, 0x00AE, 0x00AF, 0x00B0, 0x00B1, 0x00B2, 0x00B3,
        0x00B4, 0x00B5, 0x00B6, 0x00B7, 0x579C, 0x579D, 0x579E, 0x579F, 0x57A0, 0x57A1, 0x57A2, 0x57A3,
        0x57A4, 0x57A5, 0x57A6,
    ];
    //The three District roots and their complete installed TC/SC resource closure.
    private static readonly HashSet<int> DistrictAdministrationMessages =
    [
        0x0097, 0x0098, 0x0099, 0x009A, 0x009B, 0x009C, 0x009D, 0x009E, 0x009F, 0x00A0,
        0x00A1, 0x00A2, 0x00A3, 0x00A4, 0x00A5, 0x00A6, 0x00A7, 0x00B8, 0x00B9, 0x00BA,
        0x00BB, 0x00BC, 0x00BD, 0x00BE, 0x00BF, 0x00C0, 0x00C1, 0x00C2, 0x00C3, 0x00C4,
        0x00C5, 0x00C6, 0x00C7, 0x00C8, 0x00C9, 0x00CA, 0x00CB, 0x00CC, 0x00CD, 0x00CE,
        0x00CF, 0x00D0, 0x00D1, 0x00D2, 0x00D3, 0x00D4, 0x00D5, 0x00D7, 0x00D8, 0x00D9,
        0x00DA, 0x00DB, 0x00DC, 0x00DD, 0x00DE, 0x00DF, 0x00E0, 0x00E1, 0x00E2, 0x00E3,
        0x00E4, 0x00E5, 0x00E6, 0x00E7, 0x00E8, 0x00E9, 0x00EA, 0x00EB, 0x00EC, 0x00ED,
        0x00EE, 0x00EF, 0x00F0, 0x00F1, 0x00F2, 0x00F3, 0x00F4, 0x00F5, 0x00F6, 0x00F7,
        0x00F8, 0x00F9, 0x00FA, 0x00FB, 0x00FC, 0x00FD, 0x00FE, 0x00FF, 0x0100, 0x0101,
        0x0102, 0x0103, 0x0104, 0x0105, 0x0106, 0x0107, 0x0108, 0x0109, 0x010A, 0x010B,
        0x010C, 0x010D, 0x010E, 0x010F, 0x0110, 0x0111, 0x0112, 0x0113, 0x0114, 0x0115,
        0x0116, 0x0117, 0x0118, 0x0119, 0x011A, 0x011B, 0x011C, 0x011D, 0x011E, 0x011F,
        0x0120, 0x0121, 0x0122, 0x0123, 0x0124, 0x0125, 0x0127, 0x0128, 0x0289, 0x028A,
        0x028B, 0x028C, 0x028D, 0x5946, 0x5963, 0x5964, 0x5965, 0x5966, 0x5967, 0x5968,
        0x5969, 0x596A, 0x596B, 0x596C, 0x596D, 0x596E,
    ];
    internal string ResolvePersonnelAdministrationReport(INarrationCurrentContext people,
        Func<int, int> drawApp32, int root)
    {
        if (root is not (0x578C or 0x579C or 0x5C0D or 0x5C0E or 0x5C0F or 0x5C14 or 0x5C15 or 0x5C16 or
            0x585D or 0x5867 or 0x5868 or 0x5885 or 0x5886 or 0x5887 or
            0x588C or 0x5896 or 0x5888 or 0x5889 or 0x588A or 0x588B or 0x5C56 or
            0x5C5E or 0x5C5F or 0x5C51 or 0x5C52 or 0x5C53 or 0x5C54 or
            0x5D21 or 0x5D22 or 0x5D23 or 0x5D24 or 0x5946 or 0x5963 or 0x5969))
            throw Failure("personnel_administration_root");
        _prefix = "personnel.administration.text";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(people, drawApp32, root, Get(root, true), _ => false, true, null,
                id => (root is 0x5946 or 0x5963 or 0x5969 ? DistrictAdministrationMessages : PersonnelAdministrationMessages).Contains(id), (group, code) => (group, code) switch
                {
                    ('C', 'J') => "^02", ('C', 'L') => "^05",
                    ('E', 'K') or ('E', 'M') => string.Empty,
                    _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
                }, true).Text;
        }
        finally { _report = previous; }
    }

    // The installed TC/SC research guide, confirmation and ruler narration closure.
    private static readonly HashSet<int> DoctrineResearchMessages =
    [
        0x0600, 0x0601, 0x0602, 0x0603, 0x0604, 0x0605, 0x0606, 0x0607, 0x0608, 0x0609, 0x060A,
        0x57C7, 0x5C61, 0x5C62, 0x5C63, 0x5C64, 0x5C65, 0x5C67, 0x5C68, 0x5C69, 0x5C6A, 0x5C6B,
        0x5C7F, 0x5C80, 0x5C81, 0x5C82, 0x5C83, 0x5C84, 0x5C85
    ];
    internal string ResolveDoctrineResearch(INarrationCurrentContext context, Func<int, int> drawApp32, int root)
    {
        if (root is not (0x57C7 or 0x5C61 or 0x5C62 or 0x5C63 or 0x5C64 or 0x5C65 or 0x5C67 or
            0x5C68 or 0x5C69 or 0x5C6A or 0x5C6B or 0x5C7F)) throw Failure("doctrine_research_root");
        _prefix = "doctrine.research.text";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, drawApp32, root, Get(root, true), _ => false,
                true, null, DoctrineResearchMessages.Contains).Text;
        }
        finally { _report = previous; }
    }

    // Source63's complete installed TC/SC learning, hiring and mediation
    // resource graph. Narration is selected once by the actual command owner.
    private static readonly HashSet<int> PersonnelDevelopmentMessages =
    [
        0x0097, 0x0098, 0x0099, 0x009A, 0x009B, 0x009C, 0x009D, 0x009E, 0x009F, 0x00A0, 0x00A1, 0x00A2,
        0x00A3, 0x00A4, 0x00A5, 0x00A6, 0x00A7, 0x00A8, 0x00A9, 0x00AA, 0x00AB, 0x00AC, 0x00AD, 0x00AE,
        0x00AF, 0x00B0, 0x00B1, 0x00B2, 0x00B3, 0x00B4, 0x00B5, 0x00B6, 0x00B7, 0x00B8, 0x00B9, 0x00BA,
        0x00BB, 0x00BC, 0x00BD, 0x00BE, 0x00BF, 0x00C0, 0x00C1, 0x00C2, 0x00C3, 0x00C4, 0x00C5, 0x00C6,
        0x00C7, 0x00C8, 0x00C9, 0x00CA, 0x00CB, 0x00CC, 0x00CD, 0x00CE, 0x00CF, 0x00D0, 0x00D1, 0x00D2,
        0x00D3, 0x00D4, 0x00D5, 0x00D7, 0x00D8, 0x00D9, 0x00DA, 0x00DB, 0x00DC, 0x00DD, 0x00DE, 0x00DF,
        0x00E0, 0x00E1, 0x00E2, 0x00E3, 0x00E4, 0x00E5, 0x00E6, 0x00E7, 0x00E8, 0x00E9, 0x00EA, 0x00EB,
        0x00EC, 0x00ED, 0x00EE, 0x00EF, 0x00F0, 0x00F1, 0x00F2, 0x00F3, 0x00F4, 0x00F5, 0x00F6, 0x00F7,
        0x00F8, 0x00F9, 0x00FA, 0x00FB, 0x00FC, 0x00FD, 0x00FE, 0x00FF, 0x0100, 0x0101, 0x0119, 0x58A6,
        0x58A7, 0x58A8, 0x58A9, 0x58AA, 0x58AB, 0x58AC, 0x58AD, 0x58AE, 0x58B1, 0x58B5, 0x58BE, 0x58C2,
        0x58C3, 0x58C4, 0x58C5, 0x58C6, 0x58C7, 0x58C8, 0x58C9, 0x58D1, 0x58D2, 0x58D3, 0x58D4, 0x58D5,
        0x58DD, 0x58DE, 0x58DF, 0x58E0, 0x58E1, 0x58E2, 0x58EA, 0x58EB, 0x58EC, 0x58ED, 0x58EE, 0x58F6,
        0x597E, 0x597F, 0x5980, 0x5981, 0x5985, 0x5986, 0x5987, 0x5988, 0x598B, 0x598C, 0x598D, 0x5CDE,
        0x5CE6,
    ];
    private static readonly HashSet<int> PersonnelDevelopmentRoots =
    [
        0x58A6, 0x58A7, 0x58A8, 0x58A9, 0x58AA, 0x58AB, 0x58AC, 0x58AD, 0x58AE, 0x58B1, 0x58B5, 0x58BE,
        0x58C2, 0x58C3, 0x58C4, 0x58C5, 0x58C6, 0x58C7, 0x58C8, 0x58C9, 0x58D1, 0x58D2, 0x58D3, 0x58D4,
        0x58D5, 0x58DD, 0x58DE, 0x58DF, 0x58E0, 0x58E1, 0x58E2, 0x58EA, 0x58EB, 0x58EC, 0x58ED, 0x58EE,
        0x58F6, 0x597E, 0x597F, 0x5980, 0x5981, 0x5985, 0x5986, 0x5987, 0x5988, 0x598B, 0x598C, 0x598D,
        0x5CDE, 0x5CE6,
    ];
    internal string ResolvePersonnelDevelopmentReport(INarrationCurrentContext people,
        Func<int, int> drawApp32, int root)
    {
        if (!PersonnelDevelopmentRoots.Contains(root)) throw Failure("personnel_development_root");
        _prefix = "personnel.development.text";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(people, drawApp32, root, Get(root, true), _ => false, true, null,
                PersonnelDevelopmentMessages.Contains, (group, code) => (group, code) switch
                {
                    ('C', 'J') => "^02", ('C', 'L') => "^05",
                    ('E', 'K') or ('E', 'M') => string.Empty,
                    _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
                }, true).Text;
        }
        finally { _report = previous; }
    }

    internal string ResolveTalentReport(INarrationCurrentContext people, Func<int, int> drawApp32, int root)
    {
        if (root is not (0x2C82 or 0x2CA3 or 0x2CB3 or 0x2CC3 or 0xC3B5 or 0xC3F1 or 0xC3F2 or 0xC3F3 or
            0x51CE or 0x51D8 or 0xC365 or 0xC391 or 0x51A9 or 0xC392 or 0x51B3 or 0xC39D or
            0x51BD or 0xC39F or 0x51C5 or 0xC97B or 0x53C5 or 0xC3A9 or 0x298F or 0x2990 or
            0x535B or 0x5360 or 0xC3AB or 0xC3B4 or 0x51A0 or 0xC3F4 or 0xC415 or 0xC416 or
            0x51E2 or 0xC417 or 0x51EA or 0x34A0 or 0x31FC or 0x31FD or 0x31FE or 0x31FF or 0xC97C or 0xC981 or 0xC982 or
            0xC366 or 0xC3B6 or 0xC3C5 or 0xC970 or 0x5C74 or 0x5C75 or 0x5C76 or
            0x57EE or 0x3163 or 0x3162 or 0x3155 or 0x597C or 0x597D))
            throw new InvalidDataException("talent.report.root");
        _prefix = "talent.report";
        bool previous = _report; _report = true;
        try
        {
            //C97C has two byte-identical voice44 branches. Retain the
            //actor ID query and the shared body without inventing admission.
            //C3B6 must use the unmodified program: its real Voice40
            //availability branches differ and execute through the current owner.
            var program = root == 0xC97C ? ReadVoiceText(root, 44) : Get(root, true);
            if (root == 0xC97C) _ = people.PersonValue(0, '2');
            return ExecuteMessage(people, drawApp32, root, program, _ => false, true, null,
                TalentReportMessages.Contains, (group, code) => (group, code) switch
                {
                    ('C', 'J') => "^02", ('C', 'L') => "^05",
                    ('E', 'K') or ('E', 'M') => string.Empty,
                    _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
                }, true).Text;
        }
        finally { _report = previous; }
    }

    //Finite TC/SC normal message closure for the four diplomatic orders.
    private static readonly HashSet<int> DiplomacyReportMessages =
    [
        0x3507, 0x3508, 0x3509,
        0x0097, 0x0098, 0x0099, 0x009A, 0x009B, 0x009C, 0x009D, 0x009E, 0x009F, 0x00A0, 0x00A1, 0x00A2,
        0x00A3, 0x00A4, 0x00A5, 0x00A6, 0x00A7, 0x00A8, 0x00A9, 0x00AA, 0x00AB, 0x00AC, 0x00AD, 0x00AE,
        0x00AF, 0x00B0, 0x00B1, 0x00B2, 0x00B3, 0x00B4, 0x00B5, 0x00B6, 0x00B7, 0x00B8, 0x00B9, 0x00BA,
        0x00BB, 0x00BC, 0x00BD, 0x00BE, 0x00BF, 0x00C0, 0x00C1, 0x00C2, 0x00C3, 0x00C4, 0x00C5, 0x00C6,
        0x00C7, 0x00C8, 0x00C9, 0x00CA, 0x00CB, 0x00CC, 0x00CD, 0x00CE, 0x00CF, 0x00D0, 0x00D1, 0x00D2,
        0x00D3, 0x00D4, 0x00D5, 0x00D7, 0x00D8, 0x00D9, 0x00DA, 0x00DB, 0x00DC, 0x00DD, 0x00DE, 0x00DF,
        0x00E0, 0x00E1, 0x00E2, 0x00E3, 0x00E4, 0x00E5, 0x00E6, 0x00E7, 0x00E8, 0x00E9, 0x00EA, 0x00EB,
        0x00EC, 0x00ED, 0x00EE, 0x00EF, 0x00F0, 0x00F1, 0x00F2, 0x00F3, 0x00F4, 0x00F5, 0x00F6, 0x00F7,
        0x00F8, 0x00F9, 0x00FA, 0x00FB, 0x00FC, 0x00FD, 0x00FE, 0x00FF, 0x0100, 0x0101, 0x0102, 0x0103,
        0x0104, 0x0105, 0x0106, 0x0107, 0x0108, 0x0109, 0x010A, 0x010B, 0x010C, 0x010D, 0x010E, 0x010F,
        0x0110, 0x0111, 0x0112, 0x0113, 0x0114, 0x0115, 0x0116, 0x0117, 0x0118, 0x0119, 0x011A, 0x011B,
        0x011C, 0x011D, 0x011E, 0x011F, 0x0120, 0x0121, 0x0122, 0x0123, 0x0124, 0x0125, 0x0127, 0x0128,
        0x01AC, 0x01AD, 0x01AE, 0x01AF, 0x01B0, 0x01B1, 0x01B2, 0x01B3, 0x01B4, 0x01B5, 0x01B6, 0x01B7,
        0x01B8, 0x01B9, 0x01BA, 0x01BB, 0x01BC, 0x01BD, 0x01BE, 0x01BF, 0x01C0, 0x01C2, 0x01C3, 0x01C4,
        0x01C7, 0x01C8, 0x01C9, 0x01CA, 0x01CB, 0x01CC, 0x01CD, 0x01CE, 0x01CF, 0x01D0, 0x01D1, 0x01D2,
        0x01D3, 0x01D4, 0x01D5, 0x01D6, 0x01D7, 0x01D8, 0x01D9, 0x01DA, 0x01DB, 0x01DC, 0x01DD, 0x01DE,
        0x01DF, 0x01E0, 0x01E1, 0x01E2, 0x01E3, 0x01E4, 0x01E5, 0x01E8, 0x01E9, 0x01EA, 0x01EB, 0x01EC,
        0x01ED, 0x01EE, 0x01EF, 0x01F0, 0x2D03, 0x3055, 0x3056, 0x3057, 0x3058, 0x3059, 0x305A, 0x305B,
        0x305C, 0x305D, 0x305E, 0x305F, 0x3060, 0x3061, 0x3062, 0x3063, 0x3064, 0x3065, 0x3066, 0x3067,
        0x3068, 0x3069, 0x306A, 0x306B, 0x306C, 0x306D, 0x306E, 0x306F, 0x3070, 0x3071, 0x3072, 0x3073,
        0x3074, 0x3075, 0x3076, 0x3077, 0x3078, 0x308B, 0x308C, 0x308D, 0x308E, 0x308F, 0x3090, 0x3091,
        0x3092, 0x3093, 0x309D, 0x309E, 0x309F, 0x30A2, 0x3164, 0x3165, 0x3166, 0x3167, 0x3168, 0x3169,
        0x316A, 0x316B, 0x316C, 0x316D, 0x316E, 0x316F, 0x3170, 0x3171, 0x3172, 0x3173, 0x3174, 0x318C, 0x318D,
        0x318E, 0x318F, 0x3190, 0x3191, 0x3192, 0x3193, 0x3194, 0x3195, 0x3196, 0x3197, 0x3198, 0x3199,
        0x319A, 0x319B, 0x319C, 0x319D, 0x319E, 0x319F, 0x31A0, 0x31A1, 0x31A2, 0x31A3, 0x31A4, 0x31A5,
        0x31A6, 0x31A7, 0x31A8, 0x31A9, 0x31AA, 0x31AB, 0x31AC, 0x31AD, 0x31AE, 0x31AF, 0x31B0, 0x31B1,
        0x31B2, 0x31B3, 0x31B4, 0x31B5, 0x31B6, 0x31B7, 0x31B8, 0x31B9, 0x31BA, 0x31BB, 0x31BC, 0x31BD,
        0x31BE, 0x31BF, 0x31C0, 0x31C1, 0x31C2, 0x31C3, 0x31C4, 0x31C5, 0x31C6, 0x31C7, 0x31C8, 0x31C9,
        0x31CA, 0x31CB, 0x31CC, 0x31CD, 0x31E6, 0x31E7, 0x31E8, 0x31E9, 0x31EA, 0x31EB, 0x31EC, 0x31ED,
        0x31EE, 0x31EF, 0x31F0, 0x31F1, 0x31F2, 0x31F3, 0x31F4, 0x31F5, 0x31F6, 0x31F7, 0x31F8, 0x31F9,
        0x31FA, 0x31FB, 0x320A, 0x320E, 0x3212, 0x50CF, 0x50D0, 0x50D1, 0x50D2, 0x50D3, 0x50D4, 0x50D5, 0x50D6, 0x50D7, 0x50D8,
        0x50D9, 0x50DA, 0x50DB, 0x50DC, 0x50DD, 0x50DE, 0x50DF, 0x50E0, 0x50E1, 0x50E2, 0x50E3, 0x50E4,
        0x50E5, 0x50E6, 0x50E7, 0x50E8, 0x50E9, 0x50EA, 0xC418, 0xC419, 0xC41A, 0xC41B, 0xC41C, 0xC41D,
        0xC41E, 0xC41F, 0xC420, 0xC421, 0xC422, 0xC423, 0xC424, 0xC425, 0xC426, 0xC427, 0xC428, 0xC429,
        0xC42A, 0xC42B, 0xC42C, 0xC42D, 0xC42E,
    ];
    private static readonly HashSet<int> DiplomacyReportRoots =
    [
        0x3507, 0x3508, 0x3509,
        0x2D03, 0x3055, 0x3056, 0x3057, 0x3058, 0x3059, 0x305A, 0x305B, 0x305C, 0x305D, 0x305E, 0x3067,
        0x3070, 0x308B, 0x309D, 0x309E, 0x309F, 0x30A2, 0x3164, 0x3165, 0x3166, 0x316F, 0x3174, 0x318C, 0x318D,
        0x318E, 0x318F, 0x3190, 0x3199, 0x31A2, 0x31A3, 0x31A4, 0x31A5, 0x31A6, 0x31AF, 0x31B8, 0x31B9,
        0x31BA, 0x31C3, 0x31C4, 0x31C5, 0x31E6, 0x31E7, 0x31E8, 0x31F1, 0x31F2, 0x31F3, 0x320A, 0x320E, 0x3212, 0x50CF, 0x50D6,
        0x50DD, 0x50E4, 0xC418, 0xC42E,
    ];
    //Finite normal-resource closure of2F39D0 diplomatic advice.
    private static readonly HashSet<int> DiplomacyAdviceRoots = [0x2CD3, 0x2CE3, 0x2CF3, 0x2D04, 0x337C, 0x338C, 0x2D14, 0x2D1A, 0x2D7E, 0x2D8E, 0x3469];
    private static readonly HashSet<int> DiplomacyAdviceMessages =
    [
        0x0097, 0x0098, 0x0099, 0x009A, 0x009B, 0x009C, 0x009D, 0x009E, 0x009F, 0x00A0, 0x00A1, 0x00A2,
        0x00A3, 0x00A4, 0x00A5, 0x00A6, 0x00A7, 0x00A8, 0x00A9, 0x00AA, 0x00AB, 0x00AC, 0x00AD, 0x00AE,
        0x00AF, 0x00B0, 0x00B1, 0x00B2, 0x00B3, 0x00B4, 0x00B5, 0x00B6, 0x00B7, 0x00B8, 0x00B9, 0x00BA,
        0x00BB, 0x00BC, 0x00BD, 0x00BE, 0x00BF, 0x00C0, 0x00C1, 0x00C2, 0x00C3, 0x00C4, 0x00C5, 0x00C6,
        0x00C7, 0x00C8, 0x00C9, 0x00CA, 0x00CB, 0x00CC, 0x00CD, 0x00CE, 0x00CF, 0x00D0, 0x00D1, 0x00D2,
        0x00D3, 0x00D4, 0x00D5, 0x00D7, 0x00D8, 0x00D9, 0x00DA, 0x00DB, 0x00DC, 0x00DD, 0x00DE, 0x00DF,
        0x00E0, 0x00E1, 0x00E2, 0x00E3, 0x00E4, 0x00E5, 0x00E6, 0x00E7, 0x00E8, 0x00E9, 0x00EA, 0x00EB,
        0x00EC, 0x00ED, 0x00EE, 0x00EF, 0x00F0, 0x00F1, 0x00F2, 0x00F3, 0x00F4, 0x00F5, 0x00F6, 0x00F7,
        0x00F8, 0x00F9, 0x00FA, 0x00FB, 0x00FC, 0x00FD, 0x00FE, 0x00FF, 0x0100, 0x0101, 0x0102, 0x0103,
        0x0104, 0x0105, 0x0106, 0x0107, 0x0108, 0x0109, 0x010A, 0x010B, 0x010C, 0x010D, 0x010E, 0x010F,
        0x0110, 0x0111, 0x0112, 0x0113, 0x0114, 0x0115, 0x0116, 0x0117, 0x0118, 0x0119, 0x011A, 0x011B,
        0x011C, 0x011D, 0x011E, 0x011F, 0x0120, 0x0121, 0x0122, 0x0123, 0x0124, 0x0125, 0x0127, 0x0128,
        0x01AC, 0x01AD, 0x01AE, 0x01AF, 0x01B0, 0x01B1, 0x01B2, 0x01B3, 0x01B4, 0x01B5, 0x01B6, 0x01B7,
        0x01B8, 0x01B9, 0x01BA, 0x01BB, 0x01BC, 0x01BD, 0x01BE, 0x01BF, 0x01C0, 0x01C2, 0x01C3, 0x01C4,
        0x01C7, 0x01C8, 0x01C9, 0x01CA, 0x01CB, 0x01CC, 0x01CD, 0x01CE, 0x01CF, 0x01D0, 0x01D1, 0x01D2,
        0x01D3, 0x01D4, 0x01D5, 0x01D6, 0x01D7, 0x01D8, 0x01D9, 0x01DA, 0x01DB, 0x01DC, 0x01DD, 0x01DE,
        0x01DF, 0x01E0, 0x01E1, 0x01E2, 0x01E3, 0x01E4, 0x01E5, 0x01E8, 0x01E9, 0x01EA, 0x01EB, 0x01EC,
        0x01ED, 0x01EE, 0x01EF, 0x01F0, 0x01F3, 0x01F4, 0x01F5, 0x01F6, 0x01F7, 0x01F8, 0x01F9, 0x01FA,
        0x01FB, 0x01FC, 0x01FD, 0x01FE, 0x01FF, 0x0200, 0x0201, 0x0202, 0x0203, 0x0204, 0x0205, 0x0206,
        0x0207, 0x0209, 0x020A, 0x020B, 0x020E, 0x020F, 0x0210, 0x0211, 0x0212, 0x0213, 0x0214, 0x0215,
        0x0216, 0x0217, 0x0218, 0x0219, 0x021A, 0x021B, 0x021C, 0x021D, 0x021E, 0x021F, 0x0220, 0x0221,
        0x0222, 0x0223, 0x0224, 0x0225, 0x0226, 0x0227, 0x0228, 0x0229, 0x022A, 0x022B, 0x022C, 0x022F,
        0x0230, 0x0231, 0x0232, 0x0233, 0x0234, 0x0235, 0x0236, 0x0237, 0x2CD3, 0x2CD4, 0x2CD5, 0x2CD6,
        0x2CD7, 0x2CD8, 0x2CD9, 0x2CDA, 0x2CDB, 0x2CDC, 0x2CDD, 0x2CDE, 0x2CDF, 0x2CE0, 0x2CE1, 0x2CE2,
        0x2CE3, 0x2CE4, 0x2CE5, 0x2CE6, 0x2CE7, 0x2CE8, 0x2CE9, 0x2CEA, 0x2CEB, 0x2CEC, 0x2CED, 0x2CEE,
        0x2CEF, 0x2CF0, 0x2CF1, 0x2CF2, 0x2CF3, 0x2CF4, 0x2CF5, 0x2CF6, 0x2CF7, 0x2CF8, 0x2CF9, 0x2CFA,
        0x2CFB, 0x2CFC, 0x2CFD, 0x2CFE, 0x2CFF, 0x2D00, 0x2D01, 0x2D02, 0x2D04, 0x2D05, 0x2D06, 0x2D07,
        0x2D08, 0x2D09, 0x2D0A, 0x2D0B, 0x2D0C, 0x2D0D, 0x2D0E, 0x2D0F, 0x2D10, 0x2D11, 0x2D12, 0x2D13,
        0x2D14, 0x2D15, 0x2D16, 0x2D17, 0x2D18, 0x2D19, 0x2D1A, 0x2D1B, 0x2D1C, 0x2D1D, 0x2D1E, 0x2D1F,
        0x2D7E, 0x2D7F, 0x2D80, 0x2D81, 0x2D82, 0x2D83, 0x2D84, 0x2D85, 0x2D86, 0x2D87, 0x2D88, 0x2D89,
        0x2D8A, 0x2D8B, 0x2D8C, 0x2D8D, 0x2D8E, 0x2D8F, 0x2D90, 0x2D91, 0x2D92, 0x2D93, 0x2D94, 0x2D95,
        0x2D96, 0x2D97, 0x2D98, 0x2D99, 0x2D9A, 0x2D9B, 0x2D9C, 0x2D9D, 0x337C, 0x337D, 0x337E, 0x337F,
        0x3380, 0x3381, 0x3382, 0x3383, 0x3384, 0x3385, 0x3386, 0x3387, 0x3388, 0x3389, 0x338A, 0x338B,
        0x338C, 0x338D, 0x338E, 0x338F, 0x3390, 0x3391, 0x3392, 0x3393, 0x3394, 0x3395, 0x3396, 0x3397,
        0x3398, 0x3399, 0x339A, 0x339B, 0x3469, 0x346A, 0x346B, 0x346C, 0x346D, 0x346E,
    ];
    internal string ResolveDiplomacyReport(INarrationCurrentContext people, Func<int, int> drawApp32, int root)
    {
        if (!DiplomacyReportRoots.Contains(root) && !DiplomacyAdviceRoots.Contains(root)) throw new InvalidDataException("diplomacy.report.root");
        _prefix = "diplomacy.report";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(people, drawApp32, root, Get(root, true), _ => false, true, null,
                id => DiplomacyAdviceRoots.Contains(root) ? DiplomacyAdviceMessages.Contains(id) : DiplomacyReportMessages.Contains(id), (group, code) => (group, code) switch
                {
                    ('C', 'J') => "^02", ('C', 'L') => "^05",
                    ('E', 'K') or ('E', 'M') => string.Empty,
                    _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
                }, true).Text;
        }
        finally { _report = previous; }
    }

    private static readonly HashSet<int> ReportMessages =
    [
        0x5F5E,
        0x5D25, 0x5D26, 0x5D7F, 0x5D80,
        0x5A74, 0x5A75, 0x5A76,
        0x322E,
        0x0600, 0x0601, 0x0602, 0x0603, 0x0604, 0x0605, 0x0606, 0x0607, 0x0608, 0x0609,
        0x060A, 0x2A3B, 0x3225, 0x3228, 0x3229, 0x322D, 0x3231, 0x3232, 0x3239, 0x323A, 0x3452, 0x349D, 0x53D4,
        0x53D5, 0x53D6, 0x53D7, 0x53D8, 0x53D9, 0x53DA, 0x5F58, 0x5F6A, 0x5F6D, 0x5F7B,
        0x5F7C, 0x5F7D, 0x5F80, 0xC356, 0xC357, 0xC358, 0xC359, 0xC35A, 0xC35B, 0xC35D, 0xC35E, 0xC360, 0xC96A,
        0xC96B, 0xC96C, 0xC96D, 0xC96E, 0xC96F,
    ];
    private bool _report;
    private readonly Dictionary<int, Program> _reportPrograms = [];
    //Finite complete TC/SC ordinary resource trees read through the normal
    //resource/parser APIs. Only the supplied duel roots can enter this dialect.
    private static readonly HashSet<int> DuelReportMessages =
    [
        0x0097, 0x0098, 0x0099, 0x009A, 0x009B, 0x009C, 0x009D, 0x009E, 0x009F, 0x00A0,
        0x00A1, 0x00A2, 0x00A3, 0x00A4, 0x00A5, 0x00A6, 0x00A7, 0x01AC, 0x01AD, 0x01AE,
        0x01AF, 0x01B0, 0x01B1, 0x01B2, 0x01B3, 0x01B4, 0x01B5, 0x01B6, 0x01B7, 0x01B8,
        0x01B9, 0x01BA, 0x01BB, 0x01BC, 0x01BD, 0x01BE, 0x01BF, 0x01C0, 0x01C2, 0x01C3,
        0x01C4, 0x01C7, 0x01C8, 0x01C9, 0x01CA, 0x01CB, 0x01CC, 0x01CD, 0x01CE, 0x01CF,
        0x01D0, 0x01D1, 0x01D2, 0x01D3, 0x01D4, 0x01D5, 0x01D6, 0x01D7, 0x01D8, 0x01D9,
        0x01DA, 0x01DB, 0x01DC, 0x01DD, 0x01DE, 0x01DF, 0x01E0, 0x01E1, 0x01E2, 0x01E3,
        0x01E4, 0x01E5, 0x01E8, 0x01E9, 0x01EA, 0x01EB, 0x01EC, 0x01ED, 0x01EE, 0x01EF,
        0x01F0, 0x53A3, 0x53A4, 0x53A5, 0x53A6, 0xC952, 0xC953, 0xC954, 0xC955, 0xC956,
        0xC957, 0xC958, 0xC959, 0xC95B, 0xC95C, 0xC95D
    ];
    internal string ResolveDuelHistoryReport(INarrationCurrentContext context, Func<int, int> drawApp32, int root)
    {
        if (root is not (0xC952 or 0xC953 or 0xC954 or 0xC955 or 0xC956 or 0xC957 or 0xC958 or 0xC959 or
            0xC95B or 0xC95C or 0xC95D or 0x53A3 or 0x53A4 or 0x53A5 or 0x53A6))
            throw Failure("duel_history_report_root");
        _prefix = "duel.history-text";
        bool previous = _report; _report = true;
        try
        {
            string text = ExecuteMessage(context, drawApp32, root, Get(root), _ => false, true, null,
                DuelReportMessages.Contains, escapeText: (group, code) => (group, code) switch
                { ('C', 'J') => "^02", ('C', 'L') => "^05", ('E', 'K') or ('E', 'M') => string.Empty,
                  _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}") }, allowOpaqueEscapes: true).Text;
            if (text.Length > 338 || text.Any(c => c == '\0' || char.IsSurrogate(c)))
                throw Failure("complete_short_bmp_profile");
            return text;
        }
        finally { _report = previous; }
    }

    //Exact independently observed TC/SC CE resource graph,20 records.
    //This does not grant additional roots to other report consumers.
    private static readonly HashSet<int> CaptiveReleaseMessages =
        [0x3240, 0x3241, 0x3242, 0x53A7, 0x53A8, 0x53A9, 0x53AA, 0x53AB, 0x53AC, 0x53AD];
    internal string ResolveCaptiveReleaseReport(INarrationCurrentContext context, Func<int, int> drawApp32, int root)
    {
        if (root is not (0x3240 or 0x53A7)) throw Failure("captive_release_report_root");
        _prefix = "person-captive-release.report-text";
        bool previous = _report; _report = true;
        try
        {
            string text = ExecuteMessage(context, drawApp32, root, Get(root), _ => false, true, null,
                CaptiveReleaseMessages.Contains).Text;
            if (text.Length > 338 || text.Any(c => c == '\0' || char.IsSurrogate(c)))
                throw Failure("complete_short_bmp_profile");
            return text;
        }
        finally { _report = previous; }
    }

    internal string ResolveHistoryReport(INarrationCurrentContext context, Func<int,int> drawApp32, int root)
    {
        if (root is not (0x5F5E or 0x5D25 or 0x5D26 or 0x5D7F or 0x5D80 or 0x5A74 or 0x5A75 or 0x5A76 or 0x322E or 0x5F6A or 0x5F58 or 0x5F6D or 0x5F80 or 0xC96A or 0xC96B or 0xC96C or 0xC96D or 0xC96E or
            0xC356 or 0xC357 or 0xC358 or 0xC359 or 0xC35A or 0xC35B or 0xC35D or 0xC35E or 0xC96F or 0xC360 or
            0x3228 or 0x3229 or 0x3225 or 0x322D or 0x3231 or 0x3232 or 0x3239 or 0x323A or
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

    internal string ResolveGarrisonFoodShortageReport(INarrationCurrentContext context,
        Func<int, int> drawApp32, int root)
    {
        if (root is not (0x5E3D or 0x5E41)) throw Failure("garrison_food_shortage_root");
        _prefix = "garrison-food-shortage.text";
        bool previous = _report; _report = true;
        try
        {
            // Both language resources contain only literals and Base0 field2.
            // Neither root references another message or draws a random value.
            return ExecuteMessage(context, drawApp32, root, Get(root), _ => false, true, null,
                _ => false).Text;
        }
        finally { _report = previous; }
    }

    internal string ResolveGovernanceNarration(INarrationCurrentContext context,
        Func<int, int> drawApp32, int root)
    {
        if (root is not (0x57F4 or 0x57F9)) throw Failure("governance_root");
        _prefix = "governance.response";
        bool previous = _report; _report = true;
        try
        {
            // Roots and their shared personal-address references are actual
            // resource programs. The existing parser and resource index bounds
            // apply; no synthetic text, speaker or random result is substituted.
            var program = root == 0x57F9 ? ReadVoiceText(root, 33) : Get(root);
            return ExecuteMessage(context, drawApp32, root, program, _ => false,
                true, null, _ => true).Text;
        }
        finally { _report = previous; }
    }

    internal string ResolveActionAreaReport(INarrationCurrentContext context, Func<int,int> drawApp32, int root)
    {
        if(root is not (0x500f or 0x53cd or 0x5016))throw Failure("action_area_root");
        _prefix="action-report.area-text";
        bool previous=_report;_report=true;
        try
        {
            return ExecuteMessage(context,drawApp32,root,Get(root),_=>false,true,null,
                id=>id>root&&id<=root+6).Text;
        }
        finally{_report=previous;}
    }

    internal string ResolveActionArmyTargetReport(INarrationCurrentContext context,Func<int,int> drawApp32)
    {
        const int root=0x5024;
        _prefix="action-report.army-target-text";
        bool previous=_report;_report=true;
        try
        {
            return ExecuteMessage(context,drawApp32,root,Get(root),_=>false,true,null,
                id=>id>root&&id<=root+6).Text;
        }
        finally{_report=previous;}
    }

    internal string ResolveActionBaseNearbyReport(INarrationCurrentContext context,Func<int,int> drawApp32)
    {
        const int root=0x502b;
        _prefix="action-report.base-nearby-text";
        bool previous=_report;_report=true;
        try
        {
            //The six actual voice leaves include C00A8's bounded shared
            //pronoun family. It is not a blanket permit for other reports.
            return ExecuteMessage(context,drawApp32,root,Get(root),_=>false,true,null,
                id=>id is >=0x502c and <=0x5031 or >=0x00a8 and <=0x00b7).Text;
        }
        finally{_report=previous;}
    }

    internal string ResolveActionAllianceExpiryReport(INarrationCurrentContext context,Func<int,int> drawApp32)
    {
        const int root=0x5046;
        _prefix="action-report.alliance-expiry-text";
        bool previous=_report;_report=true;
        try
        {
            return ExecuteMessage(context,drawApp32,root,Get(root),_=>false,true,null,
                id=>id is >=0x5047 and <=0x504c).Text;
        }
        finally{_report=previous;}
    }

    internal string ResolveActionForceObjectiveReport(INarrationCurrentContext context, Func<int,int> drawApp32)
    {
        const int root = 0x50c1;
        _prefix = "action-report.force-objective-text";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, drawApp32, root, Get(root), _ => false, true, null,
                id => id is >= 0x50c2 and <= 0x50c7).Text;
        }
        finally { _report = previous; }
    }

    internal string ResolveActionArmySupplyReport(INarrationCurrentContext context, Func<int,int> drawApp32, int root)
    {
        if (root is not (0x5032 or 0x5039)) throw Failure("army_supply_root");
        _prefix = "action-report.army-supply-text";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, drawApp32, root, Get(root), _ => false, true, null,
                id => id > root && id <= root + 6 || id is >= 0x00b8 and <= 0x0119).Text;
        }
        finally { _report = previous; }
    }

    internal string ResolveActionAttackRequestExpiryReport(INarrationCurrentContext context, Func<int,int> drawApp32)
    {
        const int root = 0x50c8;
        _prefix = "action-report.attack-request-expiry-text";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, drawApp32, root, Get(root), _ => false, true, null,
                id => id is >= 0x50c9 and <= 0x50ce).Text;
        }
        finally { _report = previous; }
    }

    // Insert into existing NarrationResourceMessages, beside the other Action
    // report entry points. No public type or interpreter branch is required.
    internal string ResolveActionStrategistReport(INarrationCurrentContext context,
        Func<int, int> drawApp32, int root)
    {
        if (root is not (0x50ff or 0x5106 or 0x510d or 0x5114 or 0x511b or 0x5122 or
            0x5129 or 0x512d or 0x5134 or 0x513b or 0x5142 or 0x5149 or 0x5150 or
            0x5157 or 0x515e or 0x5165 or 0x516c or 0x5173 or 0x517a or 0x5181 or
            0x518f or 0x5197)) throw Failure("strategist_root");
        _prefix = "action-report.strategist-text";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, drawApp32, root, Get(root), _ => false, true, null,
                id => id is >= 0x50ff and <= 0x519d or >= 0x0097 and <= 0x0128,
                escapeText: (group, code) => (group, code) switch
                {
                    ('C', 'J') => "^02", ('C', 'L') => "^05",
                    ('E', 'K') or ('E', 'M') => string.Empty,
                    _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
                }, allowOpaqueEscapes: true).Text;
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
            City c => people.CityValue(c.Argument, c.Field),
            Force f => people.ForceValue(f.Argument, f.Field),
            Function f when f.Field == 'x' => people.BaseArgumentValidity(Eval(f.Arguments[0])),
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
          District d => people.DistrictText(d.Argument, d.Field),
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

    //The six actual Army speech roots and their installed TC/SC closure.
    private static readonly HashSet<int> ArmyMapSpeechMessages =
    [
        0x0097, 0x0098, 0x0099, 0x009A, 0x009B, 0x009C, 0x009D, 0x009E, 0x009F, 0x00A0, 0x00A1, 0x00A2,
        0x00A3, 0x00A4, 0x00A5, 0x00A6, 0x00A7, 0x00AB, 0x00AC, 0x00AD, 0x00B1, 0x00B2, 0x00B3, 0x00B4,
        0x00B5, 0x00B6, 0x00B7, 0x00BB, 0x00BC, 0x00C0, 0x00C1, 0x00C2, 0x00C3, 0x00C4, 0x00D0, 0x00D3,
        0x00D7, 0x00D8, 0x00D9, 0x00DA, 0x00DB, 0x00DC, 0x00DD, 0x00DE, 0x00DF, 0x00E0, 0x00E1, 0x00E2,
        0x00E3, 0x00E4, 0x00E5, 0x00E6, 0x00E8, 0x00E9, 0x00EA, 0x00EB, 0x00EC, 0x00ED, 0x00EE, 0x00EF,
        0x00F0, 0x00F1, 0x00F2, 0x00F3, 0x0102, 0x0103, 0x0104, 0x0105, 0x0106, 0x0107, 0x0108, 0x0109,
        0x010A, 0x010B, 0x010C, 0x010D, 0x010E, 0x010F, 0x0110, 0x0111, 0x0112, 0x0113, 0x0114, 0x0115,
        0x0116, 0x0117, 0x0118, 0x0119, 0x011A, 0x011B, 0x011C, 0x011D, 0x011E, 0x011F, 0x0120, 0x0121,
        0x0122, 0x0123, 0x0124, 0x0125, 0x0127, 0x0128, 0xC47F, 0xC480, 0xC481, 0xC482, 0xC483, 0xC484,
        0xC485, 0xC486, 0xC487, 0xC488, 0xC489, 0xC48A, 0xC48B, 0xC48C, 0xC48D, 0xC48E, 0xC48F, 0xC490,
        0xC491, 0xC492, 0xC493, 0xC494, 0xC495, 0xC496, 0xC497, 0xC498, 0xC499, 0xC49A, 0xC49B, 0xC49C,
        0xC49D, 0xC49E, 0xC49F, 0xC4A0, 0xC4A1, 0xC4A2, 0xC4A3, 0xC4A4, 0xC4A5, 0xC4A6, 0xC4A7, 0xC4A8,
        0xC4A9, 0xC4AA, 0xC4AB, 0xC4AC, 0xC4AD, 0xC4AE, 0xC4AF, 0xC4B0, 0xC4B1, 0xC4B2, 0xC4B3, 0xC4B4,
        0xC4B5, 0xC4B6, 0xC4B7, 0xC4B8, 0xC4B9, 0xC4BA, 0xC4BB, 0xC4BC, 0xC4BD, 0xC4BE, 0xC4BF, 0xC4C0,
        0xC4C1, 0xC4C2, 0xC4C3, 0xC4C4, 0xC4C5, 0xC4C6, 0xC4C7, 0xC4C8, 0xC4C9, 0xC4CA, 0xC4CB, 0xC4CC,
        0xC4CD, 0xC4CE, 0xC4E8, 0xC4E9, 0xC829, 0xC82A, 0xC82B, 0xC82C, 0xC82D, 0xC82E, 0xC82F, 0xC830,
        0xC831, 0xC832,
    ];
    internal string ResolveArmyMapSpeech(INarrationCurrentContext context, Func<int, int> drawApp32, int root = 0xC829)
    {
        if (root is not (0xC47F or 0xC4E8 or 0xC4AB or 0xC4E9 or 0xC4C3 or 0xC829) && !ArmyTraitSpeechRoot(root))
            throw new InvalidDataException("army-map-speech.text.root");
        _prefix = "army-map-speech.text";
        bool previous = _report; _report = true;
        bool previousArmySpeech = _armyMapSpeech; _armyMapSpeech = true;
        try
        {
            return ExecuteMessage(context, drawApp32, root, Get(root, true), _ => false, true, null,
                id => ArmyMapSpeechMessages.Contains(id) || ArmyTraitSpeechMessages.Contains(id), (group, code) => (group, code) switch
                {
                    ('C', 'J') => "^02", ('C', 'L') => "^05",
                    ('E', 'K') or ('E', 'M') => string.Empty,
                    _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
                }, true).Text;
        }
        finally { _report = previous; _armyMapSpeech = previousArmySpeech; }
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

    //This real no-report message has no source operands. Keep its parsed
    //resource contract narrow rather than treating arbitrary AST as plain text.
    internal string ResolveActionReportEmpty()
    {
        _prefix = "action-report.empty-text";
        var program = Get(0x5007);
        var result = new StringBuilder();
        foreach (var statement in program.Statements)
            if (statement is Literal literal) result.Append(literal.Text);
            else throw Failure("5007_nonliteral_statement");
        return result.ToString();
    }

    private static readonly HashSet<int> MonthlyMaintenanceMessages =
    [
        0x324D,
        0x00B8, 0x00B9, 0x00BA, 0x00BB, 0x00BC, 0x00BD, 0x00BE, 0x00BF, 0x00C0, 0x00C1, 0x00C2, 0x00C3,
        0x00C4, 0x00C5, 0x00C6, 0x00C7, 0x00C8, 0x00C9, 0x00CA, 0x00CB, 0x00CC, 0x00CD, 0x00CE, 0x00CF,
        0x00D0, 0x00D1, 0x00D2, 0x00D3, 0x00D4, 0x00D5, 0x00D7, 0x00D8, 0x00D9, 0x00DA, 0x00DB, 0x00DC,
        0x00DD, 0x00DE, 0x00DF, 0x00E0, 0x00E1, 0x00E2, 0x00E3, 0x00E4, 0x00E5, 0x00E6, 0x00E7, 0x00E8,
        0x00E9, 0x00EA, 0x00EB, 0x00EC, 0x00ED, 0x00EE, 0x00EF, 0x00F0, 0x00F1, 0x00F2, 0x00F3, 0x00F4,
        0x00F5, 0x00F6, 0x00F7, 0x00F8, 0x00F9, 0x00FA, 0x00FB, 0x00FC, 0x00FD, 0x00FE, 0x00FF, 0x0100,
        0x0101, 0x0119, 0x3055, 0x3056, 0x3057, 0x3058, 0x3059, 0x305A, 0x305B, 0x305C, 0x305D, 0x3174,
        0x320A, 0x320E, 0x3212, 0x324A, 0x324B, 0x324C, 0x5EF8, 0x5EF9, 0x5EFA, 0x5EFB, 0x5EFC, 0x5EFD,
        0x5EFE,

    ];
    internal string ResolveMonthlyMaintenance(INarrationCurrentContext context, Func<int, int> drawApp32, int root)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(drawApp32);
        if (root is not (0x5EF8 or 0x324A or 0x324B or 0x324C or 0x324D or 0x3174 or 0x320A or 0x320E or 0x3212 or
            0x3055 or 0x3056 or 0x3057 or 0x3058 or 0x3059 or 0x305A or 0x305B or 0x305C or 0x305D))
            throw new InvalidDataException("monthly-maintenance.text.root");
        _prefix = "monthly-maintenance.text";
        bool previousReport = _report;
        _report = true;
        try
        {
            string text = ExecuteMessage(context, drawApp32, root, Get(root), _ => false, true, null,
                MonthlyMaintenanceMessages.Contains).Text;
            if (text.Length > 338 || text.Any(c => c == '\0' || char.IsSurrogate(c)))
                throw Failure("complete_short_bmp_profile");
            return text;
        }
        finally { _report = previousReport; }
    }

    private Program Get(int id, bool allowOpaqueEscapes = false)
    {
        var cache = _report ? _reportPrograms : _history ? _historyPrograms : _programs;
        if (cache.TryGetValue(id, out var program)) return program;
        var parser = new Parser(ReadRecord(id), _prefix, _appearance || _report || allowOpaqueEscapes,
            fourPersonArguments: _history || _report, reportArguments: _report);
        program = parser.ReadProgram();
        if (_armyMapSpeech) RequireArmyMapSpeechRecordEnd(id, program, parser);
        else parser.RequireEnd();
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
    private sealed record District(int Argument, int Field) : Expression;
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
            if (selector is 'F' or 'G' || fourPersonArguments && selector is 'H' or 'I' || reportArguments && selector == 'J')
            {
                int field = Peek() >= 0x20 ? Read() : 0;
                return new Person(selector - 'F', field);
            }
            if (selector == 'x' || reportArguments && selector is >= 'y' and <= '|')
            { int field = Peek() >= 0x20 ? Read() : 0; return new City(selector - 'x', field); }
            if (selector == 'P' || reportArguments && selector is >= 'Q' and <= 'T')
            { int field = Peek() >= 0x20 ? Read() : 0; return new Force(selector - 'P', field); }
            if (reportArguments && selector == 'Z')
            { int field = Peek() >= 0x20 ? Read() : 0; return new District(0, field); }
            if (reportArguments && selector is 'n' or 'o')
            { int field = Peek() >= 0x20 ? Read() : 0; return new Army(selector - 'n', field); }
            if (selector == '<' || reportArguments && selector is >= '=' and <= '@')
            { int field = reportArguments && Peek() >= 0x20 ? Read() : 0;
              return new StringArgument(selector - '<', field); }
            if (selector == 0x96)
            { int field = Peek() >= 0x20 ? Read() : 0; return new Area(0, field); }
            if (selector != 0xC8) throw Bad("selector");
            int function = Read();
            int arity = reportArguments && function is ('x' or 'P') ? 1 : function is '2' or '3' or '<' ? 1
                : function == '4' || reportArguments && function == '5' || function is >= '=' and <= 'L' || reportArguments && function is ('M' or 'N' or 'Q') ? 2 : throw Bad("function");
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
        public void RequireReturnedChoiceEnd()
        {
            if (_at != bytes.Length - 2 || bytes[_at] != 1 || bytes[_at + 1] != 'J')
                throw Bad("returned_choice_suffix");
        }
        private InvalidDataException Bad(string field) => new($"{prefix}.resource.{field}@{_at}");
    }
}

