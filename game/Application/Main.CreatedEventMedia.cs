using Godot;
using San14.Core;

public partial class Main
{
    private bool EnsureCreatedEventMedia(CreatedEventWorkspace owner)
    {
        if (!IsCreatedEventPlaybackCurrent(owner)) return false;
        OpenCreatedEventHost(owner);
        var host = owner.Host ?? throw new InvalidDataException("edited_event.restore.host_owner");
        var media = host.MediaRestoration;
        if (media.IsCompletedSuccessfully) return true;
        if (media.IsFaulted)
        { FailCreatedEventPlayback(owner, media.Exception!.GetBaseException()); return false; }
        if (media.IsCanceled) return false;
        if (owner.MediaResume is null) owner.MediaResume = Resume();
        return false;

        async Task Resume()
        {
            var playback = owner.Session.PendingEditedEventPlayback!;
            try
            {
                await media;
                Callable.From(() =>
                {
                    owner.MediaResume = null;
                    if (!IsCreatedEventPlaybackCurrent(owner) || owner.Host != host ||
                        owner.Session.PendingEditedEventPlayback is not { } actual || actual.Source != playback.Source ||
                        actual.Story.Sequence != playback.Story.Sequence || actual.Story.ContentRevision != playback.Story.ContentRevision ||
                        !ReferenceEquals(actual.Story.Scene, playback.Story.Scene)) return;
                    //Re-enter the actual saved stage and re-read its current FIFO;
                    //a media restore never acknowledges or replays a business leaf.
                    OpenCreatedEventPlayback(owner);
                }).CallDeferred();
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                Callable.From(() =>
                {
                    owner.MediaResume = null;
                    if (IsCreatedEventPlaybackCurrent(owner) && owner.Host == host) FailCreatedEventPlayback(owner, error);
                }).CallDeferred();
            }
        }
    }
}
