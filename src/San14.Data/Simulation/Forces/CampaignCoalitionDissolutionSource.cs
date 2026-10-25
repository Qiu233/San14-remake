using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //293040(mode0), shared by calendar expiry and the two actual Force
    //retirement callbacks. No monthly predicate, relation penalty or expiry
    //announcement belongs here. The caller retains its live descriptors.
    internal GameState DissolveCampaignCoalitionCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, List<CampaignEffectRequest>? effects = null)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
            throw CoalitionDissolutionMissing("shared_pool");
        var native = state.NativeArmySimulation ?? throw CoalitionDissolutionMissing("native_owner");
        var interaction = state.NativeForceUnregisterCurrent ?? throw CoalitionDissolutionMissing("interaction_owner");
        state = state with
        {
            NativeArmySimulation = native with { World = native.World with { SelectedForce1656 = 0, Flags165C = 0 } },
            NativeForceUnregisterCurrent = interaction with { OtherForce1657 = 0 }
        };
        current.EventConditions.SelectedForce = 0;

        foreach (var force in Forces())
        {
            int key = Ordinal(force);
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army)
                ?? throw CoalitionDissolutionMissing("Army60"))
            {
                if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical })
                    throw CoalitionDissolutionMissing("Army_payload");
                //222FD0 is the current Force iterator. A saved raw Army
                //Force byte is not its Commander/District-derived identity.
                if (key is < 1 or > 51 ||
                    CoalitionDissolutionNeed(current.ReadForcePhysicalSlot(
                        current.ReadArmyUnitForceReference(new(physical))), "Army_Force") != key) continue;
                if ((Flags(force) & 2) == 0) continue;
                int rawHex = CoalitionDissolutionNeed(current.ReadArmyUnitRawHex2A(new(physical)), "Army2A");
                var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
                    ?? throw CoalitionDissolutionMissing("Army_Hex_pointer");
                int occupied = CoalitionDissolutionNeed(current.ReadHexForceSlot(hex), "Hex14");
                if (occupied == key || (ReadReportForceRelationByte(state, force, occupied,
                    CoalitionDissolutionMissing) & 127) != 0) continue;
                state = new MonthlyCampaignMaintenanceSource(this, state, current, pool,
                    borrowedEffects: effects).ApplyCoalitionExpiryArmyOccupationCurrent(physical, key);
            }
        }
        //Mode0 skips276DB0 even if occupation changed. Each C0 occurrence
        //still clears both coalition bits in its original iterator order.
        foreach (var force in Forces())
        {
            native = state.NativeArmySimulation ?? throw CoalitionDissolutionMissing("flags_owner");
            int physical = CoalitionDissolutionNeed(force.PhysicalSlot, "flags_Force_pointer");
            var before = native.Forces.GetValueOrDefault(physical) ?? throw CoalitionDissolutionMissing("flags_Force");
            state = state with { NativeArmySimulation = native with { Forces = new(native.Forces)
                { [physical] = before with { Flags12 = (byte)(Flags(force) & 0xF9) } } } };
        }
        var date = state.NativeMapSeason?.ThirdDate ?? throw CoalitionDissolutionMissing("cooldown_date");
        //TC typed/data.tsv141A49978 is the file-backed360-day constant.
        int days = StrategicParameter(state, "normal-coalition-expiry.cooldown-days", 360);
        long hours = unchecked((long)date.Year * 8640 + (date.Month - 1L) * 720 +
            (date.Day - 1L) * 24 + date.Hour + days * 24L);
        int year = checked((int)(hours / 8640));
        int rest = checked((int)(hours - year * 8640L));
        if (rest < 0) { year--; rest += 8640; }
        native = state.NativeArmySimulation ?? throw CoalitionDissolutionMissing("cooldown_owner");
        state = state with { NativeArmySimulation = native with { World = native.World with
            { PlayerCoalitionCooldownDate = new(checked((short)year), checked((sbyte)(rest / 720 + 1)),
                checked((sbyte)(rest % 720 / 24 + 1)), checked((sbyte)(rest % 24))) } } };
        //1A3E60 refreshes existing City views; it does not acknowledge a UI.
        var refresh = new CampaignEffectRequest(null, null) { ForceCoalitionVisual =
            new(ForceCoalitionVisualOperation.CityRelationIndicatorsRefresh, null) };
        if (effects is not null) effects.Add(refresh);
        else
        {
            var pending = state.CampaignEffectRequests ?? throw CoalitionDissolutionMissing("effect_owner");
            state = state with { CampaignEffectRequests = pending.Append(CampaignEffectSource.MonthlyMaintenance,
                state.NativeClock?.UpdateOrdinal ?? throw CoalitionDissolutionMissing("source_revision"), [refresh]) };
        }
        return state.WithArmyLifecycle((state.ReadArmyLifecycle() ?? throw CoalitionDissolutionMissing("lifecycle"))
            with { Indexes = pool.ProjectOwnedRoles() });

        IEnumerable<ForceReference> Forces()
        {
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force)
                ?? throw CoalitionDissolutionMissing("ForceC0"))
                yield return entry is { Kind: RegistryKind.Force, PhysicalSlot: int at }
                    ? new(at) : throw CoalitionDissolutionMissing("Force_payload");
        }
        int Ordinal(ForceReference force) => CoalitionDissolutionNeed(current.ReadForcePhysicalSlot(force), "Force60");
        byte Flags(ForceReference force) => CoalitionDissolutionNeed(state.NativeArmySimulation?.Forces
            .GetValueOrDefault(CoalitionDissolutionNeed(force.PhysicalSlot, "flags_pointer"))?.Flags12, "Force12");
    }

    private static T CoalitionDissolutionNeed<T>(T? value, string field) where T : struct =>
        value ?? throw CoalitionDissolutionMissing(field);
    private static InvalidDataException CoalitionDissolutionMissing(string field) => new("coalition.dissolution." + field);
}
