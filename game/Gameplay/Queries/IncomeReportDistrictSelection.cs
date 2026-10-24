using San14.Data;

namespace San14.Client.Gameplay;

internal sealed record IncomeReportDistrict(DistrictReference Reference, byte Number);

/// <summary>The income dialog's current Force district list. Membership comes
/// from the live registry; neither world dictionary order nor validity replaces it.</summary>
internal static class IncomeReportDistrictSelection
{
    internal static IReadOnlyList<IncomeReportDistrict> Read(SourceWorldContext current, ForceReference force)
    {
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.District)
            ?? throw Missing("district registry descriptor");
        var node = current.ReadRegistryHead(descriptor) ?? throw Missing("district registry head");
        int ordinal = current.ReadForcePhysicalSlot(force) ?? throw Missing("Force ordinal");
        var members = new List<DistrictReference>();
        while (node.Identity.HasValue)
        {
            // The real iterator rejects Force0 before dereferencing a payload.
            if (ordinal is >= 1 and <= 51)
            {
                var payload = current.ReadRegistryNodePayload(node) ?? throw Missing("district registry payload");
                if (payload.Kind != RegistryKind.District || payload.PhysicalSlot is not int physical)
                    throw Missing("district registry receiver");
                byte owner = current.ReadDistrictForceId(physical) ?? throw Missing("district Force");
                if (owner == ordinal) members.Add(new(physical));
            }
            node = current.ReadRegistryNodeNext(node) ?? throw Missing("district registry next");
        }
        // The original insertion sort moves only strict decreases. Equal
        // numbers and repeated receivers retain their original registry order.
        for (int i = 1; i < members.Count; i++)
        {
            var moving = members[i];
            int position = i;
            while (position > 0 && Number(members[position - 1]) > Number(moving))
            {
                members[position] = members[position - 1];
                position--;
            }
            members[position] = moving;
        }
        return Array.AsReadOnly(members.Take(8).Select(reference => new IncomeReportDistrict(reference, Number(reference))).ToArray());

        byte Number(DistrictReference reference) => current.ReadDistrictNumber(reference.PhysicalSlot
            ?? throw Missing("district physical identity")) ?? throw Missing("district number");
    }

    private static InvalidDataException Missing(string field) => new("Income report current input is unknown: " + field);
}
