using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One real startup World registration task. Declaration order is
/// not execution order: node16 forks these tasks and their current control
/// may split each actual list into worker jobs.</summary>
public sealed record OriginalDeferredEarlyShadowTaskModelCollection(
    int TaskNode, uint ControlObjectId, uint SceneObjectId, uint ListObjectId,
    uint FirstMask, uint SecondMask, uint ShadowMask,
    uint DeclaredMode, uint DeclaredBatchThreshold,
    OriginalOwnedWorldListDeclaration ListDeclaration, string SourceEvidence)
{
    /// <summary>F3B0A9..BC passes these three distinct source masks to
    /// D68E40, which ORs Scene3334/38/3C respectively. This is a task caller,
    /// independent of optional Scene22A8/3344.</summary>
    public OriginalDeferredEarlyShadowCurrentViewMaskFields CreateCurrentCaller(
        long actualFrame, string actualViewIdentity, uint actualRegistered3330,
        uint actualSuccessful3340, uint actualInherited3334,
        uint actualInherited3338, uint actualInherited333C)
        => new(actualFrame, actualViewIdentity, actualRegistered3330, actualSuccessful3340,
            FirstMask | actualInherited3334, SecondMask | actualInherited3338,
            ShadowMask | actualInherited333C, SourceEvidence +
            ";F3B0A9..BC->D68E40;actual-Scene3334/38/3C;not-Scene22A8-caller");
}

/// <summary>Load-once real TGK17..23 descriptors and the exact installed
/// ListContainer/control declarations. It does not populate a list, infer a
/// static/Army key, execute the Host control override or assign a native
/// global thread interleaving.</summary>
public sealed class LoadedOriginalDeferredEarlyShadowTaskModelCollectionSource
{
    private LoadedOriginalDeferredEarlyShadowTaskModelCollectionSource(
        OriginalDeferredEarlyShadowTaskModelCollection[] tasks, uint[] fork)
        => (Tasks, SourceForkNodeIndices) = (Array.AsReadOnly(tasks), Array.AsReadOnly(fork));
    public IReadOnlyList<OriginalDeferredEarlyShadowTaskModelCollection> Tasks { get; }
    public IReadOnlyList<uint> SourceForkNodeIndices { get; }
    public OriginalDeferredEarlyShadowTaskModelCollection ReadTask(int actualNode)
        => Tasks.Single(t => t.TaskNode == actualNode);

    public static LoadedOriginalDeferredEarlyShadowTaskModelCollectionSource Load(OriginalGameRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var graphAsset = repository.LoadTaskGraph("system", OriginalDefaultView0Source.TaskGraphResourceId);
        var objects = repository.LoadObjects("system", OriginalOwnedWorldListSource.DefinitionsResourceId);
        Verify(graphAsset.Source, OriginalDefaultView0Source.TaskGraphResourceId,
            OriginalDefaultView0Source.TaskGraphResourceSha256);
        Verify(objects.Source, OriginalOwnedWorldListSource.DefinitionsResourceId,
            OriginalOwnedWorldListSource.DefinitionsSha256);
        var graph = TaskGraphReader.Read(graphAsset.Source.Bytes);
        if (graph.Nodes.Count != 27 || graph.Nodes[16].Call.FactoryClassId != 0x0296E56B)
            throw new InvalidDataException("The actual startup World registration fork differs from the source.");
        uint[] fork = graph.Nodes[16].Call.ConnectedNodeIndices.ToArray();
        if (!fork.SequenceEqual(new uint[] { 22, 21, 20, 19, 18, 17, 23 }))
            throw new InvalidDataException("The actual World registration fork edges differ from the source.");
        var tasks = new List<OriginalDeferredEarlyShadowTaskModelCollection>();
        for (int node = 17; node <= 23; node++)
        {
            var call = graph.Nodes[node].Call;
            if (call.FactoryClassId != 0x8B983E31 || call.Arguments.Count != 6 ||
                !call.ConnectedNodeIndices.SequenceEqual(new uint[] { 24 }))
                throw new InvalidDataException("The actual model task descriptor differs from 8B983E31.");
            uint controlId = Argument(call, 0x2068BEE2), sceneId = Argument(call, 0x72DB7EBD),
                listId = Argument(call, 0xA7DFA945);
            if (sceneId != 0xE1B6A687)
                throw new NotSupportedException("Model task needs its actual nondefault Scene registration source.");
            var control = objects.Document.Objects.Single(o => o.ObjectId == controlId);
            var list = objects.Document.Objects.Single(o => o.ObjectId == listId);
            if (control.Kind != DokObjectKind.Inline || control.ClassId != 0xEDDA84F3 || control.Properties.Count != 2 ||
                list.Kind != DokObjectKind.Inline || list.ClassId != 0x2EB79141 || list.Properties.Count != 3)
                throw new InvalidDataException("Actual model task control/ListContainer declarations differ from source.");
            uint threshold = Scalar(control.Properties[0], 0xD787B5A5), mode = Scalar(control.Properties[1], 0x9749A0C1, 4);
            uint capacity = Scalar(list.Properties[0], 0xFC63FB31), growth = Scalar(list.Properties[1], 0x04E203A7);
            var initial = list.Properties[2];
            if (initial.NameHash != 0xFF26EE41 || initial.TypeCode != 5 || initial.ElementCount != 0 || initial.RawValue.Length != 0)
                throw new NotSupportedException("Model list's nonempty source declaration needs an actual Data owner.");
            if (capacity == 0 || growth == 0 || capacity > int.MaxValue || growth > int.MaxValue)
                throw new NotSupportedException("Actual ListContainer allocation is outside bounded managed capacity.");
            tasks.Add(new(node, controlId, sceneId, listId,
                Argument(call, 0x55F5CEC3), Argument(call, 0xF2F8E7C0), Argument(call, 0x03319682),
                mode, threshold, new(listId, (int)capacity, (int)growth),
                graphAsset.Source.ResourceUri + "@" + graphAsset.Source.DecodedSha256 + "/node" + node +
                ";" + objects.Source.ResourceUri + "@" + objects.Source.DecodedSha256 + "/" + listId.ToString("X8") +
                ";TC:10B920/F38D50/16FCFC0.v58=F39C90/16FCFA8.v8=F3A9A0;" +
                "source-declaration-only/no-owned-registration/no-HostV110-override/no-global-thread-order"));
        }
        return new(tasks.ToArray(), fork);
    }

    private static uint Argument(TaskGraphCallMetadata call, uint hash)
    {
        var argument = call.Arguments.Single(a => a.NameHash == hash);
        if (argument.RawType != 5 || argument.RawElementCount != 1 || argument.RawValue.Length != 4)
            throw new InvalidDataException("Actual model task argument is not a source scalar.");
        return BinaryPrimitives.ReadUInt32LittleEndian(argument.RawValue);
    }
    private static uint Scalar(DokProperty property, uint hash, uint actualSourceType = 5)
    {
        if (property.NameHash != hash || property.TypeCode != actualSourceType || property.ElementCount != 1 || property.RawValue.Length != 4)
            throw new InvalidDataException("Actual model task control/list scalar differs from source.");
        return BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
    }
    private static void Verify(LoadedRdbAsset source, uint id, string hash)
    {
        if (source.FileId != id || source.ResourceUri != GameResourceId.Rdb("system", id).ToString() ||
            !source.DecodedSha256.Equals(hash, StringComparison.OrdinalIgnoreCase) ||
            Convert.ToHexString(SHA256.HashData(source.Bytes)) != hash)
            throw new InvalidDataException("Actual model task source URI/hash differs from the pinned original.");
    }
}
