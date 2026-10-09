using San14.Core;

namespace San14.Data;

/// <summary>Only the CE source-facing consumers require a byte identity.
/// Wider independent appointment IDs remain unresolved for that profile.</summary>
public static class OfficerAppointmentSourceAdapter
{
    public static byte? ReadOfficeId(OfficerAppointment appointment) =>
        OfficerAppointmentRules.ReadOfficeId(appointment) is int id && id is >= 0 and <= byte.MaxValue
            ? (byte)id : null;
}
