using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class OfficerInformationCurrent
    {
        private readonly Dictionary<int, CityDomesticPersonForceName?> basicForceNames = [];

        private PreparedCell BasicCell(Person person, int column)
        {
            var candidate = person.Candidate;
            return column switch
            {
                0 => MakeCell(() => Cells.Name(candidate), () => new NameKey(Cells.Name(candidate, true))),
                1 => BasicForceCell(person),
                2 => MakeCell(() => Cells.DistrictNumber(candidate) is int number
                        ? Data(1, 0x494).Replace("%d", Number(number), StringComparison.Ordinal) : null,
                    () => BasicNumberKey(Cells.DistrictKey(candidate))),
                3 => MakeCell(() => Cells.Home(candidate) ?? Data(1, 0x353), () => BasicLocationKey(person, false)),
                4 => MakeCell(() => BasicCurrentPlace(person), () => BasicLocationKey(person, true)),
                5 => MakeCell(() => (InformationFlags(person) & 0x4000) != 0 ? Data(1, 0x859)
                        : InformationStatus(person) < 10 ? Data(0, 0xf9 + InformationStatus(person)) : null,
                    () => new NumberKey(Cells.ServiceStatusKey(candidate)), statePalette: () => BasicRowPalette(person, 10)),
                6 => MakeCell(() => Cells.Loyalty(candidate) is int loyalty ? Number(loyalty) : Data(1, 0x353),
                    () => BasicNumberKey(Cells.LoyaltyKey(candidate))),
                7 => MakeCell(() => Cells.ServiceYears(candidate, true) is int years ? Number(years) : Data(1, 0x353),
                    () => BasicNumberKey(Cells.ServiceYearsKey(candidate))),
                21 => MakeCell(() => Cells.Office(candidate) ?? Data(1, 0x353), () => BasicNumberKey(Cells.OfficeKey(candidate))),
                23 => MakeCell(() => BasicSalary(person, true) is int salary ? Number(salary) : Data(1, 0x353),
                    () => new NumberKey(BasicSalary(person, false) ?? 0), statePalette: () => BasicRowPalette(person, 12)),
                35 => MakeCell(() => BasicResponsibility(person), () => BasicResponsibilityKey(person)),
                36 => MakeCell(() => InformationNativeValid(person) && IsArmyPosition(person) ? Data(1, 0x338) : null,
                    () => new NumberKey(IsArmyPosition(person) ? 1 : 0)),
                _ => throw Missing("basic.column." + column)
            };
        }

        private static SortKey BasicNumberKey(CityDomesticPersonCellSource.SortValue key)
            => key.Text is null ? new NumberKey(key.Number, key.NotApplicable) : new NameKey(key.Text, key.NotApplicable);

        // The list's display and sort getters consume Person E4's decoded
        // position kind; an active Army lookup is a different operation.
        private bool IsArmyPosition(Person person) => PersonPositionRepair.DecodeSecondLocation(
            Need(current.ReadPersonSecondLocation(person.PhysicalSlot), "deployed.Person_E4")).Kind == 27;

        private PreparedCell BasicForceCell(Person person)
        {
            CityDomesticPersonForceName? Name()
            {
                if (!basicForceNames.TryGetValue(person.Id, out var value))
                    basicForceNames.Add(person.Id, value = Cells.ForceName(person.Candidate));
                return value;
            }
            var result = MakeCell(() => Name()?.Text, () =>
            {
                var key = Cells.ForceOrderInput(person.Candidate);
                return new BasicForceKey(key.Valid, key.PhysicalSlot, key.Key);
            });
            try { return result with { Display = result.Display with { MarkerColour = Name()?.MarkerColour } }; }
            catch (InvalidDataException error)
            { return result with { Display = result.Display with { UnresolvedOwner = Join(result.Display.UnresolvedOwner, error.Message) } }; }
        }

        private sealed record BasicForceKey(bool Valid, int Physical, string Text) : SortKey
        {
            internal override bool Before(SortKey other, bool ascending)
            {
                var right = (BasicForceKey)other;
                if (!Valid) return false;
                if (!right.Valid) return true;
                if (!ascending) return right.Before(this, true);
                string a = Head(Text), b = Head(right.Text);
                if (a.Length + b.Length + 2 > 1024) throw Missing("Force_sort_text_capacity");
                return a == b ? Physical < right.Physical : DeploymentResponsibilityNameOrder.Before(a, b);
            }
        }

        private string? BasicCurrentPlace(Person person) => Cells.CurrentLocation(person.Candidate);

        private SortKey BasicLocationKey(Person person, bool actual) =>
            new BasicLocationSortKey(Cells.LocationOrderInput(person.Candidate, actual));

        private sealed record BasicLocationSortKey(PersonLocationOrderKey Value) : SortKey
        {
            internal override bool Before(SortKey other, bool ascending) =>
                Value.Before(((BasicLocationSortKey)other).Value, !ascending);
        }

        private static string Head(string value) => value.IndexOf('\0') is int at && at >= 0 ? value[..at] : value;

        private int? BasicSalary(Person person, bool display) => display
            ? Cells.OtherMetric(person.Candidate) : Cells.OtherMetricKey(person.Candidate).Number;

        private string? BasicResponsibility(Person person)
        {
            var result = prestartListInputs is null
                ? DeploymentPersonResponsibilityDisplaySource.Read(state, current, person.PhysicalSlot)
                : DeploymentPersonResponsibilityDisplaySource.Read(state,current,person.PhysicalSlot,
                    prestartListInputs.AssignmentCount,prestartListInputs.AssignmentEntry);
            return result.Kind switch
            {
                DeploymentPersonResponsibilityKind.NotApplicable => Data(1, 0x353),
                DeploymentPersonResponsibilityKind.AreaName => state.Areas.GetValueOrDefault(result.AreaId ?? 0)?.Name ?? throw Missing("responsibility_Area_name"),
                DeploymentPersonResponsibilityKind.Recruit => Data(1, 0x20),
                DeploymentPersonResponsibilityKind.Train => Data(1, 0x21),
                DeploymentPersonResponsibilityKind.Spy => Data(1, 0x9c),
                _ => null
            };
        }

        private SortKey BasicResponsibilityKey(Person person)
        {
            if (!InformationNativeValid(person) || InformationSpecialForce(person) || Cells.Special(person.Candidate))
                return new BasicDutyKey(0, "", true);
            var key = DeploymentPersonResponsibilitySource.Read(state, current, person.PhysicalSlot, () => Cells.Special(person.Candidate));
            string name = key.NamedArea is int area ? state.Areas.GetValueOrDefault(area)?.Name ?? throw Missing("responsibility_sort_Area_name") : "";
            return new BasicDutyKey(key.Weight == 9995 ? 5 : key.Weight, name, false);
        }
        private sealed record BasicDutyKey(int Weight, string Name, bool Empty) : SortKey
        {
            internal override bool Before(SortKey other, bool ascending)
            {
                var right = (BasicDutyKey)other;
                if (Empty != right.Empty) return !Empty;
                if (Empty) return false;
                if (!ascending) return right.Before(this, true);
                return Weight == right.Weight ? DeploymentResponsibilityNameOrder.Before(right.Name, Name) : Weight < right.Weight;
            }
        }

        private readonly Dictionary<int, InformationTextStatePresets?> rowPaletteCache = [];
        private readonly Dictionary<int, string> rowPaletteMissing = [];
        private static readonly InformationTextStatePresets landAdvantageTextPresets = new(65, 65, 66, 11);

        private InformationTextStatePresets? BasicRowPalette(Person person, int selector, bool? alteredRow = null)
        {
            if (rowPaletteEnabled is false) return null;
            if (selector == 10)
            {
                // Only the known ordinary Info owner establishes its empty
                // injury predicate. Other shared selector contexts retain
                // their existing healthy early-out and unresolved callback.
                if (rowPaletteEnabled is true && !commandBodyPalette) return null;
                if (!(alteredRow ?? (Need(OfficerHealthSourceAdapter.ReadByte(current, person.PhysicalSlot),
                    "dynamic_palette.Person_health") is >= 1 and <= 3))) return null;
                if (!commandBodyPalette) throw Missing("dynamic_palette.enabled_owner");
            }
            if (rowPaletteEnabled is not true) throw Missing("dynamic_palette.enabled_owner");
            if (rowPaletteMissing.TryGetValue(selector, out var missing)) throw new InvalidDataException(missing);
            if (rowPaletteCache.TryGetValue(selector, out var saved)) return saved;
            if (selector != 12 && !(commandBodyPalette && selector is >= 3 and <= 13)) throw Missing("dynamic_palette.selector");
            try
            {
                var player = current.ReadForceTableReference(playerForceId is >= 0 and < 52 ? playerForceId : 0)
                    ?? throw Missing("dynamic_palette.player_pointer");
                InformationTextStatePresets? result = null;
                if (Need(current.ReadForceNativeValidity(player), "dynamic_palette.player_validity"))
                {
                    // Source2C68D0 delegates selectors3..13 to the real current
                    // State/Advantage aggregate, rather than policy or row flags.
                    var advantage = PersonReturnPathSource.ReadForceAdvantage(state, current, player, selector);
                    if (advantage.Value is not int value) throw new InvalidDataException(advantage.UnresolvedOwner ?? Missing("dynamic_palette.advantage").Message);
                    if (value != 0) result = landAdvantageTextPresets;
                }
                rowPaletteCache.Add(selector, result);
                return result;
            }
            catch (InvalidDataException error)
            {
                rowPaletteMissing.Add(selector, error.Message);
                throw;
            }
        }
    }
}
