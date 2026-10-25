using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2851B0's mode0 ring, before26DA10 changes the Base Force.
    /// The caller retains its State/Region temporaries and owns publication of
    /// this same current/pool and the ordered presentation requests.</summary>
    internal GameState ApplyBaseCaptureNearbyArmiesCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, BaseReference target,
        NativeMapSeasonDate? operationDate, List<CampaignEffectRequest> effects,
        TacticSourceSamplerWorld? sampler = null)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw CaptureNearbyMissing("current_pool_binding");
        int location = CaptureNearbyNeed(current.ReadBaseObjectLink(target), "Base4E");
        if (target.Kind == BaseKind.City ? target.PhysicalSlot is not (>= 1 and <= 51) || location is < 1 or > 51
            : target.Kind != BaseKind.Gate || target.PhysicalSlot is not (>= 1 and <= 10) || location is < 52 or > 61)
            throw CaptureNearbyMissing("ordinary_Base_receiver");
        var center = CurrentGateMapQueries.FindHex(target, current);
        int radius = unchecked(CaptureNearbyNeed(BaseCaptureResourceInputs.ReadCurrent(state,
            SimulationParameterKeys.BaseCapture.CentralTerritoryRadius), "radiusA7A8") + (location < 52 ? 1 : 0));
        ArmyTraitFeedback? feedback = null;
        var ring = current.CreateNearbyTemporary();
        try
        {
            _ = NearbyArmyTemporarySource.FillNearbyHexTemporary(current, center, radius, ring);
            int oldForce = ForceIdentity(); //222FD0 captures its key once.
            _ = ForceIdentity(); //223250's separately constructed end iterator.
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Army) ?? throw CaptureNearbyMissing("manager60_descriptor");
            var node = current.ReadRegistryHead(descriptor) ?? throw CaptureNearbyMissing("manager60_head");
            while (node.Identity.HasValue)
            {
                //334740 rejects key0 before reading the node's Army fields.
                if (oldForce is >= 1 and <= 51)
                {
                    var entry = current.ReadRegistryNodePayload(node) ?? throw CaptureNearbyMissing("manager60_payload");
                    if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical })
                        throw CaptureNearbyMissing("manager60_Army_pointer");
                    var army = new ArmyUnitReference(physical);
                    if (current.ReadArmyUnitRawForceSlot(army) == oldForce &&
                        !CaptureNearbyNeed(current.ReadOwnedArmyExclusion233560(entry), "Army233560"))
                    {
                        ushort rawHex = CaptureNearbyNeed(current.ReadArmyUnitRawHex2A(army), "Army2A");
                        var position = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
                            ?? throw CaptureNearbyMissing("Army_Hex_pointer");
                        var hexNode = current.ReadHexListHead(ring) ?? throw CaptureNearbyMissing("ring_head");
                        while (hexNode.Identity.HasValue)
                        {
                            if ((current.ReadHexListNodePayload(hexNode) ?? throw CaptureNearbyMissing("ring_payload")) == position)
                            {
                                //The source formats this buffer before2EED60's
                                //own Army-validity gate, even in report mode1.
                                string name = target.Kind == BaseKind.City
                                    ? state.Cities.GetValueOrDefault(location)?.Name ?? throw CaptureNearbyMissing("City_name")
                                    : state.Gates.GetValueOrDefault(location)?.Name ?? throw CaptureNearbyMissing("Gate_name");
                                string reportContext = name + ReadHistoryDataText(1, 0x233);
                                ApplyConfusion(army, physical, reportContext);
                                break;
                            }
                            hexNode = current.ReadHexListNodeNext(hexNode) ?? throw CaptureNearbyMissing("ring_next");
                        }
                        _ = ForceIdentity(); //Fresh end query after each accepted iterator payload.
                    }
                }
                node = current.ReadRegistryNodeNext(node) ?? throw CaptureNearbyMissing("manager60_next");
            }
        }
        finally { current.ClearNearbyTemporary(ring, true); }
        return feedback?.Publish(state) ?? state;

        int ForceIdentity()
        {
            int raw = CaptureNearbyNeed(current.ReadBaseForceSlot(target), "current_Base_Force");
            if (raw < 0) throw CaptureNearbyMissing("negative_Force_index");
            var force = current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw CaptureNearbyMissing("Force_pointer");
            return CaptureNearbyNeed(current.ReadForcePhysicalSlot(force), "Force_identity");
        }
        int Commander(ArmyUnitReference army)
        {
            ushort raw = CaptureNearbyNeed(current.ReadArmyUnitCommanderId(army), "Commander12");
            var person = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
                ?? throw CaptureNearbyMissing("Commander_pointer");
            return CaptureNearbyNeed(person.PhysicalSlot, "Commander_physical");
        }
        DistrictReference District(ArmyUnitReference army)
        {
            int person = Commander(army);
            byte raw = CaptureNearbyNeed(current.ReadPersonNativeValidity(person), "District_Person_validity")
                ? CaptureNearbyNeed(current.ReadPersonDistrictSlot(person), "PersonE0") : (byte)0;
            return current.ReadDistrictTableReference(raw <= 51 ? raw : 0) ?? throw CaptureNearbyMissing("District_pointer");
        }
        UnitState Image(int physical)
        {
            var arena = state.ReadArmyLifecycle() ?? throw CaptureNearbyMissing("lifecycle");
            if ((uint)physical >= arena.Slots.Count) throw CaptureNearbyMissing("Army_source_slot");
            var receiver = arena.Receivers.Find(arena.Slots[physical].ReceiverId) ?? throw CaptureNearbyMissing("Army_receiver");
            return receiver.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) ?? throw CaptureNearbyMissing("Army_active_image")
                : receiver.PendingImage ?? throw CaptureNearbyMissing("Army_pending_image");
        }
        void WriteImage(int physical, UnitState image)
        {
            if (state.Units.ContainsKey(image.Id))
                state = state with { Units = new(state.Units) { [image.Id] = image } };
            else
            {
                var arena = state.ReadArmyLifecycle() ?? throw CaptureNearbyMissing("write_lifecycle");
                var receiver = arena.Receivers.Find(arena.Slots[physical].ReceiverId) ?? throw CaptureNearbyMissing("write_receiver");
                if (receiver.PendingImage?.Id != image.Id) throw CaptureNearbyMissing("write_pending_identity");
                state = state.WithArmyLifecycle(arena with
                    { Receivers = arena.Receivers.With(receiver with { PendingImage = image }) });
            }
        }
        int Day() => sampler?.Day450 ?? FacilityOperation.Playable(state).ReadCampaignDay(state);
        int Parameter(string key) => CaptureNearbyNeed(ArmyDestructionParameterSeed.ReadCurrent(state, key), key);
        void Report(ArmyUnitReference army, int physical, int cause, string context, bool captureMode)
        {
            int district = CaptureNearbyNeed(current.ReadDistrictPhysicalSlot(District(army)), "report_District_identity");
            state = ApplyAutomaticArmyAbnormalReport(state, current, null, Image(physical), district,
                0, cause, context, Day, effects, baseCaptureMode: captureMode);
        }
        void ApplyConfusion(ArmyUnitReference army, int physical, string context)
        {
            //Immutable TC data.tsv255037: DAT141A4A7C0 is40. This
            //null-caster call has no chance gate or application random draw.
            const int duration = 40;
            if (!CaptureNearbyNeed(current.ReadArmyUnitNativeValidity(army), "effect_Army_validity")) return;
            int person = Commander(army);
            bool general = CaptureNearbyNeed(TacticRemakeCurrentTraits.CommanderImmune(current, person, 21), "immunity21");
            bool? specific = general ? null : CaptureNearbyNeed(TacticRemakeCurrentTraits.CommanderImmune(current, person, 22), "immunity22");
            TacticAbnormalSourceInput input;
            if (general || specific == true)
                input = new(0, duration, 0, true, general, specific, null, null, null, null, null, null, null, null);
            else
            {
                var traits = TacticRemakeCurrentTraits.ArmyContributions(current, person,
                    current.ReadArmyReturnTraitFields(army)?.Traits64) ?? throw CaptureNearbyMissing("target_traits127");
                input = new(0, duration, 0, true, false, false, traits[9], traits[10], 0,
                    Parameter(SimulationParameterKeys.Tactics.AbnormalGeneralResistanceRate),
                    Parameter(SimulationParameterKeys.Tactics.AbnormalType0ResistanceRate), 0, null, null);
            }
            var adjustment = TacticEffectSourceAbnormal.ResolveAdjustment(input) ?? throw CaptureNearbyMissing("adjustment");
            if (!adjustment.CounterWritten)
            {
                //The immunity path explicitly passes mode0, even though the
                //normal counter-start path below uses the caller's mode1.
                if (BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, District(army), CaptureNearbyMissing))
                    Report(army, physical, adjustment.CauseCode, context, false);
                return;
            }
            bool visible = !adjustment.HasTraitAdjustment ||
                BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, District(army), CaptureNearbyMissing);
            var image = Image(physical);
            var runtime = image.NativeTacticRuntime ?? throw CaptureNearbyMissing("Army_counter_image");
            byte before = runtime.Abnormal23;
            var result = TacticEffectSourceAbnormal.Resolve(input with { CurrentCounter = before,
                CurrentUpperBound = Parameter(SimulationParameterKeys.Tactics.AbnormalCounterUpperBound) }, runtime.ProvocationOwner)
                ?? throw CaptureNearbyMissing("counter_result");
            current.SetTacticAbnormalRawByte(new(RegistryKind.Army, physical), 0x23, result.CounterAfter);
            WriteImage(physical, image with { NativeTacticRuntime = runtime with { Abnormal23 = result.CounterAfter } });
            if (result.IncreasingNotification)
            {
                feedback ??= new ArmyTraitFeedback(state, current,
                    operationDate ?? state.NativeMapSeason?.ThirdDate ?? throw CaptureNearbyMissing("speech_date"), effects, () => state);
                feedback.AbnormalIncreased(army, 23);
            }
            if (before == 0 && result.UnclampedSum > 0)
                Report(army, physical, visible ? result.CauseCode : 0, context, true);
        }
    }
    private static T CaptureNearbyNeed<T>(T? value, string field) where T : struct => value ?? throw CaptureNearbyMissing(field);
    private static InvalidDataException CaptureNearbyMissing(string field) => new("Base-capture.nearby-Army." + field);
}
