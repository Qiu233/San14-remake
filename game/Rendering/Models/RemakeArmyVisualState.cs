using System.Collections.ObjectModel;
using System.Numerics;
using System.Security.Cryptography;
using San14.Core;
using San14.Data;

/// <summary>Explicit inputs for the remake's fresh ordinary visual route.
/// A cleared template, false context, random generator, grounding and animation
/// choice are remake policies; none establishes a current original Army object.</summary>
public sealed record RemakeArmyCreationPolicy(byte[] CompleteRecordTemplates, string CallerSource)
{
    public required bool ExplicitContextPredicate16EEC0 { get; init; }
    public required IReadOnlyList<bool> OrdinaryModelsResolved { get; init; }
    public IReadOnlyList<bool> BannerModelsResolved { get; init; } = [];
    public required IReadOnlyList<bool> FlagModelsResolved { get; init; }
    public required IReadOnlyDictionary<int, ArmyAnimationSourceInput> ActionRoutesByCategory { get; init; }
    public required string RandomSource { get; init; }
    public required string GroundSource { get; init; }
    public required string AnimationRouteSource { get; init; }
}

public readonly record struct RemakeArmyComponentKey(int DescriptorOffset, int InstanceIndex)
{
    public string ComponentId => $"source-ordinary/{DescriptorOffset:X}/{InstanceIndex}";
}

/// <summary>One source setter result followed by an explicit remake grounding
/// publication. Actor scale is retained from its source WorldPQ constructor;
/// the proven flag writer supplies its own scale before position and rotation.</summary>
public sealed record RemakeArmyPosePublication(RemakeArmyComponentKey Key,
    int RecordIndex, int Category, ArmyInstancePoseWrite SourceWrite,
    UnitWorldTransformState WorldTransform, ArmyAnimationSourceInput AnimationInput,
    string GroundPublicationSource)
{
    public string ComponentId => Key.ComponentId;
    public bool InstanceStateFidelityVerified => false;
    public bool AnimationStateFidelityVerified => false;
}

/// <summary>Owned initialization retained until an explicit remake reset.
/// CopyRecords returns independent raw storage, so a renderer cannot mutate
/// subsequent source publications by changing a previously returned snapshot.</summary>
public sealed class RemakeArmyInitialization
{
    private readonly byte[] _records;
    public int UnitId { get; }
    public int HexId { get; }
    public int FormationSlot { get; }
    public int ResetGeneration { get; }
    public Vector3 Center { get; }
    public float Heading { get; }
    public IReadOnlyList<int> RandomResults { get; }
    public string RandomSource { get; }
    public string RecordSha256 { get; }
    public string TemplateSha256 { get; }
    public string CallerSource { get; }
    public ArmyInstancePoseCodeSource CodeSource { get; }
    public string ResetPolicy => "remake visual reset on discrete Hex/Formation/source change or explicit ResetUnit";
    public bool NativeRandomGeneratorFidelityVerified => false;
    public bool MotionTimingFidelityVerified => false;

    internal RemakeArmyInitialization(UnitState unit, int generation, Vector3 center,
        float heading, int[] random, byte[] template, string randomSource, ArmyInstancePoseSnapshot snapshot,
        int? currentFormationSlot = null)
    {
        UnitId = unit.Id; HexId = unit.HexId; FormationSlot = currentFormationSlot ?? unit.FormationId
            ?? throw new InvalidDataException("Army visual initialization requires its selected source formation.");
        ResetGeneration = generation; Center = center; Heading = heading;
        RandomResults = Array.AsReadOnly((int[])random.Clone()); RandomSource = randomSource;
        _records = snapshot.Records.SelectMany(record => record.RawBytes).ToArray();
        RecordSha256 = Convert.ToHexString(SHA256.HashData(_records));
        TemplateSha256 = Convert.ToHexString(SHA256.HashData(template));
        CallerSource = snapshot.CallerSource; CodeSource = snapshot.CodeSource;
    }

    public ArmyInstancePoseSnapshot CopyRecords() =>
        ArmyInstancePose.ReadRecords(_records, CallerSource, CodeSource.Language);
}

// Borrowed world objects end here. Motion only retains these value fields.
internal readonly record struct RemakeArmyVisualUnit(int UnitId,int HexId,int? FormationSlot,
    bool NavalUsesSelectedLandFormation)
{
    internal static RemakeArmyVisualUnit Capture(UnitState unit)
        =>new(unit.Id,unit.HexId,unit.FormationId,unit.NativeTacticRuntime?.Status10==2);
}

public sealed record RemakeArmyVisualFrame(int UnitId, int HexId, int FormationSlot,
    RemakeArmyInitialization Initialization, ArmyInstancePoseSnapshot Records,
    ArmyInstancePoseWrites OrdinaryWrites, ArmyInstanceFlagPoseWrites FlagWrites,
    IReadOnlyDictionary<RemakeArmyComponentKey, RemakeArmyPosePublication> Publications)
{
    public Vector3 CurrentUnitPosition { get; init; } = Initialization.Center;
    public int GraphicsAction60 { get; init; }
    public float CurrentUnitHeading { get; init; } = Initialization.Heading;
    public float CurrentGroundHeight { get; init; }
    public uint MotionRevision { get; init; }
    public int Mode { get; init; }
    public byte SelectedFormation1B { get; init; }
    public byte SelectedFormation1C { get; init; }
    public bool ExplicitRemakeContextPredicate16EEC0 => false;
    public bool RemakeCreatedVisualState => true;
    public bool CurrentNativeArmyBytesVerified => false;
    public bool InstanceStateFidelityVerified => false;
    public bool AnimationStateFidelityVerified => false;
    public bool CompleteAssemblyFidelityVerified => false;
}

/// <summary>CPU owner of remake-created, source-written ordinary visual records.
/// It performs no mesh loads, node allocation, native process access or timing
/// inference. Stable descriptor/instance keys let the renderer keep live nodes
/// while discrete playable movement creates a fresh explicit visual prefix.</summary>
public sealed class RemakeArmyVisualState
{
    private sealed record CachedInitialization(string SourceIdentity, RemakeArmyInitialization Value);
    private readonly string _language;
    private readonly byte[] _templates;
    private readonly string _callerSource, _randomSource, _groundSource, _animationRouteSource;
    private readonly bool[] _ordinaryResolved, _bannerResolved, _flagsResolved;
    private readonly IReadOnlyDictionary<int, ArmyAnimationSourceInput> _animations;
    private readonly Dictionary<int, CachedInitialization> _units = [];
    public int CachedUnitCount => _units.Count;

    internal RemakeArmyVisualState ForkLogicalState()=>new(this);
    private RemakeArmyVisualState(RemakeArmyVisualState source)
    {
        _language=source._language;_templates=source._templates;_callerSource=source._callerSource;
        _randomSource=source._randomSource;_groundSource=source._groundSource;_animationRouteSource=source._animationRouteSource;
        _ordinaryResolved=source._ordinaryResolved;_bannerResolved=source._bannerResolved;_flagsResolved=source._flagsResolved;
        _animations=source._animations;
        foreach(var pair in source._units)_units.Add(pair.Key,pair.Value);
    }

    public RemakeArmyVisualState(string language, RemakeArmyCreationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _ = ArmyInstancePoseCodeSources.ForLanguage(language);
        if (policy.ExplicitContextPredicate16EEC0 ||
            policy.CompleteRecordTemplates is not { Length: ArmyInstancePose.RecordCount * ArmyInstancePose.RecordBytes } ||
            policy.OrdinaryModelsResolved is not { Count: ArmyInstancePose.RecordCount } ||
            policy.BannerModelsResolved is null || policy.BannerModelsResolved.Any(value => value) ||
            policy.FlagModelsResolved is null || policy.FlagModelsResolved.Count > 3 ||
            policy.ActionRoutesByCategory is null ||
            !Label(policy.CallerSource) || !Label(policy.RandomSource) || !Label(policy.GroundSource) || !Label(policy.AnimationRouteSource))
            throw Error("requires a complete caller-owned record template, explicit false context, nineteen actor readiness results, known empty/unready banner pool, bounded flag pool and source labels");
        var animations = new Dictionary<int, ArmyAnimationSourceInput>();
        foreach ((int category, ArmyAnimationSourceInput input) in policy.ActionRoutesByCategory)
        {
            if (category is not (0 or 1 or 3) || input is null || input.Category != category || input.ActionIndex < 0 ||
                input.AnimationFileId == 0 || input.CurrentTimeSeconds != 0 || input.PreviousTimeSeconds is not null)
                throw Error("creation animation routes require explicit category 0/1/3 ActionID0 source files with nonnegative declared ordinals and a fresh caller clock0");
            animations.Add(category, input);
        }
        _language = language; _templates = (byte[])policy.CompleteRecordTemplates.Clone();
        _callerSource = policy.CallerSource; _randomSource = policy.RandomSource;
        _groundSource = policy.GroundSource; _animationRouteSource = policy.AnimationRouteSource;
        _ordinaryResolved = policy.OrdinaryModelsResolved.ToArray(); _bannerResolved = policy.BannerModelsResolved.ToArray();
        _flagsResolved = policy.FlagModelsResolved.ToArray();
        _animations = new ReadOnlyDictionary<int, ArmyAnimationSourceInput>(animations);
    }

    public RemakeArmyVisualFrame Update(UnitState unit, PlayableArmySelection sourceSelection,
        Vector3 center, float heading, Func<int, int> explicitNativeRandomDraw,
        Func<float, float, float> ground,
        IReadOnlyDictionary<int, UnitWorldTransformState> sourceActorConstructorTransforms)
    {
        ArgumentNullException.ThrowIfNull(unit); ArgumentNullException.ThrowIfNull(sourceSelection);
        ArgumentNullException.ThrowIfNull(explicitNativeRandomDraw); ArgumentNullException.ThrowIfNull(ground);
        ArgumentNullException.ThrowIfNull(sourceActorConstructorTransforms);
        ValidateSelection(unit, sourceSelection);
        if (!Finite(center) || !float.IsFinite(heading)) throw Error("requires finite explicit center and heading");
        int[] requiredCategories = Enumerable.Range(0, 19).Where(index => _ordinaryResolved[index])
            .Select(index => index < 15 ? 0 : 1).Concat(_flagsResolved.Any(value => value) ? new[] { 3 } : [])
            .Distinct().ToArray();
        foreach (int category in requiredCategories)
        {
            if (!_animations.ContainsKey(category) || !sourceActorConstructorTransforms.TryGetValue(category, out var transform))
                throw Error($"resolved category{category} requires its source WorldPQ constructor and explicit ActionID0 route");
            ValidateConstructor(category, transform);
        }
        var creation = sourceSelection.Creation ?? throw Error("ordinary selection has no creation receipt");
        string sourceIdentity = creation.FormationSource.ResourceUri + "|" +
            creation.FormationSource.Sha256 + "|" + creation.FormationDecodedSha256 + "|" +
            sourceSelection.Terrain.MapSource.ResourceUri + "|" + sourceSelection.Terrain.MapSource.Sha256;
        _units.TryGetValue(unit.Id, out CachedInitialization? prior);
        bool reset = prior is null || prior.Value.HexId != unit.HexId || prior.Value.FormationSlot != unit.FormationId ||
            prior.SourceIdentity != sourceIdentity;
        RemakeArmyInitialization initialization;
        if (reset)
        {
            int[] random = new int[19];
            for (int index = 0; index < random.Length; index++)
            {
                random[index] = explicitNativeRandomDraw(index);
                if (random[index] < 0) throw Error("caller random draws must be explicit nonnegative native-result inputs");
            }
            var initialized = ArmyInstancePose.InitializeRecords(new(center, 1), heading, random,
                _templates, _callerSource + "; remake-created ordinary prefix; random inputs: " + _randomSource, _language);
            initialization = new(unit, checked((prior?.Value.ResetGeneration ?? 0) + 1), center, heading,
                random, _templates, _randomSource, initialized);
        }
        else
        {
            initialization = prior!.Value;
            if (!SameBits(initialization.Center, center) ||
                BitConverter.SingleToUInt32Bits(initialization.Heading) != BitConverter.SingleToUInt32Bits(heading))
                throw Error("center/heading changed within the retained discrete visual prefix; call ResetUnit for an explicit remake reorientation");
        }
        ArmyInstancePoseSnapshot records = initialization.CopyRecords();
        var frame = PublishRecords(RemakeArmyVisualUnit.Capture(unit), initialization, records, center, 0,
            sourceActorConstructorTransforms, ground);
        // Publish the new initialization only after every readiness, source pose
        // and ground publication passed. A failed frame leaves the prior prefix.
        if (reset) _units[unit.Id] = new(sourceIdentity, initialization);
        return frame;
    }

    /// <summary>Publish current ordinary retained graphics-motion records.
    /// Motion has already grounded their current positions; the original
    /// ordinary/flag writers and WorldPQ setters consume that Y unchanged.</summary>
    public RemakeArmyVisualFrame PublishMotion(UnitState unit,
        RemakeArmyInitialization initialization, ArmyInstancePoseSnapshot records,
        Vector3 currentUnitPosition, int graphicsAction60,
        IReadOnlyDictionary<int, UnitWorldTransformState> constructors)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return PublishMotion(RemakeArmyVisualUnit.Capture(unit),initialization,records,currentUnitPosition,
            graphicsAction60,constructors);
    }

    internal RemakeArmyVisualFrame PublishMotion(RemakeArmyVisualUnit unit,
        RemakeArmyInitialization initialization,ArmyInstancePoseSnapshot records,
        Vector3 currentUnitPosition,int graphicsAction60,
        IReadOnlyDictionary<int,UnitWorldTransformState> constructors)
    {
        ArgumentNullException.ThrowIfNull(initialization); ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(constructors);
        if (!Finite(currentUnitPosition)) throw Error("requires a finite current motion position");
        return PublishRecords(unit, initialization, records, currentUnitPosition, graphicsAction60,
            constructors, null);
    }

    private RemakeArmyVisualFrame PublishRecords(RemakeArmyVisualUnit unit,
        RemakeArmyInitialization initialization, ArmyInstancePoseSnapshot records,
        Vector3 currentUnitPosition, int graphicsAction60,
        IReadOnlyDictionary<int, UnitWorldTransformState> constructors,
        Func<float, float, float>? ground)
    {
        ArmyInstancePoseWrites ordinary = ArmyInstancePose.WriteOrdinary(records, _ordinaryResolved);
        ArmyInstanceFlagPoseWrites flags = ArmyInstancePose.WriteFlags(records, 0, _bannerResolved, _flagsResolved);
        string groundSource = ground is null ? "ordinary retained graphics motion; current record Y already grounded" : _groundSource;
        var publications = new Dictionary<RemakeArmyComponentKey, RemakeArmyPosePublication>();
        foreach (ArmyInstancePoseWrite write in ordinary.Writes.Concat(flags.Writes))
        {
            int category = write.DescriptorOffset switch { 0x778 => 0, 0x750 => 1, 0x7C8 => 3,
                _ => throw Error("resolved banner source category is outside the proven ordinary descriptor selection") };
            float y = write.PositionArgument.Y;
            if (ground is not null)
            {
                float sampled = ground(write.PositionArgument.X, write.PositionArgument.Z);
                if (!float.IsFinite(sampled)) throw Error("explicit per-record grounding produced an unknown/nonfinite height");
                y = sampled + (write.RawScaleArgument.HasValue ? ArmyInstancePoseCodeSources.FlagHeightOffset : 0);
                if (!float.IsFinite(y)) throw Error("explicit grounded publication produced a nonfinite height");
            }
            UnitWorldTransformState world = constructors[category];
            if (write.ScaleArgument is { } scale) world = world.PublishScale(scale);
            world = world.PublishPosition(new(write.PositionArgument.X, y, write.PositionArgument.Z))
                .PublishQuaternion(new(write.RotationArgument.X, write.RotationArgument.Y, write.RotationArgument.Z, write.RotationArgument.W));
            world = world with { Basis = world.Basis + "; source Army mode0 setter publication; " +
                groundSource + (ground is null ? $"; raw GraphicsAction60={graphicsAction60}" :
                    "; explicit remake ActionID0 route: " + _animationRouteSource) };
            var key = new RemakeArmyComponentKey(write.DescriptorOffset, write.DescriptorInstanceIndex);
            publications.Add(key, new(key, write.RecordIndex, category, write, world, _animations[category], groundSource));
        }
        var frame = new RemakeArmyVisualFrame(unit.UnitId, unit.HexId, unit.FormationSlot!.Value, initialization,
            records, ordinary, flags, new ReadOnlyDictionary<RemakeArmyComponentKey, RemakeArmyPosePublication>(publications))
            { CurrentUnitPosition = currentUnitPosition, GraphicsAction60 = graphicsAction60 };
        return frame;
    }

    public bool ResetUnit(int unitId) => _units.Remove(unitId);
    public void RetireAbsentUnits(IReadOnlyCollection<int> presentUnitIds)
    {
        ArgumentNullException.ThrowIfNull(presentUnitIds);
        var present = presentUnitIds.ToHashSet();
        foreach (int id in _units.Keys.Where(id => !present.Contains(id)).ToArray()) _units.Remove(id);
    }
    public void Reset() => _units.Clear();

    private void ValidateSelection(UnitState unit, PlayableArmySelection selection)
    {
        if (unit.Id < 0 || (uint)unit.HexId >= 48400 || unit.FormationId is not int slot || (uint)slot > 20 ||
            unit.Mission?.Kind == UnitMissionKind.Transport ||
            slot is 19 or 20 || slot is >= 8 and <= 10 ||
            selection.UnitId != unit.Id || selection.HexId != unit.HexId || selection.ExplicitLandFormationSlot != slot ||
            selection.Creation is null || !selection.Creation.LandWritten || selection.Creation.NavalWritten ||
            selection.Creation.Bytes.Byte1B != (byte)slot ||
            selection.Creation.CodeSource != ArmyFormationWriterCodeSources.ForLanguage(_language) ||
            selection.Creation.FormationSource.ResourceUri != GameResourceId.File($"0010_{_language}/fixdataexce.s14").ToString() ||
            selection.Terrain.UsesArmyByte1C || selection.Formation.DefinitionSlot != slot ||
            selection.Formation.RawSelectedByte != slot || selection.Formation.NativeField != "Army.byte1B" ||
            selection.Formation.Definition.RawParameterBytes[0] == 2 ||
            ArmyNativeFormationQueries.ModelMode(selection.Terrain, false, selection.Creation.Bytes.Byte1B) != 0)
            throw Error("requires a matching source-created ordinary land choice under the explicit remake false-context route; siege/naval/transport are unsupported");
        // Land terrain consumes byte1B. Retain the independently owned naval
        // byte; its presence does not change this ordinary land model route.
        var expected = ArmyModelSelection.DescribeContextIndependent(selection.Terrain,
            selection.Creation.Bytes.Byte1B, selection.Creation.Bytes.Byte1C);
        if (!selection.Descriptors.PossibleModes.SequenceEqual(expected.PossibleModes) ||
            !selection.Descriptors.Descriptors.SequenceEqual(expected.Descriptors))
            throw Error("source descriptor requests differ from the proven ordinary context-independent selection");
    }

    private static void ValidateConstructor(int category, UnitWorldTransformState state)
    {
        string definition = CeArmyModels.KnownSourceCategories[category].DefinitionId.ToString("X8");
        if (state is null || !Finite(state.Position18) || !Finite(state.Scale34) ||
            state.Scale34.X <= 0 || state.Scale34.Y <= 0 || state.Scale34.Z <= 0 ||
            !float.IsFinite(state.Quaternion24.X) || !float.IsFinite(state.Quaternion24.Y) ||
            !float.IsFinite(state.Quaternion24.Z) || !float.IsFinite(state.Quaternion24.W) ||
            state.EvidenceUri != GameResourceId.Rdb("KIDSSystemResource", CeArmyModels.CatalogId).ToString() ||
            state.EvidenceSha256 is not { Length: 64 } sha || !sha.All(Uri.IsHexDigit) ||
            !state.Basis.Contains(UnitWorldTransform.NativeContractId, StringComparison.Ordinal) ||
            !state.Basis.Contains("D9F540 definition " + definition, StringComparison.Ordinal))
            throw Error($"category{category} requires its actual finite source WorldPQ constructor provenance and scale");
    }

    private static bool Label(string? value) => !string.IsNullOrWhiteSpace(value);
    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static bool SameBits(Vector3 a, Vector3 b) => BitConverter.SingleToUInt32Bits(a.X) == BitConverter.SingleToUInt32Bits(b.X) &&
        BitConverter.SingleToUInt32Bits(a.Y) == BitConverter.SingleToUInt32Bits(b.Y) && BitConverter.SingleToUInt32Bits(a.Z) == BitConverter.SingleToUInt32Bits(b.Z);
    private static InvalidDataException Error(string message) => new($"Remake-created source Army visual state {message}.");
}
