using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal PersonFreedomHomeSource CityCaptureNeighborhoodsCurrent()
        => _captiveFreedomHomes ??= new(_scenario);

    //26F452 checks actual F0 receiver membership before definition/scope.
    //Both normal Army and winning-District capture packets borrow this same
    //current and effect sink;294FD0 retains its real no-actor lifecycle.
    internal GameState ApplyCityCaptureAreaObjectsCurrent(GameState state, SourceWorldContext current,
        CityCaptureAreaScope scope, List<CampaignEffectRequest> effects)
    {
        var working = state;
        CityCaptureTerritorySource.RequireNoReachedAreaObjectService(current, scope,
            pending2336B0: at =>
            {
                var directory = current.CurrentMapObjectDirectory
                    ?? throw new InvalidDataException("capture.area_objects.directory");
                var receiver = directory.ReceiverAtPhysical(at.PhysicalSlot
                    ?? throw new InvalidDataException("capture.area_objects.receiver"));
                return (current.ReadOwnedCurrentListPool()?.PendingObjectRetirements
                    ?? throw new InvalidDataException("capture.area_objects.current_F0")).Contains(receiver);
            },
            remove: at => working = RemoveMapObjectWithoutActor(working, current, at, effects));
        return working;
    }
}
