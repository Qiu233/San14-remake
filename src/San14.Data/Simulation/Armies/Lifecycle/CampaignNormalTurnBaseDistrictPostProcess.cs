using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Bind the completed Turn's current capacity inputs immediately
    /// before3257F0. This never invokes a callback, captures another world or
    /// replays initialization; the caller runs the existing complete kernels.</summary>
    internal void PrepareNormalTurnBaseDistrictPostProcessCurrent(GameState state, SourceWorldContext current)
    {
        if (!_baseCapacityInputs.BindCurrent(state, current)) throw TurnPostProcessMissing("Base_parameter_owner");
        //The City callbacks precede District callbacks, and Gate callbacks
        //follow them. Read reached status/membership from the same current,
        //not the immutable snapshot preceding a District's status2→4 write.
        //Credit was already published at action50 and is not written by these
        //slot7 kernels, so the existing full current policy contribution leaf
        //can consume that saved credit owner without a second capture.
        current.BaseForcePolicyEffectReader = (force, effect) =>
            ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, force, effect, true).Value;
    }

    /// <summary>Persist the extra Base/District outputs of the completed
    /// slot7 pass. The enclosing existing publication owns Person status,
    /// membership, Base resources/morale and the shared list-pool export.</summary>
    internal GameState PublishNormalTurnBaseDistrictPostProcessCurrent(GameState state, SourceWorldContext current)
    {
        //Use actual table receivers, including aliases, as the kernel did.
        //A repeated pointer has already received every original callback; its
        //final publication needs only one saved-owner update.
        var bases = new HashSet<BaseReference>();
        for (int slot = 0; slot < 52; slot++)
            PublishBase(current.ReadCityTableReference(slot) ?? throw TurnPostProcessMissing("City_table_pointer"));
        for (int slot = 0; slot < 11; slot++)
            PublishBase(current.ReadGateTableReference(slot) ?? throw TurnPostProcessMissing("Gate_table_pointer"));

        var module = state.NativeForceUnregisterCurrent ?? throw TurnPostProcessMissing("District_saved_windows");
        var windows = new Dictionary<int, byte?[]>(module.DistrictWindows10);
        var districts = new Dictionary<int, DistrictState>(state.Districts);
        var receivers = new HashSet<int>();
        for (int slot = 0; slot < 52; slot++)
        {
            var receiver = current.ReadDistrictTableReference(slot) ?? throw TurnPostProcessMissing("District_table_pointer");
            int physical = TurnPostProcessNeed(receiver.PhysicalSlot, "District_receiver");
            if (!receivers.Add(physical)) continue;
            if (!windows.ContainsKey(physical)) throw TurnPostProcessMissing("District_window_owner");
            //226DC0 deliberately leaves23 and2B untouched. Preserve their
            //actual nullable knowledge, including the fallback District0.
            windows[physical] = Enumerable.Range(0x10, 28)
                .Select(offset => current.ReadDistrictRawByte(physical, offset)).ToArray();
            bool active = TurnPostProcessNeed(current.ReadDistrictNativeValidity(physical), "District_final_validity");
            if (physical == 0 || !active) { districts.Remove(physical); continue; }
            var before = districts.GetValueOrDefault(physical) ?? throw TurnPostProcessMissing("active_District_domain_owner");
            int force = TurnPostProcessNeed(current.ReadDistrictForceId(physical), "District_Force10");
            int number = TurnPostProcessNeed(current.ReadDistrictNumber(physical), "District_number11");
            ushort leader = TurnPostProcessNeed(current.ReadDistrictLeader(physical), "District_leader12");
            int books = TurnPostProcessNeed(current.ReadDistrictRawByte(physical, 0x14), "District_books14");
            districts[physical] = before with { ForceId = force, Number = number,
                LeaderId = leader == 0 ? null : leader, OrdersRemaining = books };
        }
        return state with { Districts = districts,
            NativeForceUnregisterCurrent = module with { DistrictWindows10 = windows } };

        void PublishBase(BaseReference receiver)
        {
            if (!bases.Add(receiver)) return;
            //The complete Base kernels return immediately for City0/Gate0;
            //they neither require fictitious Domain0 objects nor seed stock.
            if (!TurnPostProcessNeed(current.ReadBaseNativeValidity(receiver), "Base_final_validity")) return;
            int physical = TurnPostProcessNeed(receiver.PhysicalSlot, "Base_receiver");
            ushort link = TurnPostProcessNeed(current.ReadBaseObjectLink(receiver), "Base_Object4E");
            var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0)
                ?? throw TurnPostProcessMissing("linked_Object_pointer");
            if (TurnPostProcessNeed(current.ReadObjectNativeValidity(obj), "linked_Object_validity"))
                state = PublishCurrentObjectDurability(state, current, obj,
                    TurnPostProcessNeed(current.ReadObjectRawField14(obj), "linked_Object14"));
            byte tier = TurnPostProcessNeed(current.ReadBasePostLoadFlag66(receiver), "Base_damage_tier66");
            if (receiver.Kind == BaseKind.City)
            {
                var city = state.Cities.GetValueOrDefault(physical) ?? throw TurnPostProcessMissing("City_domain_owner");
                if (city.DamageTier != tier)
                    state = state with { Cities = new(state.Cities) { [physical] = city with { DamageTier = tier } } };
            }
            else if (receiver.Kind == BaseKind.Gate)
            {
                int location = checked(physical + 51);
                var gate = state.Gates.GetValueOrDefault(location) ?? throw TurnPostProcessMissing("Gate_domain_owner");
                if (gate.DamageTier != tier)
                    state = state with { Gates = new(state.Gates) { [location] = gate with { DamageTier = tier } } };
            }
            else throw TurnPostProcessMissing("Base_receiver_kind");
        }
    }

    private static T TurnPostProcessNeed<T>(T? value, string field) where T : struct
        => value ?? throw TurnPostProcessMissing(field);
    private static InvalidDataException TurnPostProcessMissing(string field)
        => new("normal-turn.slot7.BaseDistrict." + field);
}
