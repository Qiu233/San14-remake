using System.Collections.ObjectModel;
using System.Numerics;
using San14.Core;
using San14.Data;

public sealed partial class OriginalRemakeArmySceneVisuals
{
    private OriginalRemakeArmyVisualFrame PublishNavalMotionFrame(UnitState unit,OwnedMotion owned)
    {
        var records=owned.Motion.CaptureRecords();var current=owned.Motion.UnitPosition10;
        var writes=new List<OriginalArmyInstancePoseWrite>();
        var publications=new Dictionary<OriginalRemakeArmyComponentKey,OriginalRemakeArmyPosePublication>();
        if(OriginalArmyModelSelection.ChooseKnownDescriptor(1,null,owned.SelectedFormation1C).Category is int category)
        {
            // CE1CCA80 writes only the first actual818 member's P/Q. Native
            // v30 owns Unit14 and preserves the bound physical model's S.
            var position=new Vector4(current.X,MathF.Max(445,Ground(current.X,current.Z)),current.Z,1);
            var rotation=OriginalArmyInstancePose.YawArgument(owned.Motion.UnitHeading744);
            var write=new OriginalArmyInstancePoseWrite(-1,0x818,0,position,rotation);
            writes.Add(write);
            var world=_constructors[category].PublishPosition(new(position.X,position.Y,position.Z))
                .PublishQuaternion(new(rotation.X,rotation.Y,rotation.Z,rotation.W));
            var key=new OriginalRemakeArmyComponentKey(0x818,0);
            publications.Add(key,new(key,-1,category,write,world,_independentAnimations[category],
                "source mode1 CE1CCA80; current Unit P/Q; no ocean-height or scale writer"));
        }
        // An unmatched1C clears818. The Unit, motion and common7F0 survive.
        return new(unit.Id,unit.HexId,unit.FormationId!.Value,owned.Initialization,records,
            new(writes,[],records.CallerSource,records.CodeSource),
            OriginalArmyInstancePose.WriteFlags(records,1,[],[]),
            new ReadOnlyDictionary<OriginalRemakeArmyComponentKey,OriginalRemakeArmyPosePublication>(publications))
            {CurrentUnitPosition=current,GraphicsAction60=owned.Motion.GraphicsAction60};
    }
}
