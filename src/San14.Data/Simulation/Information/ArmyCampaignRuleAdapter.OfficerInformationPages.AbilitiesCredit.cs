using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class OfficerInformationCurrent
    {
        private readonly Dictionary<(int Person, int Dimension), int> informationAbilities = [];
        private readonly Dictionary<int, OfficerCommandCapacityResult> informationCommandCapacities = [];
        private readonly Dictionary<(int Person, int Kind), int> informationCreditBonuses = [];
        private bool informationCreditNamesBound;
        private NewGameOfficerAbilityProducer? newGameAbilityProducer;

        private PreparedCell AbilitiesCreditCell(Person person, int column)
        {
            if (column is >= 8 and <= 12)
            {
                int dimension = column - 8;
                return MakeCell(() => Number(InformationAbility(person, dimension)),
                    () => new NumberKey(InformationAbility(person, dimension)));
            }
            return column switch
            {
                18 => MakeCell(() => Cells.Health(person.Candidate) is int health ? Data(0, 0x123 + health) : null,
                    () => new NumberKey(Cells.HealthKey(person.Candidate))),
                20 => MakeCell(() => InformationItemSuppressed(person) ? Data(1, 0x353) : Number(InformationItemCount(person)),
                    () => InformationItemSuppressed(person) ? new NumberKey(0, true) : new NumberKey(InformationItemCount(person))),
                //The visible getter gates on the District's native validity;
                //the independent sort getter gates on the Person's validity.
                22 => MakeCell(() => Number(InformationDistrictValid(person) ? InformationCommandCapacity(person) : 0),
                    () => new NumberKey(InformationNativeValid(person) ? InformationCommandCapacity(person) : 0),
                    () => InformationCommandPalette(person)),
                193 => MakeCell(() => InformationCreditDisplayVisible(person) && InformationStage(person) != 0
                        ? Number(Need(prestartListInputs is not null ? prestartListInputs.Merit(person.PhysicalSlot) : OfficerMeritSourceAdapter.EncodeMerit(person.Owner.MeritState), "information_merit1BE"))
                        : Data(1, 0x353),
                    () => InformationCellSortKey(Cells.MeritKey(person.Candidate))),
                194 => MakeCell(() => InformationRankName(person),
                    () => InformationCellSortKey(Cells.RankStageKey(person.Candidate))),
                196 => MakeCell(() => InformationBonusText(person, 1),
                    () => new NumberKey(InformationCreditBonus(person, 1))),
                197 => MakeCell(() => InformationBonusText(person, 2),
                    () => new NumberKey(InformationCreditBonus(person, 2))),
                198 => MakeCell(() => InformationBonusText(person, 3),
                    () => new NumberKey(InformationCreditBonus(person, 3))),
                _ => throw Missing("abilities_credit_column." + column)
            };
        }

        private int InformationAbility(Person person, int dimension)
        {
            var key = (person.Id, dimension);
            if (informationAbilities.TryGetValue(key, out int value)) return value;
            if (ArmyOperationContext.IsNewGamePreview(state))
            {
                var read = (newGameAbilityProducer ??= rules.CreateNewGameOfficerAbilities(state,current)).Resolve(person.Id);
                var values = read.Abilities;
                value = values is null ? throw Missing(read.UnresolvedInput ?? "information_effective_ability." + dimension)
                    : dimension switch { 0 => values.Leadership, 1 => values.Strength, 2 => values.Intelligence,
                        3 => values.Politics, 4 => values.Charm, _ => throw new ArgumentOutOfRangeException(nameof(dimension)) };
            }
            else value = Need(Abilities.ResolveDimension(state, person.Id, dimension,
                (force, effect) => rules._baseCapacityInputs.ReadPanelModifier(state, force, effect)),
                "information_effective_ability." + dimension);
            informationAbilities.Add(key, value);
            return value;
        }

        private int InformationCommandCapacity(Person person)
        {
            if (!informationCommandCapacities.TryGetValue(person.Id, out var capacity))
            {
                capacity = prestartListInputs is null ? OfficerCommandCapacity.Resolve(state, current, person.PhysicalSlot)
                    : OfficerCommandCapacity.Resolve(state,current,person.PhysicalSlot,prestartListInputs);
                informationCommandCapacities.Add(person.Id, capacity);
            }
            if (capacity.Capacity is not int value)
                throw new InvalidDataException(capacity.UnresolvedInput ?? Missing("information_command_capacity").Message);
            return value;
        }

        private bool InformationDistrictValid(Person person)
        {
            int raw = Need(current.ReadPersonDistrictSlot(person.PhysicalSlot), "information_command_District_byte");
            var district = current.ReadDistrictTableReference(raw < 52 ? raw : 0)
                ?? throw Missing("information_command_District_pointer");
            return Need(current.ReadDistrictNativeValidity(district), "information_command_District_validity");
        }

        private int? InformationCommandPalette(Person person)
            => CurrentPersonCommandCapacitySource.ReadPalette(state,current,person.PhysicalSlot,
                ()=>InformationCreditBonus(person,1),
                (force,effect)=>prestartListInputs is not null ? prestartListInputs.PolicyModifier(force,effect)
                    : rules._baseCapacityInputs.ReadPanelModifier(state,force,effect),Missing);

        private bool InformationItemSuppressed(Person person) => InformationSpecialForce(person) || Cells.Special(person.Candidate);

        private int InformationItemCount(Person person) => Cells.ItemKey(person.Candidate).Number;

        private bool InformationCreditDisplayVisible(Person person)
        {
            if (Need(rules.ReadApplicationInformationRestriction(state), "information_credit_application_restriction")) return false;
            InformationCreditModeGate();
            return InformationStatus(person) != 1 && !Cells.Special(person.Candidate) &&
                (InformationFlags(person) & 0x4000) == 0;
        }

        private string? InformationRankName(Person person)
        {
            if (!InformationCreditDisplayVisible(person)) return Data(1, 0x353);
            byte stage = InformationStage(person);
            if (stage is < 1 or > 6) return Data(1, 0x353);
            if (!informationCreditNamesBound)
            {
                var source = rules._scenario;
                if (state.ContentHashes.GetValueOrDefault(source.FixedDataSource.ResourceUri) != source.FixedDataSource.Sha256 ||
                    source.Snapshot.CreditRankParameters is not { FormatMarker: 0x66 } catalog || catalog.Ranks.Count != 7)
                    throw Missing("information_credit_rank_catalog");
                informationCreditNamesBound = true;
            }
            var rank = rules._scenario.Snapshot.CreditRankParameters!.Ranks[stage];
            if (rank.Slot != stage) throw Missing("information_credit_rank_slot");
            return rank.Name ?? throw Missing("information_credit_rank_name");
        }

        private int InformationCreditBonus(Person person, int kind)
        {
            var key = (person.Id, kind);
            if (informationCreditBonuses.TryGetValue(key, out int value)) return value;
            //2AA170's application gate returns a known zero, rather than
            //hiding the numeric sort value or reading an unneeded stage.
            if (!InformationNativeValid(person) ||
                Need(rules.ReadApplicationInformationRestriction(state), "information_credit_bonus_application_restriction")) value = 0;
            else
            {
                InformationCreditModeGate();
                if(prestartListInputs is not null)value=prestartListInputs.CreditBonus(person.PhysicalSlot,kind);
                else if (kind == 2) value = PersonAreaAppointmentSource.AdditionalCount(state, current, person.PhysicalSlot, rules.ReadApplicationGameplay()?.UserSelectionServicePresent);
                else
                {
                    var bonus = OfficerCommandCapacity.ResolveCreditKindBonus(state, current, person.PhysicalSlot, kind, rules.ReadApplicationGameplay()?.UserSelectionServicePresent);
                    value = bonus.Bonus ?? throw new InvalidDataException(bonus.UnresolvedInput
                        ?? Missing("information_credit_bonus." + kind).Message);
                }
            }
            informationCreditBonuses.Add(key, value);
            return value;
        }

        private string? InformationBonusText(Person person, int kind)
        {
            int bonus = InformationCreditBonus(person, kind);
            return bonus > 0 ? Data(1, 0x38C).Replace("%d", Number(bonus), StringComparison.Ordinal) : null;
        }
        private void InformationCreditModeGate()
        {
            //The mode3 branch asks for the actual CCCPersonSelectState. No
            //such owner is currently carried by this ordinary page scope.
            if (Need(prestartListInputs is not null ? prestartListInputs.Mode : state.CampaignEventWorld?.ScenarioMode, "information_credit_World_mode") == 3)
                throw Missing("information_credit_CCCPersonSelectState");
        }
        private static SortKey InformationCellSortKey(CityDomesticPersonCellSource.SortValue value)
            => value.Text is null ? new NumberKey(value.Number, value.NotApplicable) : new NameKey(value.Text, value.NotApplicable);
        private bool InformationNativeValid(Person person) => Need(current.ReadPersonNativeValidity(person.PhysicalSlot), "information_Person_validity");
        private byte InformationStatus(Person person) => Need(current.ReadPersonStatus(person.PhysicalSlot), "information_PersonE6");
        private ushort InformationFlags(Person person) => Need(current.ReadArmyReturnPersonFlags196(person.PhysicalSlot), "information_Person15E");
        private byte InformationStage(Person person) => ArmyOperationContext.IsNewGamePreview(state)
            ? Need((newGameAbilityProducer ??= rules.CreateNewGameOfficerAbilities(state,current)).ReadCreditStage(person.Id), "new-game.ability.credit-stage")
            : Need(prestartListInputs is not null ? prestartListInputs.Stage(person.PhysicalSlot) : OfficerMeritSourceAdapter.EncodeStage(person.Owner.MeritState), "information_Person1BC");
        private ForceReference InformationForce(Person person) => current.ReadPersonForce(person.PhysicalSlot)
            ?? throw Missing("information_Person_Force_pointer");
        private bool InformationSpecialForce(Person person) => Need(current.ReadForcePhysicalSlot(InformationForce(person)), "information_Person_Force") is >= 46 and <= 50;
    }
}
