using System.Text;
using System.Buffers.Binary;
using System.Globalization;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A retained declaration in an actual loaded database. A descriptor
/// exists before its typed Data or device resources have been constructed.</summary>
public sealed class RenderResourceDescriptor
{
    internal RenderResourceDescriptor(LoadedObjects source, DokObject declaration, ulong scope)
    {
        (Source, Object, Scope) = (source, declaration, scope);
        if (declaration.Kind == DokObjectKind.Inline && declaration.ClassId == RenderBlendStateDeclaration.ClassId)
            BlendState = RenderBlendStateDeclaration.Read(declaration);
        if (declaration.Kind == DokObjectKind.Inline && declaration.ClassId == ReflectionCaptureFieldSource.ClassId)
            ReflectionCaptureField = ReflectionCaptureFieldSource.Read(source, declaration.ObjectId);
    }
    public LoadedObjects Source { get; }
    public DokObject Object { get; }
    public ulong Scope { get; }
    public RenderBlendStateDeclaration? BlendState { get; }
    public ReflectionCaptureFieldDefinition? ReflectionCaptureField { get; }
}

/// <summary>The namespace observed by a resource initialization phase. Later
/// registrations do not change the descriptors retained by this snapshot.</summary>
public sealed class RenderResourceSnapshot
{
    private readonly Dictionary<uint, RenderResourceDescriptor> names;
    internal RenderResourceSnapshot(ulong generation, IEnumerable<KeyValuePair<uint, RenderResourceDescriptor>> entries)
        => (Generation, names) = (generation, entries.ToDictionary(pair => pair.Key, pair => pair.Value));
    public ulong Generation { get; }
    public RenderResourceDescriptor? Lookup(uint name) => names.GetValueOrDefault(name);
    internal IReadOnlyDictionary<uint, RenderResourceDescriptor> Entries => names;
}

/// <summary>Owns ordered database registration scopes for one renderer.
/// Removing a scope restores earlier names; consumers may keep their retained
/// descriptor and Data references independently of the current name lookup.</summary>
public sealed class RenderResourceContext : IDisposable
{
    public sealed class Batch : IDisposable
    {
        private readonly RenderResourceContext owner;
        internal readonly List<RenderResourceDescriptor> Descriptors = [];
        internal Batch(RenderResourceContext owner) => this.owner = owner;
        public void Dispose() => owner.Remove(this);
    }

    private const uint MainStageKey = 0x8572F577;

    private static readonly string[] Archives =
        ["system", "KIDSSystemResource", "MaterialEditor", "FieldEditor4", "CharacterEditor", "ScreenLayout", "RRPreview"];
    // These are the normal startup database identifiers, in submitted order.
    // They select source resources; they are not shader or readiness results.
    private static readonly string[][] StartupGroups =
    [
        [
            "ProjectCommonResource.motor.kidsscndb",
            "EnvironmentCommonResource.motor.kidssingletondb",
            "FE4CommonResource.motor.kidssingletondb",
            "EM3CommonResource.motor.kidssingletondb",
            "SLECommonResource.motor.kidssingletondb",
            "FieldEditor4ScriptProject",
            "ScreenLayoutEditorScriptProject",
            "EventMaker3ScriptProject",
            "EventMaker3TimelineProject",
            "SoundEditor.Common.kidssingletondb",
            "Motor_CustomResource.kidssingletondb"
        ],
        [
            "TechPreviewShaderProject",
            "TechPreviewScriptProject",
            "EnvironmentEditorScriptProject",
            "CharacterEditorScriptProject",
            "CharacterEditorBehaviorScriptProject",
            "CharacterEditorRealtimeRigScriptProject",
            "AutomatonEditorDocumentProject",
            "TechPreview.kidssingletondb",
            "CharacterEditor.shader.kidssingletondb",
            "CharacterEditor.kidssingletondb",
            "CharacterEditor.kidsscndb",
            "Field_Common.kidssingletondb",
            "Field_Common.sound.kidssingletondb",
            "Field_Common.effect.kidssingletondb",
            "Field_Common.character.level.kidssingletondb",
            "Field_Common.character.level.kidsscndb",
            "LuminousEditor.kidssingletondb",
            "Field_Common.lamp.ies.kidssingletondb",
            "EnvironmentEditor.kidssingletondb",
            "MaterialEditor.kidssingletondb",
            "TechPreviewCommon.kidsscndb",
            "TechPreviewPB2ClusteredShading.kidsscndb"
        ]
    ];

    private readonly GameResourceRepository repository;
    private readonly Dictionary<uint, LoadedObjects> databases = [];
    private readonly Dictionary<uint, List<RenderResourceDescriptor>> names = [];
    private readonly List<Batch> batches = [];
    private readonly string instanceDirectory = Guid.NewGuid().ToString("N");
    private readonly HashSet<(string Directory, ulong Revision)> written = [];
    private ulong nextScope;
    private bool disposed;
    public ulong Revision { get; private set; }
    public RenderResourceSnapshot? MaterialInitialization { get; private set; }

    public RenderResourceContext(GameResourceRepository repository)
    { ArgumentNullException.ThrowIfNull(repository); this.repository = repository; }

    /// <summary>Registers the actual global startup databases. This is the
    /// declaration phase; factories and successful typed preparation remain
    /// the responsibility of their actual resource consumers.</summary>
    public static RenderResourceContext LoadStartup(GameResourceRepository repository)
    {
        var context = new RenderResourceContext(repository);
        try
        {
            foreach (var group in StartupGroups)
                context.RegisterBatch(group.Select(name => context.LoadDatabase(ResourceNameHash.Compute(name))));
            return context;
        }
        catch { context.Dispose(); throw; }
    }

    /// <summary>Reads the supported normal Main Stage's database from the
    /// actual Common declaration. Map projection configuration is not a scene
    /// selector. This runs during the application's static resource load.</summary>
    public static uint LoadMainStageDatabaseId(GameResourceRepository repository)
    {
        using var context = LoadStartup(repository);
        var common = context.Lookup(MapSeasonResourceConsumer.CommonSettingObjectId)?.Object ??
            throw new InvalidDataException("Render startup has no Common settings.");
        var (keys, databases, _) = ReadStageDeclarations(common);
        int main = Array.IndexOf(keys, MainStageKey);
        if (main < 0 || databases[main] == 0)
            throw new NotSupportedException("Common does not declare the supported normal Main Stage database.");
        return databases[main];
    }

    private static (uint[] Keys, uint[] Databases, uint[] Objects) ReadStageDeclarations(DokObject common)
    {
        uint[] keys = Words(common, 0x04781136), databases = Words(common, 0x43EE345E);
        if (databases.Length == 0) databases = Words(common, 0xAF2F9C4C);
        uint[] objects = Words(common, 0xB0EB31B1);
        if (keys.Length != databases.Length || keys.Length != objects.Length)
            throw new InvalidDataException("Common Stage keys, databases and scene names have different lengths.");
        return (keys, databases, objects);
    }

    /// <summary>Assembles the normal map's declared load roots. Incidental
    /// model, font or shader reads do not register a database into this owner.</summary>
    public static RenderResourceContext LoadCurrentMap(GameResourceRepository repository,
        LoadedSceneResources scene, MapSeasonActionDatabase season, out Batch seasonBatch)
    {
        var context = LoadStartup(repository);
        try
        {
            var common = context.Lookup(MapSeasonResourceConsumer.CommonSettingObjectId)?.Object ??
                throw new InvalidDataException("Render startup has no Common settings.");
            var (keys, stageRoots, stageObjects) = ReadStageDeclarations(common);
            if (Words(common, 0xC92CC970).Length != 0 ||
                common.Properties.Any(p => p.NameHash == 0x5DEB6182 && p.ElementCount != 0))
                throw new NotSupportedException("Nonempty Event databases require their normal selection producer.");
            // Main's startup selects these Stage names in this order. Database
            // IDs remain values from Common, rather than array ordinals.
            foreach (uint stageKey in new uint[] { 0xAEF6D3FA, MainStageKey })
            {
                int index = Array.IndexOf(keys, stageKey);
                if (index >= 0 && stageRoots[index] != 0)
                    context.RegisterBatch([context.LoadDatabase(stageRoots[index])]);
            }
            int mainStage = Array.IndexOf(keys, MainStageKey);
            if (mainStage < 0 || scene.Scene.Source.FileId != stageRoots[mainStage] ||
                scene.RootObject.ObjectId != stageObjects[mainStage])
                throw new NotSupportedException("The loaded map scene does not match the current Common Main Stage selection.");
            var mainSource = context.LoadDatabase(stageRoots[mainStage]).Source;
            if (scene.Scene.Source.ResourceUri != mainSource.ResourceUri ||
                scene.Scene.Source.DecodedSha256 != mainSource.DecodedSha256)
                throw new NotSupportedException("The loaded map scene belongs to another selected resource generation.");
            foreach (var dependency in scene.Dependencies)
                context.RegisterBatch([dependency.Resource]);
            uint[] defaults = Words(scene.RootObject, 0xD3C3B0D0);
            if (defaults.Length == 0)
                throw new NotSupportedException("The initial map scene requires its declared default Environment.");
            var environment = context.RegisterEnvironment(defaults[0]);
            context.RequireSupportedStartupWriters(repository);
            // The normal map-load state explicitly loads this resource key
            // after its startup writer and before publishing completed loading.
            context.RegisterBatch([context.LoadDatabase(0x58E0814A)]);
            // Game-start initializes all material manager rows here. Their
            // Data keeps this namespace, independently of later Environment loads.
            context.MaterialInitialization = context.CaptureSnapshot();
            environment.Dispose();
            environment = context.RegisterEnvironment(ResourceNameHash.Compute("MAP_HEX_03_05_OP"));
            context.RequireSupportedStartupWriters(repository);
            environment.Dispose();
            seasonBatch = context.RegisterSeason(season);
            context.RequireSupportedStartupWriters(repository);
            return context;
        }
        catch { context.Dispose(); throw; }
    }

    private static uint[] Words(DokObject source, uint name)
    {
        var field = source.Properties.Single(p => p.NameHash == name);
        if (field.TypeCode != 5 || field.RawValue.Length != checked(field.ElementCount * 4))
            throw new InvalidDataException($"Resource field {name:X8} is not a declared key array.");
        return Enumerable.Range(0, field.ElementCount).Select(i =>
            BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue.AsSpan(i * 4))).ToArray();
    }

    private void RequireSupportedStartupWriters(GameResourceRepository source)
    {
        foreach (uint name in new uint[] { 0x93A75767, 0x6AB10A8E, 0x2781C3E5 })
        {
            var descriptor = Lookup(name) ?? throw new InvalidDataException("Render startup Folder is missing.");
            var folder = descriptor.Object;
            if (folder.Kind != DokObjectKind.Inline || folder.ClassId != 0x63026395)
                throw new NotSupportedException("Render startup Folder requires its actual creator.");
            uint[] expected = name == 0x93A75767 ? [0x4E508572] : [];
            if (!Words(folder, 0xE21FF09F).SequenceEqual(expected))
                throw new NotSupportedException("Changed startup scripts require their actual supported registration writers.");
        }
        // The existing finite World-light producer parses the actual program.
        // It publishes list members; it does not install material aliases.
        var folderSource = Lookup(EnvironmentWorldLightPublicationSource.FolderObjectId)!.Source;
        var code = Lookup(EnvironmentWorldLightPublicationSource.CodeResourceId) ??
            throw new InvalidDataException("Render startup Code is missing.");
        if (code.Object.Kind != DokObjectKind.Reference || code.Object.Reference is not { } reference)
            throw new NotSupportedException("Render startup Code requires its supported reference resolver.");
        if (reference.ResourceId != EnvironmentWorldLightPublicationSource.CodeResourceId ||
            reference.ObjectId != EnvironmentWorldLightPublicationSource.CodeResourceId)
            throw new NotSupportedException("Changed startup Code targets require their actual supported publication producer.");
        var codeSource = LoadDatabase(reference.ResourceId);
        _ = EnvironmentWorldLightPublicationSource.Read(folderSource, code.Source, codeSource,
            source.LoadTaskGraph("system", EnvironmentWorldLightPublicationSource.TaskGraphResourceId));
    }

    public LoadedObjects LoadDatabase(uint key)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!databases.TryGetValue(key, out var root))
            databases.Add(key, root = repository.LoadObjects(key, Archives));
        return root;
    }

    public Batch RegisterSeason(MapSeasonActionDatabase season)
    {
        ArgumentNullException.ThrowIfNull(season);
        if (EnvironmentDatabaseKey(season.Action.Selection.ActionNameHash) != season.Objects.Source.FileId)
            throw new NotSupportedException("The prepared season does not match the current Common Environment selection.");
        return RegisterBatch([season.Objects]);
    }

    private Batch RegisterEnvironment(uint key) => RegisterBatch([LoadDatabase(EnvironmentDatabaseKey(key))]);

    private uint EnvironmentDatabaseKey(uint key)
    {
        var common = Lookup(MapSeasonResourceConsumer.CommonSettingObjectId)?.Object ??
            throw new InvalidDataException("Render resource Context has no current Common settings.");
        uint[] keys = Words(common, 0x3302F081), roots = Words(common, 0x0D92FA32);
        if (keys.Length != roots.Length)
            throw new InvalidDataException("Common Environment keys and databases have different lengths.");
        int index = Array.IndexOf(keys, key);
        if (index < 0 || roots[index] == 0)
            throw new NotSupportedException("The requested Environment has no current Common database selection.");
        return roots[index];
    }

    public Batch RegisterBatch(IEnumerable<LoadedObjects> roots)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(roots);
        var batch = new Batch(this);
        ulong scope = checked(++nextScope);
        // Resolve and validate the candidate before changing current names.
        foreach (var root in roots)
        {
            if (root.Document.Objects.Select(o => o.ObjectId).Distinct().Count() != root.Document.Objects.Count)
                throw new NotSupportedException($"Database {root.Source.ResourceUri} has duplicate keys within one source document.");
            batch.Descriptors.AddRange(root.Document.Objects.OrderBy(o => o.ObjectId)
                .Select(o => new RenderResourceDescriptor(root, o, scope)));
        }
        var candidateTops = new Dictionary<uint, RenderResourceDescriptor>();
        foreach (var descriptor in batch.Descriptors)
        {
            uint name = descriptor.Object.ObjectId;
            if (name == 0) continue; // Anonymous declarations remain owned by the batch.
            var top = candidateTops.TryGetValue(name, out var prior) ? prior :
                names.TryGetValue(name, out var stack) && stack.Count != 0 ? stack[^1] : null;
            if (top is not null && ReferenceEquals(top.Source, descriptor.Source))
                throw new InvalidOperationException("The same backing database is already registered in the current scope.");
            candidateTops[name] = descriptor;
        }
        batches.Add(batch);
        foreach (var descriptor in batch.Descriptors)
            if (descriptor.Object.ObjectId != 0)
            {
                if (!names.TryGetValue(descriptor.Object.ObjectId, out var stack))
                    names.Add(descriptor.Object.ObjectId, stack = []);
                stack.Add(descriptor);
            }
        Revision = checked(Revision + 1);
        return batch;
    }

    public RenderResourceDescriptor? Lookup(uint name)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return name != 0 && names.TryGetValue(name, out var stack) && stack.Count != 0 ? stack[^1] : null;
    }

    private RenderResourceSnapshot CaptureSnapshot() => new(Revision,
        names.Select(pair => new KeyValuePair<uint, RenderResourceDescriptor>(pair.Key, pair.Value[^1])));

    private void Remove(Batch batch)
    {
        if (!batches.Remove(batch)) return;
        foreach (var descriptor in batch.Descriptors)
        {
            uint name = descriptor.Object.ObjectId;
            if (name == 0) continue;
            var stack = names[name];
            stack.Remove(descriptor);
            if (stack.Count == 0) names.Remove(name);
        }
        Revision = checked(Revision + 1);
    }

    /// <summary>Exports the actual currently visible descriptors for the
    /// Native resource owner. It conveys no typed-ready or known-null flags.</summary>
    public void WriteNativeInputs(string directory)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        directory = Path.GetFullPath(directory);
        if (written.Contains((directory, Revision))) return;
        Directory.CreateDirectory(directory);
        var snapshots = new List<RenderResourceSnapshot>();
        if (MaterialInitialization is { } initialization && initialization.Generation != Revision &&
            !written.Contains((directory, initialization.Generation))) snapshots.Add(initialization);
        snapshots.Add(CaptureSnapshot());
        foreach (var snapshot in snapshots) WriteSnapshot(directory, snapshot);
        string locatorTemporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(locatorTemporary, instanceDirectory + "\n", new UTF8Encoding(false));
            File.Move(locatorTemporary, Path.Combine(directory, "context-directory.txt"), overwrite: true);
            foreach (var snapshot in snapshots) written.Add((directory, snapshot.Generation));
        }
        finally { File.Delete(locatorTemporary); }
    }

    private void WriteSnapshot(string directory, RenderResourceSnapshot declarations)
    {
        string snapshot = Path.Combine(directory, instanceDirectory, declarations.Generation.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(snapshot);
        string temporary = Path.Combine(snapshot, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = File.Create(temporary))
            using (var output = new BinaryWriter(stream, Encoding.UTF8))
            {
                output.Write(0x32434352u); // RCC2: declarations and supported typed CPU inputs.
                output.Write((uint)declarations.Entries.Count);
                foreach (var (name, descriptor) in declarations.Entries.OrderBy(pair => pair.Key))
                {
                    var obj = descriptor.Object;
                    output.Write(name); output.Write((uint)obj.Kind); output.Write(obj.ClassId ?? 0);
                    output.Write(descriptor.Source.Source.FileId); output.Write((uint)obj.Index);
                    output.Write(obj.Reference?.ResourceId ?? 0); output.Write(obj.Reference?.ObjectId ?? 0);
                    output.Write(descriptor.Scope);
                    byte[] uri = Encoding.UTF8.GetBytes(descriptor.Source.Source.ResourceUri);
                    output.Write((uint)uri.Length); output.Write(uri);
                    output.Write(descriptor.BlendState is null ? 0u : 8u);
                    if (descriptor.BlendState is { } blend) output.Write(blend.PackedValue);
                }
            }
            File.Move(temporary, Path.Combine(snapshot, "descriptors.bin"), overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    public void Dispose()
    { disposed = true; names.Clear(); batches.Clear(); databases.Clear(); MaterialInitialization = null; }
}
