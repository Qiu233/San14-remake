using System.Buffers.Binary;

namespace San14.Data;

public enum DeferredEarlySourceViewBank { Bank1C70, Bank1C78 }
public enum DeferredEarlySourceViewBankState { Unresolved, Absent, Present }

/// <summary>An explicitly owned current pointer-bank assignment. An absent
/// bank is a known null pointer. A present bank captures current uint50 from
/// a supplied0x54-byte prefix or executes its proved fresh allocation write.
/// Neither an omitted
/// argument nor an empty kind1/2/3 lighting list proves this assignment.</summary>
public sealed class DeferredEarlySourceViewBankPacket
{
    private readonly byte[] prefix;
    private readonly uint? presentUInt50;
    private DeferredEarlySourceViewBankPacket(long frame, string view, string generation,
        DeferredEarlySourceViewBank bank, DeferredEarlySourceViewBankState state,
        string? bankOwner, byte[] bytes, string provenance, uint? sourceFreshUInt50 = null)
    {
        if (frame <= 0 || string.IsNullOrWhiteSpace(view) || string.IsNullOrWhiteSpace(generation) ||
            string.IsNullOrWhiteSpace(provenance) || bank is not (DeferredEarlySourceViewBank.Bank1C70 or DeferredEarlySourceViewBank.Bank1C78))
            throw new InvalidDataException("View bank assignment requires its current frame, owned View generation, exact bank and producer.");
        FrameNumber = frame; ViewIdentity = view; ViewGenerationIdentity = generation; Bank = bank;
        State = state; BankOwnerIdentity = bankOwner; prefix = bytes.ToArray(); ProducerProvenance = provenance;
        presentUInt50 = state == DeferredEarlySourceViewBankState.Present
            ? sourceFreshUInt50 ?? BinaryPrimitives.ReadUInt32LittleEndian(prefix.AsSpan(0x50, 4)) : null;
    }
    public long FrameNumber { get; }
    public string ViewIdentity { get; }
    public string ViewGenerationIdentity { get; }
    public DeferredEarlySourceViewBank Bank { get; }
    public int NativeBankOffset => Bank == DeferredEarlySourceViewBank.Bank1C70 ? 0x1C70 : 0x1C78;
    public DeferredEarlySourceViewBankState State { get; }
    public string? BankOwnerIdentity { get; }
    public string ProducerProvenance { get; }
    public ReadOnlyMemory<byte> CapturedPrefix54 => prefix.ToArray();
    public uint? RawUInt50 => State switch
    {
        DeferredEarlySourceViewBankState.Absent => 0,
        DeferredEarlySourceViewBankState.Present => presentUInt50,
        _ => null
    };

    public static DeferredEarlySourceViewBankPacket Absent(long frameNumber, string viewIdentity,
        string viewGenerationIdentity, DeferredEarlySourceViewBank bank, string currentProducerProvenance) =>
        new(frameNumber, viewIdentity, viewGenerationIdentity, bank,
            DeferredEarlySourceViewBankState.Absent, null, [], currentProducerProvenance);

    public static DeferredEarlySourceViewBankPacket Unresolved(long frameNumber, string viewIdentity,
        string viewGenerationIdentity, DeferredEarlySourceViewBank bank, string currentProducerProvenance) =>
        new(frameNumber, viewIdentity, viewGenerationIdentity, bank,
            DeferredEarlySourceViewBankState.Unresolved, null, [], currentProducerProvenance);

    public static DeferredEarlySourceViewBankPacket CaptureCurrentPrefix(long frameNumber,
        string viewIdentity, string viewGenerationIdentity, DeferredEarlySourceViewBank bank,
        string currentBankOwnerIdentity, ReadOnlySpan<byte> completeCurrentPrefix54, string currentProducerProvenance)
    {
        if (string.IsNullOrWhiteSpace(currentBankOwnerIdentity) || completeCurrentPrefix54.Length != 0x54)
            throw new InvalidDataException("A present View bank requires its owned resource identity and complete current0x54-byte prefix.");
        return new(frameNumber, viewIdentity, viewGenerationIdentity, bank,
            DeferredEarlySourceViewBankState.Present, currentBankOwnerIdentity,
            completeCurrentPrefix54.ToArray(), currentProducerProvenance);
    }

    /// <summary>Execute the exact fresh source bank constructor field write
    /// D02890/D028F5 on a newly allocated managed bank. Pool reuse is not a
    /// fresh allocation and must publish its actual current field instead.
    /// Only uint50 is projected; other bank bytes remain unclaimed.</summary>
    public static DeferredEarlySourceViewBankPacket ConstructFreshAllocatedBank(long frameNumber,
        string viewIdentity, string viewGenerationIdentity, DeferredEarlySourceViewBank bank,
        string newlyAllocatedBankOwnerIdentity, string actualFreshAllocationProducerProvenance)
    {
        if (string.IsNullOrWhiteSpace(newlyAllocatedBankOwnerIdentity) || string.IsNullOrWhiteSpace(actualFreshAllocationProducerProvenance))
            throw new InvalidDataException("Fresh source bank allocation requires its actual newly allocated owner identity.");
        return new(frameNumber, viewIdentity, viewGenerationIdentity, bank,
            DeferredEarlySourceViewBankState.Present, newlyAllocatedBankOwnerIdentity, [],
            actualFreshAllocationProducerProvenance + ";TC:D02890/D028A2/D028F5;SC:D035C0/D035D2/D03625;fresh-bank-uint50-write0", 0);
    }
}

/// <summary>The remake's actual owner of these two source View pointers.
/// Constructor values are supplied arguments22/23, not constructor clears.
/// Every later assignment/reset/unknown import advances this owner's revision;
/// a previously prepared packet cannot bind to that later current boundary.</summary>
public sealed class DeferredEarlySourceViewQueriesOwner
{
    public const string ConstructorOrigin = "TC:D0732D..D07349/SC:D07FED..D08009;View1C70/1C78=constructor-arguments22/23";
    public const string ResetOrigin = "TC:D52384/D5238B;SC:D530B4/D530BB;source-D520A0/D52DD0-reset-clears-View1C70/1C78";
    public const string FreshGroupPoolOrigin = "TC:D019B0/D01CF2..D01D4E;SC:D026E0/D02A22..D02A7E;group-pools438/450-count0-freeNull;TC:D0AFD0/D0ADE0;SC:D0BD00/D0BB10;return-null";
    private readonly object gate = new();
    private DeferredEarlySourceViewBankPacket bank70, bank78;
    private long revision;
    private long frameNumber;
    private string viewPublication;
    private string producer;
    private MapSourceCreatedViewFieldsPacket acceptedView;

    private DeferredEarlySourceViewQueriesOwner(MapSourceCreatedViewFieldsPacket view,
        string generation, int viewIndex, DeferredEarlySourceViewBankPacket first,
        DeferredEarlySourceViewBankPacket second, string constructorProducer)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (string.IsNullOrWhiteSpace(generation) || string.IsNullOrWhiteSpace(view.Binding.ViewIdentity) ||
            string.IsNullOrWhiteSpace(view.Binding.CameraOwnerIdentity) || viewIndex is < 0 or >= 32 || string.IsNullOrWhiteSpace(constructorProducer))
            throw new InvalidDataException("View query owner requires its actual View generation, selected registry index and constructor producer.");
        ViewIdentity = view.Binding.ViewIdentity; CameraOwnerIdentity = view.Binding.CameraOwnerIdentity;
        ViewGenerationIdentity = generation; SelectedViewIndex = viewIndex;
        bank70 = first; bank78 = second; frameNumber = view.Camera.FrameNumber; viewPublication = view.SourceProvenance;
        acceptedView = view;
        RequireAssignment(view, first, DeferredEarlySourceViewBank.Bank1C70);
        RequireAssignment(view, second, DeferredEarlySourceViewBank.Bank1C78);
        producer = constructorProducer + ";" + ConstructorOrigin; revision = 1;
    }
    public string ViewIdentity { get; }
    public string CameraOwnerIdentity { get; }
    public string ViewGenerationIdentity { get; }
    public int SelectedViewIndex { get; }

    public static DeferredEarlySourceViewQueriesOwner CreateFromConstructorArguments(
        MapSourceCreatedViewFieldsPacket acceptedView, string viewGenerationIdentity, int selectedViewIndex,
        DeferredEarlySourceViewBankPacket actualArgument22Bank1C70,
        DeferredEarlySourceViewBankPacket actualArgument23Bank1C78, string actualConstructorProducerProvenance) =>
        new(acceptedView, viewGenerationIdentity, selectedViewIndex, actualArgument22Bank1C70,
            actualArgument23Bank1C78, actualConstructorProducerProvenance);

    /// <summary>Execute fresh source group pool initialization and its null
    /// pool-getter projection for this newly owned View. This scope requires
    /// an actual new group generation before capacity/free-list assignments;
    /// it cannot initialize a reused or imported group's current bank state.</summary>
    public static DeferredEarlySourceViewQueriesOwner CreateFromFreshSourceGroupPools(
        MapSourceCreatedViewFieldsPacket acceptedView, string viewGenerationIdentity, int selectedViewIndex,
        string actualNewGroupOwnerIdentity, string actualGroupConstructorProducerProvenance)
    {
        ArgumentNullException.ThrowIfNull(acceptedView);
        if (string.IsNullOrWhiteSpace(actualNewGroupOwnerIdentity) || string.IsNullOrWhiteSpace(actualGroupConstructorProducerProvenance))
            throw new InvalidDataException("Fresh View pool projection requires its actual newly owned group and constructor producer.");
        string provenance = actualGroupConstructorProducerProvenance + ";Group=" + actualNewGroupOwnerIdentity + ";" + FreshGroupPoolOrigin;
        long frame = acceptedView.Camera.FrameNumber;
        var first = DeferredEarlySourceViewBankPacket.Absent(frame, acceptedView.Binding.ViewIdentity,
            viewGenerationIdentity, DeferredEarlySourceViewBank.Bank1C70, provenance);
        var second = DeferredEarlySourceViewBankPacket.Absent(frame, acceptedView.Binding.ViewIdentity,
            viewGenerationIdentity, DeferredEarlySourceViewBank.Bank1C78, provenance);
        return new(acceptedView, viewGenerationIdentity, selectedViewIndex, first, second, provenance);
    }

    /// <summary>Publish both actual current pointer assignments. This also
    /// accepts an unresolved assignment; its subsequent query cannot select
    /// a shader until the corresponding current owner is supplied.</summary>
    public void ApplyCurrentAssignments(MapSourceCreatedViewFieldsPacket acceptedView,
        DeferredEarlySourceViewBankPacket actualBank1C70,
        DeferredEarlySourceViewBankPacket actualBank1C78, string actualAssignmentProducerProvenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualAssignmentProducerProvenance);
        lock (gate)
        {
            RequireAssignment(acceptedView, actualBank1C70, DeferredEarlySourceViewBank.Bank1C70);
            RequireAssignment(acceptedView, actualBank1C78, DeferredEarlySourceViewBank.Bank1C78);
            bank70 = actualBank1C70; bank78 = actualBank1C78;
            AcceptBoundary(acceptedView, actualAssignmentProducerProvenance);
        }
    }

    /// <summary>Execute the recovered reset projection on this owned View's
    /// bank state. The call is the managed pointer-clear producer; it does not
    /// claim other native reset effects or infer that native reset ran.</summary>
    public void ApplySourceBankReset(MapSourceCreatedViewFieldsPacket acceptedView,
        string currentResetProducerProvenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentResetProducerProvenance);
        lock (gate)
        {
            RequireView(acceptedView);
            long frame = acceptedView.Camera.FrameNumber;
            bank70 = DeferredEarlySourceViewBankPacket.Absent(frame, ViewIdentity, ViewGenerationIdentity,
                DeferredEarlySourceViewBank.Bank1C70, currentResetProducerProvenance + ";" + ResetOrigin);
            bank78 = DeferredEarlySourceViewBankPacket.Absent(frame, ViewIdentity, ViewGenerationIdentity,
                DeferredEarlySourceViewBank.Bank1C78, currentResetProducerProvenance + ";" + ResetOrigin);
            AcceptBoundary(acceptedView, currentResetProducerProvenance + ";" + ResetOrigin);
        }
    }

    public void MarkCurrentAssignmentsUnresolved(MapSourceCreatedViewFieldsPacket acceptedView,
        string unresolvedAssignmentProducerProvenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unresolvedAssignmentProducerProvenance);
        lock (gate)
        {
            RequireView(acceptedView);
            long frame = acceptedView.Camera.FrameNumber;
            bank70 = DeferredEarlySourceViewBankPacket.Unresolved(frame, ViewIdentity, ViewGenerationIdentity,
                DeferredEarlySourceViewBank.Bank1C70, unresolvedAssignmentProducerProvenance);
            bank78 = DeferredEarlySourceViewBankPacket.Unresolved(frame, ViewIdentity, ViewGenerationIdentity,
                DeferredEarlySourceViewBank.Bank1C78, unresolvedAssignmentProducerProvenance);
            AcceptBoundary(acceptedView, unresolvedAssignmentProducerProvenance);
        }
    }

    public DeferredEarlySourceViewQueriesPacket PrepareCurrent(
        MapSourceCreatedViewFieldsPacket acceptedView, string currentPreparationProvenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPreparationProvenance);
        lock (gate)
        {
            RequireCurrentBoundary(acceptedView);
            return new(this, revision, frameNumber, viewPublication, bank70, bank78,
                producer + ";" + currentPreparationProvenance);
        }
    }

    internal void ValidatePrepared(DeferredEarlySourceViewQueriesPacket packet,
        MapSourceCreatedViewFieldsPacket acceptedView)
    {
        lock (gate)
        {
            RequireCurrentBoundary(acceptedView);
            if (packet.Owner != this || packet.OwnerRevision != revision || packet.FrameNumber != frameNumber ||
                packet.CurrentViewPublicationProvenance != viewPublication)
                throw new InvalidDataException("View raw query packet precedes a later assignment or belongs to another current boundary.");
        }
    }
    private void RequireView(MapSourceCreatedViewFieldsPacket view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.Camera.FrameNumber <= 0 || view.Camera.FrameNumber < frameNumber || view.Binding.ViewIdentity != ViewIdentity ||
            view.Binding.CameraOwnerIdentity != CameraOwnerIdentity || string.IsNullOrWhiteSpace(view.SourceProvenance))
            throw new InvalidDataException("View bank producer belongs to another accepted View/Camera owner.");
    }
    private void RequireAssignment(MapSourceCreatedViewFieldsPacket view,
        DeferredEarlySourceViewBankPacket bank, DeferredEarlySourceViewBank expected)
    {
        RequireView(view); ArgumentNullException.ThrowIfNull(bank);
        if (bank.FrameNumber != view.Camera.FrameNumber || bank.ViewIdentity != ViewIdentity ||
            bank.ViewGenerationIdentity != ViewGenerationIdentity || bank.Bank != expected)
            throw new InvalidDataException("View pointer bank assignment has another frame, generation or native field.");
    }
    private void RequireCurrentBoundary(MapSourceCreatedViewFieldsPacket view)
    {
        RequireView(view);
        if (!ReferenceEquals(view, acceptedView) || view.Camera.FrameNumber != frameNumber || view.SourceProvenance != viewPublication)
            throw new InvalidDataException("Current View query banks were not published for this accepted frame/View preparation.");
    }
    private void AcceptBoundary(MapSourceCreatedViewFieldsPacket view, string provenance)
    {
        acceptedView = view;
        frameNumber = view.Camera.FrameNumber; viewPublication = view.SourceProvenance; producer = provenance;
        revision = checked(revision + 1);
    }
}

public sealed class DeferredEarlySourceViewQueriesPacket
{
    internal DeferredEarlySourceViewQueriesPacket(DeferredEarlySourceViewQueriesOwner owner,
        long revision, long frame, string viewPublication, DeferredEarlySourceViewBankPacket first,
        DeferredEarlySourceViewBankPacket second, string producer)
    {
        Owner = owner; OwnerRevision = revision; FrameNumber = frame; CurrentViewPublicationProvenance = viewPublication;
        Bank1C70 = first; Bank1C78 = second; CurrentProducerProvenance = producer;
    }
    internal DeferredEarlySourceViewQueriesOwner Owner { get; }
    internal long OwnerRevision { get; }
    public long FrameNumber { get; }
    public string ViewIdentity => Owner.ViewIdentity;
    public string CameraOwnerIdentity => Owner.CameraOwnerIdentity;
    public string ViewGenerationIdentity => Owner.ViewGenerationIdentity;
    public int SelectedViewIndex => Owner.SelectedViewIndex;
    public string CurrentViewPublicationProvenance { get; }
    public DeferredEarlySourceViewBankPacket Bank1C70 { get; }
    public DeferredEarlySourceViewBankPacket Bank1C78 { get; }
    public uint? RawQueryD5BA00 => Bank1C70.RawUInt50;
    public uint? RawQueryD5B9D0 => Bank1C78.RawUInt50;
    public string CurrentProducerProvenance { get; }

    public void ValidateCurrent(MapSourceCreatedViewFieldsPacket acceptedView) => Owner.ValidatePrepared(this, acceptedView);

    public DeferredEarlyF103CurrentVariantInputs BindVariantInputs(
        DeferredF103CurrentVariantInputs actualCurrentVariant,
        MapSourceCreatedViewFieldsPacket acceptedView)
    {
        ArgumentNullException.ThrowIfNull(actualCurrentVariant); ValidateCurrent(acceptedView);
        if (actualCurrentVariant.FrameNumber != FrameNumber || string.IsNullOrWhiteSpace(actualCurrentVariant.CurrentProducerProvenance) ||
            RawQueryD5BA00 is not uint first || RawQueryD5B9D0 is not uint second)
            throw new InvalidDataException("Early F103 requires this accepted frame and both resolved current owned View bank queries.");
        return new(actualCurrentVariant, first, second, CurrentProducerProvenance);
    }

    public DeferredEarlyF103CurrentVariantInputs BindCurrentFrame(
        MapSourceCreatedF103CurrentFrame acceptedFrame, uint actualEarlyNativeColorMode) =>
        BindCurrentFrame(acceptedFrame, actualEarlyNativeColorMode, SelectedViewIndex);

    /// <summary>Join the current owned View, with an explicit native registry
    /// query index when that index is available at the caller's boundary. The
    /// lighting source's Game setter slot is a distinct source association.</summary>
    public DeferredEarlyF103CurrentVariantInputs BindCurrentFrame(
        MapSourceCreatedF103CurrentFrame acceptedFrame, uint actualEarlyNativeColorMode,
        int actualCurrentSelectedViewIndex)
    {
        ArgumentNullException.ThrowIfNull(acceptedFrame);
        if (acceptedFrame.FrameNumber != FrameNumber || actualCurrentSelectedViewIndex != SelectedViewIndex ||
            acceptedFrame.View.ViewIdentity != ViewIdentity || acceptedFrame.View.CameraOwnerIdentity != CameraOwnerIdentity)
            throw new InvalidDataException("Early raw queries select a different owned registry View/Camera/frame.");
        return BindVariantInputs(acceptedFrame.Variant with { NativeColorMode = actualEarlyNativeColorMode }, acceptedFrame.ViewFields);
    }
}
