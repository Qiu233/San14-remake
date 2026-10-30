using San14.Core;

namespace San14.Data;

public static partial class CityCaptureTerritorySource
{
    private static void ApplyStateAdvantageTransition(GameState state, SourceWorldContext current,
        CityCaptureAreaScope scope, CityCaptureStateTransition transition)
    {
        bool gained = transition.Gained;
        var oldForce = Force(scope.OldForcePhysical);
        bool lost = transition.Lost && K(current.ReadForceNativeValidity(oldForce), "State_old_Force_validity");
        if (!gained && !lost) return;
        bool? suppressSpecial = null;
        foreach (byte effect in transition.Effects)
        {
            if (effect is 1 or 2)
            {
                uint active = effect == 1 ? 0x4000u : 0x8000u;
                uint notified = effect == 1 ? 0x10000u : 0x20000u;
                if (gained && IsPlayer(scope.NewForcePhysical))
                {
                    uint flags = Flags();
                    if ((flags & notified) == 0)
                    {
                        if (effect == 1 && suppressSpecial is null)
                            suppressSpecial = ReadStateAdvantageSpecialForce(state, current, transition).Suppressed;
                        if (effect != 1 || suppressSpecial == false)
                            current.RawTransferServiceFlags16A8 = flags | active | notified;
                    }
                }
                if (lost && IsPlayer(scope.OldForcePhysical) &&
                    !PersonReturnPathSource.HasQualifiedStateAdvantage(state, current, oldForce, effect))
                    current.RawTransferServiceFlags16A8 = Flags() & ~active;
            }
            else if (effect == 5)
            {
                //3366D0 invalidates the native BF60 distance cache. This
                //engine computes those paths on demand and retains no cache.
                if (gained) PersonReturnCommands.RefreshStateAdvantageJourneys(state, current, Force(scope.NewForcePhysical));
                if (lost) PersonReturnCommands.RefreshStateAdvantageJourneys(state, current, oldForce);
            }
        }

        ForceReference Force(int physical) => current.ReadForceTableReference(physical is >= 0 and <= 51 ? physical : 0)
            ?? throw Missing("State_Force_pointer");
        bool IsPlayer(int physical)
        {
            var force = Force(physical);
            if (!K(current.ReadForceNativeValidity(force), "State_Force_validity")) return false;
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("State_player_Force");
            int playerId = K(current.ReadForcePhysicalSlot(player), "State_player_Force_identity");
            return playerId != 0 && K(current.ReadForcePhysicalSlot(force), "State_Force_identity") == playerId;
        }
        uint Flags()
        {
            _ = OwnedArmyReturnServiceInputs.ReadCurrent(state) ?? throw Missing("State_saved_World16A8");
            return current.ReadWorldTransferServiceFlags() ?? throw Missing("State_current_World16A8");
        }
    }

    internal static (ForceReference? Force, bool Suppressed) ReadStateAdvantageSpecialForce(GameState state,
        SourceWorldContext current, CityCaptureStateTransition transition)
    {
        ForceReference? selected = null;
        for (int index = 0; index < transition.Effects.Length; index++)
        {
            if (transition.Effects[index] != 1) continue;
            string key = PersonGrowthCurrentQueries.MagnitudeKey(transition.AdvantagePhysical, index);
            var parameters = state.NativeArmySimulation?.Parameters;
            if (parameters is null || parameters.Float32Values.ContainsKey(key) ||
                !parameters.Int32Values.TryGetValue(key, out int magnitude) || magnitude is < 0 or > 255)
                throw Missing("State_special_Force_magnitude");
            selected = current.ReadForceTableReference(magnitude <= 51 ? magnitude : 0) ?? throw Missing("State_special_Force_pointer");
            if (!K(current.ReadForceNativeValidity(selected), "State_special_Force_validity")) return (selected, true);
            if (K(current.ReadConditionForceDependence(new(K(current.ReadForcePhysicalSlot(selected), "State_special_Force_identity"))),
                "State_special_Force194") != 0) return (selected, true);
        }
        return (selected, false);
    }

    internal static GameState PublishStateAdvantageFlags(GameState state, SourceWorldContext current)
    {
        if (OwnedArmyReturnServiceInputs.ReadCurrent(state) is not uint before ||
            current.ReadWorldTransferServiceFlags() is not uint after || before == after) return state;
        var simulation = state.NativeArmySimulation!;
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
            { Int32Values = new(simulation.Parameters.Int32Values)
                { [OwnedArmyReturnServiceInputs.Flags16A8Key] = unchecked((int)after) } } } };
    }
}
