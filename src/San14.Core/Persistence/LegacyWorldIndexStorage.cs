namespace San14.Core;

/// <summary>Storage shapes used only by historical save decoders.</summary>
internal sealed record NativeSharedListHandle(int? Identity);
internal sealed record NativeSharedListPayload(string Kind, int? Identity);
internal sealed record NativeSharedListNode(int? Next, int? Previous, NativeSharedListPayload? Payload);
internal sealed record NativeSharedListDescriptor(int? Head, int? Tail, int? Count);
internal sealed record NativeSharedListPoolState(int DescriptorCapacity, int NodeCapacity, int ActiveNodeCount,
    int CreatedNodeCount, int? FreeNodeHead, int? NullDescriptorCount, int[] FreeDescriptors, int[] LiveDescriptors,
    Dictionary<int, NativeSharedListDescriptor> Descriptors, Dictionary<int, NativeSharedListNode> Nodes);
