using San14.Core;

namespace San14.Data;

internal sealed record ForeignTradeCountryDefinition(int Id, string Name, byte FirstItemId,
    byte FirstWeaponId, byte SecondWeaponId, byte GemId, byte HostCityId, int ReturnDays)
{ internal string? NameSortKey { get; init; } }

/// <summary>The existing saved Force184/18E/193 owner is shared by dispatch,
/// the overseas journey and seasonal maintenance. No parallel progress bank
/// or source-executable dependency is created by the new command.</summary>
internal static class ForeignTradeCurrentSource
{
    internal static NativeArmyForceState ReadForce(GameState state, int forceId) =>
        state.NativeArmySimulation?.Forces.GetValueOrDefault(forceId) ?? throw Missing("Force_owner");

    internal static ForeignTradeProgressState ReadProgress(GameState state, int forceId)
    {
        var progress = ReadForce(state, forceId).ForeignTradeProgress ?? throw Missing("Force184_18E");
        if (progress.FriendshipPoints.Length != 5 || progress.AcquiredTechnologyLevels.Length != 5)
            throw Missing("five_country_progress_slots");
        return progress;
    }
    internal static int ReadFriendship(GameState state, int forceId, int country) => ReadProgress(state, forceId).FriendshipPoints[Index(country)];
    internal static int ReadStage(GameState state, int forceId, int country) => ReadProgress(state, forceId).AcquiredTechnologyLevels[Index(country)];
    internal static byte ReadFlags(GameState state, int forceId) => forceId == 0
        ? PlaceholderWorldCurrentSource.ReadForceAdministrationFlags(state, forceId) ?? throw Missing("placeholder_Force193")
        : ReadForce(state, forceId).Flags193 ?? throw Missing("Force193");

    internal static GameState WriteFriendship(GameState state, int forceId, int country, int value)
    {
        var force = ReadForce(state, forceId); var progress = ReadProgress(state, forceId);
        return WriteForce(state, forceId, force with { ForeignTradeProgress = progress with
            { FriendshipPoints = progress.FriendshipPoints.SetItem(Index(country), checked((ushort)Math.Clamp(value, 0, 1000))) } });
    }
    internal static GameState WriteStage(GameState state, int forceId, int country, int value)
    {
        var force = ReadForce(state, forceId); var progress = ReadProgress(state, forceId);
        return WriteForce(state, forceId, force with { ForeignTradeProgress = progress with
            { AcquiredTechnologyLevels = progress.AcquiredTechnologyLevels.SetItem(Index(country), checked((byte)Math.Clamp(value, 0, 4))) } });
    }
    internal static GameState WriteFlags(GameState state, int forceId, byte flags) => forceId == 0
        ? PlaceholderWorldCurrentSource.WritePlaceholderForceAdministrationFlags(state, flags)
        : WriteForce(state, forceId, ReadForce(state, forceId) with { Flags193 = flags });
    private static GameState WriteForce(GameState state, int forceId, NativeArmyForceState force)
    {
        var native = state.NativeArmySimulation ?? throw Missing("native_owner");
        return state with { NativeArmySimulation = native with { Forces = new(native.Forces) { [forceId] = force } } };
    }
    private static int Index(int country) => country is >= 0 and <= 4 ? country : throw Missing("country_index");
    private static InvalidDataException Missing(string name) => new("foreign-trade.current." + name);
}
