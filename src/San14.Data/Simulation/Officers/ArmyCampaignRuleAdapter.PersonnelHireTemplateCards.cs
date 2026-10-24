using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private PersonnelDevelopmentOfficer ReadPersonnelHireTemplateCard(GameState state,
        SourceWorldContext current, PersonnelHireTemplate template, ArmyMapUiSources naming)
    {
        //Template IDs have their own table. An overlapping world Person is
        //never a source for this card's name, appearance, abilities or traits.
        var person = template.Person;
        var abilities = new List<AreaAdministrationOfficerAbility>(5);
        for (int dimension = 0; dimension < 5; dimension++)
        {
            int? value = person.BaseAbilities is not { } basic ? null : dimension switch
            { 0 => basic.Leadership, 1 => basic.Strength, 2 => basic.Intelligence, 3 => basic.Politics, _ => basic.Charm };
            abilities.Add(new(dimension, value is int known ? AreaAdministrationQuery<int?>.Known(known)
                : AreaAdministrationQuery<int?>.Unknown("personnel.hire.template.base_ability." + dimension),
                AreaAdministrationQuery<int?>.Unknown("personnel.hire.template.effective_ability_context")));
        }
        OfficerPortraitRoute portrait = person.PortraitAppearanceIndex is ushort appearance
            ? ReadOfficerPortraitAppearance(state, current, _definitions, template.Id, appearance, naming)
            : new(template.Id, OfficerPortraitRouteKind.Unresolved, null, null, null, null, null,
                "personnel.hire.template.portrait_appearance");
        var card = new AreaAdministrationOfficerCard(null, state.PeriodNumber, template.Id, person.Name,
            abilities.AsReadOnly(), portrait, Traits(), Flag());
        return new(template.Id, person.Name, portrait, card)
        { NameSortKey = person.CurrentNameSortKey, TemplateId = template.Id };

        AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>> Traits()
        {
            try
            {
                if (Seed.Language != "TC") throw HireMissing("template.traits_context0_selected_edition_table");
                var slots = person.CurrentTraitIds ?? throw HireMissing("template.trait_slots");
                var result = new List<AreaAdministrationOfficerTrait>(5);
                for (int index = 0; index < 5; index++)
                {
                    int slot = index;
                    ushort lookup = TacticRemakeCurrentTraits.SelectPersonalityLookup(current,
                        checked((ushort)person.Id), index, () => slot < slots.Length && slots[slot] is >= 0 and <= ushort.MaxValue
                            ? (ushort)slots[slot] : null) ?? throw HireMissing("template.trait_lookup");
                    if (lookup is < 1 or > 355) continue;
                    var trait = current.ReadPersonalityTableReference(lookup) ?? throw HireMissing("template.trait_pointer");
                    int physical = trait.PhysicalSlot ?? throw HireMissing("template.trait_physical");
                    byte first = current.ReadPersonalityByte(trait, 0xB6) ?? throw HireMissing("template.trait_first_effect");
                    if (!Matches(first))
                    {
                        byte second = current.ReadPersonalityByte(trait, 0xBA) ?? throw HireMissing("template.trait_second_effect");
                        if (!Matches(second)) continue;
                    }
                    byte style = current.ReadPersonalityByte(trait, 0xBE) ?? throw HireMissing("template.trait_style");
                    result.Add(new(physical, current.ReadPersonalityName(trait), style) { LookupTraitId = lookup });
                }
                return AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>>.Known(result.AsReadOnly());
            }
            catch (InvalidDataException error)
            { return AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>>.Unknown(error.Message); }
            static bool Matches(byte effect) => effect is >= 1 and <= 126 && effect != 106;
        }

        AreaAdministrationQuery<AreaAdministrationOfficerFlag> Flag()
        {
            //Only a known empty template affiliation publishes the actual
            //empty-force palette. Missing template facts are not Force0.
            if (person.ForceId != 0) return AreaAdministrationQuery<AreaAdministrationOfficerFlag>
                .Unknown("personnel.hire.template.force_flag");
            var empty = current.ReadForceTableReference(0);
            if (empty is null || current.ReadForceReportColor(empty) is not byte color)
                return AreaAdministrationQuery<AreaAdministrationOfficerFlag>.Unknown("personnel.hire.template.empty_force_color");
            return AreaAdministrationQuery<AreaAdministrationOfficerFlag>.Known(new(0, "", MapSpritePalettes.ForceFill(color), null));
        }
    }
}
