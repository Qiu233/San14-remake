using Godot;
using San14.Data;

/// <summary>The ordinary 749460 single-foothold selector. Its seven-column
/// publication and retained map current come from the movement draft.</summary>
internal partial class CityOfficerMovementCitySelectionView : Control
{
    private DiplomacyTargetSelectionView? selection;
    internal bool IsMapMode=>selection?.IsMapMode==true;
    internal bool CanChooseTarget(int id)=>selection?.CanChooseTarget(id)==true;
    internal void Configure(GameResourceRepository repository,PreviewText text,
        DiplomacyTargetSelectionPage page,Func<bool> admitted,Action<int?> closed,
        Action<bool> modeChanged,Func<InputEventMouseButton,bool> cancelReleased,Func<int> scrollSpeed)
    {
        Name="CityOfficerMovementCities";ZIndex=5;MouseFilter=MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        selection=new();AddChild(selection);
        selection.Configure(repository,text,page,null,0x545,admitted,closed,scrollSpeed,
            new DiplomacyTargetSelectionView.MapMode(0x3AF,true,modeChanged,cancelReleased,0x578C,0x2733));
        selection.Name="MovementDestinationSelector";
        SetMeta("source_selector","749460 / foothold single selection / 141A386F0");
        SetMeta("original_movement_destination_columns_recovered",true);
    }
    internal void PickMapDestination(int id)=>selection?.PickMapTarget(id);
    internal void RequestCancel()=>selection?.RequestCancel();
    internal void Abandon(){selection?.Abandon();Hide();}
}
