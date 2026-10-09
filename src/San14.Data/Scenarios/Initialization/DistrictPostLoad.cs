namespace San14.Data;

public sealed record NativeDistrict(int PhysicalSlot, byte RawForceSlot10,
    byte RawNumber11, ushort RawLeader12);

/// <summary>Field-level current queries: membership and status do not consume unrelated person fields.</summary>
public interface IDistrictPostLoadState
{
    bool? ReadDistrictNativeValidity(int physicalSlot);
    void ResetDistrict(int physicalSlot);
    byte? ReadPersonDistrictSlot(int physicalSlot);
    byte? ReadPersonStatus(int physicalSlot);
    bool? ReadPersonNativeValidity(int physicalSlot);
    ushort? ReadPersonIdentity(int physicalSlot);
    void WritePersonStatus(int physicalSlot, byte status);
    void WriteDistrictLeader(int physicalSlot, ushort leaderId);
}

public sealed record DistrictPostLoadWrite(int PersonPhysicalSlot, ushort LeaderId);
public sealed record DistrictPostLoadResult(bool DistrictReset, int? LastStatus1PhysicalSlot,
    IReadOnlyList<int> CapturedStatus2PhysicalSlots, IReadOnlyList<DistrictPostLoadWrite> Writes);

/// <summary>
/// CE district slot7 (2244C0). Requires the explicit current registered-person
/// pointer sequence. Preserves duplicate pointers and native read/write order.
/// Does not complete the subsequent person slot7 or first-player initialization.
/// </summary>
public static class DistrictPostLoad
{
    public static DistrictPostLoadResult Apply(int districtPhysicalSlot,
        IEnumerable<int> registeredPersonPhysicalSlots, IDistrictPostLoadState state)
    {
        ArgumentNullException.ThrowIfNull(registeredPersonPhysicalSlots);
        ArgumentNullException.ThrowIfNull(state);
        Check(districtPhysicalSlot, 52, "district physical slot");
        bool reset = !Known(state.ReadDistrictNativeValidity(districtPhysicalSlot), "district validity");
        if (reset) state.ResetDistrict(districtPhysicalSlot);
        var captured = new List<int>();
        var writes = new List<DistrictPostLoadWrite>();
        int? last = null;
        // Native iterator rejects sentinel district 0 before consuming person fields.
        if (districtPhysicalSlot != 0)
        {
            foreach (int physical in registeredPersonPhysicalSlots)
            {
                Check(physical, 1851, "registered person physical slot");
                if (Known(state.ReadPersonDistrictSlot(physical), $"person {physical} district118") != districtPhysicalSlot)
                    continue;
                if (Known(state.ReadPersonStatus(physical), $"person {physical} status11E first read") == 2)
                    captured.Add(physical);
                // This is an independent second native read, not an else-if or cached status.
                if (Known(state.ReadPersonStatus(physical), $"person {physical} status11E second read") == 1)
                    last = physical;
            }
        }
        if (last.HasValue && Known(state.ReadPersonNativeValidity(last.Value), $"last person {last.Value} validity"))
        {
            foreach (int physical in captured)
            {
                state.WritePersonStatus(physical, 4);
                ushort leader = (ushort)Math.Min((int)Known(state.ReadPersonIdentity(last.Value), $"last person {last.Value} current ID"), 6000);
                state.WriteDistrictLeader(districtPhysicalSlot, leader);
                writes.Add(new(physical, leader));
            }
        }
        return new(reset, last, captured.AsReadOnly(), writes.AsReadOnly());
    }

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current district post-load input {field} is unknown.");
    private static void Check(int value, int count, string field)
    {
        if (value < 0 || value >= count)
            throw new InvalidDataException($"{field} {value} is outside supported native profile 0..{count - 1}.");
    }
}
