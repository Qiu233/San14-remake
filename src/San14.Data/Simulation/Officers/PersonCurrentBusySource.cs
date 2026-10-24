namespace San14.Data;

/// <summary>The current232370 location and associated-command predicate.
/// Caller flags and native validity gates precede this leaf.</summary>
internal static class PersonCurrentBusySource
{
    internal static bool Read(SourceWorldContext current, int physical,
        Func<string, InvalidDataException> missing)
    {
        byte status = K(current.ReadPersonStatus(physical), "E6");
        if (status is >= 1 and <= 4 &&
            PersonPositionRepair.DecodeFirstLocation(K(current.ReadPersonFirstLocation(physical), "E2")) !=
            PersonPositionRepair.DecodeSecondLocation(K(current.ReadPersonSecondLocation(physical), "E4"))) return true;
        // Preserve the existing legacy Disappearances:null knowledge gate.
        var linked = current.ReadPersonLinkedEntity(physical) ?? throw missing("associated22AC00");
        if (!K(current.ReadLinkedEntityNativeValidity(linked), "associated_validity")) return false;
        var commands = current.ReadOwnedPersonCommandManager();
        if (linked.Identity is int id)
        {
            if (commands?.Returns.ContainsKey(id) == true) return false; // v98=15.
            if (commands?.Talents?.ContainsKey(id) == true) return true; // Search3 or Employ2.
            if (commands?.Disappearances?.ContainsKey(id) == true) return true; // v98=16.
        }
        throw missing("associated_virtual98");

        T K<T>(T? value, string field) where T : struct => value ?? throw missing(field);
    }
}
