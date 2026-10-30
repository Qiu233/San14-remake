using System.Collections.Immutable;

namespace San14.Data;

/// <summary>The merchant's ten authored Person-list groups. Captions belong
/// to Data section 1; the shared columns retain their ordinary cell readers.</summary>
internal static class MerchantPersonInformationSources
{
    internal static ImmutableArray<OfficerInformationGroup> Create(Func<int, int, string> text)
    {
        var common = OfficerInformationSources.Create(text);
        var abilities = common[1].Columns;
        var responsibility = common[0].Columns;
        var credit = common[7].Columns;
        var merchant = new OfficerInformationGroup(0, 0x03CD, text(1, 0x03CD),
        [
            abilities.First(column => column.Id == 0),
            abilities.First(column => column.Id == 10),
            abilities.First(column => column.Id == 11),
            responsibility.First(column => column.Id == 35),
            new(141, 0x0637, text(1, 0x0637), 104, 5, false, true, true),
            credit.First(column => column.Id == 194)
        ]);
        return
        [
            merchant,
            common[0] with { Index = 1 },
            common[1] with { Index = 2 },
            common[2] with { Index = 3 },
            common[3] with { Index = 4 },
            common[4] with { Index = 5 },
            common[5] with { Index = 6 },
            common[6] with { Index = 7 },
            common[8] with { Index = 8 },
            common[9] with { Index = 9 }
        ];
    }
}
