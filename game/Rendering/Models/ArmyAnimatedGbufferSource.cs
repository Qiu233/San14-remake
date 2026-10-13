using System.Buffers.Binary;
using System.Numerics;
using San14.Data;
using San14.Data.Formats;

/// <summary>Action, clock and three original bSkin decisions belong to the
/// caller. RepeatDeclaredDuration is an explicit remake playback policy.</summary>
public enum ArmyAnimationClockPolicy { CallerSeconds, RepeatDeclaredDuration }
public sealed record ArmyAnimatedSourceGates(bool ActionSamplingEnabled,
    IReadOnlyList<bool> SkinFlags, ArmyAnimationClockPolicy ClockPolicy);

public sealed record ArmyAnimatedGbufferInputs(ArmyAnimationSourceInput Animation,
    UnitWorldTransformState CurrentWorld, DeferredCameraInputs Camera,
    ArmyAnimatedSourceGates Gates, ReadOnlyMemory<byte> CompleteVertexTemplate,
    ReadOnlyMemory<byte> PreviousPixelTemplate, uint PreviousCachedMaterialFlags,
    bool BackendAlphaEnabled, byte? ForcedAlphaReference, ModelRasterInputs Raster)
{
    public UnitWorldTransformState? PreviousWorld { get; init; }
    public ReadOnlyMemory<byte>? CompletePreviousVertexTemplate { get; init; }
}

public sealed record ArmyAnimatedGbufferPreparedDraw(ModelGbufferPreparedDraw Draw,
    ArmyAnimationResolvedFrame Animation, ArmyNativePaletteRows CurrentPalette,
    ArmyNativePaletteRows? PreviousPalette, ArmyAnimatedSourceGates Gates)
{
    public ModelGbufferInstancePacket Packet => Draw.Packet;
    public bool AnimationStateFidelityVerified => false;
}

/// <summary>Per source model/draw preparation owner. Resolves actual source
/// local tracks, full hierarchy and native palette into the selected source
/// G-buffer VS. The installed Army dependency is B90AEDD3 combination1:
/// VS1 CB0=6256, PS2 CB0=80. Forward D869FD7A and extra CBs do not participate.
/// Recreate this owner after a source/override revision changes.</summary>
public sealed class ArmyAnimatedGbufferSource
{
    public const uint GbufferShaderFileId = 0xB90AEDD3;
    public LoadedArmyModel Model { get; }
    public ModelGbufferSourceDraw SourceDraw { get; }
    public int SourcePaletteIndex { get; }
    public ArmySkinVertexProfile? VertexProfile { get; }
    private readonly ArmyAnimationRuntime _runtime;
    private readonly byte[] _defaultLocals;

    private ArmyAnimatedGbufferSource(GameResourceRepository repository,
        LoadedArmyModel model, ModelGbufferSourceDraw draw, int palette)
    {
        (Model, SourceDraw, SourcePaletteIndex) = (model, draw, palette);
        VertexProfile=ArmySkinVertexAdapter.Select(model.Transforms.Geometry,draw.DrawIndex);
        _runtime = new(repository, model.Catalog);
        _defaultLocals = model.Transforms.Transforms.Joints.SelectMany(j => j.RawBytes).ToArray();
    }

    public static ArmyAnimatedGbufferSource Load(GameResourceRepository repository,
        LoadedArmyModel model, LoadedObjects shaderObjects, LoadedRenderGraph graph,
        int graphNode = 49, int drawIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(model);
        string modelUri = GameResourceId.Rdb("CharacterEditor", model.Category.ModelFileId).ToString();
        if (model.Transforms.Source.ResourceUri != modelUri || model.Resources.Model.Source.ResourceUri != modelUri)
            throw new InvalidDataException("Animated Army category and imported model resources differ.");
        var nativeDraw = G1ModelDrawReader.Read(model.Transforms.Geometry).Draws.Single(d => d.Index == drawIndex);
        int palette = checked((int)nativeDraw.UnknownValue8);
        if ((uint)palette >= model.Transforms.Transforms.JointPalettes.Count)
            throw new InvalidDataException("Animated Army draw has no declared original joint palette.");
        var draw = ModelGbufferSourceDraw.LoadSourceTextureViews(repository, model.Resources,
            shaderObjects, graph, graphNode, drawIndex, 1, new(false, false, 0));
        if (draw.Shader.Source.ResourceUri != GameResourceId.Rdb("system", GbufferShaderFileId).ToString())
            throw new InvalidDataException("Animated Army G-buffer provider differs from the supported source dependency.");
        var stages = draw.Shader.ReadG1Bindings().Combinations.Rows[draw.ExplicitCombinationIndex].Stages;
        if (stages.Single(s => s.StageCode == 1).Entry?.ProgramIndex != 1 ||
            stages.Single(s => s.StageCode == 0).Entry?.ProgramIndex != 2)
            throw new InvalidDataException("Animated Army G-buffer combination differs from source VS1/PS2.");
        ArmySkinConstants.Validate(draw.VertexDefinitions);
        return new(repository, model, draw, palette);
    }

    public ArmyAnimatedGbufferPreparedDraw Prepare(string instanceKey, ArmyAnimatedGbufferInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs); ArgumentNullException.ThrowIfNull(inputs.Gates);
        if (inputs.Animation.Category != Model.Category.Category || inputs.Gates.SkinFlags is not { Count: 3 } ||
            !Enum.IsDefined(inputs.Gates.ClockPolicy))
            throw new InvalidDataException("Animated Army packet requires matching category and three explicit source skin gates.");
        bool hasPrevious = inputs.Animation.PreviousTimeSeconds is not null;
        if (hasPrevious != (inputs.PreviousWorld is not null) || hasPrevious != inputs.CompletePreviousVertexTemplate.HasValue)
            throw new InvalidDataException("Animated Army previous pose requires its own explicit clock, world and complete VS template.");
        if (inputs.CompleteVertexTemplate.Length != 6256 ||
            inputs.CompletePreviousVertexTemplate is { Length: not 6256 })
            throw new InvalidDataException("Animated Army requires complete source 6256-byte vertex templates.");
        var selected = _runtime.Select(inputs.Animation);
        var clock = inputs.Animation;
        if (inputs.Gates.ClockPolicy == ArmyAnimationClockPolicy.RepeatDeclaredDuration)
        {
            float duration = selected.Animation.Clip.FrameRate > 0 ? selected.Animation.Clip.FrameCount / selected.Animation.Clip.FrameRate : 0;
            if (!(duration > 0) || !float.IsFinite(duration))
                throw new InvalidDataException("Animated Army explicit repeat policy requires a positive finite declared clip duration.");
            float Repeat(float time) { float value = time % duration; return value < 0 ? value + duration : value; }
            clock = clock with { CurrentTimeSeconds = Repeat(clock.CurrentTimeSeconds),
                PreviousTimeSeconds = clock.PreviousTimeSeconds is { } previous ? Repeat(previous) : null };
        }
        var resolved = _runtime.SampleResolvedPose(selected with { Input = clock }, Model.Transforms, _defaultLocals,
            Root(inputs.CurrentWorld), hasPrevious ? _defaultLocals : null,
            inputs.PreviousWorld is { } priorWorld ? Root(priorWorld) : null,
            actionSamplingEnabled: inputs.Gates.ActionSamplingEnabled);
        var current = ArmyNativePaletteWriter.Build(Model.Transforms.Transforms, SourcePaletteIndex,
            resolved.Current.BuildMatrices(), Model.Transforms.Transforms.Joints.Count,
            ArmySkinConstants.ReadPalette(inputs.CompleteVertexTemplate.Span), ArmyNativePoseBufferKind.Current);
        var prior = resolved.Previous is { } previousPose ? ArmyNativePaletteWriter.Build(Model.Transforms.Transforms,
            SourcePaletteIndex, previousPose.BuildMatrices(), Model.Transforms.Transforms.Joints.Count,
            ArmySkinConstants.ReadPalette(inputs.CompletePreviousVertexTemplate!.Value.Span), ArmyNativePoseBufferKind.Previous) : null;
        byte[] vertex = ArmySkinConstants.Write(SourceDraw.VertexDefinitions, inputs.CompleteVertexTemplate.Span,
            MatrixRows(inputs.Camera.SourceWorldToProjection), current.Rows, inputs.Gates.SkinFlags);
        if(VertexProfile is { } profile)ArmySkinVertexAdapter.ValidateGates(vertex,profile);
        var prepared = SourceDraw.Prepare(instanceKey, new Dictionary<uint, ReadOnlyMemory<byte>> { [0] = vertex },
            inputs.PreviousPixelTemplate.Span, MapStaticModelTransforms.ToShaderAffineRows(inputs.Camera.SourceWorldToView),
            inputs.PreviousCachedMaterialFlags, inputs.BackendAlphaEnabled, inputs.ForcedAlphaReference, inputs.Raster);
        prepared=prepared with{Packet=prepared.Packet with{ArmySkinProfile=VertexProfile}};
        return new(prepared, resolved, current, prior, inputs.Gates with { SkinFlags = inputs.Gates.SkinFlags.ToArray() });
    }

    private static ArmyAnimationRootTransform Root(UnitWorldTransformState world)
    {
        return new(new(world.Scale34, 1), world.Quaternion24, new(world.Position18, 1));
    }
    private static float[] MatrixRows(Matrix4x4 m) => [m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,
        m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
}

/// <summary>Exact selected Army G-buffer skin profile. Padding and unused
/// rows retain the complete caller template; bSkin has native stride16.</summary>
public static class ArmySkinConstants
{
    public static void Validate(DxbcResourceDefinitions definitions)
    {
        if (definitions.Buffers.Count != 1 || definitions.Buffers[0].Name.Text != "$Globals" ||
            definitions.Buffers[0].ByteSize != 6256 || definitions.Bindings.Count != 1 ||
            definitions.Bindings[0] is not { InputType: 0, BindPoint: 0, BindCount: 1 } ||
            !definitions.Buffers[0].Variables.Select(v => v.Name.Text).Order().SequenceEqual(new[] { "bSkin", "mL2W", "mW2P" }))
            throw new InvalidDataException("Army G-buffer skin constants differ from the exact VS6256 source profile.");
        Field("mW2P", 0, 64, 2, 3, 4, 4, 0);
        Field("mL2W", 64, 6144, 1, 3, 1, 4, 384);
        Field("bSkin", 6208, 36, 0, 1, 1, 1, 3);
        void Field(string name, uint offset, uint size, ushort cls, ushort type, ushort rows, ushort columns, ushort elements)
        {
            var field = definitions.Buffers[0].Variables.Single(v => v.Name.Text == name);
            var shape = definitions.Types.Single(t => t.Offset == field.TypeOffset);
            if (field.StartOffset != offset || field.ByteSize != size || shape.Class != cls || shape.BaseType != type ||
                shape.Rows != rows || shape.Columns != columns || shape.Elements != elements || shape.Members.Count != 0)
                throw new InvalidDataException("Army G-buffer named skin field differs from its exact source RDEF shape.");
        }
    }
    public static IReadOnlyList<Vector4> ReadPalette(ReadOnlySpan<byte> completeTemplate)
    {
        if (completeTemplate.Length != 6256) throw new InvalidDataException("Army palette requires the complete VS6256 template.");
        var rows = new Vector4[384];
        for (int i = 0; i < rows.Length; i++) { int at = 64 + i * 16; rows[i] = new(F(completeTemplate, at),
            F(completeTemplate, at+4), F(completeTemplate, at+8), F(completeTemplate, at+12)); }
        return rows;
    }
    public static byte[] Write(DxbcResourceDefinitions definitions, ReadOnlySpan<byte> completeTemplate,
        float[] worldToProjectionRows, IReadOnlyList<Vector4> paletteRows, IReadOnlyList<bool> flags)
    {
        Validate(definitions);
        if (completeTemplate.Length != 6256 || worldToProjectionRows.Length != 16 || paletteRows.Count != 384 || flags.Count != 3)
            throw new InvalidDataException("Army skin constants require complete explicit matrices, rows384 and flags3.");
        byte[] result = completeTemplate.ToArray();
        for (int i = 0; i < 16; i++) WriteFloat(i * 4, worldToProjectionRows[i]);
        for (int i = 0; i < 384; i++) { var r = paletteRows[i]; WriteFloat(64+i*16,r.X);WriteFloat(68+i*16,r.Y);
            WriteFloat(72+i*16,r.Z);WriteFloat(76+i*16,r.W); }
        for (int i = 0; i < 3; i++) BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(6208+i*16,4), flags[i] ? 1 : 0);
        return result;
        void WriteFloat(int offset, float value) { if (!float.IsFinite(value)) throw new InvalidDataException("Army skin input has nonfinite float rows.");
            BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(offset,4),value); }
    }
    private static float F(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(offset,4));
}
