using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private void OpenStoryPoem(StoryPresentationBinding binding,StoryPoemEvent request,long sequence,int step)
    {
        if(!IsStoryCurrent(binding)||binding.PoemEvent is not null)return;
        var audio=_sceneAudioBinding??throw new InvalidDataException("story.poem.audio_owner");
        var view=new PoemEventView();binding.PoemEvent=view;AddChild(view);
        binding.View.Hide();
        try
        {
            var prepared=TryBorrowApplicationPreparedResources(view);
            var publication=prepared.PoemEvents.ReadEvent(request.RawId);
            view.Configure(_repository!,_ui,publication,
                ()=>IsStoryCurrent(binding)&&ReferenceEquals(binding.PoemEvent,view)&&
                    !binding.Cancellation.IsCancellationRequested&&!_failed&&!_shuttingDown,
                ()=>RunUiAction(()=>
                {
                    if(!IsStoryCurrent(binding)||!ReferenceEquals(binding.PoemEvent,view))return;
                    //The real poem pop resumes its parent before returning the
                    //music owner. The script receipt follows that completed pop.
                    CloseStoryPoem(binding,restore:true);
                    CompleteStoryStep(binding,sequence,step);
                }),
                error=>RunUiAction(()=>ReportPlayFailure(error)),
                ()=>audio.PlayPoemAppearanceSoundAsync(),prepared.CreateChromeResources());
            audio.BeginPoemEvent(_ui.SourceLocale,_preferences.MusicSelection);
        }
        catch{CloseStoryPoem(binding,restore:false);throw;}
    }

    private void CloseStoryPoem(StoryPresentationBinding binding,bool restore)
    {
        var view=binding.PoemEvent;binding.PoemEvent=null;
        if(view is null)return;
        view.Abandon();view.GetParent()?.RemoveChild(view);view.QueueFree();
        bool resume=restore&&IsStoryCurrent(binding)&&!_failed&&!_shuttingDown;
        if(resume)binding.View.Show();
        _sceneAudioBinding?.EndPoemEvent(resume);
    }

    private bool HandleStoryPoemInput(InputEvent input)
    {
        if(_storyPresentation is not{} binding||binding.PoemEvent is not{} view||!IsStoryCurrent(binding))return false;
        if(input is InputEventMouseButton {ButtonIndex:MouseButton.Right} mouse)
        {
            if(!mouse.Pressed)RunUiAction(view.RequestBack);
            return true;
        }
        if(input is not InputEventKey key||_preferences is null)return false;
        int usage=KeyboardPhysicalInput.Hid(key.PhysicalKeycode);
        int encoded=usage|(key.ShiftPressed?KeyboardBindingCatalog.ShiftModifier:0)|
            (key.CtrlPressed?KeyboardBindingCatalog.ControlModifier:0);
        var bindings=_preferences.KeyboardBindings;
        const int backAction=13;
        if(encoded==0||!(bindings[backAction*2]==encoded||bindings[backAction*2+1]==encoded))return false;
        if(key.Pressed&&!key.Echo)RunUiAction(view.RequestBack);
        return true;
    }
}
