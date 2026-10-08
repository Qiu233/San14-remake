using System.Collections.ObjectModel;
using System.Numerics;
using San14.Core;
using San14.Data;

public sealed partial class OriginalRemakeArmySceneVisuals
{
    private readonly Dictionary<int,OriginalArmyAnimationSourceInput> _independentAnimations=[];

    private void EnsureIndependentSourceModel(int category)
    {
        if(_models.ContainsKey(category))return;
        var model=OriginalCeArmyModels.LoadKnownCategory(_repository,_catalog!,category);
        var action=new OriginalArmyGraphicsActions(_repository,[category]).Actions[(category,0)].Animation;
        _models.Add(category,model);
        _constructors.Add(category,OriginalUnitWorldTransform.ConstructDefinition(model.Resources));
        _independentAnimations.Add(category,new(category,action.ActionIndex,action.AnimationData.Single().Source.FileId,0));
    }

    private OriginalRemakeArmyVisualFrame PublishSiegeMotionFrame(UnitState unit,OwnedMotion owned)
    {
        int category=owned.SelectedFormation1B-4;
        var records=owned.Motion.CaptureRecords();var current=owned.Motion.UnitPosition10;
        // The independent native Unit14/v30 gate owns the presented root. This
        // candidate uses its source ground clamp without grounding each model.
        var position=new Vector3(current.X,MathF.Max(420,Ground(current.X,current.Z)),current.Z);
        // Unit20's producer 1CCA80 invokes 1CF8C0 normalization before the
        // model setter stores its resulting four words verbatim.
        var rawUnitQuaternion20=OriginalArmyInstancePose.YawArgument(owned.Motion.UnitHeading744);
        var writes=Enumerable.Range(0,3).Select(index=>OriginalArmySiegePose.WriteSiege(index,position,rawUnitQuaternion20)).ToArray();
        var publications=new Dictionary<OriginalRemakeArmyComponentKey,OriginalRemakeArmyPosePublication>();
        const string source="source mode2 1CCA80; retained Unit center/quaternion; no model scale or per-model ground writer";
        foreach(var write in writes)
        {
            var p=write.PositionArgument;var q=write.RotationArgument;
            var world=_constructors[category].PublishPosition(new(p.X,p.Y,p.Z)).PublishQuaternion(new(q.X,q.Y,q.Z,q.W));
            var key=new OriginalRemakeArmyComponentKey(write.DescriptorOffset,write.DescriptorInstanceIndex);
            publications.Add(key,new(key,write.RecordIndex,category,write,world,_independentAnimations[category],source));
        }
        return new(unit.Id,unit.HexId,unit.FormationId!.Value,owned.Initialization,records,
            new(writes,[],records.CallerSource,records.CodeSource),
            OriginalArmyInstancePose.WriteFlags(records,2,[],[]),
            new ReadOnlyDictionary<OriginalRemakeArmyComponentKey,OriginalRemakeArmyPosePublication>(publications))
            {CurrentUnitPosition=current,GraphicsAction60=owned.Motion.GraphicsAction60};
    }
}
