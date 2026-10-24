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
                Templates = CreatePersonnelHireTemplateCatalogue()
            }
        };
        //335D00's two application guards precede classification and generation.
        if (!PersonnelHireServiceAllowedCurrent(state)) return state;

        var current = CaptureWritableTransportRouteCurrent(state);
        var pool = current.ReadOwnedCurrentListPool() ?? throw HireMissing("fresh_list_pool");
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        var session = CreatePersonnelHireGenerationSession(state, current);
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force)
            ?? throw HireMissing("fresh_Force_registryC0"))
        {
            if (entry is null) continue;
            int physical = entry.PhysicalSlot ?? throw HireMissing("fresh_Force_pointer");
            var force = new ForceReference(physical);
            if (!HireNeed(current.ReadForceNativeValidity(force), "fresh_Force_validity")) continue;
            state = GeneratePersonnelHireTemplatesCurrent(state, current, physical, session);
        }
        var lifecycle = state.ReadArmyLifecycle() ?? throw HireMissing("fresh_lifecycle_owner");
        return state.WithArmyLifecycle(lifecycle with { Indexes = pool.ExportOwnedState() });
    }
}
