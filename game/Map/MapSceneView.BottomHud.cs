using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private MapBottomHud? bottomHud;
    private GameState? bottomHudWorld;
    private Func<GameState,AreaAdministrationQuery<MapAdvicePublication>>? queryBottomAdvice;
    private Func<GameState,AreaAdministrationQuery<MapBottomLogPublication>>? queryBottomLog;
    private Func<GameState,int[],IReadOnlyDictionary<int,OfficerPortraitRoute>>? queryBottomPortraits;
    private Action<MapAdviceEntry>? executeBottomAdvice;
    private Action<int>? focusBottomLog;
    private partial class BottomHudClock:Node
    {
        internal Action<double>? Tick;
        public override void _Process(double delta)=>Tick?.Invoke(delta);
        public override void _ExitTree()=>Tick=null;
    }
    internal void SetMapBottomHudQueries(Func<GameState,AreaAdministrationQuery<MapAdvicePublication>> advice,
        Func<GameState,AreaAdministrationQuery<MapBottomLogPublication>> log,
        Func<GameState,int[],IReadOnlyDictionary<int,OfficerPortraitRoute>> portraits,
        Action<MapAdviceEntry> execute,Action<int> focus)
    {
        queryBottomAdvice=advice;queryBottomLog=log;queryBottomPortraits=portraits;
        executeBottomAdvice=execute;focusBottomLog=focus;
        AddChild(new BottomHudClock{Name="OrdinaryBottomHudClock",Tick=TickMapBottomHud});
    }
    private bool BottomHudAdmitted()=>_summaryCanQuery?.Invoke()==true&&!_detailsOpen&&!_periodMapActive&&IsVisibleInTree();
    private void TickMapBottomHud(double delta)
    {
        if(_summaryRoot is null||!GodotObject.IsInstanceValid(_summaryRoot)||_summaryResources is null)
        {bottomHud=null;bottomHudWorld=null;return;}
        if(bottomHud is not null&&!GodotObject.IsInstanceValid(bottomHud)){bottomHud=null;bottomHudWorld=null;}
        if(!BottomHudAdmitted()||_logicalWorld is not{} world)
        {bottomHud?.Hide();return;}
        if(bottomHud is null)
        {
            bottomHud=new();_summaryRoot.AddChild(bottomHud);
            bottomHud.Configure(_summaryResources,_text.SourceLocale,BottomHudAdmitted,
                row=>executeBottomAdvice?.Invoke(row),hex=>focusBottomLog?.Invoke(hex));
        }
        if(!ReferenceEquals(bottomHudWorld,world))
        {
            var advice=queryBottomAdvice!(world);var log=queryBottomLog!(world);
            var rows=log.Available?log.Value?.Entries:Array.Empty<MapBottomLogEntry>();
            var requested=rows?.Select(row=>row.PersonId).Distinct().ToArray()??[];
            IReadOnlyDictionary<int,OfficerPortraitRoute> portraits=requested.Length==0?new Dictionary<int,OfficerPortraitRoute>():queryBottomPortraits!(world,requested);
            bottomHud.Apply(advice.Available?advice.Value:null,log.Available?log.Value:null,portraits);
            bottomHud.SetMeta("source_owner_issue",string.Join(";",new[]{advice.UnresolvedInput,log.UnresolvedInput}.Where(x=>x is not null)));
            bottomHudWorld=world;
        }
        bottomHud.Show();bottomHud.Tick(delta);
    }
}
