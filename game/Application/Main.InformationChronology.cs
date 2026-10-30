using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private void OpenInformationChronology(MapInformationBrowserView browser,GameState world,ChronologyInformationPage page)
    {
        if(!IsInformationBrowserCurrent(browser)||_residentMap is not {IsPlayableMapReady:true} map||
            _session is not { } session||!ReferenceEquals(_world,world)||page.Selected is not { } initial)return;
        // Keep this exact InfoMenu/proposal parent, its tab and draft. The
        // browser owns temporary blur suppression while the map state is open.
        var visibility=new List<(CanvasItem Item,bool Visible)>();
        foreach(var item in new CanvasItem?[]{_forceHud,_mapFocusToggle,_commanderCards})
            if(item is not null&&GodotObject.IsInstanceValid(item))visibility.Add((item,item.Visible));
        var view=new MapInformationChronologyView();
        bool restored=false;
        bool HostCurrent()=>!_failed&&!_shuttingDown&&ReferenceEquals(_session,session)&&_residentMap==map&&
            IsInformationBrowserCurrent(browser)&&browser.IsCurrentDetail(view);
        bool Current()=>HostCurrent()&&ReferenceEquals(_world,world);
        void RestoreOuter()
        {
            if(restored)return;restored=true;
            if(_failed||_shuttingDown||!ReferenceEquals(_session,session)||_residentMap!=map)return;
            foreach(var (item,visible) in visibility)
                if(GodotObject.IsInstanceValid(item)&&item.IsInsideTree())item.Visible=visible;
        }
        void End()
        {
            if(GodotObject.IsInstanceValid(map)&&map.IsInsideTree())map.EndChronology(view);
        }
        view.TreeExiting+=()=>{End();RestoreOuter();};
        browser.ShowMapDetail(view,close=>
        {
            void Finish(){End();close();RestoreOuter();}
            if(!map.BeginChronology(view,initial.Map,out string? missing))
            {GD.PushWarning(missing??"chronology.map_admission");Finish();return;}
            foreach(var (item,_) in visibility)item.Hide();
            // The temporary map owns this immutable-world identity. If the
            // actual host adopts another world, return before another saved
            // snapshot can target that replacement's map owner.
            RetainCommandDetailSpeechWorld(view,_=>Finish());
            view.Configure(_repository!,_ui,page,Current,
                ()=>RunUiAction(()=>{if(HostCurrent())Finish();}),
                snapshot=>{if(Current())map.UpdateChronology(view,snapshot);},CurrentListScrollSpeed);
        });
    }
}
