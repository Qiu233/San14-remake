using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The enclosing action table retains69's actual month-start guard.
    internal GameState ApplyNormalTurnTerritoryFinalizationCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int action)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw FinalizationMissing("shared_current_pool");
        return action switch
        {
            67 => ApplyNormalTurnTerritoryExpansionCurrent(state, current, pool),
            68 => RefreshNormalTurnCommandAvailabilityCurrent(state, current, pool, false),
            69 => RefreshNormalTurnCommandAvailabilityCurrent(state, current, pool, true),
            70 => ResetNormalTurnPersonnelGroupMoveDestinations(state),
            71 => ApplyNormalTurnSpecialPersonnelStartCurrent(state, current, pool),
            72 => ApplyNormalTurnSpecialForceDemandsCurrent(state, current, pool),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }

    private static GameState ResetNormalTurnPersonnelGroupMoveDestinations(GameState state)
    {
        //6BF9A0 clears the ten destination words used by the PersonGroupEdit/
        //PersonGroupMove pages. Their saved member lists are not cleared.
        var pending = state.CampaignEffectRequests ?? throw FinalizationMissing("group_move_pending_owner");
        long revision = state.NativeClock?.UpdateOrdinal ?? throw FinalizationMissing("group_move_revision");
        return state with { CampaignEffectRequests = pending.Append(CampaignEffectSource.MonthlyMaintenance, revision,
            [new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.ResetPersonnelGroupMoveDestinations, null, null, null) }]) };
    }

    private static IEnumerable<int> FinalizationRegistry(SourceWorldContext current, RegistryKind kind)
    {
        foreach (var pointer in current.ReadRegistryReferences(kind) ?? throw FinalizationMissing("registry_" + kind))
        {
            if (pointer is null || pointer.Kind != kind) throw FinalizationMissing("registry_payload_" + kind);
            yield return FinalizationNeed(pointer.PhysicalSlot, "registry_pointer_" + kind);
        }
    }
    private static HexListHandle FinalizationTemporary(SourceWorldContext current)
    {
        if (FinalizationNeed(current.ReadRegistryLockEnabled(), "pool_lock")) current.EnterRegistryLock();
        try { return current.CreateTemporaryHexList() ?? throw FinalizationMissing("temporary_descriptor"); }
        finally { if (FinalizationNeed(current.ReadRegistryLockEnabled(), "pool_unlock")) current.ExitRegistryLock(); }
    }
    private static void FinalizationAppend(SourceWorldContext current, HexListHandle list, RegistryObjectReference pointer)
    {
        if (FinalizationNeed(current.ReadRegistryLockEnabled(), "append_lock")) current.EnterRegistryLock();
        try
        {
            var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw FinalizationMissing("temporary_node");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, pointer);
        }
        finally { if (FinalizationNeed(current.ReadRegistryLockEnabled(), "append_unlock")) current.ExitRegistryLock(); }
    }
    private static void FinalizationRelease(SourceWorldContext current, HexListHandle list)
    {
        if (FinalizationNeed(current.ReadRegistryLockEnabled(), "release_lock")) current.EnterRegistryLock();
        try { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        finally { if (FinalizationNeed(current.ReadRegistryLockEnabled(), "release_unlock")) current.ExitRegistryLock(); }
    }
    private static T FinalizationNeed<T>(T? value, string name) where T : struct => value ?? throw FinalizationMissing(name);
    private static InvalidDataException FinalizationMissing(string name) => new("normal-turn.finalization." + name);
}
