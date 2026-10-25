using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

internal sealed record CoalitionInvitationPreparation(GameState State, CoalitionInvitationRequest? Request);

public sealed partial class ArmyCampaignRuleAdapter
{
    //2A4500's question preparation precedes the actual player decision. Its
    //2B44E0 result is only a validity gate; Force52 independently owns the
    //speaker, with the actual World9E08 lookup5014 fallback.
    internal CoalitionInvitationPreparation PrepareNormalTurnCoalitionInvitationCurrent(
        GameState state, SourceWorldContext current, ForceReference target,
        IReadOnlyList<int> orderedMemberForceIds)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw Missing("World3A_player_Force");
        if (!K(current.ReadForceNativeValidity(player), "player_Force_validity")) return new(state, null);
        var primary = ForcePrimaryDistrictSource.Resolve(current, player);
        if (!K(current.ReadDistrictNativeValidity(primary), "player_primary_validity")) return new(state, null);

        var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("advisor_shared_pool");
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        var selected = new PersonDeathHistorySelection(state, current, pool, abilities, _governorInputs,
            (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect)).Select(primary);
        if (!Valid(selected)) return new(state, null);

        var playerRuler = Ruler(player);
        if (!Valid(playerRuler)) return new(state, null);
        var targetRuler = Ruler(target);
        if (!Valid(targetRuler)) return new(state, null);
        var advisor = Lookup(K(current.ReadAssignmentForceAdvisor52(K(player.PhysicalSlot, "advisor_Force_pointer")), "Force52"));
        if (!Valid(advisor))
        {
            advisor = Lookup(5014);
            if (!Valid(advisor)) return new(state, null);
        }
        int speakerId = Identity(advisor);
        int rulerId = Identity(playerRuler);
        var question = FormatNormalTurnReport(state, current, 0x5E88,
            new(Person0: speakerId, Person1: rulerId, Person2: Identity(targetRuler)));
        return new(question.State, new(Ordinal(target), Ordinal(player), rulerId, speakerId,
            orderedMemberForceIds.ToImmutableArray(), question.Text));

        TransferPersonReference Ruler(ForceReference force)
            => Lookup(K(current.ReadForceRecord(K(force.PhysicalSlot, "ruler_Force_pointer"))?.RawRuler10, "ruler_Force10"));
        TransferPersonReference Lookup(int raw)
            => (raw < 6001 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
                ?? throw Missing("Person_lookup");
        bool Valid(TransferPersonReference person)
            => person.PhysicalSlot is int physical && K(current.ReadPersonNativeValidity(physical), "Person_validity");
        int Identity(TransferPersonReference person)
            => K(current.ReadPersonIdentity(K(person.PhysicalSlot, "Person_pointer")), "Person10");
        int Ordinal(ForceReference force) => K(current.ReadForcePhysicalSlot(force), "Force60");
        T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        InvalidDataException Missing(string field) => new("normal-turn.coalition-invitation." + field);
    }
}
