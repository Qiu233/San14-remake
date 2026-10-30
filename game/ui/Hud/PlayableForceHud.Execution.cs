using Godot;
using San14.Core;
using San14.Data;

public partial class PlayableForceHud
{
    private MapExecutionControls? executionControls;
    private MapProgressRing? progressRing;
    private PreviewText executionText=null!;
    private bool executingPeriod,lastCanAdvance;

    internal void ApplyExecution(bool active,bool paused,int speed,NativeMapSeasonDate? date)
    {
        executingPeriod=active;
        _advance.ApplyExecution(active&&!paused);
        _advance.Disabled=active?false:!lastCanAdvance;
        executionControls?.ApplySpeed(speed);
        if(date is not null)
        {
            _year.Text=executionText.Format("map_hud_year",date.Year);
            _month.Text=executionText.Format("map_hud_month",date.Month);
            _period.Text=executionText.Get(date.Day<11?"map_hud_early_period":
                date.Day<21?"map_hud_middle_period":"map_hud_late_period");
            progressRing?.ApplyFrame(active?((date.Hour+((date.Day-1)%10)*24)*100)/240:0);
        }
        else if(!active)progressRing?.ApplyFrame(0);
        SetMeta("period_execution",active);SetMeta("period_user_paused",paused);
        SetMeta("in_progress_animation_restored",false);
    }
}
