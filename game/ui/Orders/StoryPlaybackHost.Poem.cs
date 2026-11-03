using San14.Core;
using San14.Data;

internal partial class StoryPlaybackHost
{
    private PoemEventView? poem;

    private void OpenPoem(StoryPoemEvent request,long sequence,int step)
    {
        if(abandoned||poem is not null)return;
        var publication=preparedUiResources.PoemEvents.ReadEvent(request.RawId);
        var view=new PoemEventView();poem=view;AddChild(view);scene.Hide();
        try
        {
            view.Configure(repository,text,publication,
                ()=>!abandoned&&IsVisibleInTree()&&admitted()&&ReferenceEquals(poem,view),
                ()=>
                {
                    if(abandoned||!ReferenceEquals(poem,view))return;
                    ClosePoem(restore:true);CompleteStep(sequence,step);
                },Fail,()=>audio.PlayPoemAppearanceSoundAsync(),preparedUiResources.CreateChromeResources());
            audio.BeginPoemEvent(text.SourceLocale,musicPreferences);
        }
        catch{ClosePoem(restore:false);throw;}
    }

    private void ClosePoem(bool restore)
    {
        var view=poem;poem=null;
        if(view is null)return;
        view.Abandon();view.GetParent()?.RemoveChild(view);view.QueueFree();
        bool resume=restore&&!abandoned;
        if(resume)scene.Show();
        audio.EndPoemEvent(resume);
    }
}
