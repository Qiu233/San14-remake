using San14.Core;
using San14.Data.Formats;
namespace San14.Data;

public interface IStatelessNativeProgressStageRules : IPlayableNativeArmyProgressStageRules { }
public interface ICampaignStageOperation : IPlayableNativeArmyProgressStageRules
{
    CampaignGroupingCheckpoint CaptureCheckpoint();
    void RestoreCheckpoint(CampaignGroupingCheckpoint checkpoint);
}
public sealed class CampaignStoryBindings(IS14AudioEventExecutionState audio, TransferServiceState profile,
    bool? skipCamera, TransferModeState? externalMode, bool? applicationPresent, int? applicationUserIndex,
    Func<CampaignCameraSnapshot?>? captureCamera = null)
{
    public IS14AudioEventExecutionState Audio { get; } = audio;
    public bool? SkipCamera { get; } = skipCamera;
    public TransferModeState? ExternalMode { get; } = externalMode;
    public bool? ApplicationPresent { get; } = applicationPresent;
    public bool? ModalParentPresent { get; init; }
    public int? ApplicationUserIndex { get; } = applicationUserIndex;
    public CampaignCameraSnapshot CaptureCamera() => captureCamera?.Invoke() ?? throw new InvalidDataException("Story caller requires its actual campaign camera snapshot.");
    private readonly CampaignServiceProfileOperation service = new(profile);
    public TransferServiceState Current => service.Current;
    internal void BeginOperation(CampaignServiceChanges saved) => service.BeginOperation(saved);
    internal CampaignServiceChanges PreserveClockReads(CampaignServiceChanges changes) => service.PreserveClockReads(changes);
    internal TransferServiceState Materialize(CampaignServiceChanges changes) => service.Materialize(changes);
    internal CampaignServiceChanges Capture(TransferServiceState candidate) => service.Capture(candidate);
    internal void Publish(TransferServiceState validated) => service.Publish(validated);
}
