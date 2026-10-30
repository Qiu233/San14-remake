using System.Text;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class OfficerInformationCurrent
    {
        private readonly Dictionary<(int Person, int Slot), TacticReference> militaryTactics = [];
        private readonly Dictionary<(int Person, int Slot), PersonalityReference> militaryTraits = [];
        private readonly Dictionary<int, int> militaryPolicies = [];
        private S14PolicyCatalog? militaryPolicyCatalog;

        private PreparedCell MilitaryCell(Person person, int column)
        {
            if (column is >= 40 and <= 50)
            {
                int formation = column == 50 ? 14 : column - 39;
                return MakeCell(() => MilitaryFormation(person, formation) ? Data(1, 0x338) : null,
                    () => new NumberKey(0, !MilitaryFormation(person, formation)));
            }
            if (column is >= 51 and <= 60)
            {
                int slot = column - 51;
                return MakeCell(() =>
                {
                    var tactic = MilitaryTactic(person, slot);
                    if (!Need(current.ReadSkillTacticValidity(tactic), "tactic_validity")) return null;
                    int id = Need(current.ReadSkillTacticPhysicalSlot(tactic), "tactic_receiver");
                    return state.Tactics.GetValueOrDefault(id)?.Name ?? throw Missing("tactic_name");
                }, () =>
                {
                    var tactic = MilitaryTactic(person, slot);
                    return Need(current.ReadSkillTacticValidity(tactic), "tactic_sort_validity")
                        ? new NumberKey(Need(current.ReadSkillTacticPhysicalSlot(tactic), "tactic_sort_receiver"))
                        : new NumberKey(0, true);
                });
            }
            if (column is >= 61 and <= 65)
            {
                int slot = column - 61;
                return MakeCell(() =>
                {
                    var trait = MilitaryTrait(person, slot);
                    return Need(current.ReadSkillPersonalityValidity(trait), "trait_validity")
                        ? MilitaryTraitText(trait, true) : null;
                }, () =>
                {
                    var trait = MilitaryTrait(person, slot);
                    bool valid = Need(current.ReadSkillPersonalityValidity(trait), "trait_sort_validity");
                    return valid ? new MilitaryTraitKey(true, MilitaryTraitText(trait, true), MilitaryTraitText(trait, false))
                        : new MilitaryTraitKey(false, "", "");
                });
            }
            return column switch
            {
                97 => MakeCell(() =>
                {
                    if (MilitarySuppressed(person, true)) return Data(1, 0x353);
                    if (!MilitaryNativeValid(person)) return null;
                    var policy = MilitaryPolicy(person);
                    if (policy is null) return null;
                    int group = Need(policy.DepartmentCode, "policy_department");
                    return group is >= 0 and <= 4 ? Data(1, 0x2a0 + group) : null;
                }, () =>
                {
                    if (!MilitaryNativeValid(person) || MilitarySuppressed(person, true)) return new NumberKey(0, true);
                    var policy = MilitaryPolicy(person);
                    return policy is null ? new NumberKey(0, true)
                        : new NumberKey(Need(policy.DepartmentCode, "policy_sort_department"));
                }),
                98 => MakeCell(() =>
                {
                    if (MilitarySuppressed(person, true)) return Data(1, 0x353);
                    return MilitaryNativeValid(person) ? MilitaryPolicy(person)?.Name : null;
                }, () =>
                {
                    //34E550 returns a known empty text for an invalid Person,
                    //but a tag2 value for the special/flagged exclusions.
                    if (!MilitaryNativeValid(person)) return new NameKey("");
                    if (MilitarySuppressed(person, true)) return new NameKey("", true);
                    int id = MilitaryPolicyPhysical(person);
                    if (id == 0) return new NameKey("");
                    var rows = MilitaryPolicyDefinitions.Policies;
                    if ((uint)id >= (uint)rows.Count || rows[id].Slot != id) throw Missing("policy_sort_definition");
                    return new NameKey(rows[id].SecondaryText ?? throw Missing("policy_sort_text"));
                }),
                99 => MakeCell(() =>
                {
                    if (MilitarySuppressed(person, true)) return Data(1, 0x353);
                    return MilitaryNativeValid(person) ? Number(MilitaryPolicyLevel(person)) : null;
                }, () => !MilitaryNativeValid(person) || MilitarySuppressed(person, true)
                    ? new NumberKey(0, true) : new NumberKey(MilitaryPolicyLevel(person)),
                    () => Cells.CreditBonus(person.Candidate, 3) > 0 ? 0x41 : null),
                26 => MakeCell(() =>
                {
                    if (MilitarySuppressed(person, true)) return Data(1, 0x353);
                    var cost = MilitaryPolicyCost(person);
                    return cost.Total < cost.BeforeDiscount
                        ? Number(cost.Total) + "(" + Number(cost.Total - cost.BeforeDiscount) + ")"
                        : Number(cost.Total);
                }, () => MilitarySuppressed(person, true) ? new NumberKey(0, true)
                    : new NumberKey(MilitaryPolicyCost(person).Total)),
                101 => MakeCell(() =>
                {
                    //361220 omits the15E.bit14 exclusion used by97..99/26.
                    if (MilitarySuppressed(person, false)) return Data(1, 0x353);
                    return MilitaryNativeValid(person) ? MilitaryPolicy(person)?.Description : null;
                }),
                _ => throw Missing("military_column." + column)
            };
        }

        private bool MilitaryFormation(Person person, int formation) => DeploymentFormationEligibility.Allows(
            Need(current.ReadPersonFormationMask164(person.PhysicalSlot), "formation_mask"), formation, person.Id);

        private TacticReference MilitaryTactic(Person person, int slot)
        {
            if (militaryTactics.TryGetValue((person.Id, slot), out var tactic)) return tactic;
            //Same22A4B0 qualification/gem gate as the mature deployment list.
            tactic = DeploymentTacticSelectionSource.ReadCurrentSlot(current, person.Id, person.PhysicalSlot, slot);
            return militaryTactics[(person.Id, slot)] = tactic;
        }

        private PersonalityReference MilitaryTrait(Person person, int slot)
        {
            if (militaryTraits.TryGetValue((person.Id, slot), out var trait)) return trait;
            //The table reads the actual first five230730 slots. Unlike the
            //detail's marker list, it never compacts nine slots to five valid ones.
            trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, person.PhysicalSlot, slot)
                ?? throw Missing("trait_reference." + slot);
            return militaryTraits[(person.Id, slot)] = trait;
        }

        private string MilitaryTraitText(PersonalityReference trait, bool name)
        {
            int count = name ? 6 : 16, offset = name ? 0x10 : 0x1c;
            var text = new StringBuilder(count);
            for (int index = 0; index < count; index++)
            {
                ushort unit = Need(current.ReadPersonalityWord(trait, offset + index * 2),
                    name ? "trait_name" : "trait_sort_text");
                if (unit == 0) return text.ToString();
                text.Append((char)unit);
            }
            throw Missing(name ? "trait_name_terminator" : "trait_sort_text_terminator");
        }

        private sealed record MilitaryTraitKey(bool Valid, string Name, string Text) : SortKey
        {
            internal override bool Before(SortKey other, bool ascending)
            {
                if (other is not MilitaryTraitKey key) throw Missing("trait_sort_key_type");
                //354A00's invalid branch does not consult the direction flag.
                if (!Valid || !key.Valid) return !key.Valid;
                bool before = Text.Length != 0 && key.Text.Length != 0
                    ? string.CompareOrdinal(Text, key.Text) < 0
                    : Text.Length == 0 && key.Text.Length == 0
                        ? string.CompareOrdinal(Name, key.Name) < 0 : key.Text.Length == 0;
                //The real reverse is non-strict: equal valid keys return true.
                return ascending ? before : !before;
            }
        }

        private bool MilitaryNativeValid(Person person) => Need(
            current.ReadPersonNativeValidity(person.PhysicalSlot), "military_person_validity");

        private bool MilitarySuppressed(Person person, bool flagged)
        {
            var force = current.ReadPersonForce(person.PhysicalSlot) ?? throw Missing("policy_person_force");
            return Need(force.PhysicalSlot, "policy_force_receiver") is >= 46 and <= 50 ||
                Cells.Special(person.Candidate) || flagged &&
                (Need(current.ReadArmyReturnPersonFlags196(person.PhysicalSlot), "policy_person_flags") & 0x4000) != 0;
        }

        private int MilitaryPolicyPhysical(Person person)
        {
            if (militaryPolicies.TryGetValue(person.Id, out int physical)) return physical;
            int raw = person.Owner.PolicyId ?? throw Missing("policy_personal_reference");
            if ((uint)raw > byte.MaxValue) throw Missing("policy_personal_byte");
            var reference = current.ReadStaticTableReference(0x78d50, raw < 121 ? raw : 0)
                ?? throw Missing("policy_lookup");
            physical = Need(reference.PhysicalSlot, "policy_receiver");
            return militaryPolicies[person.Id] = physical;
        }

        private PolicyDefinition? MilitaryPolicy(Person person)
        {
            int physical = MilitaryPolicyPhysical(person);
            //CPolicyData.v3 native validity is its nonzero physical ordinal.
            return physical == 0 ? null : state.Policies.GetValueOrDefault(physical)
                ?? throw Missing("policy_current_definition");
        }

        private S14PolicyCatalog MilitaryPolicyDefinitions => militaryPolicyCatalog ??=
            rules._scenario.Snapshot.PolicyParameters ?? rules._repository.LoadPolicies(
                rules._scenario.FixedDataSource.SourcePath).Catalog;

        private int MilitaryPolicyLevel(Person person) => Math.Min(10,
            MilitaryPolicyPersonalByte(person) + Cells.CreditBonus(person.Candidate, 3));

        private static int MilitaryPolicyPersonalByte(Person person)
        {
            int value = person.Owner.PolicyBaseCost ?? throw Missing("policy_personal_level");
            return (uint)value <= byte.MaxValue ? value : throw Missing("policy_personal_level_byte");
        }

        private PolicyCost MilitaryPolicyCost(Person person)
        {
            var policy = MilitaryPolicy(person);
            //230A50 starts at zero for the invalid policy receiver, then clamps
            //to one. It does not return a blank merely because policy0 is invalid.
            int basis = policy is null ? 0 : MilitaryPolicyPersonalByte(person);
            int addition = policy?.CostAddition ?? 0;
            bool same = false, rulerIsPerson = false;
            var force = current.ReadPersonForce(person.PhysicalSlot) ?? throw Missing("policy_cost_force");
            if (Need(current.ReadForceNativeValidity(force), "policy_cost_force_validity"))
            {
                int forcePhysical = Need(force.PhysicalSlot, "policy_cost_force_receiver");
                int raw = Need(current.ReadForceRecord(forcePhysical)?.RawRuler10, "policy_cost_ruler");
                var ruler = current.ReadPersonLookupReference(raw < 6001 ? raw : 0) ?? throw Missing("policy_cost_ruler_lookup");
                int physical = Need(ruler.PhysicalSlot, "policy_cost_ruler_receiver");
                if (Need(current.ReadPersonNativeValidity(physical), "policy_cost_ruler_validity"))
                {
                    int id = Need(current.ReadPersonIdentity(physical), "policy_cost_ruler_identity");
                    rulerIsPerson = id == person.Id;
                    if (!rulerIsPerson) same = MilitaryDoctrine(person.Owner) == MilitaryDoctrine(ReadPerson(id).Owner);
                }
            }
            return PolicyCosts.Calculate(basis, addition, same, rulerIsPerson);
        }

        private int MilitaryDoctrine(OfficerState person)
        {
            int raw = person.DoctrineId ?? person.AbilityState?.ObservedDoctrineId ?? throw Missing("policy_cost_doctrine");
            if ((uint)raw > byte.MaxValue) throw Missing("policy_cost_doctrine_byte");
            var reference = current.ReadStaticTableReference(0x78cf8, raw < 7 ? raw : 0)
                ?? throw Missing("policy_cost_doctrine_lookup");
            return Need(reference.PhysicalSlot, "policy_cost_doctrine_receiver");
        }
    }
}
