using San14.Core;

namespace San14.Data;

internal sealed record CampaignNormalTurnEndingResult(GameState? State, bool Stop,
    bool? EndingRequested, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    //429070 ->288800, on the enclosing normal Turn's existing current/pool.
    //The result distinguishes a source stop from the presentation request;
    //no caller may continue to the next Turn action after Stop=true.
    internal CampaignNormalTurnEndingResult CheckCampaignNormalTurnEndingCurrent(
        GameState state, SourceWorldContext current, WorldIndexContext pool)
    {
        try
        {
            if (state.CampaignTurnStop is { } previous)
                return new(state, true, previous.EndingRequested, null);
            if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw EndingMissing("shared_current_pool");
            //1D3840 and signedBC are separate owners. In particular an absent
            //service does not read or invent a saved user selection.
            if (ReadApplicationInformationRestriction(state) is not bool restricted)
                throw EndingMissing("Application_user_selection_service");
            if (restricted) return new(state, false, false, null);

            int count = 0, only = 0;
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force)
                ?? throw EndingMissing("Force_C0_registry"))
            {
                if (entry is not { Kind: RegistryKind.Force, PhysicalSlot: int physical })
                    throw EndingMissing("Force_C0_payload");
                var force = new ForceReference(physical);
                int ordinal = EndingNeed(current.ReadForcePhysicalSlot(force), "Force_virtual60");
                //288800 has no validity filter or deduplication. Each node
                //reads its real ordinal;0 is counted,46..51 are excluded.
                if (ordinal is >= 46 and <= 51) continue;
                only = ordinal;
                count = unchecked(count + 1);
                if (count > 1) return new(state, false, false, null);
            }

            //429070 stops the Turn even if29EBB0 does not enqueue an ending.
            //The optional presentation decision cannot erase that known stop.
            bool? endingRequested = null;
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
            bool? valid = player is null ? null : current.ReadForceNativeValidity(player);
            int? playerOrdinal = player is null ? null : current.ReadForcePhysicalSlot(player);
            //With an invalid player and no observer flag,29EBB0 returns
            //without an ending for either value of the earlier host-exit gate.
            if (valid is false && current.ReadWorldTransferServiceFlags() is uint flags && (flags & 0x100) == 0)
                endingRequested = false;

            //Only this independently proved player result is a victory. An
            //empty chain, observer or sole foreign Force still stops, but does
            //not acquire a player's terminal result or a presentation ACK.
            if (state.NativePlayableTerminal is null && valid is true &&
                playerOrdinal is >= 1 and <= 45 && count == 1 && only == playerOrdinal)
                state = state with { NativePlayableTerminal = new(NativePlayableTerminalKind.OrdinaryUnified,
                    playerOrdinal.Value, null, PlayableTerminalSource.OrdinaryUnificationRuleId) };
            state = state with { CampaignTurnStop = new(CampaignTurnStopReason.OrdinaryForcesResolved,
                count == 1 ? only : null, endingRequested) };
            return new(state, true, endingRequested, null);
        }
        catch (InvalidDataException error) { return new(null, false, false, error.Message); }
    }

    private static T EndingNeed<T>(T? value, string owner) where T : struct => value ?? throw EndingMissing(owner);
    private static InvalidDataException EndingMissing(string owner) => new("normal.turn.ending." + owner);
}
