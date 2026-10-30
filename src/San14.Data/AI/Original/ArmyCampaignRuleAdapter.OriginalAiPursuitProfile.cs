using San14.Core;

namespace San14.Data;

internal sealed record OriginalAiPursuitApplication(GameState State, bool Admitted);

public sealed partial class ArmyCampaignRuleAdapter
{
    // One CD400 host borrows this scope across all its candidate queries.
    // Neither construction nor an ordinary query copies/scans the profile.
    internal OriginalAiPursuitProfileScope BeginOriginalAiPursuitProfileCurrent(GameState state) =>
        new(() => applicationProfileOperation ?? throw ApplicationProfileMissing("pursuit_store"),
            state.PendingCampaignServices);

    internal IHexOwnershipState BindOriginalAiPursuitHexOwnershipCurrent(GameState state,
        SourceWorldContext current, OriginalAiPursuitProfileScope profile)
    {
        BindApplicationServiceCurrent(state, current);
        return new PursuitHexOwnership(current, profile);
    }

    private sealed class PursuitHexOwnership(SourceWorldContext current,
        OriginalAiPursuitProfileScope profile) : IHexOwnershipState
    {
        public byte? ReadHexSurfaceCode(HexReference hex) => current.ReadHexSurfaceCode(hex);
        public void WriteHexForceSlot(HexReference hex, byte force) => current.WriteHexForceSlot(hex, force);
        public byte? ReadHexForceSlot(HexReference hex) => current.ReadHexForceSlot(hex);
        public ForceReference? ReadCurrentPlayerForce() => current.ReadCurrentPlayerForce();
        public int? ReadForcePhysicalSlot(ForceReference force) => current.ReadForcePhysicalSlot(force);
        public bool RefreshAreaOwnershipService() => AreaOwnershipService.Refresh(current, profile);
        // The independent game has no original CSceneryLayout receiver. Its
        // ordinary map publication owns actual scenery refreshes instead.
        public SceneryLayoutReference? ReadSceneryLayoutReference() => new(null);
        public byte? ReadHexScenerySlot(HexReference hex) => current.ReadHexScenerySlot(hex);
        public SceneryReference? ReadSceneryTableReference(int slot) => current.ReadSceneryTableReference(slot);
        public bool? ReadSceneryNativeValidity(SceneryReference scenery) => current.ReadSceneryNativeValidity(scenery);
        public byte? ReadSceneryFlags47(SceneryReference scenery) => current.ReadSceneryFlags47(scenery);
        public void WriteSceneryLayoutDirty(SceneryLayoutReference layout, int value) => current.WriteSceneryLayoutDirty(layout, value);
    }
}

/// <summary>The actual application's operation-local profile is acquired only
/// if the source Area ownership service reaches a flag/date access. The host
/// completes once after its candidate queries, preserving their real changes.</summary>
internal sealed class OriginalAiPursuitProfileScope(Func<CampaignServiceProfileOperation> readOperation,
    CampaignServiceChanges pending) : ITransferServiceState
{
    private CampaignServiceProfileOperation? operation;
    private TransferServiceState? candidate;

    private TransferServiceState Current
    {
        get
        {
            if (candidate is not null) return candidate;
            operation = readOperation();
            operation.BeginOperation(pending);
            return candidate = operation.Materialize(pending);
        }
    }

    internal OriginalAiPursuitApplication Complete(GameState state, bool admitted) =>
        new(candidate is null ? state : state with
            { PendingCampaignServices = operation!.Capture(candidate) }, admitted);

    public void WriteDirtyFlag(byte value) => Current.WriteDirtyFlag(value);
    public bool? ReadPrimaryFlag(int index) => Current.ReadPrimaryFlag(index);
    public void WritePrimaryFlag(int index, bool value) => Current.WritePrimaryFlag(index, value);
    public bool? ReadPlatformPresent() => Current.ReadPlatformPresent();
    public void NotifyPlatform(int index) => Current.NotifyPlatform(index);
    public TransferServiceClockSnapshot? CaptureClockSnapshot() => Current.CaptureClockSnapshot();
    public TransferServiceClockSnapshot? CaptureClockSnapshotForFlag(int index) => Current.CaptureClockSnapshotForFlag(index);
    public void WriteDateYear(int index, ushort value) => Current.WriteDateYear(index, value);
    public void WriteDateMonth(int index, byte value) => Current.WriteDateMonth(index, value);
    public void WriteDateDay(int index, byte value) => Current.WriteDateDay(index, value);
    public void WriteDateHour(int index, byte value) => Current.WriteDateHour(index, value);
}
