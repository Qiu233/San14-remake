using System.Text.Json.Serialization;

namespace San14.Core;

public enum SettlementBaseKind { City, Gate }
public sealed record SettlementBase(SettlementBaseKind Kind, int Id);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "SettlementKind")]
[JsonDerivedType(typeof(UnitDefeatRequest), "unit-defeat")]
[JsonDerivedType(typeof(BaseCaptureRequest), "base-capture")]
[JsonDerivedType(typeof(ExpiredCombatSettlement), "expired")]
public abstract record CombatSettlementRequest;
public enum UnitDefeatCause { OrdinaryBattle, DuelDeath, DuelCapture }
public sealed record UnitDefeatRequest(int VictimUnitId, int? WinnerUnitId,
    SettlementBase? WinnerBase) : CombatSettlementRequest
{
    public UnitDefeatCause Cause { get; init; }
}
public sealed record BaseCaptureRequest(SettlementBase Target, int? WinnerUnitId)
    : CombatSettlementRequest;
// Old saves can contain a request whose receiver has already been retired.
// Keep its place until settlement, without attaching it to a reused entity.
public sealed record ExpiredCombatSettlement : CombatSettlementRequest;

public sealed record CombatSettlementQueue(List<CombatSettlementRequest> Requests)
{
    [JsonIgnore]
    public bool IsEmpty => Requests.Count == 0;
    public static CombatSettlementQueue Create() => new([]);

    public IEnumerable<int> ReferencedUnitIds() => Requests.SelectMany(request => request switch
    {
        UnitDefeatRequest unit => unit.WinnerUnitId is int winner ? [unit.VictimUnitId, winner] : new[] { unit.VictimUnitId },
        BaseCaptureRequest { WinnerUnitId: int winner } => new[] { winner },
        _ => Array.Empty<int>()
    });
}

public enum CombatSettlementAdmissionKind { Accepted, Duplicate, Full }
public sealed record CombatSettlementAdmission(CombatSettlementQueue Queue,
    CombatSettlementAdmissionKind? Kind, string? MissingInput = null);

/// <summary>Current entity facts supplied by the operation's world, not a
/// cached copy of validity or ownership at admission time.</summary>
public interface ICombatSettlementQueries
{
    bool? IsUnitValid(int unitId);
    bool? IsBaseValid(SettlementBase target);
    bool? IsCaptureExclusionApplicable(int winnerUnitId);
}

public static class CombatSettlementRules
{
    // The original command admission limit is observable: a full queue takes
    // the immediate settlement path before checking for duplicate requests.
    public const int PendingSettlementLimit = 128;

    public static CombatSettlementAdmission Admit(CombatSettlementQueue queue,
        CombatSettlementRequest request, ICombatSettlementQueries current)
    {
        if (queue.Requests.Count >= PendingSettlementLimit) return new(queue, CombatSettlementAdmissionKind.Full);
        foreach (var pending in queue.Requests)
        {
            bool? duplicate = SameCurrentVictim(pending, request, current);
            if (duplicate is null) return new(queue, null, "Current settlement victim validity is unavailable.");
            if (duplicate.Value) return new(queue, CombatSettlementAdmissionKind.Duplicate);
        }
        return new(new([.. queue.Requests, request]), CombatSettlementAdmissionKind.Accepted);
    }

    public static bool? ExcludesUnit(CombatSettlementQueue queue, int unitId, ICombatSettlementQueries current)
    {
        foreach (var request in queue.Requests)
        {
            if (request is BaseCaptureRequest capture)
            {
                bool? targetValid = current.IsBaseValid(capture.Target);
                if (targetValid is null) return null;
                if (!targetValid.Value || capture.WinnerUnitId is not int winner) continue;
                bool? winnerValid = current.IsUnitValid(winner);
                if (winnerValid is null) return null;
                if (!winnerValid.Value) continue;
                bool? applicable = current.IsCaptureExclusionApplicable(winner);
                if (applicable is null) return null;
                if (applicable.Value && winner == unitId) return true;
            }
            else if (request is UnitDefeatRequest defeat)
            {
                bool? valid = current.IsUnitValid(defeat.VictimUnitId);
                if (valid is null) return null;
                if (valid.Value && defeat.VictimUnitId == unitId) return true;
            }
        }
        return false;
    }

    public static bool? ExcludesBase(CombatSettlementQueue queue, SettlementBase target,
        ICombatSettlementQueries current)
    {
        foreach (var request in queue.Requests.OfType<BaseCaptureRequest>())
        {
            bool? valid = current.IsBaseValid(request.Target);
            if (valid is null) return null;
            if (valid.Value && request.Target == target) return true;
        }
        return false;
    }

    private static bool? SameCurrentVictim(CombatSettlementRequest first,
        CombatSettlementRequest second, ICombatSettlementQueries current)
    {
        if (first is UnitDefeatRequest a && second is UnitDefeatRequest b)
        {
            bool? validA = current.IsUnitValid(a.VictimUnitId);
            if (validA is null) return null;
            if (!validA.Value) return false;
            bool? validB = current.IsUnitValid(b.VictimUnitId);
            return validB is null ? null : validB.Value && a.VictimUnitId == b.VictimUnitId;
        }
        if (first is BaseCaptureRequest x && second is BaseCaptureRequest y)
        {
            bool? validX = current.IsBaseValid(x.Target);
            if (validX is null) return null;
            if (!validX.Value) return false;
            bool? validY = current.IsBaseValid(y.Target);
            return validY is null ? null : validY.Value && x.Target == y.Target;
        }
        return false;
    }
}
