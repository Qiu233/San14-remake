using San14.Core;

namespace San14.Data;

public sealed partial class SourceWorldContext
{
    internal GameState WriteMonthlyCaptivityProgress(GameState state, int physical, byte value)
    {
        var old = ReadOfficerAftermath(physical) ?? throw new InvalidDataException("monthly-maintenance.current_captivity_owner");
        int id = ReadPersonIdentity(physical) ?? throw new InvalidDataException("monthly-maintenance.current_captivity_identity");
        SetOfficerAftermath(physical, old with { SourceKnowledge =
            (old.SourceKnowledge ?? new(id, null, null, null)) with { CaptureResetCode = value } });
        return PublishOfficerAftermath(state, id);
    }
    internal GameState WriteMonthlyResentment(GameState state, int physical, byte intensity, bool clearTarget)
    {
        var old = ReadOfficerAftermath(physical) ?? throw new InvalidDataException("monthly-maintenance.current_resentment_owner");
        int id = ReadPersonIdentity(physical) ?? throw new InvalidDataException("monthly-maintenance.current_resentment_identity");
        //The zero-transition store does not read the previous target. Other
        //updates preserve an unknown target instead of manufacturing one.
        SetOfficerAftermath(physical, old with { Resentment = new(
            clearTarget ? 0 : old.Resentment?.TargetOfficerId, intensity),
            SourceKnowledge = clearTarget && old.SourceKnowledge is { } residue
                ? residue with { UnmappedResentmentTarget = null } : old.SourceKnowledge });
        return PublishOfficerAftermath(state, id);
    }
}
