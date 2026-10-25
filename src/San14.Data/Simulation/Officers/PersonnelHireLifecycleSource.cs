using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The fresh hire manager and its independent template catalogue.
    /// Restore never enters this constructor or refills a historical unknown.</summary>
    public GameState InitializeFreshPersonnelHire(GameState state)
    {
        if (state.PersonnelHire is not null)
            throw HireMissing("fresh_manager_already_owned");
        state = InstallFreshPersonnelHireParameters(state);
        var forces = Enumerable.Range(0, 52).ToImmutableDictionary(id => id,
            _ => new PersonnelHireForceState([], []));
        state = state with
        {
            PersonnelHire = new PersonnelHireState(200, forces,
                ImmutableDictionary<int, PersonnelHireContract>.Empty)
            {
                Templates = CreatePersonnelHireTemplateCatalogue(),
                PendingReleaseOfficerIds = ImmutableArray<int>.Empty
            }
        };
        //335D00's two application guards precede classification and generation.
        if (!PersonnelHireServiceAllowedCurrent(state)) return state;

        var current = CaptureWritableTransportRouteCurrent(state);
        var pool = current.ReadOwnedCurrentListPool() ?? throw HireMissing("fresh_list_pool");
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        state = GeneratePersonnelHireForceOffersCurrent(state, current, initializePools: true);
        var lifecycle = state.ReadArmyLifecycle() ?? throw HireMissing("fresh_lifecycle_owner");
        return state.WithArmyLifecycle(lifecycle with { Indexes = pool.ExportOwnedState() });
    }

    //430ED0/334C80. The normal Turn dispatcher owns action52's monthly
    //guard. This leaf cannot skip any preceding Turn action or advance time.
    internal GameState RefreshPersonnelHireTemplatesCurrent(GameState state, SourceWorldContext current)
    {
        if (!PersonnelHireServiceAllowedCurrent(state)) return state;
        var owner = state.PersonnelHire ?? throw HireMissing("refresh_manager");
        var templates = owner.Templates ?? throw HireMissing("refresh_template_catalogue");
        var reset = templates.ToBuilder();
        foreach (var pair in templates)
            reset[pair.Key] = ResetPersonnelHireTemplate(pair.Value);
        state = state with { PersonnelHire = owner with { Templates = reset.ToImmutable() } };
        //Unlike335D00, this caller does not rebuild3294A0's shuffled pools.
        return GeneratePersonnelHireForceOffersCurrent(state, current, initializePools: false);
    }

    private GameState GeneratePersonnelHireForceOffersCurrent(GameState state,
        SourceWorldContext current, bool initializePools)
    {
        BindPersonnelHireGenerationEconomyCurrent(state, current);
        var session = CreatePersonnelHireGenerationSession(state, current, initializePools);
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force)
            ?? throw HireMissing("fresh_Force_registryC0"))
        {
            if (entry is null) continue;
            int physical = entry.PhysicalSlot ?? throw HireMissing("fresh_Force_pointer");
            var force = new ForceReference(physical);
            if (!HireNeed(current.ReadForceNativeValidity(force), "fresh_Force_validity")) continue;
            state = GeneratePersonnelHireTemplatesCurrent(state, current, physical, session);
        }
        return CompletePersonnelHireGeneration(state, session);
    }

    //428F80/33D010, reached only at actual normal Turn action53. Force260
    //removal belongs to32BC50's producer, not a repeated roster scan here.
    internal GameState ReclaimPersonnelHireReleasesCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
    {
        var owner = state.PersonnelHire ?? throw HireMissing("release_manager");
        var queued = owner.PendingReleaseOfficerIds ?? throw HireMissing("release_queue");
        if (queued.IsEmpty) return state;
        var reset = new PersonDeathSource(this, state, current, pool);
        int remaining = owner.RemainingSlots;
        var contracts = owner.Contracts;
        foreach (int officerId in queued)
        {
            int physical = current.ResolveOfficerSource(officerId)
                ?? throw HireMissing("release_Person_pointer");
            state = reset.ReclaimHirePersonCurrent(physical);
            remaining = unchecked(remaining + 1);
            contracts = contracts.Remove(officerId);
        }
        return state with { PersonnelHire = owner with
        {
            RemainingSlots = remaining, Contracts = contracts,
            PendingReleaseOfficerIds = ImmutableArray<int>.Empty
        } };
    }

    internal sealed partial class PersonDeathSource
    {
        //Only the shared226480 reset and registry removal are consumed. No
        //death report, injury, loyalty loss, date or random draw is invoked.
        internal GameState ReclaimHirePersonCurrent(int physical)
        {
            ResetPerson(physical);
            RemoveRegistration(physical);
            return latest;
        }
    }
}
