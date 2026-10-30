using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal void BindStoryCoalitionInputsCurrent(GameState state, SourceWorldContext current)
    {
        current.StoryCoalitionCountdown = state.NativeArmySimulation?.World.Flags165C;
        //Both ordinary eligibility and every Story refresh use the latest
        //saved flags. A previous coalition mutation cannot leave an old reader.
        current.ConditionForceCoalitionReader = physical =>
        {
            byte? flags = state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(physical, out var reset) == true
                ? reset[0x12] : state.NativeArmySimulation?.Forces.GetValueOrDefault(physical)?.Flags12;
            return flags is byte known ? (known & 2) != 0 : null;
        };
    }

    internal (GameState State, int NativeReturn) ApplyStoryCoalitionBusinessCurrent(GameState state,
        SourceWorldContext current, int code, IReadOnlyList<int> arguments, ForceReference? force = null,
        IReadOnlyList<int>? forcePhysicalSlots = null)
    {
        if (arguments.Count != 8) throw StoryCoalitionMissing("operands");
        WorldIndexContext Pool() => current.ReadOwnedCurrentListPool() ?? throw StoryCoalitionMissing("current_pool");
        switch (code)
        {
            case 137:
                //The event passes1; calendar expiry and Force retirement keep
                //their existing mode0. These two tails are unconditional.
                state = DissolveCampaignCoalitionCurrent(state, current, Pool(), refreshArmyLogistics: true);
                state = RefreshEditedEventRegionStatistics(state, current);
                state = MaintainEditedEventAppointments(state, current);
                state = SynchronizeDiplomacyProjection(state);
                return (PublishNormalTurnCurrent(state, current, Pool()), 1);
            case 138:
            {
                byte remaining = StoryCoalitionNeed(state.NativeArmySimulation?.World.Flags165C, "World165C");
                if (remaining == 0) return (state, 1);
                var members = new List<ForceReference>();
                foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force) ?? throw StoryCoalitionMissing("ForceC0"))
                {
                    if (entry is not { Kind: RegistryKind.Force, PhysicalSlot: int physical })
                        throw StoryCoalitionMissing("Force_payload");
                    //3A9D80 adds no Force native-validity filter. Retain every
                    //membership occurrence before the first pair mutation.
                    byte flags = ReadStoryCoalitionFlags(state, physical);
                    if ((flags & 2) != 0) members.Add(new(physical));
                }
                for (int first = 0; first < members.Count; first++)
                    for (int second = first + 1; second < members.Count; second++)
                        state = ApplyNormalTurnForceRelationCurrent(state, current, Pool(),
                            members[first], members[second], arguments[0]);
                return (SynchronizeDiplomacyProjection(state), 1);
            }
            case 284:
            {
                var target = force ?? throw StoryCoalitionMissing("District_Force");
                var operation = new EventPersonMovementCurrent(this, state, current,
                    new EditedEventResultSelectors(current, null, () => BindCampaignStoryInputs(state, current, false)));
                return (operation.CollapseStoryDistricts(target), 1);
            }
            case 439:
            {
                var members = forcePhysicalSlots ?? throw StoryCoalitionMissing("Force_list");
                if (members.Count == 0) return (state, 1);
                _baseCapacityInputs.BindCurrent(state, current);
                //122 and439 share the actual223A70/223990 kernel. Reuse its
                //Base/Gate/Army publication and special-Force cap behavior.
                var operation = new StoryForceRelationsCurrent(this, state, current, (_, _, _) => { });
                int[] moraleArguments = [0, arguments[0], 0, 0, 0, 0, 0, 0];
                foreach (int physical in members) operation.Execute(122, moraleArguments, new(physical), null);
                return (operation.State, 1);
            }
            default: throw StoryCoalitionMissing("leaf");
        }
    }

    private static byte ReadStoryCoalitionFlags(GameState state, int physical) =>
        state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(physical, out var reset) == true
            ? StoryCoalitionNeed(reset[0x12], "Force12")
            : StoryCoalitionNeed(state.NativeArmySimulation?.Forces.GetValueOrDefault(physical)?.Flags12, "Force12");

    //Only the actual mode1 caller uses this wrapper. Its selected Army order
    //comes from CForceArmyUnitIterator and its effects keep the outer sink.
    private sealed partial class NormalTurnTerritoryMaintenanceSource
    {
        internal GameState ApplyStoryCoalitionLogistics(ForceReference force, List<CampaignEffectRequest>? sink)
        {
            int key = N(current.ReadForcePhysicalSlot(force), "coalition_logistics_Force60");
            UpdateLogistics(Armies());
            PublishPeople();
            if (sink is not null) sink.AddRange(effects);
            else if (effects.Count != 0) state = AppendEditedForceResultEffects(state, effects);
            return state;

            IEnumerable<int> Armies()
            {
                foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw Missing("coalition_logistics_Army60"))
                {
                    if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical })
                        throw Missing("coalition_logistics_Army_payload");
                    if (key is < 1 or > 51 || current.ReadArmyUnitRawForceSlot(new(physical)) != key) continue;
                    if (N(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "coalition_logistics_Army233560")) continue;
                    yield return physical;
                }
            }
        }
    }

    private static T StoryCoalitionNeed<T>(T? value, string field) where T : struct => value ?? throw StoryCoalitionMissing(field);
    private static InvalidDataException StoryCoalitionMissing(string field) => new("story.coalition_business." + field);
}
