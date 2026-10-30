using San14.Data.Formats;

namespace San14.Data;

internal static partial class CollectionEventConditionSource
{
    // Descriptor vslot 1, condition codes 20..58. The surrounding consumer owns
    // selector context and resource names; formatting never captures a world.
    private static int PrepareCommon(S14ConditionInstruction operation, Arguments output,
        CollectionEventConditionTextQueries queries, Func<int, int, string> data)
    {
        var a = operation.RawArguments;
        if (a.Count != 8) throw Missing("arguments");
        string Table(int value, int[] ids)
        {
            if ((uint)value >= (uint)ids.Length) throw Missing("comparison_mode");
            return ids[value] < 0 ? string.Empty : data(1, ids[value]);
        }
        string Numeric(int value) => Table(value, [-1, 0x4F4, 0x4F5, 0x4F6, 0x4F7, 0x4F8, 0x4F9]);
        string Equality(int value) => Table(value, [-1, 0x4F4, 0x4F5, -1, -1, -1, -1]);
        string Boolean(int value) => Table(value, [0x4F2, 0x4F3]);
        string Binary(int value) => Table(value, [0x4F0, 0x4F1]);
        string ForceLabel(int value) => Table(value, [-1, 0x500, 0x501, 0x502, 0x503]);
        string Force(int selector) => selector switch
        {
            -5 => data(1, 0x500), -4 => data(1, 0x501), -3 => data(1, 0x502),
            -2 => data(1, 0x503), -1 => data(1, 0x50C),
            >= 1 and <= 6000 => (queries.ForceName ?? throw Missing("force_name"))(operation, selector),
            _ => string.Empty
        };
        string Name(Func<int, string>? lookup, int id, int count, string field)
        {
            if (id < 0) throw Missing(field + "_index");
            return (lookup ?? throw Missing(field))(id < count ? id : 0);
        }
        int Person(int selector) => queries.ResolvePersonId(operation, selector);
        int City(int selector) => queries.ResolveCityId(operation, selector);
        switch (operation.Code)
        {
            case 0: return 0;
            case 1: return 0x873B;
            case 2: return 0x873C;
            case 3: return 0x873D;
            case 20: case 21: case 23: case 28: case 29: case 39: case 42:
                output.Strings[0] = Numeric(a[1]); output.Strings[1] = Force(a[0]);
                output.Integers[0] = a[2];
                return operation.Code switch
                {
                    20 => 0x874A, 21 => 0x874B, 23 => 0x874D, 28 => 0x8752,
                    29 => 0x8753, 39 => 0x875D, _ => 0x8760
                };
            case 22:
                output.Strings[0] = Numeric(a[1]);
                output.Strings[1] = Name(queries.RegionName, a[3], 12, "region_name");
                output.Strings[2] = Force(a[0]); output.Integers[0] = a[2];
                return 0x874C;
            case 24:
                output.Strings[0] = Table(a[1], [-1, 0x4F4, 0x4F5, 0x4FE, 0x4F9, 0x4FF, 0x4F7]);
                // 30C3B0 has a separate 1..12 gate and otherwise returns empty.
                output.Strings[1] = a[2] is >= 1 and <= 12
                    ? (queries.RankName ?? throw Missing("rank_name"))(a[2]) : string.Empty;
                output.Strings[2] = Force(a[0]); return 0x874E;
            case 25: case 27:
                output.Strings[0] = operation.Code == 25 ? Binary(a[1]) : Boolean(a[1]);
                output.Strings[1] = Force(a[0]); output.Strings[2] = Force(a[2]);
                return operation.Code == 25 ? 0x874F : 0x8751;
            case 26:
                output.Strings[0] = Numeric(a[2]);
                output.Strings[1] = a[3] is >= 1 and <= 6 ? data(0, 0x141 + a[3] - 1) : string.Empty;
                output.Strings[2] = Force(a[0]); output.Strings[3] = Force(a[1]); return 0x8750;
            case 30:
                output.Strings[0] = Name(queries.NationalTitleName, a[1], 101, "national_title_name");
                output.Strings[1] = Binary(a[2]); output.Strings[2] = Force(a[0]); return 0x8754;
            case 31:
                output.Strings[0] = Name(queries.DoctrineName, a[1], 7, "doctrine_name");
                output.Strings[1] = Force(a[0]); return 0x8755;
            case 32:
                output.Strings[0] = Name(queries.NationalTitleName, a[0], 101, "national_title_name");
                output.Strings[1] = Boolean(a[1]); return 0x8756;
            case 33:
                output.People[0] = Person(a[0]); output.Strings[0] = Force(a[1]); return 0x8757;
            case 34:
                output.People[0] = Person(a[0]); output.Strings[0] = ForceLabel(a[1]); return 0x8758;
            case 35:
                output.People[0] = Person(a[1]); output.Strings[0] = Binary(a[2]);
                output.Strings[1] = Force(a[0]); return 0x8759;
            case 36: case 48:
                output.Strings[0] = Equality(a[0]); output.Strings[1] = Force(a[1]);
                return operation.Code == 36 ? 0x875A : 0x8764;
            case 37:
                output.Strings[0] = Numeric(a[1]); output.Strings[1] = Force(a[0]);
                output.Strings[2] = Force(a[2]); output.Integers[0] = a[3]; return 0x875B;
            case 38:
                output.Strings[0] = Numeric(a[1]);
                output.Strings[1] = Name(queries.ProvinceName, a[3], 23, "province_name");
                output.Strings[2] = Force(a[0]); output.Integers[0] = a[2]; return 0x875C;
            case 40:
                output.Strings[0] = Numeric(a[0]); output.Integers[0] = a[1]; return 0x875E;
            case 41:
                output.Strings[0] = Name(queries.RankName, a[0], 13, "rank_name");
                output.Strings[1] = Numeric(a[1]); output.Integers[0] = a[2]; return 0x875F;
            case 43:
                output.Strings[0] = a[1] is >= 1 and <= 5 ? data(0, 0x169 + a[1] - 1) : string.Empty;
                output.Strings[1] = Boolean(a[2]); output.Strings[2] = Force(a[0]); return 0x8761;
            case 44:
                output.LiteralText = "【不要】勢力打倒判定"; return 0;
            case 45:
                output.LiteralText = "【不要】反董卓連合達成判定"; return 0;
            case 46: case 47:
                output.Strings[0] = ForceLabel(a[1]); output.Strings[1] = Force(a[0]);
                return operation.Code == 46 ? 0x8762 : 0x8763;
            case 49:
                output.Cities[0] = (queries.ResolveBaseId ?? throw Missing("base_selector"))(operation, a[0]);
                output.Strings[0] = Equality(a[1]); output.Strings[1] = Force(a[2]); return 0x8765;
            case 50:
                output.Cities[0] = City(a[0]); output.Strings[0] = Equality(a[1]);
                output.Strings[1] = Force(a[2]); return 0x8766;
            case 51: case 52:
                output.Cities[0] = City(a[0]); output.Strings[0] = Numeric(a[1]);
                output.Integers[0] = a[2]; return operation.Code == 51 ? 0x8767 : 0x8768;
            case 53:
                output.LiteralText = "【不要】都市地域担当官任命率"; return 0;
            case 54:
                output.Cities[0] = City(a[0]); output.Strings[0] = Boolean(a[1]); return 0x8769;
            case 55:
                output.Cities[0] = City(a[0]); output.Strings[0] = Numeric(a[1]);
                output.Strings[1] = Table(a[2], [0x46C, 0x46C, 0x46D, 0x46E, 0x46F]); return 0x876A;
            case 56:
                output.People[0] = Person(a[0]); output.Strings[0] = Numeric(a[1]);
                output.Strings[1] = Force(a[2]); return 0x876B;
            case 57:
                output.People[0] = Person(a[0]); output.Strings[0] = Numeric(a[1]);
                output.Cities[0] = City(a[2]); return 0x876C;
            case 58:
                output.People[0] = Person(a[0]); output.People[1] = Person(a[2]);
                output.Strings[0] = Equality(a[1]); return 0x876D;
            default: return Prepare(operation, output, queries, data);
        }
    }
}
