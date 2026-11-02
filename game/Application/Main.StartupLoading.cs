using Godot;
using San14.Data;

public partial class Main
{
    // The source loading surface borrows the completed startup UI generation.
    // It displays real work without a percentage or a campaign/session owner.
    private async Task RunStartupLoadingAsync(Func<Task> prepare)
    {
        ArgumentNullException.ThrowIfNull(prepare);
        if (_shuttingDown || _failed || !IsInsideTree()) return;
        // A headless process has no Canvas draw event to await. It still
        // performs and awaits the same real static source preparation.
        if (DisplayServer.GetName() == "headless")
        {
            await PrepareApplicationAudioResourcesAsync();
            await prepare();
            return;
        }
        var repository = _applicationUiResourceRepository ?? _repository
            ?? throw new InvalidOperationException("Startup resources require their repository owner.");
        var view = new NewGamePreparationView();
        AddChild(view);
        try
        {
            view.Configure(repository, _ui, WindowCloseRequested,
                TryBorrowApplicationUiResources(view), messageText: _ui.Get("startup_loading"), showCancel: false);
            await view.Drawn;
            if (_shuttingDown || _failed || !IsInsideTree()) return;
            await PrepareApplicationAudioResourcesAsync();
            await prepare();
        }
        finally
        {
            if (GodotObject.IsInstanceValid(view))
            {
                view.Abandon();
                if (view.GetParent() is Node parent) parent.RemoveChild(view);
                view.QueueFree();
            }
        }
    }
}
