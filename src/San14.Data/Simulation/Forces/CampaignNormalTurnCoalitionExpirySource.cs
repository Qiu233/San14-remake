using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //Normal Turn24: TC2FF130, followed by293040(mode0). The retained
    //coalition member order precedes pairwise2704F0's actual side effects.
    internal GameState ApplyNormalTurnMonthlyCoalitionExpiryCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
    {
        var native = state.NativeArmySimulation ?? throw Missing("native_owner");
        byte countdown = K(native.World.Flags165C, "World165C");
        if (countdown == 0) return state;
        int rawTarget = K(native.World.SelectedForce1656, "World1656");
        var target = current.ReadForceTableReference(rawTarget < 52 ? rawTarget : 0) ?? throw Missing("target_Force_pointer");
        if (!K(current.ReadForceNativeValidity(target), "target_Force_validity")) return state;
        int remaining = countdown - 1;
        state = state with { NativeArmySimulation = native with { World = native.World with { Flags165C = (byte)remaining } } };
        var effects = new List<CampaignEffectRequest>();
        int warning = Parameter("warning-months");
        if (remaining == warning) History(0x5E98, 0x5E99, new(Force0: Ordinal(target), Integers: [warning]));
        if (remaining != 0) return state;

        History(0x5E9A, 0x5E9B, new(Force0: Ordinal(target)));
        effects.Add(new(null, null) { ForceCoalitionVisual = new(ForceCoalitionVisualOperation.NarrationEnter, null) { SceneKind = countdown + 0x34 } });
        effects.Add(new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.ControllerQueueEvent, countdown + 7, null, null) });
        Announcement(0x5E9C, new(Force0: Ordinal(target)));
        effects.Add(new(null, null) { ForceCoalitionVisual = new(ForceCoalitionVisualOperation.NarrationExit, null) });
        Flush();

        var members = Forces().Where(force => (Flags(force) & 2) != 0).ToArray();
        for (int index = 0; index < members.Length; index++)
        {
            var force = members[index];
            if ((Flags(force) & 4) != 0) continue;
            if (AbsorptionPlayer(current, state, force))
            {
                Announcement(0x5E9D, new(Force0: Ordinal(target), Force1: Ordinal(force)));
                Flush();
            }
            for (int other = index + 1; other < members.Length; other++)
            {
                var second = members[other];
                int ordinal = Ordinal(second);
                int before = ordinal < 52 ? DRelations(state, K(force.PhysicalSlot, "relation_Force_pointer"))[ordinal] : 0;
                int after = Math.Clamp(unchecked(before + Parameter("dissolution-relation-adjustment")), 0, 200);
                state = ApplyNormalTurnForceRelationCurrent(state, current, pool, force, second, after - before);
            }
        }

        //293040 resets the interaction before restoring member armies' actual
        //occupied cells. Its mode0 does not invoke276DB0's Army regrouping.
        native = state.NativeArmySimulation ?? throw Missing("dissolution_native_owner");
        var interaction = state.NativeForceUnregisterCurrent ?? throw Missing("World1657_owner");
        state = state with
        {
            NativeArmySimulation = native with { World = native.World with { SelectedForce1656 = 0, Flags165C = 0 } },
            NativeForceUnregisterCurrent = interaction with { OtherForce1657 = 0 }
        };
        foreach (var force in Forces())
        {
            int key = Ordinal(force);
            foreach (var armyEntry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw Missing("Army60"))
            {
                if (armyEntry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical }) throw Missing("Army_payload");
                if (key is < 1 or > 51 || current.ReadArmyUnitRawForceSlot(new(physical)) != key) continue;
                if ((Flags(force) & 2) == 0) continue;
                var army = new ArmyUnitReference(physical);
                int rawHex = K(current.ReadArmyUnitRawHex2A(army), "Army2A");
                var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw Missing("Army_Hex_pointer");
                int occupied = K(current.ReadHexForceSlot(hex), "Army_Hex14");
                if (occupied == Ordinal(force)) continue;
                if ((ReadReportForceRelationByte(state, force, occupied, Missing) & 127) != 0) continue;
                state = new MonthlyCampaignMaintenanceSource(this, state, current, pool)
                    .ApplyCoalitionExpiryArmyOccupationCurrent(physical, Ordinal(force));
            }
        }
        foreach (var force in Forces())
        {
            native = state.NativeArmySimulation ?? throw Missing("reset_flags_owner");
            int physical = K(force.PhysicalSlot, "reset_flags_Force");
            var before = native.Forces.GetValueOrDefault(physical) ?? throw Missing("reset_flags_native_Force");
            state = state with { NativeArmySimulation = native with
                { Forces = new(native.Forces) { [physical] = before with { Flags12 = (byte)(K(before.Flags12, "reset_Force12") & 0xF9) } } } };
        }
        var date = state.NativeMapSeason?.ThirdDate ?? throw Missing("cooldown_date");
        int days = Parameter("cooldown-days");
        long totalHours = unchecked((long)date.Year * 8640 + (date.Month - 1L) * 720 + (date.Day - 1L) * 24 + date.Hour + days * 24L);
        int year = checked((int)(totalHours / 8640));
        int rest = checked((int)(totalHours - year * 8640L));
        if (rest < 0) { year--; rest += 8640; }
        native = state.NativeArmySimulation ?? throw Missing("cooldown_native_owner");
        state = state with { NativeArmySimulation = native with { World = native.World with
            { PlayerCoalitionCooldownDate = new(checked((short)year), checked((sbyte)(rest / 720 + 1)),
                checked((sbyte)(rest % 720 / 24 + 1)), checked((sbyte)(rest % 24))) } } };
        return state.WithArmyLifecycle((state.ReadArmyLifecycle() ?? throw Missing("lifecycle")) with { Indexes = pool.ProjectOwnedRoles() });

        void History(int mainRoot, int detailRoot, HistoryMessageOperands operands)
        {
            var main = FormatNormalTurnReport(state, current, mainRoot, operands); state = main.State;
            var detail = FormatNormalTurnReport(state, current, detailRoot, operands); state = detail.State;
            int rawRuler = K(current.ReadForceRecord(K(target.PhysicalSlot, "history_target_Force"))?.RawRuler10, "history_ruler10");
            int ruler = K((rawRuler <= 6000 ? current.ReadPersonLookupReference(rawRuler) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "history_ruler_pointer");
            var home = BaseLookup.Find(K(current.ReadPersonFirstLocation(ruler), "history_rulerE2"), current);
            int rawObject = K(current.ReadBaseObjectLink(home), "history_Base4E");
            var obj = current.ReadObjectTableReference(rawObject < 3001 ? rawObject : 0) ?? throw Missing("history_Object_pointer");
            int hex = K(current.ReadObjectNativeValidity(obj), "history_Object_validity")
                ? K(current.ReadObjectHexIndex(obj), "history_Object10") : 48400;
            int day = (ReadArmyDestructionSampler(state, current) ?? throw Missing("history_World450")).Day450;
            state = WriteHistory(state, current, new(1, 0, hex, day, main.Text, detail.Text, 5012, MarkBit0: 1, MarkBit4: 1), effects);
            Flush();
        }
        void Announcement(int root, HistoryMessageOperands operands)
        {
            var text = FormatNormalTurnReport(state, current, root, operands); state = text.State;
            effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.Announcement, null, null, null, null, text.Text) });
        }
        void Flush()
        {
            if (effects.Count == 0) return;
            var queue = state.CampaignEffectRequests ?? throw Missing("effect_owner");
            state = state with { CampaignEffectRequests = queue.Append(CampaignEffectSource.MonthlyMaintenance,
                state.NativeClock?.UpdateOrdinal ?? throw Missing("effect_revision"), effects) };
            effects.Clear();
        }
        int Parameter(string name)
        {
            //Existing offline TC typed/readable data tables agree at
            //141A4AC50=3,141A4AC58=-20,141A49978=360 days.
            int original = name switch
            {
                "warning-months" => 3,
                "dissolution-relation-adjustment" => -20,
                "cooldown-days" => 360,
                _ => throw Missing("parameter_" + name)
            };
            return StrategicParameter(state, "normal-coalition-expiry." + name, original);
        }
        IEnumerable<ForceReference> Forces()
        {
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force) ?? throw Missing("ForceC0"))
                yield return entry is { Kind: RegistryKind.Force, PhysicalSlot: int physical } ? new(physical) : throw Missing("Force_payload");
        }
        byte Flags(ForceReference force) => K(state.NativeArmySimulation?.Forces.GetValueOrDefault(K(force.PhysicalSlot, "flags_Force_pointer"))?.Flags12, "Force12");
        int Ordinal(ForceReference force) => K(current.ReadForcePhysicalSlot(force), "Force60");
        T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        InvalidDataException Missing(string field) => new("normal-turn.coalition-expiry." + field);
    }

    internal sealed partial class MonthlyCampaignMaintenanceSource
    {
        //293040's raw2413D0(clear) precedes the fresh Army/Home getters and
        //185D00(flags1,0,0). The existing publication retains real Area and
        //Person ownership writers and source-time territory presentation.
        internal GameState ApplyCoalitionExpiryArmyOccupationCurrent(int physical, int force)
        {
            var hex = ArmyHex(physical);
            current.AssignHexForce(hex, 0);
            PublishRestoredHex(hex, null);
            hex = ArmyHex(physical);
            int commander = PersonLookup(Need(current.ReadArmyUnitCommanderId(new(physical)), "coalition_Army12"));
            int home = ValidPerson(commander) ? Need(current.ReadPersonFirstLocation(commander), "coalition_PersonE2") : 0;
            var city = CurrentGateMapQueries.FindCity(BaseLookup.Find(home, current), current);
            RestoreMonthlyOccupation(hex, force, Need(city.PhysicalSlot, "coalition_City_pointer"), enqueueEncirclement: false);
            return state;
        }
    }
}
