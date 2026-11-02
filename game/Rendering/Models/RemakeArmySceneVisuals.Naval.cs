using System.Collections.ObjectModel;
using System.Numerics;
using San14.Core;
using San14.Data;

public sealed partial class RemakeArmySceneVisuals
{
    private RemakeArmyVisualFrame PublishNavalMotionFrame(OwnedMotion owned)
    {
        return PublishNavalCurrentFrame(owned.Unit, owned.SelectedFormation1C, owned.Motion.UnitPosition10,
            owned.Motion.UnitHeading744, owned.Initialization, owned.Motion.CaptureRecords(), owned.Motion.GraphicsAction60,
            owned.Unit.NavalUsesSelectedLandFormation ? owned.SelectedFormation1B : owned.Unit.FormationSlot!.Value);
    }

    private RemakeArmyVisualFrame PublishNavalCurrentFrame(RemakeArmyVisualUnit unit, byte naval,
        Vector3 current, float heading, RemakeArmyInitialization initialization,
        ArmyInstancePoseSnapshot records, int graphicsAction, int formationSlot)
    {
        var writes=new List<ArmyInstancePoseWrite>();
        var publications=new Dictionary<RemakeArmyComponentKey,RemakeArmyPosePublication>();
        if(ArmyModelSelection.ChooseKnownDescriptor(1,null,naval).Category is int category)
        {
            // CE1CCA80 writes only the first actual818 member's P/Q. Native
            // v30 owns Unit14 and preserves the bound physical model's S.
            var position=new Vector4(current.X,MathF.Max(445,Ground(current.X,current.Z)),current.Z,1);
            var rotation=ArmyInstancePose.YawArgument(heading);
            var write=new ArmyInstancePoseWrite(-1,0x818,0,position,rotation);
            writes.Add(write);
            var world=_constructors[category].PublishPosition(new(position.X,position.Y,position.Z))
                .PublishQuaternion(new(rotation.X,rotation.Y,rotation.Z,rotation.W));
            var key=new RemakeArmyComponentKey(0x818,0);
            publications.Add(key,new(key,-1,category,write,world,_independentAnimations[category],
                "source mode1 CE1CCA80; current Unit P/Q; no ocean-height or scale writer"));
        }
        // An unmatched1C clears818. The Unit, motion and common7F0 survive.
        return new(unit.UnitId,unit.HexId,formationSlot,initialization,records,
            new(writes,[],records.CallerSource,records.CodeSource),
            ArmyInstancePose.WriteFlags(records,1,[],[]),
            new ReadOnlyDictionary<RemakeArmyComponentKey,RemakeArmyPosePublication>(publications))
            {CurrentUnitPosition=current,GraphicsAction60=graphicsAction};
    }
}
