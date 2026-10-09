using San14.Core;

namespace San14.Data;

/// <summary>Immutable source-only receiver fields. No gameplay unit, cargo,
/// target, group readiness or map ownership is stored in this format carrier.</summary>
public sealed record ArmyReceiverSourceSlot(int PhysicalSlot, ushort? Commander12,
    byte? Status10, ushort? Priority14)
{
    private IReadOnlyList<ushort>? _auxiliaryAC, _auxiliaryF4;
    public ArmyReceiverId ReceiverId { get; init; }
    public IReadOnlyList<ushort>? AuxiliaryAC
    {
        get => _auxiliaryAC;
        init => _auxiliaryAC = value is null ? null : Array.AsReadOnly(value.ToArray());
    }
    public IReadOnlyList<ushort>? AuxiliaryF4
    {
        get => _auxiliaryF4;
        init => _auxiliaryF4 = value is null ? null : Array.AsReadOnly(value.ToArray());
    }
    public byte? Direction36 { get; init; }
    public byte? MapUiByte13C { get; init; }
    public byte? CommandControl140 { get; init; }
    public byte? RouteHold56 { get; init; }
    public ushort? RouteGoal1F8 { get; init; }
}

/// <summary>Resource compatibility knowledge, independent of the simulation's
/// receiver roster and indexes. Array inputs are admitted once into owned values.</summary>
public sealed record ArmyLifecycleSourceState(string Revision, string Language, string RuleVersion,
    string SourceScenarioUri, string SourceScenarioSha256)
{
    private IReadOnlyList<ArmyReceiverSourceSlot> _slots = Array.Empty<ArmyReceiverSourceSlot>();
    private IReadOnlyList<byte?>? _flags;
    public IReadOnlyList<ArmyReceiverSourceSlot> Slots
    {
        get => _slots;
        init => _slots = Array.AsReadOnly(value.ToArray());
    }
    public IReadOnlyList<byte?>? ArmyFlags150
    {
        get => _flags;
        init => _flags = value is null ? null : Array.AsReadOnly(value.ToArray());
    }
}

/// <summary>Short-lived source-operation view. Domain and compatibility updates
/// are published together; this joined shape is never retained in a campaign.</summary>
public sealed record ArmyLifecycleSourceView(ArmyLifecycleState Domain, ArmyLifecycleSourceState Source)
{
    public string Revision => Source.Revision;
    public string Language => Source.Language;
    public string RuleVersion => Source.RuleVersion;
    public string SourceScenarioUri => Source.SourceScenarioUri;
    public string SourceScenarioSha256 => Source.SourceScenarioSha256;
    public IReadOnlyList<ArmyReceiverSourceSlot> Slots => Source.Slots;
    public IReadOnlyList<byte?>? ArmyFlags150
    {
        get => Source.ArmyFlags150;
        init => Source = Source with { ArmyFlags150 = value };
    }
    public ArmyReceiverRoster Receivers { get => Domain.Receivers; init => Domain = Domain with { Receivers = value }; }
    public WorldIndexesState Indexes { get => Domain.Indexes; init => Domain = Domain with { Indexes = value }; }
    public ArmyGroupsState? ArmyGroups { get => Domain.ArmyGroups; init => Domain = Domain with { ArmyGroups = value }; }
    public ArmyCautionsState? ArmyCautions { get => Domain.ArmyCautions; init => Domain = Domain with { ArmyCautions = value }; }
    public ArmyTargetsState? Targets { get => Domain.Targets; init => Domain = Domain with { Targets = value }; }
    public ArmyLifecycleSourceView Copy() => this with { Domain = Domain.Copy() };
}

public static class ArmyLifecycleSourceAdapter
{
    public static ArmyLifecycleSourceView? ReadArmyLifecycle(this GameState state) =>
        state.ArmyLifecycle is { } domain && state.ResourceState is CampaignSourceKnowledge
            { ArmyLifecycleSource: { } source } ? new(domain, source) : null;

    public static GameState WithArmyLifecycle(this GameState state, ArmyLifecycleSourceView? view) => state with
    {
        ArmyLifecycle = view?.Domain,
        ResourceState = CampaignSourceKnowledge.From(state.ResourceState).WithArmyLifecycleSource(view?.Source)
    };
}
