using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyNormalTurnForceAbsorptionEventsCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw AbsorptionMissing("shared_current_pool");
        if (state.PlayerCoalitionAbsorptionTail is not null || state.NpcForceAbsorptionTail is not null)
        {
            if (HasPendingForceAbsorptionCurrent(state))
                state = ResumeNormalTurnForceAbsorptionCurrent(state, current, pool);
            if (!HasPendingForceAbsorptionCurrent(state))
                state = ResumeForceAbsorptionCallerTailCurrent(state, current, pool);
            var resumedArena = state.ReadArmyLifecycle() ?? throw AbsorptionMissing("resumed_lifecycle_owner");
            return state.WithArmyLifecycle(resumedArena with { Indexes = pool.ExportOwnedState() });
        }
        if (!NormalTurnForceAbsorptionAllowed(state, current)) return state;
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw AbsorptionMissing("player_pointer");
        HexListHandle temporary;
        Lock(true);
        try { temporary = current.CreateTemporaryHexList() ?? throw AbsorptionMissing("Force_temporary_descriptor"); }
        finally { Lock(false); }
        try
        {
            foreach (var force in AbsorptionForces(current))
            {
                int id = AbsorptionKnown(current.ReadForcePhysicalSlot(force), "Force_ordinal");
                if (id is >= 46 and <= 51) continue;
                Lock(true);
                try
                {
                    var node = current.AllocateRegistryNode(new(temporary.Identity))
                        ?? throw AbsorptionMissing("Force_temporary_node");
                    //The original allocator may return a null node; only a
                    //real allocation writes its payload and enters selection.
                    if (node.Identity.HasValue) current.WriteRegistryNodePayload(node,
                        new(RegistryKind.Force, force.PhysicalSlot));
                }
                finally { Lock(false); }
            }
            //427320/427B90: stable descending City90 count. No Force/native
            //validity filter is present, and equal counts retain registry order.
            var nodes = new List<HexListNodeReference>();
            for (var node = Head(); node.Identity.HasValue; node = current.ReadHexListNodeNext(node)
                ?? throw AbsorptionMissing("Force_temporary_next")) nodes.Add(node);
            var sorted = nodes.OrderByDescending(node => CountCities(Payload(node))).ToArray();
            current.ReorderCurrentList(temporary, sorted);
            var first = Head();
            if (!first.Identity.HasValue) throw AbsorptionMissing("Force_temporary_first");
            state = Payload(first) == player
                ? ApplyNormalTurnPlayerForceAbsorptionCurrent(state, current, pool, player)
                : ApplyNormalTurnNpcForceAbsorptionCurrent(state, current, pool, player);
        }
        finally
        {
            Lock(true);
            try { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
            finally { Lock(false); }
        }
        //The live outer descriptor is released before exporting the actual
        //post-operation pool. No registry is reconstructed from domain rows.
        var arena = state.ReadArmyLifecycle() ?? throw AbsorptionMissing("lifecycle_owner");
        return state.WithArmyLifecycle(arena with { Indexes = pool.ExportOwnedState() });

        HexListNodeReference Head() => current.ReadHexListHead(temporary) ?? throw AbsorptionMissing("Force_temporary_head");
        ForceReference Payload(HexListNodeReference node)
        {
            var row = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw AbsorptionMissing("Force_temporary_payload");
            if (row.Kind != RegistryKind.Force || row.PhysicalSlot is not int physical)
                throw AbsorptionMissing("Force_temporary_kind");
            return new(physical);
        }
        int CountCities(ForceReference force)
        {
            int id = AbsorptionKnown(current.ReadForcePhysicalSlot(force), "rank_Force_ordinal");
            int count = 0;
            foreach (var row in current.ReadRegistryReferences(RegistryKind.City) ?? throw AbsorptionMissing("rank_City90"))
            {
                if (row is not { Kind: RegistryKind.City, PhysicalSlot: int physical })
                    throw AbsorptionMissing("rank_City_payload");
                if (AbsorptionKnown(current.ReadBaseForceSlot(new(BaseKind.City, physical)), "rank_City_force") == id)
                    count = unchecked(count + 1);
            }
            return count;
        }
        void Lock(bool enter)
        {
            if (!AbsorptionKnown(current.ReadRegistryLockEnabled(), "primary_lock")) return;
            if (enter) current.EnterRegistryLock(); else current.ExitRegistryLock();
        }
    }

    //42C060/27A5C0: the Force absorption event is a once-per-Turn operation,
    //not an Army Progress callback or a replacement for the ordinary AI pass.
    private bool NormalTurnForceAbsorptionAllowed(GameState state, SourceWorldContext current)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw AbsorptionMissing("player_pointer");
        if (!AbsorptionKnown(current.ReadForceNativeValidity(player), "player_validity")) return false;
        int selector = AbsorptionKnown(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selector");
        if (AbsorptionKnown(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x374 : 0xF8),
            "force_absorption_setting") == 1) return false;
        if (AbsorptionKnown(state.NativeArmySimulation?.World.Flags165C, "World165C") != 0) return false;
        int flags = AbsorptionKnown(BattleRemakeCurrentSettings.ReadInt32(state, 0xB0), "environment_B0");
        if (((uint)flags & (1u << 9)) != 0) return false;
        if (ReadApplicationInformationRestriction(state) is not bool restricted)
            throw AbsorptionMissing("Application_user_selection_service");
        if (restricted) return false;
        foreach (var force in AbsorptionForces(current))
        {
            int physical = AbsorptionKnown(force.PhysicalSlot, "Force_pointer");
            ushort value = state.NativeArmySimulation?.Forces.GetValueOrDefault(physical)?.DiplomacyFlags178
                ?? throw AbsorptionMissing("Force178");
            if ((value & 1) != 0) return false;
        }
        return true;
    }

    private static IEnumerable<ForceReference> AbsorptionForces(SourceWorldContext current)
    {
        foreach (var row in current.ReadRegistryReferences(RegistryKind.Force)
            ?? throw AbsorptionMissing("Force_C0_registry"))
        {
            if (row is not { Kind: RegistryKind.Force, PhysicalSlot: int physical })
                throw AbsorptionMissing("Force_C0_payload");
            yield return new(physical);
        }
    }

    //2704F0 notify0 still performs its actual special-person tail. Reuse the
    //same monthly numerical owner/callbacks without running monthly maintenance.
    internal GameState ApplyNormalTurnForceRelationCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, ForceReference first, ForceReference second, int adjustment)
        => new MonthlyCampaignMaintenanceSource(this, state, current, pool)
            .ApplyRelation(first, second, adjustment, 0);

    internal GameState AppendNormalTurnCoalitionCameraCurrent(GameState state, int targetHex, bool begin)
        => AppendNormalTurnCoalitionVisualCurrent(state, new(begin
            ? ForceCoalitionVisualOperation.CameraFocus : ForceCoalitionVisualOperation.CameraRestore,
            begin ? targetHex : null));

    internal GameState AppendNormalTurnCoalitionEventCurrent(GameState state, bool enter)
        => AppendNormalTurnCoalitionVisualCurrent(state, new(enter
            ? ForceCoalitionVisualOperation.NarrationEnter : ForceCoalitionVisualOperation.NarrationExit, null));

    private static GameState AppendNormalTurnCoalitionVisualCurrent(GameState state, ForceCoalitionVisualRequest request)
    {
        var pending = state.CampaignEffectRequests ?? throw AbsorptionMissing("presentation_owner");
        long revision = state.NativeClock?.UpdateOrdinal ?? throw AbsorptionMissing("presentation_revision");
        return state with { CampaignEffectRequests = pending.Append(CampaignEffectSource.MonthlyMaintenance,
            revision, [new(null, null) { ForceCoalitionVisual = request }]) };
    }

    private static T AbsorptionKnown<T>(T? value, string field) where T : struct
        => value ?? throw AbsorptionMissing(field);
    private static InvalidDataException AbsorptionMissing(string field) => new("normal.turn.force_absorption." + field);
}
