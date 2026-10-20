using San14.Core;

namespace San14.Data;

[Flags]
public enum DeploymentPersonDisabledReason { None = 0, Rumor = 1, InsufficientTroops = 2 }

public sealed record DeploymentPersonCandidate(int OfficerId, int PhysicalSlot,
    DeploymentPersonDisabledReason DisabledReasons);

/// <summary>A complete read-only candidate production result, before list sorting.
/// Unknown input yields no partial selectable list. Outside belongs to the caller's
/// deployment draft, not to the world or this page's current selection.</summary>
public sealed record DeploymentPersonSelectionQuery(IReadOnlyList<DeploymentPersonCandidate>? Candidates,
    int? MaximumSelection, string? UnresolvedInput)
{
    public bool Available => Candidates is not null && MaximumSelection.HasValue && UnresolvedInput is null;
}

public sealed partial class ArmyCampaignRuleAdapter
{
    public DeploymentPersonSelectionQuery QueryDeploymentPersonSelection(GameState state,
        int locationId, int outside)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            return BuildDeploymentPersonSelection(state, CaptureDeploymentPersonSelectionCurrent(state), locationId, outside);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    private SourceWorldContext CaptureDeploymentPersonSelectionCurrent(GameState state)
    {
        var arena = state.ReadArmyLifecycle() ?? throw PersonSelectionMissing("saved_indexes");
        var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
        _ = WorldIndexContext.ImportOwnedState(current, arena.Indexes, state);
        if (state.PersonnelCommands is null) throw PersonSelectionMissing("person_association_owner");
        PersonReturnCommands.BindCurrent(state, current);
        _personAssignmentPrecallInputs.BindCurrent(state, current);
        return current;
    }

    private DeploymentPersonSelectionQuery BuildDeploymentPersonSelection(GameState state,
        SourceWorldContext current, int locationId, int outside)
    {
        var target = locationId switch
        {
            >= 1 and <= 51 when state.Cities.ContainsKey(locationId) => new BaseReference(BaseKind.City, locationId),
            >= 52 and <= 61 when state.Gates.ContainsKey(locationId) => new BaseReference(BaseKind.Gate, locationId - 51),
            _ => throw PersonSelectionMissing("base_identity")
        };
        ushort link = PersonSelectionKnown(current.ReadBaseObjectLink(target), "base_link");
        var candidates = new List<DeploymentPersonCandidate>();
        foreach (int physical in EnumerateBaseResidents(current, link, PersonSelectionMissing))
            {
                if (PersonSelectionKnown(current.ReadPersonSecondLocation(physical), "second_location") != link) continue;
                if ((PersonSelectionKnown(current.ReadArmyReturnPersonFlags196(physical), "person_flags") & 1) != 0) continue;
                var association = current.ReadPersonLinkedEntity(physical) ?? throw PersonSelectionMissing("person_association");
                if (association.Identity.HasValue) continue;
                int id = PersonSelectionKnown(current.ReadPersonIdentity(physical), "person_identity");
                if (_definitions.ResolveOfficerSource(id) != physical || !state.Officers.ContainsKey(id))
                    throw PersonSelectionMissing("person_domain_identity");
                var disabled = (PersonSelectionKnown(current.ReadPersonResetWord1FF(physical), "rumor_counter") & 255) != 0
                    ? DeploymentPersonDisabledReason.Rumor : DeploymentPersonDisabledReason.None;
                // Native validity is queried here, after the candidate and
                // rumor branch, solely for the additional troop prohibition.
                if (PersonSelectionKnown(current.ReadPersonNativeValidity(physical), "person_validity") && !CanAffordFormation(physical, id))
                    disabled |= DeploymentPersonDisabledReason.InsufficientTroops;
                candidates.Add(new(id, physical, disabled));
            }
        int district = PersonSelectionKnown(current.ReadBaseDistrictSlot(target), "base_district");
        if (district is < 0 or >= 52) district = 0;
        int books = state.Districts.GetValueOrDefault(district)?.OrdersRemaining is int budget && budget is >= 0 and <= 255
            ? budget : throw PersonSelectionMissing("district_command_budget");
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.Army) ?? throw PersonSelectionMissing("army_registry");
        int registeredArmies = PersonSelectionKnown(current.ReadTemporaryHexListCount(new(descriptor.Identity)), "army_registry_count");
        int maximum = Math.Max(0, Math.Min(Math.Min(unchecked(books - outside), 9),
            unchecked(500 - outside - registeredArmies)));
        return new(candidates.AsReadOnly(), maximum, null);

        bool CanAffordFormation(int physical, int id)
        {
            int stock = PersonSelectionKnown(current.ReadBaseResource(target, BaseResourceKind.Troops), "base_troops");
            uint mask = PersonSelectionKnown(current.ReadPersonFormationMask164(physical), "current_formation_capabilities");
            int minimum = 5000;
            // The installed complete 21-row Formation table has immutable
            // physical identity; native v3 rejects only physical zero.
            for (int formation = 1; formation <= 20; formation++)
            {
                if (!DeploymentFormationEligibility.Allows(mask, formation, id)) continue;
                minimum = Math.Min(minimum, PersonSelectionKnown(TacticRemakeCurrentFormations.ReadRawWord(state, formation, 0x9A), "formation_minimum_troops"));
                if (minimum == 0) break;
            }
            return minimum <= stock;
        }
    }

    // Both deployment consumers use the same native resident iterator. Their
    // post-iterator filters deliberately remain separate and keep their order.
    private static IEnumerable<int> EnumerateBaseResidents(SourceWorldContext current, ushort link,
        Func<string, InvalidDataException> missing)
    {
        if (link is < 1 or > 61) yield break;
        var people = current.ReadRegisteredPersonPhysicalSlots() ?? throw missing("person_registry");
        foreach (int? entry in people)
        {
            int physical = entry ?? throw missing("person_registry_pointer");
            if ((current.ReadPersonFirstLocation(physical) ?? throw missing("first_location")) != link) continue;
            if ((current.ReadPersonStatus(physical) ?? throw missing("service_status")) is not (1 or 2 or 3 or 4)) continue;
            yield return physical;
        }
    }

    private static T PersonSelectionKnown<T>(T? value, string input) where T : struct =>
        value ?? throw PersonSelectionMissing(input);
    private static InvalidDataException PersonSelectionMissing(string input) => new($"deployment.person-selection.{input}");
}
