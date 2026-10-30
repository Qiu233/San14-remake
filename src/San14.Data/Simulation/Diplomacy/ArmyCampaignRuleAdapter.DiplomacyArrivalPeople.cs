using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record DiplomacyArrivalSpecialTrait(int PhysicalTraitId, string Name, byte Style);

/// <summary>A current person for the normal Face2 diplomacy mounts. A null
/// special trait is known absence only when TraitUnresolvedInput is also null.</summary>
public sealed record DiplomacyArrivalPerson(int OfficerId, string? Name,
    OfficerPortraitRoute Portrait, DiplomacyArrivalSpecialTrait? SpecialTrait,
    string? NameUnresolvedInput, string? TraitUnresolvedInput);

public sealed record DiplomacyArrivalPeople(int DisplayedForceId,
    AreaAdministrationQuery<DiplomacyArrivalPerson> Ruler,
    ImmutableArray<DiplomacyArrivalPerson> Captives, string? CaptivesUnresolvedInput);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One current capture for the actually displayed party's ruler
    /// and the task's ordered captive people. No other force is substituted.</summary>
    public DiplomacyArrivalPeople QueryDiplomacyArrivalPeople(GameState state,
        DiplomacyNegotiationRequest request, int displayedForceId)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request);
        SourceWorldContext current;
        try { current = CapturePersonPresentationCurrent(state); }
        catch (InvalidDataException error)
        {
            return new(displayedForceId, AreaAdministrationQuery<DiplomacyArrivalPerson>.Unknown(error.Message),
                [], error.Message);
        }
        var naming = ArmyMapUiSources.Create();
        AreaAdministrationQuery<DiplomacyArrivalPerson> ruler;
        try
        {
            if (displayedForceId is < 0 or > 51 ||
                current.ReadForceTableReference(displayedForceId)?.PhysicalSlot != displayedForceId)
                throw ArrivalPeopleMissing("displayed_force_identity");
            int rulerId = current.ReadForceRecord(displayedForceId)?.RawRuler10
                ?? throw ArrivalPeopleMissing("displayed_force_ruler");
            ruler = AreaAdministrationQuery<DiplomacyArrivalPerson>.Known(
                ReadDiplomacyArrivalPerson(state, current, rulerId, false, naming));
        }
        catch (InvalidDataException error)
        { ruler = AreaAdministrationQuery<DiplomacyArrivalPerson>.Unknown(error.Message); }
        var captives = request.RequestedCaptiveOfficerIds.IsDefault
            ? AreaAdministrationQuery<ImmutableArray<DiplomacyArrivalPerson>>.Unknown("diplomacy.people.captive_list")
            : ReadDiplomacyCaptivePeople(state, current, request.RequestedCaptiveOfficerIds, naming);
        return new(displayedForceId, ruler, captives.Available ? captives.Value : [], captives.UnresolvedInput);
    }

    /// <summary>The genuine dispatch selection uses the same people producer,
    /// without inventing an arrival request or allocating a diplomacy task.</summary>
    public AreaAdministrationQuery<ImmutableArray<DiplomacyArrivalPerson>> QueryDiplomacyCaptivePeople(
        GameState state, IReadOnlyList<int> actualIds)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(actualIds);
        try
        {
            var current = CapturePersonPresentationCurrent(state);
            return ReadDiplomacyCaptivePeople(state, current, actualIds, ArmyMapUiSources.Create());
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<ImmutableArray<DiplomacyArrivalPerson>>.Unknown(error.Message); }
    }

    private AreaAdministrationQuery<ImmutableArray<DiplomacyArrivalPerson>> ReadDiplomacyCaptivePeople(
        GameState state, SourceWorldContext current, IReadOnlyList<int> actualIds, ArmyMapUiSources naming)
    {
        if (actualIds.Count > 3)
            return AreaAdministrationQuery<ImmutableArray<DiplomacyArrivalPerson>>.Unknown("diplomacy.people.captive_count");
        var result = ImmutableArray.CreateBuilder<DiplomacyArrivalPerson>(actualIds.Count);
        foreach (int id in actualIds)
            result.Add(ReadDiplomacyArrivalPerson(state, current, id, true, naming));
        return AreaAdministrationQuery<ImmutableArray<DiplomacyArrivalPerson>>.Known(result.ToImmutable());
    }

    private DiplomacyArrivalPerson ReadDiplomacyArrivalPerson(GameState state, SourceWorldContext current,
        int officerId, bool includeSpecialTrait, ArmyMapUiSources naming)
    {
        string? name = null, nameMissing = null, traitMissing = null;
        DiplomacyArrivalSpecialTrait? trait = null;
        OfficerPortraitRoute portrait;
        int physical;
        try
        {
            if (officerId is < 0 or > 6000)
                throw ArrivalPeopleMissing("person_lookup");
            physical = current.ReadPersonLookupReference(officerId)?.PhysicalSlot
                ?? throw ArrivalPeopleMissing("person_pointer");
            if (current.ReadPersonIdentity(physical) != officerId ||
                _definitions.ResolveOfficerSource(officerId) != physical)
                throw ArrivalPeopleMissing("person_identity");
        }
        catch (InvalidDataException error)
        {
            return new(officerId, null, UnknownPortrait(officerId, error.Message), null, error.Message,
                includeSpecialTrait ? error.Message : null);
        }
        var person = state.Officers.GetValueOrDefault(officerId);
        try
        {
            //22F020/22F060 concatenate the current surname and given name.
            //The domain display name is not a fallback for absent saved units.
            if (person is null || person.CurrentSurname is not string surname || person.CurrentGivenName is not string given)
                throw ArrivalPeopleMissing("current_name");
            if (surname.Contains('\uFFFD') || given.Contains('\uFFFD'))
                throw ArrivalPeopleMissing("current_name_encoding");
            name = surname + given;
        }
        catch (InvalidDataException error) { nameMissing = error.Message; }
        try
        {
            bool valid = current.ReadPersonNativeValidity(physical)
                ?? throw ArrivalPeopleMissing("portrait_person_validity");
            if (!valid)
                portrait = new(officerId, OfficerPortraitRouteKind.Hidden, null, null, null, null, null, null);
            else
            {
                ushort appearance = person?.PortraitAppearanceIndex ?? throw ArrivalPeopleMissing("current_appearance");
                if (current.ReadPersonPortraitAppearance(physical) != appearance)
                    throw ArrivalPeopleMissing("appearance_binding");
                portrait = ReadOfficerPortraitAppearance(state, current, _definitions, officerId, appearance, naming);
            }
        }
        catch (InvalidDataException error) { portrait = UnknownPortrait(officerId, error.Message); }
        if (includeSpecialTrait)
        {
            try { trait = ReadDiplomacyArrivalSpecialTrait(current, physical); }
            catch (InvalidDataException error) { traitMissing = error.Message; }
        }
        return new(officerId, name, portrait, trait, nameMissing, traitMissing);
    }

    private static DiplomacyArrivalSpecialTrait? ReadDiplomacyArrivalSpecialTrait(SourceWorldContext current,
        int personPhysical)
    {
        HexListHandle? temporary = null;
        try
        {
            temporary = current.CreateTemporaryHexList() ?? throw ArrivalPeopleMissing("trait_list");
            //2307D0 scans all nine selected slots. Preserve successful append
            //order and duplicates, including entries beyond the detail's five.
            var admitted = new List<PersonalityReference>(9);
            for (int slot = 0; slot < 9; slot++)
            {
                int raw = TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, personPhysical, slot)
                    ?? throw ArrivalPeopleMissing("trait_slot_" + slot);
                var trait = current.ReadPersonalityTableReference(raw <= 355 ? raw : 0)
                    ?? throw ArrivalPeopleMissing("trait_lookup_" + slot);
                bool valid = current.ReadSkillPersonalityValidity(trait)
                    ?? throw ArrivalPeopleMissing("trait_validity_" + slot);
                if (valid && current.AppendSkillTemporaryPersonality(temporary, trait)) admitted.Add(trait);
            }
            PersonalityReference? selected = null;
            foreach (var trait in admitted)
            {
                byte first = current.ReadPersonalityByte(trait, 0xB6)
                    ?? throw ArrivalPeopleMissing("trait_effect_first");
                if (first != 15 && (current.ReadPersonalityByte(trait, 0xBA)
                    ?? throw ArrivalPeopleMissing("trait_effect_second")) != 15) continue;
                selected = trait; //Later matching entries overwrite the same source mount.
            }
            if (selected is null) return null;
            int physical = selected.PhysicalSlot ?? throw ArrivalPeopleMissing("special_trait_receiver");
            string name = current.ReadPersonalityName(selected);
            byte style = current.ReadPersonalityByte(selected, 0xBE)
                ?? throw ArrivalPeopleMissing("special_trait_style");
            //858A80 selects the common marker's node2 sprite from StyleBE.
            //This is not a font preset or a text/sprite palette index.
            return new(physical, name, style);
        }
        finally
        {
            if (temporary is not null)
            {
                current.ClearTemporaryHexList(temporary);
                current.ReleaseTemporaryHexList(temporary);
            }
        }
    }

    private static OfficerPortraitRoute UnknownPortrait(int id, string input) =>
        new(id, OfficerPortraitRouteKind.Unresolved, null, null, null, null, null, input);
    private static InvalidDataException ArrivalPeopleMissing(string field) => new("diplomacy.people." + field);
}
