using Godot;
using San14.Core;
using San14.Data;
using NVector3 = System.Numerics.Vector3;

public sealed partial class RemakeArmySceneVisuals
{
    //Static/RGB and native-world preparations have no paced motion clock. Read
    //the owned current position/direction instead of inventing a timing tick.
    private RemakeArmyVisualFrame? PrepareCurrentTransportWater(GameState world,
        UnitState unit, PlayableArmySelection selection)
    {
        if (selection.Transport is not { } source || !selection.Terrain.UsesArmyByte1C ||
            source.Bytes.Byte1C is not byte naval || source.Bytes.Byte1B is not byte land ||
            unit.NativeDirection36 is not byte direction ||
            ArmyMarchSourceAdapter.ReadCreatedCurrentHex(world, unit) is not ushort hex || hex != unit.HexId)
            return null;
        _catalog ??= CeArmyModels.LoadCatalog(_repository);
        if (ArmyModelSelection.ChooseKnownDescriptor(1, null, naval).Category is int category)
            EnsureIndependentSourceModel(category);
        var position = HexPosition(hex);
        float heading = ArmyGraphicsMotionMath.Direction(direction);
        Frames.TryGetValue(unit.Id, out var prior);
        bool retained = prior is { Mode: 1 } && prior.HexId == unit.HexId &&
            prior.SelectedFormation1B == land && prior.SelectedFormation1C == naval &&
            prior.Initialization.Heading == heading;
        RemakeArmyInitialization initialization;
        ArmyInstancePoseSnapshot records;
        if (retained)
        {
            initialization = prior!.Initialization;
            records = prior.Records;
        }
        else
        {
            byte[] templates = new byte[ArmyInstancePose.RecordCount * ArmyInstancePose.RecordBytes];
            int[] random = Enumerable.Range(0, ArmyInstancePose.RecordCount)
                .Select(index => RandomDraw(world, unit.Id, index)).ToArray();
            const string randomSource = "explicit remake SHA256-counter results at native random query boundary";
            records = ArmyInstancePose.InitializeRecords(position, heading, random, templates,
                "owned current transport visual; cleared retained record template; " + randomSource, _language);
            initialization = new(unit, checked((prior?.Initialization.ResetGeneration ?? 0) + 1),
                new NVector3(position.X, position.Y, position.Z), heading,
                random, templates, randomSource, records, selection.ExplicitLandFormationSlot);
        }
        return PublishNavalCurrentFrame(unit, naval,
            new NVector3(position.X, position.Y, position.Z), heading, initialization, records, 0,
            selection.ExplicitLandFormationSlot) with
        {
            Mode = 1, SelectedFormation1B = land, SelectedFormation1C = naval,
            CurrentGroundHeight = Ground(position.X, position.Z),
            MotionRevision = retained ? prior!.MotionRevision : checked((prior?.MotionRevision ?? 0) + 1)
        };
    }

    private IReadOnlyList<ArmyComponentVisualBinding> CurrentTransportBindings(RemakeArmyVisualFrame frame)
    {
        var point = _projection.Project(frame.HexId);
        float x = (float)point.X, z = (float)point.Z;
        var anchor = new Vector3(x, (float)_height.HeightField.InterpolateSurfaceHeight(x, z), z);
        return frame.Publications.Values.Select(actor =>
        {
            var transform = actor.WorldTransform;
            return new ArmyComponentVisualBinding(actor.ComponentId,
                new(actor.Category,
                    new Quaternion(transform.Quaternion24.X, transform.Quaternion24.Y, transform.Quaternion24.Z, transform.Quaternion24.W),
                    new Vector3(transform.Scale34.X, transform.Scale34.Y, transform.Scale34.Z),
                    "source mode1 current transport P/Q and source model constructor scale"),
                new Vector3(transform.Position18.X, transform.Position18.Y, transform.Position18.Z) - anchor)
            { SourceDescriptorOffset = actor.SourceWrite.DescriptorOffset,
                SourceInstanceIndex = actor.SourceWrite.DescriptorInstanceIndex };
        }).ToArray();
    }
}
