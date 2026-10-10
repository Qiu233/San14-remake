using System.Numerics;
using San14.Core;
using San14.Data;

public sealed partial class RemakeArmySceneVisuals
{
    private sealed record OwnedMotion(RemakeArmyInitialization Initialization,
        ArmyGraphicsMotion Motion, ArmyGraphicsCurrentInputs Inputs)
    {
        public uint Revision { get; set; }
        public int Mode { get; init; }
        public byte SelectedFormation1B { get; init; }
        public byte SelectedFormation1C { get; init; }
    }
    private readonly Dictionary<int, OwnedMotion> _motions = [];
    private readonly Dictionary<ushort, Vector4> _hexPositions = [];

    /// <summary>World changes supply new targets and current rule inputs.
    /// The nineteen records survive current-Hex and model-mode changes.</summary>
    public void PrepareMotion(GameState world, PlayableArmyVisualPlan plan,
        ArmyGraphicsMotionFrame clock,
        Func<GameState, UnitState, ArmyGraphicsCurrentInputs?> captureInputs)
    {
        var frames = new Dictionary<int, RemakeArmyVisualFrame>();
        foreach (var (id, selection) in plan.Selections)
        {
            var unit = world.Units[id];
            if (ArmyConfigurationSourceAdapter.ReadCurrentLand(world, unit) is not byte formation||
                captureInputs(world, unit) is not { } inputs) continue;
            int mode=ArmyNativeFormationQueries.ModelMode(selection.Terrain,false,formation);
            if(mode is not(0 or 1 or 2))continue;
            byte naval=ArmyConfigurationSourceAdapter.ReadNaval(world, unit)??0;
            EnsureSourceModels();
            if(mode==2)EnsureIndependentSourceModel(formation-4);
            if(mode==1&&ArmyModelSelection.ChooseKnownDescriptor(1,null,naval).Category is int ship)
                EnsureIndependentSourceModel(ship);
            if (!_motions.TryGetValue(id, out var owned))
            {
                var position = HexPosition(inputs.CurrentHex2A);
                var heading = ArmyGraphicsMotionMath.Direction(inputs.Direction36);
                if (!_states.TryGetValue(id, out var state))
                {
                    var policy = new RemakeArmyCreationPolicy(new byte[19 * 64],
                        "remake-created ordinary visual; owned cleared record template")
                    {
                        OrdinaryModelsResolved = Enumerable.Range(0, 19).Select(i => _models.ContainsKey(i < 15 ? 0 : 1)).ToArray(),
                        BannerModelsResolved = [], FlagModelsResolved = [], ActionRoutesByCategory = _routes,
                        RandomSource = "explicit remake SHA256-counter results at native random query boundary",
                        GroundSource = "owned retained Field wrapper QueryA0B830",
                        AnimationRouteSource = "source ActionID0 mapped to its declared clip; native retained player",
                        ExplicitContextPredicate16EEC0 = false
                    };
                    _states.Add(id, state = new(_language, policy));
                }
                byte[] templates=new byte[19*64];
                int[] random=Enumerable.Range(0,19).Select(i=>RandomDraw(world,id,i)).ToArray();
                const string randomSource="explicit remake SHA256-counter results at native random query boundary";
                var records=ArmyInstancePose.InitializeRecords(position,heading,random,templates,
                    "remake-created retained land visual; owned cleared record template; "+randomSource,_language);
                var initial=new RemakeArmyInitialization(unit,1,new(position.X,position.Y,position.Z),heading,
                    random,templates,randomSource,records);
                owned = new(initial, new ArmyGraphicsMotion(initial.CopyRecords(), position, heading,
                    inputs.CurrentHex2A, inputs.Commander12, clock.UpdateInterval32C510, 48400), inputs)
                    {Revision=1,Mode=mode,SelectedFormation1B=formation,SelectedFormation1C=naval};
            }
            else
            {
                uint revision=owned.Revision+(owned.Mode!=mode?1u:0u);
                owned = owned with { Inputs = inputs,Mode=mode,SelectedFormation1B=formation,SelectedFormation1C=naval,Revision=revision };
            }
            _motions[id] = owned;
            frames.Add(id, PublishMotionFrame(unit, owned));
        }
        foreach (var id in _motions.Keys.Where(id => !frames.ContainsKey(id)).ToArray())
        { _motions.Remove(id); _states.Remove(id); _randomCounters.Remove(id); }
        Frames = frames;
    }

    /// <summary>Actual paced manager event, before native camera admission.
    /// A camera-only draw never invokes this motion owner.</summary>
    public bool AdvanceMotion(GameState world, ArmyGraphicsMotionFrame clock)
    {
        Dictionary<int, RemakeArmyVisualFrame>? frames = null;
        foreach (var (id, owned) in _motions)
        {
            int priorAction = owned.Motion.GraphicsAction60;
            var input = owned.Inputs;
            var step = owned.Motion.UpdateOrdinary(new(input.CurrentHex2A, input.Direction36,
                input.ArmyValid, input.Flags26, input.Abnormal23, input.Mobility2B0660,
                clock.Milliseconds936440, clock.UpdateInterval32C510), HexPosition, (x, z) => Ground(x, z));
            if(step.Changed)++owned.Revision;
            if (!step.Changed && priorAction == step.GraphicsAction60) continue;
            frames ??= new(Frames);
            frames[id] = PublishMotionFrame(world.Units[id], owned);
        }
        if (frames is null) return false;
        Frames = frames;
        return true;
    }

    private RemakeArmyVisualFrame PublishMotionFrame(UnitState unit, OwnedMotion owned)
    {
        var frame=owned.Mode==1?PublishNavalMotionFrame(unit,owned):owned.Mode==2?PublishSiegeMotionFrame(unit,owned):
            _states[unit.Id].PublishMotion(unit,owned.Initialization,owned.Motion.CaptureRecords(),
                owned.Motion.UnitPosition10,owned.Motion.GraphicsAction60,_constructors);
        return frame with{CurrentGroundHeight=Ground(owned.Motion.UnitPosition10.X,owned.Motion.UnitPosition10.Z),
            MotionRevision=owned.Revision,Mode=owned.Mode,SelectedFormation1B=owned.SelectedFormation1B,
            SelectedFormation1C=owned.SelectedFormation1C};
    }

    private Vector4 HexPosition(ushort hex)
    {
        if (_hexPositions.TryGetValue(hex, out var position)) return position;
        var p = _projection.Project(hex);
        float x = (float)p.X, z = (float)p.Z;
        // 188880→1889A0 retains the positive terrain height for each Hex.
        position = new(x, MathF.Max(445, Ground(x, z)), z, 1);
        _hexPositions[hex] = position;
        return position;
    }
}
