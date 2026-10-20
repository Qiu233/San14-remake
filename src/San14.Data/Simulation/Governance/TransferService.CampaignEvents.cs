using San14.Data.Formats;

namespace San14.Data;

public sealed record CampaignEventServiceInputs(IServiceFlagQueries Eligibility,
    IS14PlayerEventConditionQueries Current, Func<int, int?> ReadRecordKind,
    bool? ApplicationPresent, int? CurrentUserIndex);

/// <summary>Local service candidate only. No world occurrence, playback
/// acknowledgement, profile save or external callback is performed here.</summary>
public sealed record CampaignEventServicePreparation(TransferServiceState? Service, string? UnresolvedInput)
{
    public bool Available => Service is not null && UnresolvedInput is null;
}

public enum CampaignEventServicePart { All, Event, Landmark }

public static class CampaignEventService
{
    public static CampaignEventServicePreparation PrepareLandmarkCompletion(CampaignEventServiceInputs inputs,
        TransferServiceState service, int? storyEventId, CampaignEventServicePart part = CampaignEventServicePart.All)
    {
        ArgumentNullException.ThrowIfNull(inputs); ArgumentNullException.ThrowIfNull(service);
        if (storyEventId is int id && id is not (>= 339 and <= 438))
            throw new InvalidDataException("This service consumer requires the complete landmark story family.");
        try
        {
            var working = service.Copy();
            if (part != CampaignEventServicePart.Landmark && storyEventId is int eventId && !HasSelectedUser())
            {
                //3F0750 repeats eligibility before touching its independent bitmap.
                if (Eligible() && Eligible())
                {
                    working.WriteDirtyFlag(1); working.SetEventFlag(eventId, false); working.SetEventFlag(eventId, true);
                    //3EA800 performs this App gate again, then both generic
                    //checks, even if the first one has already succeeded.
                    if (!HasSelectedUser() && K(working.ReadEventFlag(eventId), "new event service bit"))
                    { CheckEventCount(1, 20, 14, false); CheckEventCount(4, 10, 49, true); }
                }
            }
            if (part != CampaignEventServicePart.Event) CheckLandmarks();
            return new(working, null);

            bool Eligible() => K(inputs.Eligibility.ReadTransferServiceEligibility(), "eligibility");
            bool HasSelectedUser() => K(inputs.ApplicationPresent, "application pointer") &&
                K(inputs.CurrentUserIndex, "current user index") != -1;
            bool ValidPlayer(out int physical)
            {
                var player = inputs.Current.ReadConditionCurrentPlayerForce() ?? throw Missing("player pointer");
                physical = 0;
                if (player.Value is not { } force || !K(inputs.Current.ReadConditionForceValidity(force), "player validity")) return false;
                physical = force.PhysicalSlot; return true;
            }
            void Completion()
            {
                for (int bit = 1; bit <= 37; bit++) if (!K(working.ReadPrimaryFlag(bit), "completion bit")) return;
                SetAchievement(0);
            }
            void SetAchievement(int flag)
            {
                //Only a reached SetFlag consumes platform presence. No callback
                //may escape a transaction that can still fail later.
                if (working.PlatformPolicy != TransferServicePlatformPolicy.Absent)
                    throw new InvalidDataException("Current story service achievement requires an explicitly absent platform during preparation.");
                TransferService.SetFlag(flag, inputs.Eligibility, working);
            }
            void CheckEventCount(int kind, int threshold, int flag, bool early)
            {
                if (!Eligible() || !ValidPlayer(out _)) return;
                int count = 0;
                for (int index = 0; index < 1024; index++)
                    if (K(inputs.ReadRecordKind(index), "event resource kind") == kind &&
                        K(working.ReadEventFlag(index), "existing event service bit"))
                    { count++; if (early && count >= threshold) break; }
                if (count < threshold) return;
                SetAchievement(flag); Completion();
            }
            void CheckLandmarks()
            {
                if (!Eligible() || !ValidPlayer(out int player)) return;
                int count = 0;
                for (int index = 0; index < 101; index++)
                {
                    var scenery = inputs.Current.ReadConditionSceneryTableSlot(index) ?? throw Missing("landmark table pointer");
                    if (!K(inputs.Current.ReadConditionSceneryValidity(scenery), "landmark validity") ||
                        (K(inputs.Current.ReadConditionSceneryFlags(scenery), "landmark flags") & 1) == 0 ||
                        K(inputs.Current.ReadConditionSceneryForceSlot(scenery), "landmark current land owner") != player ||
                        K(inputs.Current.ReadConditionSceneryLevel(scenery), "landmark current level") <= 5) continue;
                    if (++count <= 14) continue;
                    SetAchievement(53); Completion(); return;
                }
            }
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }
    private static T K<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new($"Current story service {owner} is unknown.");
}
