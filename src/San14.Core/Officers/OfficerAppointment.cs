using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>Appointment knowledge owned by the officer. A declared appointment
/// and a retained observation are independent: historical saves can contain
/// either, neither, or contradictory values. Commands change the declaration;
/// custody transitions explicitly synchronize both channels.</summary>
public sealed record OfficerAppointment
{
    [JsonConstructor]
    public OfficerAppointment(int? assignedOfficeId, int? observedOfficeId)
    {
        if (assignedOfficeId < 0 || observedOfficeId < 0)
            throw new InvalidDataException("Officer appointment identities cannot be negative.");
        AssignedOfficeId = assignedOfficeId;
        ObservedOfficeId = observedOfficeId;
    }

    public int? AssignedOfficeId { get; }
    public int? ObservedOfficeId { get; }
    public static OfficerAppointment Unknown { get; } = new(null, null);
    public OfficerAppointment Assign(int? officeId) => new(officeId, ObservedOfficeId);
    public OfficerAppointment Observe(int? officeId) => new(AssignedOfficeId, officeId);
    public static OfficerAppointment Synchronize(int officeId) => new(officeId, officeId);
}

public static class OfficerAppointmentRules
{
    /// <summary>Ordering and morale may use a retained observation when no
    /// independent appointment is known.</summary>
    public static int? ReadOfficeId(OfficerAppointment appointment) =>
        appointment.AssignedOfficeId ?? appointment.ObservedOfficeId;

    /// <summary>Full abilities only accept the observed invalid-office sentinel
    /// as a fallback. A nonzero observation cannot establish an appointment.</summary>
    public static int? ReadAbilityOfficeId(OfficerAppointment appointment) =>
        appointment.AssignedOfficeId ?? (appointment.ObservedOfficeId == 0 ? 0 : null);
}

public sealed partial record OfficerState
{
    public OfficerAppointment Appointment { get; init; } = OfficerAppointment.Unknown;

    // Compatibility for command/UI callers; this is a query and assignment
    // facade over the owner, never a second stored appointment.
    [JsonIgnore]
    public int? OfficeId
    {
        get => Appointment.AssignedOfficeId;
        init => Appointment = Appointment.Assign(value);
    }
}
