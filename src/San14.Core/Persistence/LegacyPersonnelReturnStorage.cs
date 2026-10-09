namespace San14.Core;

/// <summary>Schema 61 and earlier saved command image. Used only during save migration.</summary>
internal sealed record NativePersonCommandManagerState(string Revision, ushort? FreeHeadEA8,
    ushort?[] FreeNext30, int NextReceiverIdentity, Dictionary<int, int> IdToReceiver,
    Dictionary<int, NativePersonCommandState> Commands, NativeSharedListHandle Secondary28Descriptor,
    NativeSharedListPoolState MembersPool, NativeSharedListPoolState TargetsPool,
    Dictionary<int, NativeCommandUnionValue> TargetNodeValues);

/// <summary>Source-sized numeric byte window. Pointer and unwritten padding
/// bytes remain null; actual ctor/writers set only the reached numeric bytes.</summary>
internal sealed record NativePersonCommandState(string Kind, byte?[] RawFields,
    NativeSharedListHandle Members20, NativeSharedListHandle Targets38);
internal sealed record NativeCommandUnionValue(int FirstKind8, int? FirstPhysical0,
    int SecondKindC, int? SecondPhysical10);
