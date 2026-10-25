using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private TransferServiceProfileStore? _applicationProfile;

    private void AttachApplicationProfile(CampaignComposition composition, GameState adopted)
    {
        if (_applicationProfile is null)
        {
            string path = ProjectSettings.GlobalizePath("user://application-progress.s14p");
            _applicationProfile = File.Exists(path) ? TransferServiceProfileStore.OpenExisting(path)
                : TransferServiceProfileStore.CreateNew(path);
        }
        composition.ConfigureApplicationProfile(_applicationProfile);
        composition.PublishApplicationServices(adopted);
    }

    private CampaignStoryBindings? CaptureNormalTurnStoryBindings(GameplaySession session,
        CampaignComposition composition, MapSceneView? map, CancellationToken cancellation)
    {
        AttachApplicationProfile(composition, PersonnelPresentationOwner(session));
        if (map is null) return null;
        var ui = SynchronizationContext.Current ?? throw new InvalidOperationException("Story camera requires its UI context.");
        int thread = System.Environment.CurrentManagedThreadId;
        CampaignCameraSnapshot? Capture()
        {
            CampaignCameraSnapshot? snapshot = null;
            void Read()
            {
                cancellation.ThrowIfCancellationRequested();
                if (!ReferenceEquals(_session, session) || !ReferenceEquals(_residentMap, map) ||
                    _shuttingDown || _failed || !GodotObject.IsInstanceValid(map))
                    throw new OperationCanceledException("Story camera belongs to a retired map.", cancellation);
                snapshot = map.CaptureStoryCamera();
            }
            //Capture at the actual event boundary, rather than saving the
            //camera's earlier pose before forty Army updates. Godot remains on
            //its owner thread while the private worker waits for this small read.
            if (System.Environment.CurrentManagedThreadId == thread) Read();
            else
            {
                var captured = new TaskCompletionSource<CampaignCameraSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var registration = cancellation.Register(() => captured.TrySetCanceled(cancellation));
                ui.Post(_ =>
                {
                    try { Read(); captured.TrySetResult(snapshot); }
                    catch (OperationCanceledException) { captured.TrySetCanceled(cancellation); }
                    catch (Exception error) { captured.TrySetException(error); }
                }, null);
                snapshot = captured.Task.GetAwaiter().GetResult();
            }
            return snapshot;
        }
        var audio = new CampaignNormalEventAudioInputs(_ui.SourceLocale,
            readApplicationField1AC: () => 0,
            readApplicationPointerPresent: () => true);
        return composition.PrepareArmyQueries(PersonnelPresentationOwner(session)).CreateNormalTurnStoryBindings(audio, Capture);
    }
}
