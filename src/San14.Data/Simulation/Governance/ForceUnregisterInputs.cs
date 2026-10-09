using System.Buffers.Binary;
using San14.Core;

namespace San14.Data;

/// <summary>Fresh226DC0/2374A0 District fields and227070/237CC0 Force1BC/1C4.
/// Saved captures consume these writers; they never reseed after a defeat.</summary>
public static class ForceUnregisterInputs
{
    public const string Revision = "ce-force-unregister-current-20261007";
    public static GameState InstallFresh(GameState state, LoadedScenario source)
    {
        if (state.NativeForceUnregisterCurrent is not null || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.NativeArmySimulation?.Parameters.Language != source.Snapshot.Locale ||
            state.ContentHashes.GetValueOrDefault(source.ScenarioSource.ResourceUri) != source.ScenarioSource.Sha256 ||
            source.Snapshot.Forces.Count != 52 || source.Snapshot.Districts.Count != 52)
            throw new InvalidDataException("Force unregistration fields need their unused fresh CE campaign.");
        var current = new SourceWorldContext();
        var districts = new Dictionary<int, byte?[]>();
        foreach (var row in source.Snapshot.Districts)
        {
            current.ResetDistrict(row.Slot);
            current.SetDistrictRecord(new(row.Slot, row.ForceSlot, row.Number, row.LeaderSlot));
            districts.Add(row.Slot, Enumerable.Range(0x10, 28).Select(at => current.ReadDistrictRawByte(row.Slot, at)).ToArray());
        }
        var ranking = new Dictionary<int, NativeForceRankingCurrent>();
        foreach (var row in source.Snapshot.Forces)
        {
            var bytes = Convert.FromHexString(row.RemainingBytesHex);
            if (bytes.Length != 327) throw new InvalidDataException("Force237CC0 needs its complete329-byte source row.");
            //Row offsets327..328 are1BC; the tail omits the first ruler word.
            ranking.Add(row.Slot, new(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(325, 2)), 0));
        }
        var placeholder = source.Snapshot.Forces[0];
        var result = state with { NativeForceUnregisterCurrent = new(Revision, districts, ranking, [],
            placeholder.RawIndexedBytes13?.ToArray() ?? throw new InvalidDataException("Force0 current relation13 constructor/source is missing."),
            placeholder.RawIndexedBytesEA?.ToArray() ?? throw new InvalidDataException("Force0 current relationEA constructor/source is missing.")) { OtherForce1657 = 0 } };
        var native = result.NativeArmySimulation!;
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        foreach (var row in source.Snapshot.Forces.Where(x => x.Slot != 0))
            values.TryAdd(BattleRemakeCurrentForcePolicies.ForceFieldKey(row.Slot, 0x15E),
                Convert.FromHexString(row.RemainingBytesHex)[318]);
        result = result with { NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = values },
            World = native.World with { SelectedForce1656 = native.World.SelectedForce1656 ?? 0 } } };
        NativeForceUnregisterCurrentStateValidation.Validate(result); return result;
    }

    public static void BindCurrent(GameState state, SourceWorldContext current)
    {
        NativeForceUnregisterCurrentStateValidation.Validate(state);
        if (state.NativeForceUnregisterCurrent is not { } owner) return;
        foreach (var row in owner.DistrictWindows10)
            for (int index = 0; index < 28; index++)
                //Live Core ownership/leader stores have precedence. A real
                //reset0 overrides them, including an inactive historical row.
                if (index >= 4 || row.Value[0] == 0)
                    current.SetDistrictRawByte(row.Key, 0x10 + index, row.Value[index]);
        foreach (var row in owner.ForceResetImages)
        {
            if (row.Value[0x10] is byte low && row.Value[0x11] is byte high)
                current.SetOwnedForceRulerSlot10(row.Key, (ushort)(low | high << 8));
            if (row.Value[0x52] == 0 && row.Value[0x53] == 0) current.SetAssignmentForceAdvisor52(row.Key, 0);
            if (row.Value[0x47] is byte rank) current.SetForceRankSlot47(row.Key, rank);
        }
    }
}
