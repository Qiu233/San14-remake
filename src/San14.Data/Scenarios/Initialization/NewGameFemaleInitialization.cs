using San14.Core;

namespace San14.Data;

internal sealed record FreshFemaleInitialization(GameState State, IReadOnlyList<int> ResetIdentities);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The fresh female-officer setting, before wandering-city and
    /// bloodline initialization. Borrows the enclosing fresh current world;
    /// the caller has installed its relation, governor, lifecycle and Item owners.</summary>
    internal FreshFemaleInitialization ApplyFreshFemaleSetting(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
    {
        int selector = FreshFemaleNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selector");
        bool enabled = selector == 0
            ? FreshFemaleNeed(BattleRemakeCurrentSettings.ReadInt32(state, 0x1DC), "advanced_setting") == 1
            : FreshFemaleNeed(BattleRemakeCurrentSettings.ReadByte(state, 0x78), "ordinary_setting") != 0;
        if (!enabled) return new(state, Array.Empty<int>());
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0)
            throw FreshFemaleMissing("fresh_state");
        return new PersonDeathSource(this, state, current, pool).ApplyFreshFemaleInitialization();
    }

    private static T FreshFemaleNeed<T>(T? value, string field) where T : struct => value ?? throw FreshFemaleMissing(field);
    private static InvalidDataException FreshFemaleMissing(string field) => new("new-game.female." + field);

    internal sealed partial class PersonDeathSource
    {
        internal FreshFemaleInitialization ApplyFreshFemaleInitialization()
        {
            var reset = new List<int>();
            // The source walks the full physical table. Native validity,
            // appearance status and current registration are not entry filters.
            for (int slot = 0; slot < 1851; slot++)
            {
                var reference = current.ReadTransferPersonTableReference(slot) ?? throw Missing("fresh_female_person_table");
                int person = Known(reference.PhysicalSlot, "fresh_female_person_pointer");
                ushort identity = Identity(person);
                if (identity is < 1 or > 1000 || identity == 383 ||
                    Known(Owner(person).Sex, "fresh_female_sex") != OfficerSex.Female) continue;
                var oldBase = BaseLookup.Find(Known(current.ReadPersonFirstLocation(person), "fresh_female_old_home"), current);

                // CPersonItemIterator supplies every matching registered
                // pointer in chain order, including duplicates. Copy before
                // deletion, as the original temporary Item list does.
                foreach (var item in PersonItems(person).ToArray())
                {
                    WriteItemHolder(item, 0);
                    WriteItemLocation(item, 0);
                    WriteItemStatus(item, 3);
                    _ = UnregisterItem(item);
                    var owner = ItemOwner(item);
                    var lifecycle = owner.Lifecycle ?? throw Missing("fresh_female_Item_lifecycle");
                    byte flags = Known(lifecycle.RetainedFlags, "fresh_female_Item_flags");
                    var definition = latest.ItemDefinitions.GetValueOrDefault(owner.DefinitionId)
                        ?? throw Missing("fresh_female_Item_definition");
                    var appearance = definition.Appearance ?? throw Missing("fresh_female_Item_kind");
                    latest = latest with { ItemDefinitions = new(latest.ItemDefinitions)
                        { [owner.DefinitionId] = definition with { Appearance = appearance with { Kind = 0 } } } };
                    ReplaceItem(item, owner with { Lifecycle = lifecycle with { RetainedFlags = (byte)(flags & 0xFA) } });
                }
                // The following Item-disposition call sees an empty held-Item
                // iterator. Its recipient temporary-list sort has no world
                // writes; all RNG, Item writes and reports are inside that loop.
                ResetPerson(person);
                SetStatus(person, 9);
                Change(person, p => p with { Presence = OfficerPresence.Dead, Deployed = false });
                RemoveRegistration(person);
                RepairFreshFemaleGovernor(oldBase);
                reset.Add(identity);
            }
            var lifecycleOwner = latest.ReadArmyLifecycle() ?? throw Missing("fresh_female_registry_owner");
            latest = latest.WithArmyLifecycle(lifecycleOwner with { Indexes = pool.ExportOwnedState() });
            return new(latest, reset.AsReadOnly());
        }

        private void RepairFreshFemaleGovernor(BaseReference oldBase)
        {
            if (!Known(current.ReadBaseNativeValidity(oldBase), "fresh_female_old_Base_validity")) return;
            var packet = package.ApplyPersonFreedomGovernorCurrent(latest, current, oldBase);
            foreach (var write in packet.PersonWrites) SetStatus(write.PersonPhysical, write.StatusAfter);
            int at = Known(oldBase.PhysicalSlot, "fresh_female_governor_Base_pointer");
            if (oldBase.Kind == BaseKind.City && latest.Cities.TryGetValue(at, out var city))
                latest = latest with { Cities = new(latest.Cities) { [at] = city with
                { GovernorId = packet.GovernorWord2EAfter == 0 ? null : packet.GovernorWord2EAfter,
                    NativeReturnRuntime = (city.NativeReturnRuntime ?? throw Missing("fresh_female_governor_City_owner")) with
                    { Governor2E = packet.GovernorWord2EAfter } } } };
            else if (oldBase.Kind == BaseKind.Gate && latest.Gates.TryGetValue(at + 51, out var gate))
            {
                var simulation = latest.NativeArmySimulation ?? throw Missing("fresh_female_governor_Gate_owner");
                latest = latest with { Gates = new(latest.Gates) { [gate.Id] = gate with
                    { GovernorId = packet.GovernorWord2EAfter == 0 ? null : packet.GovernorWord2EAfter } },
                    NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
                    { Int32Values = new(simulation.Parameters.Int32Values)
                        { [OwnedArmyLifecycleDetachedInputs.Key("Gate", at, "2E")] = packet.GovernorWord2EAfter } } } };
            }
            else throw Missing("fresh_female_governor_Base_publication");
        }
    }
}
